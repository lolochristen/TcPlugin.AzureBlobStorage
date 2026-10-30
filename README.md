# TcPlugin.AzureBlobStorage

A [Total Commander](https://www.ghisler.com/) file system plugin (`.wfx` / `.wfx64`) for **Azure Blob Storage**.
Browse your storage accounts, containers and blobs like a regular drive. Copy files in and out with F5/F6.

## Features

- Browse tenants, subscriptions, storage accounts, containers and blobs using your existing Azure sign-in
- Add individual storage accounts or containers by connection string, SAS URL or plain URL
- Upload and download with progress and background transfer support; the content type is set from the file extension on upload
- Copy and move blobs within storage (server-side copy)
- Create and delete containers; create virtual folders
- Show properties of tenants, subscriptions, storage accounts, connections, containers and blobs (`Alt+Enter`)
- Custom columns for blob properties such as `AccessTier`, `BlobType`, `ContentType` and `ETag`

## Installation

1. Download the latest release zip.
2. In Total Commander, navigate to the zip file and press `Enter`.
3. Confirm the installer prompt.
4. Open the plugin under **Network Neighborhood** (`\\\`).

Requires Windows with .NET Framework 4.8.

## Browsing via your Azure sign-in

At the root, the plugin lists every tenant your Azure identity can access. From there you can drill down:

```
\<tenant>\<subscription>\<storage account>\<container>\<folder>\<blob>
```

**The plugin uses the current context of the Azure CLI or the Azure PowerShell module.** If you are already signed in, tenants, subscriptions and storage accounts appear without any further login:

```bash
az login
```

```powershell
Connect-AzAccount
```

Credentials are resolved with `DefaultAzureCredential`, in this order: environment variables, Azure CLI, Azure PowerShell, Azure Developer CLI. If none of them are signed in, or if a storage account rejects the token as unauthenticated (HTTP 401), a browser window opens for interactive sign-in.

Required permissions:

- **Reader** (or any role with `Microsoft.Storage/storageAccounts/read`) on the subscription, to list storage accounts.
- **Storage Blob Data Reader** to browse and download, or **Storage Blob Data Contributor** to upload, copy, move and delete. The plugin accesses blob data with your Azure AD identity, not with account keys.

## Saved connections

Storage accounts or containers you can't reach through your sign-in (e.g. other organizations, SAS links) can be added as saved connections. They appear at the root as `#<name>` folders.

| Action | How |
|---|---|
| Add | Press `F7` at the root or inside the `Connections` folder, enter a name, then the connection string or URL |
| Edit | Open the `Connections` folder and press `Enter` on a connection |
| Delete | Delete the connection in the `Connections` folder, or delete the `#<name>` folder at the root |

Supported formats:

| Format | Example | Authentication |
|---|---|---|
| Connection string | `DefaultEndpointsProtocol=https;AccountName=…;AccountKey=…` | Account key or SAS |
| Connection string for one container | `…;BlobEndpoint=…;ContainerName=<container>` (as written by .NET Aspire) | Account key or SAS |
| Account SAS URL | `https://<account>.blob.core.windows.net/?sv=…&sig=…` | SAS |
| Container SAS URL | `https://<account>.blob.core.windows.net/<container>?sv=…&sig=…` | SAS |
| Account URL | `https://<account>.blob.core.windows.net/` | Your Azure sign-in |
| Container URL | `https://<account>.blob.core.windows.net/<container>` | Anonymous (public containers) |

Connections are stored encrypted for the current Windows user (DPAPI) in `%APPDATA%\FsAzureStorage\connections.bin`.

## Notes

- Blob storage has no real folders. A folder created with `F7` inside a container exists only until Total Commander is closed, unless you upload a file into it.
- Deleting a container folder **deletes the container and all of its blobs**.
- Resuming interrupted transfers is not supported.

## Building

Build with Visual Studio's MSBuild. `dotnet build` can't embed the icon resources.

```bash
"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" TcPlugin.AzureBlobStorage.slnx -restore
dotnet test test/TcPlugin.AzureBlobStorage.Test --no-build
```

The build packages the plugin with [TcBuild](https://www.nuget.org/packages/TcBuild) and deploys it into the portable Total Commander in `tc/` for debugging.

## Credits

Thanks to René Larch for creating the basis for this project.
