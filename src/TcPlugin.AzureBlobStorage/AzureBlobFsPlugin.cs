using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TcPlugin.AzureBlobStorage.Resources;
using TcPluginBase;
using TcPluginBase.FileSystem;

namespace TcPlugin.AzureBlobStorage
{
    public class AzureBlobFsPlugin : FsPlugin
    {
        private readonly BlobFileSystem _blobFileSystem;
        private readonly Settings _pluginSettings;

        public AzureBlobFsPlugin(Settings pluginSettings) : base(pluginSettings)
        {
            _pluginSettings = pluginSettings;
            Title = "Azure Blob Plugin";
            _blobFileSystem = new BlobFileSystem();

            ContentPlugin = new AzureBlobContentPlugin(pluginSettings, _blobFileSystem);

            BackgroundFlags = FsBackgroundFlags.Download | FsBackgroundFlags.Upload;

            try
            {
                _blobFileSystem.LoadConnections();
            }
            catch (Exception exp)
            {
                Log.Warning("Could not read settings: " + exp);
            }

            // to debug!
            //AppDomain.CurrentDomain.FirstChanceException += (sender, eventArgs) => {
            //    Log.Error($"FirstChanceException: " + eventArgs.Exception.ToString());
            //};
        }

        public override bool DeleteFile(RemotePath fileName)
        {
            return _blobFileSystem.DeleteFile(fileName).Result;
        }

        public override ExecResult ExecuteOpen(TcWindow mainWin, RemotePath remoteName)
        {
            return ExecResult.Yourself;
            return ExecResult.Yourself;
        }

        public override ExecResult ExecuteProperties(TcWindow mainWin, RemotePath remoteName)
        {
            if (_blobFileSystem.ShowProperties(remoteName))
                return ExecResult.Ok;
            return ExecResult.Yourself;
        }

        public override ExtractIconResult ExtractCustomIcon(RemotePath remoteName, ExtractIconFlags extractFlags)
        {
            var path = new BlobStoragePath(remoteName);

            if (path.Path.EndsWith("..")) return ExtractIconResult.UseDefault;

            if (path.IsConfigurationBased)
            {
                if (path.Level == 1)
                    return ExtractIconResult.Extracted(Icons.settings_icon);
                if (path.Level == 2)
                    return ExtractIconResult.Extracted(Icons.container_icon);
            }
            else
            {
                if (path.Level == 1)
                    return ExtractIconResult.Extracted(Icons.tenant);
                if (path.Level == 2)
                    return ExtractIconResult.Extracted(Icons.subscription);
                if (path.Level == 3)
                    return ExtractIconResult.Extracted(Icons.storage_account);
                if (path.Level == 4)
                    return ExtractIconResult.Extracted(Icons.container_icon);
            }

            return ExtractIconResult.UseDefault;
        }


        public override async Task<FileSystemExitCode> GetFileAsync(RemotePath remoteName, string localName,
            CopyFlags copyFlags, RemoteInfo remoteInfo, Action<int> setProgress, CancellationToken token)
        {
            Log.Warning($"GetFile({remoteName}, {localName}, {copyFlags})");

            var overWrite = (CopyFlags.Overwrite & copyFlags) != 0;
            var performMove = (CopyFlags.Move & copyFlags) != 0;
            var resume = (CopyFlags.Resume & copyFlags) != 0;

            if (resume) return FileSystemExitCode.NotSupported;

            if (File.Exists(localName) && !overWrite) return FileSystemExitCode.FileExists;

            var prevPercent = -1;
            return await _blobFileSystem.DownloadFile(
                remoteName,
                new FileInfo(localName),
                overWrite,
                (source, destination, percent) =>
                {
                    if (percent != prevPercent)
                    {
                        prevPercent = percent;

                        setProgress(percent);
                    }
                },
                performMove,
                token
            );
        }

        public override IEnumerable<FindData> GetFiles(RemotePath path)
        {
            var items = _blobFileSystem.ListDirectory(path);
            return items;
        }

        public override bool MkDir(RemotePath dir)
        {
            if (dir.Level == 1)
            {
                var connectionString = Prompt.AskUrl("Blob Connection String or URL:");
                if (string.IsNullOrEmpty(connectionString))
                    return false;
                _blobFileSystem.AddConfiguration(dir.FileName, connectionString);
                return true;
                //return _blobFileSystem.AddStorageConnectionByUrlString("Blob Connection String:", dir);
            }
            return _blobFileSystem.CreateDirectory(dir);
        }

        public override async Task<FileSystemExitCode> PutFileAsync(string localName, RemotePath remoteName,
            CopyFlags copyFlags, Action<int> setProgress, CancellationToken token)
        {
            var overWrite = (CopyFlags.Overwrite & copyFlags) != 0;
            var performMove = (CopyFlags.Move & copyFlags) != 0;
            var resume = (CopyFlags.Resume & copyFlags) != 0;

            if (resume) return FileSystemExitCode.NotSupported;

            if (!File.Exists(localName)) return FileSystemExitCode.FileNotFound;

            var prevPercent = -1;
            var ret = await _blobFileSystem.UploadFile(new FileInfo(localName), remoteName, overWrite,
                (source, destination, percent) =>
                {
                    if (percent != prevPercent)
                    {
                        prevPercent = percent;

                        setProgress(percent);
                    }
                },
                token
            );

            if (performMove && ret == FileSystemExitCode.OK) File.Delete(localName);

            return ret;
        }

        public override bool RemoveDir(RemotePath dirName)
        {
            return _blobFileSystem.RemoveDirectory(dirName);
        }

        public override FileSystemExitCode RenMovFile(RemotePath oldName, RemotePath newName, bool move, bool overwrite,
            RemoteInfo remoteInfo)
        {
            ProgressProc(oldName, newName, 0);
            try
            {
                if (move)
                    return _blobFileSystem.Move(oldName, newName, overwrite, default).Result;
                else
                    return _blobFileSystem.Copy(oldName, newName, overwrite, default).Result;
            }
            finally
            {
                ProgressProc(oldName, newName, 100);
            }
        }
    }
}
