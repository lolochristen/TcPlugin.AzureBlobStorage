using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using TcPlugin.AzureBlobStorage.Connections;
using TcPlugin.AzureBlobStorage.Paths;

namespace TcPlugin.AzureBlobStorage.Storage
{
    /// <summary>
    ///     Creates blob storage clients for a path, either from a saved connection or from the Azure AD discovery layout.
    /// </summary>
    internal class BlobClientFactory
    {
        private readonly ConcurrentDictionary<string, BlobContainerClient> _containerClients = new ConcurrentDictionary<string, BlobContainerClient>();
        private readonly ConnectionStore _connections;
        private readonly TokenCredential _credential;

        public BlobClientFactory(ConnectionStore connections, TokenCredential credential)
        {
            _connections = connections;
            _credential = credential;
            _connections.Changed += (sender, args) => _containerClients.Clear();
        }

        public BlobServiceClient GetServiceClient(BlobStoragePath path)
        {
            if (!path.IsConnectionPath)
            {
                return new BlobServiceClient(GetAccountUri(path.AccountName), _credential);
            }

            var connection = _connections.Get(path.AccountName);

            switch (ConnectionStore.GetConnectionType(connection))
            {
                case BlobConnectionType.ServiceUrl:
                    return new BlobServiceClient(new Uri(connection), _credential);
                case BlobConnectionType.ContainerUrl:
                    return new BlobServiceClient(new Uri(new Uri(connection).GetLeftPart(UriPartial.Authority)), _credential);
                case BlobConnectionType.ServiceUrlWithSAS:
                case BlobConnectionType.ContainerUrlWithSAS:
                    return new BlobServiceClient(new Uri(connection));
                default:
                    return new BlobServiceClient(connection);
            }
        }

        public BlobContainerClient GetContainerClient(BlobStoragePath path)
        {
            return _containerClients.GetOrAdd(path.ContainerPath, _ => CreateContainerClient(path));
        }

        /// <returns>null if no client can be created for the path, e.g. unknown connection or invalid connection string.</returns>
        public BlobClient TryGetBlobClient(BlobStoragePath path)
        {
            return TryCreate(() => GetContainerClient(path).GetBlobClient(path.BlobPath));
        }

        /// <returns>null if no client can be created for the path, e.g. unknown connection or invalid connection string.</returns>
        public BlockBlobClient TryGetBlockBlobClient(BlobStoragePath path)
        {
            return TryCreate(() => GetContainerClient(path).GetBlockBlobClient(path.BlobPath));
        }

        private BlobContainerClient CreateContainerClient(BlobStoragePath path)
        {
            if (path.IsConnectionPath)
            {
                var connection = _connections.Get(path.AccountName);
                var connectionType = ConnectionStore.GetConnectionType(connection);

                if (connectionType is BlobConnectionType.ContainerUrl or BlobConnectionType.ContainerUrlWithSAS)
                {
                    return new BlobContainerClient(new Uri(connection));
                }
            }

            return GetServiceClient(path).GetBlobContainerClient(path.ContainerName);
        }

        private static Uri GetAccountUri(string accountName)
        {
            return new Uri($"https://{accountName}.blob.core.windows.net");
        }

        private static T TryCreate<T>(Func<T> create) where T : class
        {
            try
            {
                return create();
            }
            catch (Exception exp) when (exp is KeyNotFoundException || exp is FormatException || exp is ArgumentException)
            {
                return null;
            }
        }
    }
}
