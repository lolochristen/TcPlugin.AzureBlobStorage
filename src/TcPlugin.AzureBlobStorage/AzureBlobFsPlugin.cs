using Azure.Storage.Blobs.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TcPlugin.AzureBlobStorage.Paths;
using TcPlugin.AzureBlobStorage.Resources;
using TcPlugin.AzureBlobStorage.Storage;
using TcPluginBase;
using TcPluginBase.FileSystem;

namespace TcPlugin.AzureBlobStorage
{
    /// <summary>
    ///     Total Commander file system plugin entry point. Maps TC callbacks onto <see cref="BlobFileSystem" />.
    /// </summary>
    public class AzureBlobFsPlugin : FsPlugin
    {
        // well above the time a failing attempt plus TC's own error dialog takes between two files
        private static readonly TimeSpan BulkErrorWindow = TimeSpan.FromMinutes(5);

        private readonly ErrorThrottle _bulkErrors = new ErrorThrottle(BulkErrorWindow);
        private readonly BlobFileSystem _fileSystem;
        private bool _tenantsErrorShown;

        public AzureBlobFsPlugin(Settings pluginSettings) : base(pluginSettings)
        {
            Title = "Azure Blob Plugin";
            BackgroundFlags = FsBackgroundFlags.Download | FsBackgroundFlags.Upload;

            // failures are reported as plain messages; anything that still gets through is only logged
            ShowErrorDialog = false;

            _fileSystem = new BlobFileSystem();
            ContentPlugin = new AzureBlobContentPlugin(pluginSettings, _fileSystem);

            try
            {
                _fileSystem.LoadConnections();
            }
            catch (Exception exp)
            {
                Log.Warning("Could not read settings: " + exp);
            }
        }

        public override IEnumerable<FindData> GetFiles(RemotePath path)
        {
            List<FindData> items;
            try
            {
                BlobStoragePath storagePath = path;
                items = (storagePath.Level == 0
                    ? _fileSystem.ListRoot(ShowTenantsError)
                    : _fileSystem.ListDirectory(storagePath)).ToList();
            }
            catch (Exception exp)
            {
                // a null listing keeps TC out of the directory
                ShowError($"Could not open {path}.", exp);
                return null;
            }

            return ToListing(items);
        }

        /// <summary>
        ///     TC only opens an empty directory when the listing reports no more files; any other empty result counts
        ///     as a failure.
        /// </summary>
        internal static IEnumerable<FindData> ToListing(List<FindData> items)
        {
            if (items.Count == 0)
            {
                throw new NoMoreFilesException();
            }

            return items;
        }

        public override bool MkDir(RemotePath dir)
        {
            try
            {
                if ((dir.Level == 2 && dir.GetSegment(1) == BlobStoragePath.ConnectionsRootName) || dir.Level == 1)
                {
                    var connection = Prompt.AskUrl("Blob Connection String or URL:");
                    if (string.IsNullOrEmpty(connection))
                    {
                        return false;
                    }

                    _fileSystem.AddOrUpdateConnection(dir.FileName, connection);
                    return true;
                }

                return _fileSystem.CreateDirectory(dir);
            }
            catch (Exception exp)
            {
                ShowError($"Could not create {dir.FileName}.", exp);
                return false;
            }
        }

        public override bool RemoveDir(RemotePath dirName)
        {
            try
            {
                return _fileSystem.RemoveDirectory(dirName);
            }
            catch (Exception exp)
            {
                ShowBulkError($"Could not remove {dirName.FileName}.", exp);
                return false;
            }
        }

        public override bool DeleteFile(RemotePath fileName)
        {
            try
            {
                if (fileName.Level == 2 && fileName.GetSegment(1) == BlobStoragePath.ConnectionsRootName)
                {
                    _fileSystem.RemoveConnection(fileName.FileName);
                    return true;
                }

                return _fileSystem.DeleteFile(fileName).Result;
            }
            catch (Exception exp)
            {
                ShowBulkError($"Could not delete {fileName.FileName}.", exp);
                return false;
            }
        }

        public override FileSystemExitCode RenMovFile(RemotePath oldName, RemotePath newName, bool move, bool overwrite,
            RemoteInfo remoteInfo)
        {
            ProgressProc(oldName, newName, 0);
            try
            {
                return RunTransfer($"Could not {(move ? "move" : "copy")} {oldName.FileName}.", FileSystemExitCode.WriteError, () =>
                {
                    var operation = move
                        ? _fileSystem.Move(oldName, newName, overwrite, CancellationToken.None)
                        : _fileSystem.Copy(oldName, newName, overwrite, CancellationToken.None);

                    return operation.Result;
                });
            }
            finally
            {
                ProgressProc(oldName, newName, 100);
            }
        }

        public override FileSystemExitCode GetFile(RemotePath remoteName, string localName, CopyFlags copyFlags, RemoteInfo remoteInfo)
        {
            return RunTransfer($"Could not download {remoteName.FileName}.", FileSystemExitCode.ReadError,
                () => base.GetFile(remoteName, localName, copyFlags, remoteInfo));
        }

        public override FileSystemExitCode PutFile(string localName, RemotePath remoteName, CopyFlags copyFlags)
        {
            return RunTransfer($"Could not upload {Path.GetFileName(localName)}.", FileSystemExitCode.WriteError,
                () => base.PutFile(localName, remoteName, copyFlags));
        }

