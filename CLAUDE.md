# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A Total Commander file-system plugin (`.wfx`) that exposes Azure Blob Storage as a browsable file system, written in C# on .NET Framework 4.8. It also registers a content plugin (`.wdx`-style custom columns) for blob properties. Windows only.

## Commands

```bash
dotnet build TcPlugin.AzureBlobStorage.slnx
dotnet test TcPlugin.AzureBlobStorage.slnx
dotnet test test/TcPlugin.AzureBlobStorage.Test --filter "FullyQualifiedName~CloudPathTest.BlobName"
```

- `Directory.Build.props` sets `TreatWarningsAsErrors=true` (CS1591 and CS0618 suppressed), so any new warning breaks the build.
- The plugin project outputs to the repo-level `bin/` (not the project's own `bin`).
- The `TcBuild` NuGet package wraps the managed assembly into native Total Commander plugin binaries and produces `bin/out/TcPlugin.AzureBlobStorage.zip` (a TC plugin installer archive). The custom `DeployPlugin` target then unzips it into `tc/Plugins/wfx/TcPlugin.AzureBlobStorage`.
- `tc/` holds a portable Total Commander install used for debugging; the `FsAzureStorage` launch profile starts `tc/TOTALCMD64.EXE`. Close Total Commander before rebuilding, or the deploy step fails because the plugin files are locked.

## Architecture

The plugin is built on `TcPluginBase` (brought in by `TcBuild`). TC calls the plugin classes, and they delegate to one shared `BlobFileSystem`:

- `AzureBlobFsPlugin` (`FsPlugin`) is the entry point. It maps TC operations (list, get/put, mkdir, rmdir, rename/move, delete, properties, icons) onto `BlobFileSystem`. Several sync TC callbacks block on async methods with `.Result`.
- `AzureBlobContentPlugin` (`ContentPlugin`) builds its custom column fields by reflecting over `Azure.Storage.Blobs.Models.BlobItemProperties`. Values come from `BlobFileSystem._blobItemPropertiesCache`, which is filled as a side effect of listing a directory. It makes no network calls of its own.
- `BlobFileSystem` does all the Azure work: listing, transfers, copy/move (server-side copy, then delete), connection persistence, auth and client caching.

### Path model (`BlobStoragePath`)

Paths are split into segments, and there are two namespaces at the root:

1. **Azure AD discovery**: `/<tenant>/<subscription>/<storageAccount>/<container>/<blob/path...>`. Tenants, subscriptions and storage accounts are listed through raw ARM REST calls to `management.azure.com`, using a token from `DefaultAzureCredential` (interactive browser login allowed). Names are resolved to IDs through `_tenantsDictionary` and `_subscriptionsDictionary`. Blob endpoints are built as `https://<account>.blob.core.windows.net`.
2. **Saved connections**: `/#<name>/<container>/<blob/path...>`. A segment that starts with `#` marks a configured connection (`IsConfigurationBased`), which shifts the meaning of `Level`, `AccountName`, `ContainerName` and `BlobPath`.

Always use `BlobStoragePath` properties instead of indexing segments by hand, because the offsets differ between the two modes. Icons in `ExtractCustomIcon` and the dispatch in `ListDirectory` both switch on mode + `Level`.

Blob storage has no real folders. Directories are virtual prefixes listed with `GetBlobsByHierarchy` and `/` as the delimiter. Folders created with `MkDir` exist only in `PathCache` (in memory) until a blob is uploaded under them, and `PathCache.WithCached` merges them into listings.

### Saved connections

- Creating a directory at level 1 (the root) asks for a connection string or URL and saves it under that name. Removing a level-1 directory deletes the saved connection.
- `GetConnectionType` classifies each entry as a service or container URL (with or without a SAS token `sig=`), or as a connection string (with or without a SAS token). URLs without a SAS token use the AAD token credential.
- Connections are saved as JSON encrypted with DPAPI (`ProtectedData`, CurrentUser scope) to `%APPDATA%\FsAzureStorage\connections.bin`.

### Legacy / in-progress

The working tree is mid-refactor: `CloudPath` became `BlobStoragePath`, and the WPF connection dialogs and `StorageConnection` were removed. WPF references and large commented-out blocks are still in the csproj and `BlobFileSystem.cs`. Don't bring back the WPF UI; prompts now use TC's `Prompt.AskUrl` and WinForms `MessageBox`. The test project (MSTest) only covers path parsing (`CloudPathTest.cs`).
