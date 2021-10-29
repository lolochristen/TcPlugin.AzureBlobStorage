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
        private readonly BlobFileSystem _fs;
        private readonly Settings _pluginSettings;

        public AzureBlobFsPlugin(Settings pluginSettings) : base(pluginSettings)
        {
            _pluginSettings = pluginSettings;
            Title = "Azure Blob Plugin";
            _fs = new BlobFileSystem();

            ContentPlugin = new AzureBlobContentPlugin(pluginSettings, _fs);

            BackgroundFlags = FsBackgroundFlags.Download | FsBackgroundFlags.Upload;

            try
            {
                _fs.LoadConnections();
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
            return _fs.DeleteFile(fileName).Result;
        }

        public override ExecResult ExecuteOpen(TcWindow mainWin, RemotePath remoteName)
        {
            CloudPath path = remoteName;

            if (path.Level == 2 && path.AccountName == BlobFileSystem.CONNECT_AZURE_TITLE)
            {
                _fs.ExecuteAzureSettings(path);
                return ExecResult.Ok;
            }

            return ExecResult.Yourself;
        }

        public override ExecResult ExecuteProperties(TcWindow mainWin, RemotePath remoteName)
        {
            if (_fs.ShowProperties(remoteName))
                return ExecResult.Ok;
            return ExecResult.Yourself;
        }

        public override ExtractIconResult ExtractCustomIcon(RemotePath remoteName, ExtractIconFlags extractFlags)
        {
            var path = new CloudPath(remoteName);

            if (path.Path.EndsWith("..")) return ExtractIconResult.UseDefault;

            if (path.Level == 1)
                switch (path)
                {
                    case "/" + BlobFileSystem.CONNECT_AZURE_TITLE:
                        return ExtractIconResult.Extracted(Icons.settings_icon);

                    default:
                        // accounts
                        return ExtractIconResult.Extracted(Icons.storage_account);
                }

            if (path.Level == 2) return ExtractIconResult.Extracted(Icons.container_icon);

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
            return await _fs.DownloadFile(
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
            if (path.Level == 1 && path.Segments[0] == BlobFileSystem.CONNECT_AZURE_TITLE)
                return _fs.GetAzureConnectOptions();

            var items = _fs.ListDirectory(path);

            if (path.Level == 0)
                return items.Append(new FindData(BlobFileSystem.CONNECT_AZURE_TITLE, FileAttributes.Directory));

            return items;
        }

        public override bool MkDir(RemotePath dir)
        {
            if (dir.Level == 1)
                return _fs.AddStorageConnectionByUrlString("Blob Connection String:", dir);
            return _fs.CacheDirectory(dir);
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
            var ret = await _fs.UploadFile(new FileInfo(localName), remoteName, overWrite,
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
            return _fs.RemoveDirectory(dirName);
        }


        public override FileSystemExitCode RenMovFile(RemotePath oldName, RemotePath newName, bool move, bool overwrite,
            RemoteInfo remoteInfo)
        {
            ProgressProc(oldName, newName, 0);
            try
            {
                if (move)
                    return _fs.Move(oldName, newName, overwrite, default).Result;
                else
                    return _fs.Copy(oldName, newName, overwrite, default).Result;
            }
            finally
            {
                ProgressProc(oldName, newName, 100);
            }
        }
    }
}
