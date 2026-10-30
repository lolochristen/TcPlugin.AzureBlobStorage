using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using TcPlugin.AzureBlobStorage.Authentication;
using TcPlugin.AzureBlobStorage.Connections;
using TcPlugin.AzureBlobStorage.Details;
using TcPlugin.AzureBlobStorage.Management;
using TcPlugin.AzureBlobStorage.Paths;
using TcPlugin.AzureBlobStorage.Storage;
using TcPluginBase.FileSystem;

namespace TcPlugin.AzureBlobStorage
{
    /// <summary>
    ///     File system operations on Azure Blob Storage, addressed by <see cref="BlobStoragePath" />.
    /// </summary>
    public class BlobFileSystem
    {
        private static readonly TimeSpan CopyPollingInterval = TimeSpan.FromMilliseconds(100);

        private readonly ConcurrentDictionary<string, BlobItemProperties> _blobPropertiesCache = new ConcurrentDictionary<string, BlobItemProperties>();
        private readonly BlobClientFactory _clients;
        private readonly ConnectionStore _connections = new ConnectionStore();
        private readonly AzureCredentialProvider _credential = new AzureCredentialProvider();
        private readonly ItemDetailsProvider _details;
        private readonly PathCache _pathCache = new PathCache();
        private readonly AzureResourceManagerClient _resourceManager;

        public BlobFileSystem()
        {
            _clients = new BlobClientFactory(_connections, _credential);
            _resourceManager = new AzureResourceManagerClient(_credential);
            _details = new ItemDetailsProvider(_connections, _clients, _resourceManager);

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        }

        public void LoadConnections()
        {
            _connections.Load();
        }

        /// <exception cref="FormatException">The connection is not valid; nothing is saved.</exception>
        public void AddOrUpdateConnection(string name, string connection)
        {
            ConnectionStore.Validate(connection);
            _connections.AddOrUpdate(name.TrimStart(BlobStoragePath.ConnectionPrefix[0]), connection);
        }

        public string GetConnection(string name)
        {
            return _connections.Get(name);
        }

        public void RemoveConnection(string name)
        {
            _connections.Remove(name);
        }

        /// <summary>
        ///     Lists the root. Saved connections stay reachable when Azure can't be reached or nobody is signed in,
        ///     so that failure is only reported.
        /// </summary>
        public IEnumerable<FindData> ListRoot(Action<Exception> onTenantsFailed)
        {
            var items = new List<FindData>();

            try
            {
                items.AddRange(ToDirectories(_resourceManager.GetTenants()));
            }
            catch (Exception exp)
            {
                onTenantsFailed(exp);
            }

            items.AddRange(ToDirectories(_connections.Names.Select(name => BlobStoragePath.ConnectionPrefix + name)));
            items.Add(new FindData(BlobStoragePath.ConnectionsRootName, FileAttributes.Directory));
            return items;
        }

        public IEnumerable<FindData> ListDirectory(BlobStoragePath path)
        {
            if (path.Level == 1 && path.IsConnectionsPath)
            {
                return _connections.Names.Select(p => new FindData(p, FileAttributes.Normal));
            }

            if (path.IsBlobPath || path.IsContainerPath)
            {
                return GetBlobs(path);
            }

            if (path.Level == path.ContainerLevel - 1)
            {
                return GetContainers(path);
            }

            return path.Level == 1
                ? ToDirectories(_resourceManager.GetSubscriptions(path.Tenant))
                : ToDirectories(_resourceManager.GetStorageAccounts(path.Tenant, path.Subscription));
        }

        /// <summary>Blob properties captured by the last listing of the blob's directory.</summary>
        public BlobItemProperties GetBlobItemProperties(string path)
        {
            return _blobPropertiesCache.TryGetValue(NormalizeCacheKey(path), out var properties) ? properties : null;
        }

