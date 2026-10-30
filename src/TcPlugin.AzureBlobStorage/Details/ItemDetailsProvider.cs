using System;
using Azure;
using TcPlugin.AzureBlobStorage.Connections;
using TcPlugin.AzureBlobStorage.Management;
using TcPlugin.AzureBlobStorage.Paths;
using TcPlugin.AzureBlobStorage.Storage;

namespace TcPlugin.AzureBlobStorage.Details
{
    /// <summary>Loads the properties of whatever item a path points to.</summary>
    internal class ItemDetailsProvider
    {
        private readonly BlobClientFactory _clients;
        private readonly ConnectionStore _connections;
        private readonly AzureResourceManagerClient _resourceManager;

        public ItemDetailsProvider(ConnectionStore connections, BlobClientFactory clients, AzureResourceManagerClient resourceManager)
        {
            _connections = connections;
            _clients = clients;
            _resourceManager = resourceManager;
        }

        /// <returns>null if the path has no properties to show.</returns>
        public PropertySheet Get(BlobStoragePath path)
        {
            if (path.Level == 0 || path.Path.EndsWith("..", StringComparison.Ordinal))
            {
                return null;
            }

            if (path.IsConnectionsPath)
            {
                return path.Level == 2 ? GetConnection(path.GetSegment(2)) : null;
            }

            if (path.IsBlobPath)
            {
                return GetBlob(path);
            }

            if (path.IsContainerPath)
            {
                return GetContainer(path);
            }

            if (path.IsConnectionRoot)
            {
                return GetConnectionWithAccount(path);
            }

            switch (path.Level)
            {
                case 1:
                    return ItemSheets.Tenant(_resourceManager.GetTenantDetails(path.Tenant));
                case 2:
                    return ItemSheets.Subscription(_resourceManager.GetSubscriptionDetails(path.Tenant, path.Subscription));
                default:
                    return ItemSheets.StorageAccount(_resourceManager.GetStorageAccountDetails(path.Tenant, path.Subscription, path.AccountName));
            }
        }

        private PropertySheet GetConnection(string name)
        {
            return ItemSheets.Connection(name, _connections.Get(name));
        }

        /// <summary>The saved connection is always shown, even if the storage account behind it can't be reached.</summary>
        private PropertySheet GetConnectionWithAccount(BlobStoragePath path)
        {
            var connection = _connections.Get(path.AccountName);
            var sheet = ItemSheets.Connection(path.AccountName, connection);

            // a container URL can't ask for account information, and without a SAS token it would trigger a sign-in
            var connectionType = ConnectionStore.GetConnectionType(connection);
            if (connectionType is BlobConnectionType.ContainerUrl or BlobConnectionType.ContainerUrlWithSAS)
            {
                return sheet;
            }

            try
            {
                var client = _clients.GetServiceClient(path);
                return ItemSheets.Account(sheet, client.Uri, client.GetAccountInfo().Value);
            }
            catch (Exception exp) when (exp is RequestFailedException || exp is AggregateException || exp is FormatException)
            {
                return sheet.Group("Storage account").Add("Not available", StorageError.Describe(exp));
            }
        }

        private PropertySheet GetContainer(BlobStoragePath path)
        {
            var container = _clients.GetContainerClient(path);
            return ItemSheets.Container(path.ContainerName, container.Uri, container.GetProperties().Value);
        }

        private PropertySheet GetBlob(BlobStoragePath path)
        {
            var blob = _clients.GetContainerClient(path).GetBlobClient(path.BlobPath);

            try
            {
                return ItemSheets.Blob(path.BlobPath, blob.Uri, blob.GetProperties().Value);
            }
            catch (RequestFailedException exp) when (exp.ErrorCode == "BlobNotFound")
            {
                return ItemSheets.Folder(path.ContainerName, path.BlobPath);
            }
        }
    }
}
