namespace TcPlugin.AzureBlobStorage.Connections
{
    public enum BlobConnectionType
    {
        ConnectionString,
        ConnectionStringWithSAS,
        ServiceUrl,
        ServiceUrlWithSAS,
        ContainerUrl,
        ContainerUrlWithSAS
    }
}
