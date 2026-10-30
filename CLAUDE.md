# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A Total Commander file-system plugin (`.wfx`) that exposes Azure Blob Storage as a browsable file system, written in C# on .NET Framework 4.8. It also registers a content plugin for custom columns showing blob properties. Windows only.

## Commands

Build with Visual Studio's MSBuild, not `dotnet build`. `Resources/Icons.resx` embeds `.ico` files (non-string resources), and SDK MSBuild fails on those with MSB3822/MSB3823. A `dotnet build` may only appear to succeed when the generated `.resources` file is already up to date.

```bash
"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" TcPlugin.AzureBlobStorage.slnx -restore -v:m
dotnet test test/TcPlugin.AzureBlobStorage.Test --no-build
dotnet test test/TcPlugin.AzureBlobStorage.Test --no-build --filter "FullyQualifiedName~BlobStoragePathTest.PathKinds"
```

- `Directory.Build.props` sets `TreatWarningsAsErrors=true` (CS1591 and CS0618 suppressed). MSTest analyzer findings in the test project also fail the build.
- The plugin project outputs to the repo-level `bin/net480`. The `TcBuild` package merges the assemblies into the native `.wfx`/`.wfx64` wrappers and builds a TC installer zip. The custom `DeployPlugin` target unzips it into `tc/Plugins/wfx/TcPlugin.AzureBlobStorage`.
- `tc/` is a portable Total Commander install for debugging; the `FsAzureStorage` launch profile starts `tc/TOTALCMD64.EXE`. Close it before building: while it runs, the plugin files are locked and `DeployPlugin` fails the build with MSB3936 (the plugin itself still compiles). To build and run the tests anyway, build the test project with `-p:BuildProjectReferences=false`.

## Architecture

The plugin is built on `TcPluginBase` (brought in by `TcBuild`). TC callbacks are synchronous or return a `Task`; sync callbacks block on async operations with `.Result`.

- `AzureBlobFsPlugin` (`FsPlugin`) is the entry point. It stays a thin mapping from TC callbacks (list, get/put, mkdir/rmdir, rename/move, delete, properties, icons) to `BlobFileSystem`.
  - Errors are handled here and nowhere else: `BlobFileSystem` lets exceptions propagate, and each callback catches them and shows `Storage/StorageError.Describe` through `Prompt.MsgOk`. `ShowErrorDialog` is off, so `TcPluginBase`'s exception window never appears. Bulk operations (transfers, delete) go through `Storage/ErrorThrottle` because TC reports each failed file itself. Transfers override the sync `GetFile`/`PutFile` so the dialog runs on TC's calling thread, not in the async continuation.
  - A listing that returns `null` keeps TC out of the directory; an empty directory must throw `NoMoreFilesException`.
- `AzureBlobContentPlugin` (`ContentPlugin`) builds its columns by reflecting over `BlobItemProperties` (see `BlobContentField`). Values come from a properties cache that `BlobFileSystem` fills while listing a directory, so the column plugin never calls Azure.
- `BlobFileSystem` holds the file operations and wires together the components below.
  - `Paths/BlobStoragePath`: parses paths and identifies which kind of path it is (see below).
  - `Paths/PathCache`: in-memory folders that don't exist in storage yet.
  - `Connections/ConnectionStore`: saved connections, plus `GetConnectionType` to classify a connection string or URL.
  - `Authentication/AzureCredentialProvider`: a single `TokenCredential` shared by every client. After a 401 it can switch to an interactive browser login once, without recreating the clients.
  - `Management/AzureResourceManagerClient`: raw ARM REST calls for tenants, subscriptions and storage accounts, with cached name→id lookups.
  - `Storage/BlobClientFactory`: builds service, container and blob clients for a path. Container clients are cached by `ContainerPath`, and the cache is cleared when connections change. `TryGet*` methods return null when no client can be created. Every client must use `CreateOptions`, which keeps SDK retries short because TC waits for most calls.
  - `Storage/PercentProgress`: turns transferred bytes into TC progress percentages, reported synchronously and only when the value changes.

### Path model (`BlobStoragePath`)

The root lists two namespaces side by side:

1. **Azure AD discovery**: `/<tenant>/<subscription>/<storageAccount>/<container>/<blob/path...>`. Endpoints are `https://<account>.blob.core.windows.net`.
2. **Saved connections**: `/#<name>/<container>/<blob/path...>`. The `#` prefix sets `IsConnectionPath`.

Use the path properties (`ContainerLevel`, `IsConnectionRoot`, `IsContainerPath`, `IsBlobPath`, `AccountName`, `ContainerName`, `BlobPath`, `ContainerPath`) instead of checking levels by hand. The container sits at level 2 in one layout and level 4 in the other. For saved connections, `AccountName` holds the connection name.

Blob storage has no real folders. Directories are virtual prefixes listed with `GetBlobsByHierarchy` and `/` as the delimiter. `MkDir` below a container only adds the folder to `PathCache` until a blob is uploaded into it, and `PathCache.WithCached` merges cached folders into listings.

### Saved connections

- Creating a directory at level 1 asks for a connection string or URL (`Prompt.AskUrl`) and saves it. Removing a connection root deletes the saved connection.
- Connection types are service or container URL, with or without a SAS token (`sig=`), or a connection string, with or without `SharedAccessSignature`. Service URLs without a SAS token authenticate with the shared AAD credential. Container URLs expose exactly one container, and their container client is created without a credential (anonymous access unless a SAS token is present).
- Connections are saved as JSON encrypted with DPAPI (CurrentUser scope) to `%APPDATA%\FsAzureStorage\connections.bin`.
