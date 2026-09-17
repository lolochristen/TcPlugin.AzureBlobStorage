using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Rest;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using TcPluginBase.FileSystem;
using TokenCredential = Azure.Core.TokenCredential;

// ReSharper disable LocalizableElement
namespace TcPlugin.AzureBlobStorage
{
    public delegate void FileProgress(string source, string destination, int percentDone);

    public class BlobFileSystem
    {
        private const int DEFAULT_BUFFER_SIZE = 32768;

        //public const string CONNECT_AZURE_TITLE = "Connect to Azure";

        //private const string CONNECT_STORAGE_CONNECTION_STRING = "[Connect to Storage by Connection String]";
        //private const string CONNECT_STORAGE_SASURL = "[Connect to Storage by SAS Url]";
        //private const string CONNECT_SUBSCRIPTION = "[Connect to Subscription]";
        //private const string CONNECT_BLOB_AD = "[Connect to Blob by Azure AD]";
        //private const string CONNECT_BLOB_SASURL = "[Connect to Blob by SAS Url]";
        private readonly Dictionary<string, BlobItemProperties> _blobItemPropertiesCache;
        internal readonly PathCache _pathCache;

        //private Dictionary<string, StorageConnection> _connections;

        private Dictionary<string, string> _connections;

        private Dictionary<string, Guid> _tenantsDictionary;

        public BlobFileSystem()
        {
            //_connections = new Dictionary<string, StorageConnection>();
            _connections = new Dictionary<string, string>();
            _pathCache = new PathCache();
            _blobItemPropertiesCache = new Dictionary<string, BlobItemProperties>();

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        }

        private string ConnectionSettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FsAzureStorage\\connections.bin");

        //public bool AddNewStorageConnection(string containerString)
        //{
        //    var storageConnection = StorageConnection.Parse(containerString);

        //    if (storageConnection == null)
        //        return false;

        //    if (storageConnection.RequiresAad)
        //    {
        //        // logon and select tenant
        //        var tenants = GetTenants(out var credentials, out var authenticatedTenantId);
        //        var tenantWindow = new SelectTenantWindow();
        //        SetOwnerWindow(tenantWindow);
        //        tenantWindow.SetTenants(tenants);
        //        if (tenantWindow.ShowDialog() == true)
        //        {
        //            storageConnection.TenantId = tenantWindow.SelectedTenant.Key;
        //            if (authenticatedTenantId == storageConnection.TenantId) // reuse if they match
        //                storageConnection.Credential = credentials;
        //            storageConnection.UseActiveDirectory = true;
        //        }
        //        else
        //        {
        //            return false;
        //        }
        //    }

        //    AddReplaceStorageConnection(storageConnection);
        //    return true;
        //}

        //public void AddNewStorageConnection(string accountName, string accountKey,
        //    TokenCredential tokenCredential = null, string tenantId = null)
        //{
        //    var storageConnection = StorageConnection.FromAccountKey(accountName, accountKey);

        //    if (tokenCredential != null)
        //    {
        //        storageConnection.TenantId = tenantId;
        //        storageConnection.Credential = tokenCredential;
        //    }

        //    AddReplaceStorageConnection(storageConnection);
        //}

        //public bool AddStorageConnectionByUrlString(string label = null, string connectionString = null)
        //{
        //    // TODO Label
        //    var connect = new EnterConnectionInfoWindow();
        //    SetOwnerWindow(connect);
        //    if (label != null)
        //        connect.Label = label;
        //    if (connectionString != null)
        //        connect.ConnectionInfoText = connectionString;
        //    var result = connect.ShowDialog();
        //    if (result != null && result.Value)
        //    {
        //        AddNewStorageConnection(connect.ConnectionInfoText);
        //        SaveConnections();
        //        return true;
        //    }

        //    return false;
        //}

        //public bool AddStorageConnectionsBySubscription()
        //{
        //    try
        //    {
        //        var tenants = GetTenants(out var credentials, out var authenticatedTenantId);
        //        if (tenants == null)
        //            return false;

        //        string tenantId = null;
        //        if (tenants.Count == 0)
        //            return false;

        //        if (tenants.Count == 1)
        //        {
        //            tenantId = tenants.First().Value;
        //        }
        //        else
        //        {
        //            var tenantWindow = new SelectTenantWindow();
        //            SetOwnerWindow(tenantWindow);
        //            tenantWindow.SetTenants(tenants);
        //            if (tenantWindow.ShowDialog() == true)
        //            {
        //                tenantId = tenantWindow.SelectedTenant.Key;
        //                if (tenantId != authenticatedTenantId)
        //                    credentials = null; // new authentication
        //            }
        //        }

        //        var subscriptions = GetSubscriptions(tenantId, ref credentials);
        //        if (subscriptions == null)
        //            return false;

        //        string subscriptionId = null;

        //        if (subscriptions.Count() == 1)
        //        {
        //            subscriptionId = subscriptions.First().SubscriptionId;
        //        }
        //        else
        //        {
        //            var subWindow = new SelectSubscriptionWindow();
        //            SetOwnerWindow(subWindow);
        //            subWindow.SetSubscriptions(subscriptions);
        //            if (subWindow.ShowDialog() == true)
        //                subscriptionId = subWindow.SelectedSubscription.SubscriptionId;
        //        }