        public bool CreateDirectory(BlobStoragePath directory)
        {
            if (directory.IsBlobPath)
            {
                _pathCache.Add(directory);
                return true;
            }

            if (directory.IsContainerPath)
            {
                _clients.GetServiceClient(directory).CreateBlobContainer(directory.ContainerName);
                return true;
            }

            return false;
        }

        public bool RemoveDirectory(BlobStoragePath directory)
        {
            var removedFromCache = _pathCache.Remove(directory);

            if (directory.IsConnectionRoot)
            {
                return _connections.Remove(directory.AccountName);
            }

            if (directory.IsContainerPath)
            {
                _clients.GetServiceClient(directory).DeleteBlobContainer(directory.ContainerName);
                return true;
            }

            return directory.IsBlobPath && removedFromCache;
        }

        public async Task<bool> DeleteFile(BlobStoragePath path)
        {
            if (!path.IsBlobPath)
            {
                return false;
            }

            var blob = _clients.TryGetBlobClient(path);
            if (blob is null)
            {
                return false;
            }

            var removedFromCache = _pathCache.Remove(path);

            if (!await blob.DeleteIfExistsAsync())
            {
                return removedFromCache;
            }

            // keep the (possibly now empty) folder visible to allow adding files
            _pathCache.Add(path.Directory);
            return true;
        }

        public Task<FileSystemExitCode> Copy(BlobStoragePath source, BlobStoragePath destination, bool overwrite, CancellationToken token)
        {
            return CopyBlob(source, destination, overwrite, false, token);
        }

        public Task<FileSystemExitCode> Move(BlobStoragePath source, BlobStoragePath destination, bool overwrite, CancellationToken token)
        {
            return CopyBlob(source, destination, overwrite, true, token);
        }

        public async Task<FileSystemExitCode> DownloadFile(BlobStoragePath source, FileInfo destination, bool overwrite,
            Action<int> setProgress, bool deleteSource, CancellationToken token)
        {
            var blob = _clients.TryGetBlobClient(source);
            if (blob is null)
            {
                return FileSystemExitCode.FileNotFound;
            }

            try
            {
                var properties = await GetPropertiesOrDefault(blob, token);
                if (properties is null)
                {
                    return FileSystemExitCode.FileNotFound;
                }

                var progress = new PercentProgress(properties.ContentLength, setProgress);
                progress.Report(0);

                var mode = overwrite ? FileMode.Create : FileMode.CreateNew;
                using (var destinationStream = File.Open(destination.FullName, mode, FileAccess.Write, FileShare.Read))
                {
                    await blob.DownloadToAsync(destinationStream, new BlobDownloadToOptions { ProgressHandler = progress }, token);
                }

                if (deleteSource)
                {
                    await blob.DeleteAsync(cancellationToken: token);
                }

                progress.Report(properties.ContentLength);
                return FileSystemExitCode.OK;
            }
            catch (OperationCanceledException)
            {
                return FileSystemExitCode.UserAbort;
            }
        }

        public async Task<FileSystemExitCode> UploadFile(FileInfo source, BlobStoragePath destination, bool overwrite,
            Action<int> setProgress, CancellationToken token)
        {
            var blob = _clients.TryGetBlockBlobClient(destination);
            if (blob is null)
            {
                return FileSystemExitCode.NotSupported;
            }

            if (!overwrite && await blob.ExistsAsync(token))
            {
                return FileSystemExitCode.FileExists;
            }

            var progress = new PercentProgress(source.Length, setProgress);
            progress.Report(0);

            try
            {
                using (var sourceStream = source.OpenRead())
                {
                    await blob.UploadAsync(sourceStream,
                        new BlobHttpHeaders { ContentType = MimeTypes.GetMimeType(source.Name) },
                        conditions: overwrite ? null : new BlobRequestConditions { IfNoneMatch = ETag.All },
                        progressHandler: progress,
                        cancellationToken: token);
                }

                progress.Report(source.Length);
                return FileSystemExitCode.OK;
            }
            catch (OperationCanceledException)
            {
                return FileSystemExitCode.UserAbort;
            }
        }

