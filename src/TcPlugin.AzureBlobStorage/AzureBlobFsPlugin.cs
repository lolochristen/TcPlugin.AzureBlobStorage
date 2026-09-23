using Azure.Storage.Blobs.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TcPlugin.AzureBlobStorage.Paths;
using TcPlugin.AzureBlobStorage.Resources;
using TcPluginBase;
using TcPluginBase.FileSystem;

namespace TcPlugin.AzureBlobStorage
{
    /// <summary>
    ///     Total Commander file system plugin entry point. Maps TC callbacks onto <see cref="BlobFileSystem" />.
    /// </summary>
    public class AzureBlobFsPlugin : FsPlugin
    {
        private readonly BlobFileSystem _fileSystem;

        public AzureBlobFsPlugin(Settings pluginSettings) : base(pluginSettings)
        {
            Title = "Azure Blob Plugin";
            BackgroundFlags = FsBackgroundFlags.Download | FsBackgroundFlags.Upload;

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
            return _fileSystem.ListDirectory(path);
        }

        public override bool MkDir(RemotePath dir)
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

        public override bool RemoveDir(RemotePath dirName)
        {
            return _fileSystem.RemoveDirectory(dirName);
        }

        public override bool DeleteFile(RemotePath fileName)
        {
            if (fileName.Level == 2 && fileName.GetSegment(1) == BlobStoragePath.ConnectionsRootName)
            {
                _fileSystem.RemoveConnection(fileName.FileName);
                return true;
            }

            return _fileSystem.DeleteFile(fileName).Result;
        }

        public override FileSystemExitCode RenMovFile(RemotePath oldName, RemotePath newName, bool move, bool overwrite,
            RemoteInfo remoteInfo)
        {
            ProgressProc(oldName, newName, 0);
            try
            {
                var operation = move
                    ? _fileSystem.Move(oldName, newName, overwrite, CancellationToken.None)
                    : _fileSystem.Copy(oldName, newName, overwrite, CancellationToken.None);

                return operation.Result;
            }
            finally
            {
                ProgressProc(oldName, newName, 100);
            }
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
                var connection = Prompt.AskUrl("Update Connection String or Url", _fileSystem.GetConnection(remoteName.FileName));
                if (!string.IsNullOrEmpty(connection))
                {
                    _fileSystem.AddOrUpdateConnection(remoteName.FileName, connection);
                }
                return ExecResult.Ok;
            }

            return ExecResult.Yourself;
        }

        public override ExecResult ExecuteProperties(TcWindow mainWin, RemotePath remoteName)
        {
            return _fileSystem.ShowProperties(remoteName) ? ExecResult.Ok : ExecResult.Yourself;
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