        //        if (subscriptionId != null)
        //        {
        //            AddStorageAccountsFromSubscription(subscriptionId, credentials, tenantId);
        //            return true;
        //        }
        //    }
        //    catch
        //    {
        //        // ?
        //    }

        //    return false;
        //}

        public bool CreateDirectory(BlobStoragePath dir)
        {
            if (dir.IsBlobPath)
            {
                _pathCache.Add(dir);
                return true;
            }

            if ((dir.IsConfigurationBased && dir.Level == 2) ||
                (dir.IsConfigurationBased == false && dir.Level == 4))
            {
                // Create container
                var client = GetBlobServiceClient(dir);
                client.CreateBlobContainer(dir.ContainerName);
                return true;
            }

            return false;
        }

        public async Task<FileSystemExitCode> Copy(BlobStoragePath sourceFileName, BlobStoragePath destFileName, bool overwrite,
            CancellationToken token)
        {
            var source = GetBlobClient(sourceFileName);
            var target = GetBlobClient(destFileName);

            if (source is null || target is null) return FileSystemExitCode.NotSupported;

            if (!overwrite && await target.ExistsAsync(token)) return FileSystemExitCode.FileExists;

            var res = await CopyAndOverwrite(source, target, token);
            if (res != FileSystemExitCode.OK) return res;

            if (!await target.ExistsAsync(token))
                throw new Exception("Move failed because the target file wasn't created.");

            return FileSystemExitCode.OK;
        }

        public async Task<bool> DeleteFile(BlobStoragePath path)
        {
            if (path.Level == 1)
            {
                if (_connections.ContainsKey(path.Tenant))
                {
                    _connections.Remove(path.ContainerName);
                    SaveConnections();
                    return true;
                }
                return false;
            }

            var blob = GetBlobClient(path);
            if (blob is null) return false;

            var success = RemoveDirectory(path);

            if (await blob.DeleteIfExistsAsync())
            {
                // cache the directory to allow adding some files
                _pathCache.Add(path.Directory);
                return true;
            }

            return success;
        }

        public async Task<FileSystemExitCode> DownloadFile(BlobStoragePath srcFileName, FileInfo dstFileName, bool overwrite,
            FileProgress fileProgress, bool deleteAfter = false, CancellationToken token = default)
        {
            var blob = GetBlobClient(srcFileName);
            if (blob is null || !await blob.ExistsAsync(token)) return FileSystemExitCode.FileNotFound;

            try
            {
                var result = await blob.DownloadAsync(token);
                var downloadInfo = result.Value;
                var fileSize = downloadInfo.ContentLength;

                void Progress(long transfered)
                {
                    var percent = fileSize == 0
                        ? 0
                        : decimal.ToInt32(transfered * 100 / (decimal)fileSize);

                    fileProgress(srcFileName, dstFileName.FullName, percent);
                }

                Progress(0);

                var mode = overwrite ? FileMode.Create : FileMode.CreateNew;

                using var destStream = File.Open(dstFileName.FullName, mode, FileAccess.Write, FileShare.Read);

                await downloadInfo.Content.CopyToAsync(destStream, DEFAULT_BUFFER_SIZE,
                    new CopyProgressInfoCallback(Progress), token);

                if (deleteAfter) await blob.DeleteAsync(cancellationToken: token);

                Progress(fileSize);
                return FileSystemExitCode.OK;
            }
            catch (TaskCanceledException)
            {
                return FileSystemExitCode.UserAbort;
            }
        }

        //public void ExecuteAzureSettings(BlobStoragePath path)
        //{
        //    switch (path.ContainerName)
        //    {
        //        case CONNECT_STORAGE_SASURL:
        //            AddStorageConnectionByUrlString("SAS Storage Url:",
        //                "https://[StorageAccount].blob.core.windows.net/?[SASKey]");
        //            break;
        //        case CONNECT_STORAGE_CONNECTION_STRING:
        //            AddStorageConnectionByUrlString("Storage Connection String:",
        //                "DefaultEndpointsProtocol=https;AccountName=[StorageAccount];AccountKey=[Key];EndpointSuffix=core.windows.net");
        //            break;
        //        case CONNECT_BLOB_AD:
        //            AddStorageConnectionByUrlString("Storage Connection String:",
        //                "https://[StorageAccount].blob.core.windows.net/[Container]?");
        //            break;
        //        case CONNECT_BLOB_SASURL:
        //            AddStorageConnectionByUrlString("SAS Blob Url:",
        //                "https://[StorageAccount].blob.core.windows.net/[Container]?[SASKey]");
        //            break;

        //        case CONNECT_SUBSCRIPTION:
        //            AddStorageConnectionsBySubscription();

        //            // select 
        //            break;
        //    }
        //}