        public override async Task<FileSystemExitCode> GetFileAsync(RemotePath remoteName, string localName,
            CopyFlags copyFlags, RemoteInfo remoteInfo, Action<int> setProgress, CancellationToken token)
        {
            if (copyFlags.HasFlag(CopyFlags.Resume))
            {
                return FileSystemExitCode.NotSupported;
            }

            var overwrite = copyFlags.HasFlag(CopyFlags.Overwrite);
            if (File.Exists(localName) && !overwrite)
            {
                return FileSystemExitCode.FileExists;
            }

            return await _fileSystem.DownloadFile(remoteName, new FileInfo(localName), overwrite, setProgress,
                copyFlags.HasFlag(CopyFlags.Move), token);
        }

        public override async Task<FileSystemExitCode> PutFileAsync(string localName, RemotePath remoteName,
            CopyFlags copyFlags, Action<int> setProgress, CancellationToken token)
        {
            if (copyFlags.HasFlag(CopyFlags.Resume))
            {
                return FileSystemExitCode.NotSupported;
            }

            if (!File.Exists(localName))
            {
                return FileSystemExitCode.FileNotFound;
            }

            var result = await _fileSystem.UploadFile(new FileInfo(localName), remoteName,
                copyFlags.HasFlag(CopyFlags.Overwrite), setProgress, token);

            if (copyFlags.HasFlag(CopyFlags.Move) && result == FileSystemExitCode.OK)
            {
                File.Delete(localName);
            }

            return result;
        }

        public override ExecResult ExecuteOpen(TcWindow mainWin, RemotePath remoteName)
        {
            if (remoteName.Level == 2 && remoteName.GetSegment(1) == BlobStoragePath.ConnectionsRootName)
            {
                try
                {
                    var connection = Prompt.AskUrl("Update Connection String or Url", _fileSystem.GetConnection(remoteName.FileName));
                    if (!string.IsNullOrEmpty(connection))
                    {
                        _fileSystem.AddOrUpdateConnection(remoteName.FileName, connection);
                    }
                }
                catch (Exception exp)
                {
                    ShowError($"Could not update {remoteName.FileName}.", exp);
                }

                return ExecResult.Ok;
            }

            return ExecResult.Yourself;
        }

        public override ExecResult ExecuteProperties(TcWindow mainWin, RemotePath remoteName)
        {
            try
            {
                return _fileSystem.ShowProperties(remoteName) ? ExecResult.Ok : ExecResult.Yourself;
            }
            catch (Exception exp)
            {
                ShowError($"Could not read the properties of {remoteName.FileName}.", exp);
                return ExecResult.Ok;
            }
        }

        public override ExtractIconResult ExtractCustomIcon(RemotePath remoteName, ExtractIconFlags extractFlags)
        {
            BlobStoragePath path = remoteName;
            if (path.Path.EndsWith(".."))
            {
                return ExtractIconResult.UseDefault;
            }

            var icon = GetFolderIcon(path);
            return icon is null ? ExtractIconResult.UseDefault : ExtractIconResult.Extracted(icon);
        }

        private FileSystemExitCode RunTransfer(string action, FileSystemExitCode failureCode, Func<FileSystemExitCode> transfer)
        {
            try
            {
                return transfer();
            }
            catch (Exception exp) when (StorageError.IsCancellation(exp))
            {
                return FileSystemExitCode.UserAbort;
            }
            catch (Exception exp)
            {
                ShowBulkError(action, exp);
                return failureCode;
            }
        }

        /// <summary>Reports a failed tenant listing once, so browsing saved connections isn't interrupted on every visit to the root.</summary>
        private void ShowTenantsError(Exception exception)
        {
            if (!_tenantsErrorShown)
            {
                _tenantsErrorShown = true;
                ShowError("Could not list your Azure tenants. Saved connections are still available.", exception);
            }
        }

        /// <summary>For operations on many files: TC reports each failure itself, the reason is only shown once.</summary>
        private void ShowBulkError(string action, Exception exception)
        {
            Log.Error(action, exception);

            if (_bulkErrors.ShouldShow(StorageError.Describe(exception), DateTime.UtcNow))
            {
                ShowDialog(action, exception);
            }
        }

        private void ShowError(string action, Exception exception)
        {
            Log.Error(action, exception);
            ShowDialog(action, exception);
        }

        private void ShowDialog(string action, Exception exception)
        {
            Prompt.MsgOk(Title, $"{action}\n\n{StorageError.Describe(exception)}");
        }

        private static Icon GetFolderIcon(BlobStoragePath path)
        {
            if (path.IsContainerPath)
            {
                return Icons.container_icon;
            }

            if (path.IsConnectionPath)
            {
                return path.IsConnectionRoot ? Icons.storage_account : null;
            }

            switch (path.Level)
            {
                case 1:
                    if (path.IsConnectionsPath)
                        return Icons.settings_icon;
                    return Icons.tenant;
                case 2:
                    if (path.IsConnectionsPath)
                        return Icons.settings_icon;
                    return Icons.subscription;
                case 3:
                    return Icons.storage_account;
                default:
                    return null;
            }
        }
    }
}