        /// <returns>The properties of the item, or null if it has none to show.</returns>
        internal PropertySheet GetProperties(BlobStoragePath path)
        {
            return _details.Get(path);
        }

        private async Task<FileSystemExitCode> CopyBlob(BlobStoragePath source, BlobStoragePath destination, bool overwrite,
            bool deleteSource, CancellationToken token)
        {
            var sourceBlob = _clients.TryGetBlobClient(source);
            var destinationBlob = _clients.TryGetBlobClient(destination);

            if (sourceBlob is null || destinationBlob is null)
            {
                return FileSystemExitCode.NotSupported;
            }

            if (!overwrite && await destinationBlob.ExistsAsync(token))
            {
                return FileSystemExitCode.FileExists;
            }

            if (!await sourceBlob.ExistsAsync(token))
            {
                return FileSystemExitCode.FileNotFound;
            }

            var operation = await destinationBlob.StartCopyFromUriAsync(sourceBlob.Uri, cancellationToken: token);
            await operation.WaitForCompletionAsync(CopyPollingInterval, token);

            if (deleteSource)
            {
                await sourceBlob.DeleteIfExistsAsync(cancellationToken: token);
            }

            return FileSystemExitCode.OK;
        }

        private IEnumerable<FindData> GetContainers(BlobStoragePath path)
        {
            if (path.IsConnectionPath)
            {
                var scopedContainer = ConnectionStore.GetScopedContainer(_connections.Get(path.AccountName));
                if (scopedContainer != null)
                {
                    return new[] { new FindData(scopedContainer, FileAttributes.Directory) };
                }
            }

            try
            {
                return _clients.GetServiceClient(path)
                    .GetBlobContainers(BlobContainerTraits.Metadata)
                    .Select(container => new FindData(container.Name, 0, FileAttributes.Directory,
                        container.Properties.LastModified.LocalDateTime))
                    .ToList();
            }
            catch (RequestFailedException exp) when (exp.Status == 401 && _credential.TrySwitchToInteractiveLogin())
            {
                return GetContainers(path);
            }
        }

        private IEnumerable<FindData> GetBlobs(BlobStoragePath path)
        {
            var prefix = path.BlobPath;
            if (prefix.Length > 0)
            {
                prefix += "/";
            }

            var items = _clients.GetContainerClient(path)
                .GetBlobsByHierarchy(BlobTraits.Metadata, BlobStates.None, "/", prefix);

            var list = new List<FindData>();
            foreach (var item in items)
            {
                if (item.IsPrefix)
                {
                    list.Add(new FindData(item.Prefix.Substring(prefix.Length).Trim('/'), 0, FileAttributes.Directory));
                    continue;
                }

                var blobProperties = item.Blob.Properties;
                var findData = new FindData(
                    item.Blob.Name.Substring(prefix.Length),
                    (ulong)(blobProperties.ContentLength ?? 0),
                    blobProperties.AccessTier == AccessTier.Archive ? FileAttributes.Archive : FileAttributes.Normal,
                    blobProperties.LastModified?.LocalDateTime,
                    blobProperties.CreatedOn?.LocalDateTime,
                    blobProperties.LastModified?.LocalDateTime);

                list.Add(findData);
                _blobPropertiesCache[NormalizeCacheKey(path.Path + "/" + findData.FileName)] = blobProperties;
            }

            return _pathCache.WithCached(path, list);
        }

        private static async Task<BlobProperties> GetPropertiesOrDefault(BlobClient blob, CancellationToken token)
        {
            try
            {
                return await blob.GetPropertiesAsync(cancellationToken: token);
            }
            catch (RequestFailedException exp) when (exp.Status == 404)
            {
                return null;
            }
        }

        private static IEnumerable<FindData> ToDirectories(IEnumerable<string> names)
        {
            return names.Select(name => new FindData(name, FileAttributes.Directory));
        }

        private static string NormalizeCacheKey(string path)
        {
            return path.Replace('\\', '/').Trim('/');
        }
    }
}