        //public IEnumerable<FindData> GetAzureConnectOptions()
        //{
        //    var items = new List<FindData>
        //    {
        //        new(CONNECT_STORAGE_CONNECTION_STRING, FileAttributes.Normal),
        //        new(CONNECT_STORAGE_SASURL, FileAttributes.Normal),
        //        new(CONNECT_BLOB_SASURL, FileAttributes.Normal),
        //        new(CONNECT_BLOB_AD, FileAttributes.Normal),
        //        new(CONNECT_SUBSCRIPTION, FileAttributes.Normal)
        //    };

        //    items.AddRange(_connections.Select(p => new FindData(p.Key, FileAttributes.Normal)));

        //    return items;
        //}

        public BlobItemProperties GetBlobItemProperties(string path)
        {
            path = path.Replace('\\', '/');
            if (_blobItemPropertiesCache.TryGetValue(path, out var properties))
                return properties;
            return null;
        }

        public IEnumerable<FindData> ListDirectory(BlobStoragePath path)
        {
            if (path.Level == 0)
            {
                var items = GetTenantsList();
                return items.Concat(GetConfigItems());
            }
            if (path.IsConfigurationBased)
            {
                switch (path.Level)
                {
                    case 1:
                        return GetContainers(path);
                    default:
                        return GetBlobs(path);
                }
            }
            else
            {
                switch (path.Level)
                {
                    case 1:
                        return GetSubscriptions(path.Tenant);
                    case 2:
                        return GetStorageAccounts(path.Tenant, path.Subscription);
                    case 3:
                        return GetContainers(path);
                    default:
                        return GetBlobs(path);
                }
            }
        }

        public void LoadConnections()
        {
            var buffer = new byte[16000];
            using var fs = File.OpenRead(ConnectionSettingsPath);
            var read = fs.Read(buffer, 0, buffer.Length);
            var decrypted = ProtectedData.Unprotect(buffer, null, DataProtectionScope.CurrentUser);
            var reader = new Utf8JsonReader(decrypted);
            _connections = JsonSerializer.Deserialize<Dictionary<string, string>>(ref reader);
        }


        public async Task<FileSystemExitCode> Move(BlobStoragePath sourceFileName, BlobStoragePath destFileName, bool overwrite,
            CancellationToken token)
        {
            var source = GetBlobClient(sourceFileName);
            var target = GetBlobClient(destFileName);

            if (source is null || target is null) return FileSystemExitCode.NotSupported;

            if (!overwrite && await target.ExistsAsync(token)) return FileSystemExitCode.FileExists;

            var res = await CopyAndOverwrite(source, target, token);
            if (res != FileSystemExitCode.OK) return res;

            if (!await target.ExistsAsync(token))
                throw new Exception("Move failed because the target file wasn't created.");

            await source.DeleteIfExistsAsync(cancellationToken: token);
            return FileSystemExitCode.OK;
        }


        public bool RemoveDirectory(BlobStoragePath directory)
        {
            _pathCache.Remove(directory);

            // remove config
            if (directory.Level == 1)
            {
                // remove config
                if (_connections.ContainsKey(directory.AccountName))
                {
                    _connections.Remove(directory.AccountName);
                    SaveConnections();
                    return true;
                }
            }

            return false;
        }


        public void SaveConnections()
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(_connections);
            var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            Directory.CreateDirectory(Path.GetDirectoryName(ConnectionSettingsPath));
            using var fs = File.Create(ConnectionSettingsPath);
            fs.Write(encrypted, 0, encrypted.Length);
        }

        public bool ShowProperties(BlobStoragePath cloudPath)
        {
            if (!cloudPath.IsBlobPath)
                return false;

            try
            {
                var client = GetBlobClient(cloudPath);
                var properties = client.GetProperties();

                var propJson =
                    JsonSerializer.Serialize(properties,
                        new JsonSerializerOptions
                            { WriteIndented = true, Converters = { new JsonStringEnumConverter() } });

                //MessageBox.Show(propJson, "Properties " + cloudPath.BlobName, MessageBoxButton.OK, MessageBoxImage.Information);
                MessageBox.Show(propJson, "Properties " + cloudPath.BlobName, MessageBoxButtons.OK);
                return true;
            }
            catch
            {
                return false;
            }
        }


        public async Task<FileSystemExitCode> UploadFile(FileInfo srcFileName, BlobStoragePath dstFileName, bool overwrite,
            FileProgress fileProgress, CancellationToken token = default)
        {
            var blob = GetBlockBlobClient(dstFileName);
            if (blob == null) return FileSystemExitCode.NotSupported;

            if (!overwrite && await blob.ExistsAsync(token)) return FileSystemExitCode.FileExists;

            var fileSize = srcFileName.Length;

            Progress(0);

            void Progress(long transfered)
            {
                var percent = fileSize == 0
                    ? 0
                    : decimal.ToInt32(transfered * 100 / (decimal)fileSize);

                fileProgress(dstFileName, srcFileName.FullName, percent);
            }

            try
            {
                string contentType = MimeTypes.GetMimeType(srcFileName.Name);
                using var sourceStream = srcFileName.OpenRead();
                await blob.UploadAsync(sourceStream,
                    conditions: overwrite ? null : new BlobRequestConditions { IfNoneMatch = ETag.All },
                    progressHandler: new Progress<long>(Progress), cancellationToken: token, httpHeaders: new BlobHttpHeaders() { ContentType = contentType});

                Progress(fileSize);

                return FileSystemExitCode.OK;
            }
            catch (TaskCanceledException)
            {
                return FileSystemExitCode.UserAbort;
            }
        }

        //private void AddReplaceStorageConnection(StorageConnection storageConnection)
        //{
        //    if (_connections.ContainsKey(storageConnection.AccountName))
        //        _connections[storageConnection.AccountName] = storageConnection;
        //    else
        //        _connections.Add(storageConnection.AccountName, storageConnection);
        //}

        //private void AddStorageAccountsFromSubscription(string subscriptionId, TokenCredential tokenCredential,
        //    string tenantId)
        //{
        //    var _requestContext =
        //        new TokenRequestContext(new[] { "https://management.azure.com/.default" }, tenantId: tenantId);

        //    var token = tokenCredential.GetToken(_requestContext, CancellationToken.None);
        //    var tokenCredentials = new TokenCredentials(token.Token);

        //    //var client = new Microsoft.Azure.Management.Storage.StorageManagementClient(tokenCredentials);
        //    //var storageAccounts = client.StorageAccounts.List();

        //    var azureCredentials = new AzureCredentials(tokenCredentials, tokenCredentials, tenantId,
        //        AzureEnvironment.AzureGlobalCloud);

        //    var storageAccounts = Microsoft.Azure.Management.Fluent.Azure.Authenticate(azureCredentials)
        //        .WithSubscription(subscriptionId).StorageAccounts.List();

        //    foreach (var storageAccount in storageAccounts)
        //    {
        //        //var keys = client.StorageAccounts.ListKeys("", storageAccount.Name);
        //        // ? check if only aad is supported?
        //        var keys = storageAccount.GetKeys();
        //        //_fs.AddNewStorageConnection(storageAccount.Name, keys.First().Value, azureCredentials);
        //        AddNewStorageConnection(storageAccount.Name, keys.First().Value, tokenCredential, tenantId);
        //    }

        //    MessageBox.Show($"{storageAccounts.Count()} Storage Accounts added.");

        //    SaveConnections();
        //}

        private static async Task<FileSystemExitCode> CopyAndOverwrite(BlobClient src, BlobClient dst,
            CancellationToken token)
        {
            if (!await src.ExistsAsync(token)) return FileSystemExitCode.FileNotFound;

            // sas???

            var copy = await dst.StartCopyFromUriAsync(src.Uri, cancellationToken: token);
            while (copy.HasCompleted == false)
            {
                Thread.Sleep(
                    100); // prevent endless loops: https://stackoverflow.com/questions/14152087/copying-one-azure-blob-to-another-blob-in-azure-storage-client-2-0#42255582
                await copy.UpdateStatusAsync(token);
                //await dst.FetchAttributesAsync(token);
            }

            await copy.WaitForCompletionAsync(token);

            if (copy.HasCompleted == false) throw new Exception("Copy Blob failed,");

            return FileSystemExitCode.OK;
        }

        //private BlobServiceClient GetAccountClient(string accountName)
        //{
        //    if (!_connections.TryGetValue(accountName, out var conn))
        //        return null;

        //    if (conn.UseActiveDirectory || conn.RequiresAad)
        //    {
        //        if (conn.Credential == null)
        //        {
        //            // logon
        //            var tenantId = conn.TenantId;
        //            var credential = LogonToAzure(ref tenantId);
        //            if (credential == null)
        //                return null; // ?
        //            conn.Credential = credential;
        //        }

        //        if (conn.Url == null)
        //        {
        //            var uri = conn.GetBaseUriFromConnectionString();
        //            return new BlobServiceClient(uri, conn.Credential);
        //        }

        //        return new BlobServiceClient(conn.Url, conn.Credential);
        //    }

        //    if (conn.IsStorageSAS)
        //        return new BlobServiceClient(conn.Url);
        //    if (conn.IsContainerSAS)
        //        return null;


        //    return new BlobServiceClient(conn.ConnectionString);
        //}

        private IEnumerable<FindData> GetConfigItems()
        {
            return _connections.Keys.Select(p => new FindData($"#{p}", FileAttributes.Directory) );
        }

        private IEnumerable<FindData> GetStorageAccounts(string tenant, string subscription)
        {
            // GET https://management.azure.com/subscriptions/{subscriptionId}/providers/Microsoft.Storage/storageAccounts?api-version=2023-01-01

            var subscriptionId = GetSubscriptionId(tenant, subscription);

            var request = new HttpRequestMessage(HttpMethod.Get,
                $"https://management.azure.com/subscriptions/{subscriptionId}/providers/Microsoft.Storage/storageAccounts?api-version=2023-01-01");

            // TODO cache
            var tokenCredentials = new TokenCredentials(GetAccessToken(GetTenantId(tenant).ToString()).Token);

            tokenCredentials.ProcessHttpRequestAsync(request, CancellationToken.None).Wait();
            var httpClient = new HttpClient();
            var task = httpClient.SendAsync(request);
            task.Wait();

            var result = JsonDocument.Parse(task.Result.Content.ReadAsStringAsync().Result);

            var saDic = result.RootElement.GetProperty("value").EnumerateArray()
                .ToDictionary(p => p.GetProperty("name").GetString(),
                    p => p.GetProperty("id").GetString());

            return saDic.Select(p => new FindData(p.Key, FileAttributes.Directory));
        }

        private Guid GetTenantId(string tenant)
        {
            if (Guid.TryParse(tenant, out var tenantId))
                return tenantId;

            if (_tenantsDictionary == null)
            {
                GetTenantsList();
            }

            if (_tenantsDictionary.ContainsKey(tenant))
                return _tenantsDictionary[tenant];

            return Guid.Empty;
        }

        private Dictionary<Guid, Dictionary<string, Guid>> _subscriptionsDictionary = new();

        private Guid GetSubscriptionId(string tenant, string subscription)
        {
            var tenantId = GetTenantId(tenant);

            if (!_subscriptionsDictionary.ContainsKey(tenantId))
            {
                GetSubscriptions(tenant); // try to read
            }

            if (_subscriptionsDictionary.ContainsKey(tenantId))
            {
                Guid subscriptionId;
                if (!Guid.TryParse(tenant, out subscriptionId))
                {
                    if (_subscriptionsDictionary[tenantId].ContainsKey(subscription))
                        return _subscriptionsDictionary[tenantId][subscription];
                }
            }
            return Guid.Empty;
        }

        private IEnumerable<FindData> GetSubscriptions(string tenant, bool reload = false)
        {
            var tenantId = GetTenantId(tenant);

            if (!_subscriptionsDictionary.ContainsKey(tenantId) || reload)
            {
                var request = new HttpRequestMessage(HttpMethod.Get,
                    "https://management.azure.com/subscriptions?api-version=2022-12-01");

                var tokenCredentials = new TokenCredentials(GetAccessToken(tenantId.ToString()).Token);

                tokenCredentials.ProcessHttpRequestAsync(request, CancellationToken.None).Wait();
                var httpClient = new HttpClient();
                var task = httpClient.SendAsync(request);
                task.Wait();

                var result = JsonDocument.Parse(task.Result.Content.ReadAsStringAsync().Result);

                var subDic = result.RootElement.GetProperty("value").EnumerateArray()
                    .ToDictionary(p => p.GetProperty("displayName").GetString(),
                        p => p.GetProperty("subscriptionId").GetGuid());

                _subscriptionsDictionary[tenantId] = subDic;
            }

            return _subscriptionsDictionary[tenantId].Select(p => new FindData(p.Key, FileAttributes.Directory));
        }

        private AccessToken GetAccessToken(string tenantId = null)
        {
            // TODO Cache
            var credential = GetAzureTokenCredential();

            var requestContext =
                new TokenRequestContext(new[] { "https://management.azure.com/.default" }, tenantId: tenantId);

            var token = credential.GetToken(requestContext, CancellationToken.None);

            return token;
        }

        private Azure.Core.TokenCredential GetAzureTokenCredential()
        {
            if (_credentialCache != null)
                return _credentialCache; 

            var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions()
            {
                ExcludeVisualStudioCodeCredential = true,
                ExcludeVisualStudioCredential = true,
                ExcludeManagedIdentityCredential = true,
                ExcludeWorkloadIdentityCredential = true,
                ExcludeInteractiveBrowserCredential = false
            });

            _credentialCache = credential;
            return credential;
        }

        private static TokenCredential _credentialCache; 

        private IEnumerable<FindData> GetTenantsList(bool reload = false)
        {
            if (_tenantsDictionary == null || reload)
            {
                var request = new HttpRequestMessage(HttpMethod.Get,
                    "https://management.azure.com/tenants?api-version=2022-12-01");

                var tokenCredentials = new TokenCredentials(GetAccessToken().Token);

                tokenCredentials.ProcessHttpRequestAsync(request, CancellationToken.None).Wait();
                var httpClient = new HttpClient();
                var task = httpClient.SendAsync(request);
                task.Wait();

                var tenantsDocument = JsonDocument.Parse(task.Result.Content.ReadAsStringAsync().Result);

                _tenantsDictionary = tenantsDocument.RootElement.GetProperty("value").EnumerateArray()
                    .ToDictionary(p => p.GetProperty("defaultDomain").GetString(),
                        p => p.GetProperty("tenantId").GetGuid());
            }

            return _tenantsDictionary.Select(p => new FindData(p.Key, FileAttributes.Directory) );
        }

        private BlobClient GetBlobClient(BlobStoragePath path)
        {
            try
            {
                var containerClient = GetBlobContainerClient(path);
                return containerClient.GetBlobClient(path.BlobPath);

                //if (azurePath.IsConfigItemPath)
                //{

                //    var containerClient = GetBlobContainerClient(azurePath);
                //    return containerClient.GetBlobClient(azurePath.BlobPath);
                //}

                //var tokenCredential = GetAzureTokenCredential();
                //return new BlobClient(GetBlobUri(azurePath), tokenCredential);

                //return GetAccountClient(azurePath.AccountName)
                //    ?.GetBlobContainerClient(azurePath.ContainerName)
                //    ?.GetBlobClient(azurePath.BlobName);
            }
            catch
            {
                return null;
            }
        }

        private BlockBlobClient GetBlockBlobClient(BlobStoragePath path)
        {
            try
            {
                var containerClient = GetBlobContainerClient(path);
                return containerClient.GetBlockBlobClient(path.BlobPath);

                //if (azurePath.IsConfigItemPath)
                //{
                //    var connection = GetStorageConnection(azurePath);
                //    return new BlockBlobClient(connection.ConnectionString, azurePath.ContainerName, azurePath.BlobPath);
                //}

                //var tokenCredential = GetAzureTokenCredential();
                //return new BlockBlobClient(GetBlobUri(azurePath), tokenCredential);

                //return GetAccountClient(blobFileName.AccountName)
                //    ?.GetBlobContainerClient(blobFileName.ContainerName)
                //    ?.GetBlockBlobClient(blobFileName.BlobName);
            }
            catch
            {
                return null;
            }
        }


        private static Dictionary<string, BlobContainerClient> _blobContainerClientCache = new Dictionary<string, BlobContainerClient>();

        private BlobContainerClient GetBlobContainerClient(BlobStoragePath path)
        {
            BlobContainerClient containerClient;

            if (path.IsConfigurationBased)
            {
                if (_blobContainerClientCache.ContainsKey(path.ContainerName))
                {
                    containerClient = _blobContainerClientCache[path.ContainerName];
                }
                else
                {
                    var connection = _connections[path.AccountName];
                    var connectionType = GetConnectionType(connection);

                    switch (connectionType)
                    {
                        case BlobConnectionType.ContainerUrl:
                        case BlobConnectionType.ContainerUrlWithSAS:
                            containerClient = new BlobContainerClient(new Uri(connection));
                            break;
                        case BlobConnectionType.ServiceUrlWithSAS:
                            containerClient = new BlobServiceClient(new Uri(connection)).GetBlobContainerClient(path.ContainerName);
                            break;
                        case BlobConnectionType.ServiceUrl:
                            containerClient = new BlobServiceClient(new Uri(connection), GetAzureTokenCredential()).GetBlobContainerClient(path.ContainerName);
                            break;
                        default:
                            containerClient = new BlobContainerClient(connection, path.ContainerName);
                            break;
                    }

                    _blobContainerClientCache.Add(path.ContainerName, containerClient);
                }
            }
            else
            {
                var endpoint = GetEndpointUri(path);
                if (_blobContainerClientCache.ContainsKey(endpoint.ToString()))
                {
                    containerClient = _blobContainerClientCache[endpoint.ToString()];
                }
                else
                {
                    containerClient = new BlobContainerClient(endpoint, GetAzureTokenCredential());
                    _blobContainerClientCache.Add(endpoint.ToString(), containerClient);
                }
            }
            return containerClient;
        }

        private BlobServiceClient GetBlobServiceClient(BlobStoragePath path)
        {
            if (path.IsConfigurationBased)
            {
                var connection = _connections[path.AccountName];
                var connectionType = GetConnectionType(connection);

                switch (connectionType)
                {
                    case BlobConnectionType.ContainerUrl:
                    case BlobConnectionType.ServiceUrl:
                        return new BlobServiceClient(new Uri(new Uri(connection).GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped)), GetAzureTokenCredential());
                    default:
                        return new BlobServiceClient(new Uri(connection));
                }
            }
            else
            {
                var endpoint = GetEndpointUri(path);
                return new BlobServiceClient(new Uri(endpoint.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped)), GetAzureTokenCredential());
            }
        }

        private IEnumerable<FindData> GetBlobs(BlobStoragePath azurePath)
        {
            var prefix = azurePath.BlobPath;
            if (prefix.Length > 0 && !prefix.EndsWith("/"))
                prefix += "/";

            //BlobContainerClient container;

            //var connection = GetStorageConnection(path);
            //if (!connection.IsContainerSAS)
            //    container = GetAccountClient(path.AccountName)?.GetBlobContainerClient(path.ContainerName);
            //else
            //    container = new BlobContainerClient(connection.Url);

            //if (container == null) return Array.Empty<FindData>();

            var containerClient = GetBlobContainerClient(azurePath);

            var list = new List<FindData>();
            var items = containerClient.GetBlobsByHierarchy(BlobTraits.Metadata, BlobStates.None, "/", prefix);

            foreach (var blobItem in items)
            {
                var findData = blobItem.IsPrefix
                    ? new FindData(
                        blobItem.Prefix.Substring(prefix.Length).Trim('/'),
                        0,
                        FileAttributes.Directory)
                    : new FindData(
                        blobItem.Blob.Name.Substring(prefix.Length),
                        (ulong)(blobItem.Blob.Properties.ContentLength ?? 0),
                        blobItem.Blob.Properties.AccessTier == AccessTier.Archive
                            ? FileAttributes.Archive
                            : FileAttributes.Normal,
                        blobItem.Blob.Properties.LastModified?.LocalDateTime,
                        blobItem.Blob.Properties.CreatedOn?.LocalDateTime,
                        blobItem.Blob.Properties.LastModified?.LocalDateTime);

                list.Add(findData);

                if (blobItem.Blob != null)
                    _blobItemPropertiesCache[azurePath.Path.Trim('/') + '/' + findData.FileName] = blobItem.Blob.Properties;
            }

            return _pathCache
                .WithCached(azurePath, list)
                .DefaultIfEmpty(new FindData("..", FileAttributes.Directory));
        }


        private IEnumerable<FindData> GetContainers(BlobStoragePath azurePath)
        {
            //var connection = GetStorageConnection(accountName);

            //// show container name again
            //if (connection.IsContainerSAS)
            //    return new[] { new FindData(connection.ContainerName, FileAttributes.Directory) };
            //var tokenCredentials = new TokenCredentials(GetAccessToken(GetTenantId(tenant).ToString()).Token);

            BlobServiceClient client;

            if (azurePath.IsConfigurationBased)
            {
                var connection = _connections[azurePath.AccountName];
                var connectionType = GetConnectionType(connection);

                switch (connectionType)
                {
                    case BlobConnectionType.ContainerUrl:
                    case BlobConnectionType.ContainerUrlWithSAS:
                        var uri = new Uri(connection);
                        return new List<FindData> { new FindData(uri.AbsolutePath.Trim('/'), FileAttributes.Directory) };
                    case BlobConnectionType.ServiceUrlWithSAS:
                        client = new BlobServiceClient(new Uri(connection));
                        break;
                    case BlobConnectionType.ServiceUrl:
                        client = new BlobServiceClient(new Uri(connection), GetAzureTokenCredential());
                        break;
                    default:
                        client = new BlobServiceClient(connection);
                        break;
                }
            }
            else
            {
                client = new BlobServiceClient(GetEndpointUri(azurePath), GetAzureTokenCredential());
            }

            //var client = GetAccountClient(accountName);
            if (client == null) return Enumerable.Empty<FindData>();

            try
            {
                var list = client.GetBlobContainers(BlobContainerTraits.Metadata).ToList();

                return list.Select(_ => new FindData(
                    _.Name,
                    0,
                    FileAttributes.Directory,
                    _.Properties.LastModified.LocalDateTime
                ));
            }
            catch (RequestFailedException exp)
            {
                if (exp.Status == 401)
                {
                    // try with re authentication
                    _credentialCache = new InteractiveBrowserCredential();
                    return GetContainers(azurePath);
                }

                //if (exp.ErrorCode == "KeyBasedAuthenticationNotPermitted")
                //{
                //    // need to try using AD
                //    //connection.UseActiveDirectory = true;
                //    return GetContainers(accountName); // retry
                //}

                // AuthorizationPermissionMismatch
                MessageBox.Show($"Cannot connect to Azure {exp.ErrorCode}", "Azure Error", MessageBoxButtons.OK);
                return null;
            }
        }

        //private string GetConnectionString(BlobStoragePath azurePath)
        //{
        //    // DefaultEndpointsProtocol=https;AccountName=zmrsd1saai;AccountKey=...==;EndpointSuffix=core.windows.net
        //    return $"Endpoint=https://{azurePath.AccountName}.blob.core.windows.net";
        //}

        private Uri GetEndpointUri(BlobStoragePath azurePath)
        {
            // DefaultEndpointsProtocol=https;AccountName=zmrsd1saai;AccountKey=...==;EndpointSuffix=core.windows.net
            if (!string.IsNullOrEmpty(azurePath.ContainerName))
            {
                return new Uri($"https://{azurePath.AccountName}.blob.core.windows.net/{azurePath.ContainerName}");
            }
            return new Uri($"https://{azurePath.AccountName}.blob.core.windows.net");
        }

        //private Uri GetBlobUri(BlobStoragePath azurePath)
        //{
        //    return new Uri($"https://{azurePath.AccountName}.blob.core.windows.net/{azurePath.ContainerName}/{azurePath.BlobPath}");
        //}

        //private StorageConnection GetStorageConnection(BlobStoragePath cloudPath)
        //{
        //    return _connections[cloudPath.AccountName];
        //}

        //private StorageConnection GetStorageConnection(string accountName)
        //{
        //    return _connections[accountName];
        //}

        //private IEnumerable<ISubscription> GetSubscriptions(string tenantId, ref Azure.Core.TokenCredential tokenCredential)
        //{
        //    var _requestContext =
        //        new TokenRequestContext(new[] { "https://management.azure.com/.default" }, tenantId: tenantId);

        //    if (tokenCredential == null)
        //    {
        //        var newTokenCredential = new InteractiveBrowserCredential(
        //            new InteractiveBrowserCredentialOptions { TenantId = tenantId });
        //        var auth = newTokenCredential.Authenticate(_requestContext);

        //        if (auth == null)
        //        {
        //            tokenCredential = null;
        //            return null;
        //        }

        //        tokenCredential = newTokenCredential;
        //    }

        //    var token = tokenCredential.GetToken(_requestContext, CancellationToken.None);
        //    var tokenCredentials = new TokenCredentials(token.Token);
        //    var azureCredentials = new AzureCredentials(tokenCredentials, tokenCredentials, tenantId,
        //        AzureEnvironment.AzureGlobalCloud);

        //    //var subscriptionClient = new Microsoft.Azure.Management.ResourceManager.SubscriptionClient(tokenCredentials);
        //    //return subscriptionClient.Subscriptions.List();

        //    var azure = Microsoft.Azure.Management.Fluent.Azure.Authenticate(azureCredentials);
        //    var subscriptions = azure.Subscriptions.List();
        //    return subscriptions;
        //}

        //private Dictionary<string, string> GetTenants(out TokenCredential tokenCredential,
        //    out string authenticatedTenantId)
        //{
        //    var _requestContext =
        //        new TokenRequestContext(new[] { "https://management.azure.com/.default" });

        //    authenticatedTenantId = null;
        //    tokenCredential = LogonToAzure(ref authenticatedTenantId);

        //    var token = tokenCredential.GetToken(_requestContext, CancellationToken.None);
        //    var tokenCredentials = new TokenCredentials(token.Token);
        //    //var azureCredentials = new AzureCredentials(tokenCredentials, tokenCredentials, null, AzureEnvironment.AzureGlobalCloud);

        //    var request = new HttpRequestMessage(HttpMethod.Get,
        //        "https://management.azure.com/tenants?api-version=2020-01-01");
        //    tokenCredentials.ProcessHttpRequestAsync(request, CancellationToken.None).Wait();
        //    var httpClient = new HttpClient();
        //    var task = httpClient.SendAsync(request);
        //    task.Wait();

        //    var tenantsDocument = JsonDocument.Parse(task.Result.Content.ReadAsStringAsync().Result);

        //    var tenantsDic = tenantsDocument.RootElement.GetProperty("value").EnumerateArray()
        //        .ToDictionary(p => p.GetProperty("tenantId").GetString(),
        //            p => p.GetProperty("displayName").GetString());

        //    return tenantsDic;

        //    //var subscriptionClient = new Microsoft.Azure.Management.ResourceManager.SubscriptionClient(tokenCredentials);
        //    //return subscriptionClient.Tenants.List();

        //    // this just returns only the id. no display name
        //    //return Microsoft.Azure.Management.Fluent.Azure
        //    //    .Authenticate(azureCredentials)
        //    //    .Tenants
        //    //    .List();
        //}

        //private TokenCredential LogonToAzure(ref string tenantId)
        //{
        //    try
        //    {
        //        var _requestContext =
        //            new TokenRequestContext(new[] { "https://management.azure.com/.default" }, tenantId: tenantId);

        //        var tokenCredential = new InteractiveBrowserCredential(
        //            new InteractiveBrowserCredentialOptions { TenantId = tenantId });
        //        var auth = tokenCredential.Authenticate(_requestContext);

        //        if (auth == null)
        //            return null;

        //        if (tenantId == null)
        //            tenantId = auth.TenantId;

        //        var token = tokenCredential.GetToken(_requestContext, CancellationToken.None);
        //        return tokenCredential;
        //    }
        //    catch
        //    {
        //        return null;
        //    }
        //}

        //private void SetOwnerWindow(Window window)
        //{
        //    var wih = new WindowInteropHelper(window);
        //    wih.Owner = Process.GetCurrentProcess().MainWindowHandle;
        //}

        public void AddConfiguration(string name, string connection)
        {
            if (_connections.ContainsKey(name))
                _connections[name] = connection;
            else
                _connections.Add(name, connection);
            SaveConnections();
        }

        public BlobConnectionType GetConnectionType(string connection)
        {
            if (Uri.TryCreate(connection, UriKind.Absolute, out var uri))
            {
                var sas = uri.Query.Contains("sig=");
                if (uri.AbsolutePath == "/")
                    return sas ? BlobConnectionType.ServiceUrlWithSAS : BlobConnectionType.ServiceUrl;
                else
                    return sas ? BlobConnectionType.ContainerUrlWithSAS : BlobConnectionType.ContainerUrl;
            }
            var parts = connection.Split(';').ToDictionary(p => p.Split('=')[0], p => p.Split('=')[1], StringComparer.OrdinalIgnoreCase);
            if (parts.ContainsKey("SharedAccessSignature"))
                return BlobConnectionType.ConnectionStringWithSAS;
            return BlobConnectionType.ConnectionString;
        }
    }

    public enum BlobConnectionType
    {
        ServiceUrlWithSAS,
        ContainerUrlWithSAS,
        ConnectionString,
        ServiceUrl,
        ContainerUrl,
        ConnectionStringWithSAS,
    }



    //public class TenantInfo
    //{
    //    public Guid TenantId { get; set; }
    //    public string Name { get; set; }
    //    public AccessToken AccessToken { get; set; }

    //}

    //public class SubscriptionInfo
    //{
    //    public Guid SubscriptionId { get; set; }
    //    public string Name { get; set; }
    //}

    //public class StorageAccountInfo
    //{
    //    public string Name { get; set; }
    //    public string Id  { get; set; }
    //    public Uri BlobEndpoint { get; set; }
    //}

}

