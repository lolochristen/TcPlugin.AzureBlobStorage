using Microsoft.VisualStudio.TestTools.UnitTesting;
using TcPlugin.AzureBlobStorage.Connections;

namespace TcPlugin.AzureBlobStorage.Test
{
    [TestClass]
    public class ConnectionStoreTest
    {
        [TestMethod]
        [DataRow("https://account.blob.core.windows.net/", BlobConnectionType.ServiceUrl)]
        [DataRow("https://account.blob.core.windows.net/?sv=2022&sig=abc", BlobConnectionType.ServiceUrlWithSAS)]
        [DataRow("https://account.blob.core.windows.net/container", BlobConnectionType.ContainerUrl)]
        [DataRow("https://account.blob.core.windows.net/container?sv=2022&sig=abc", BlobConnectionType.ContainerUrlWithSAS)]
        [DataRow("DefaultEndpointsProtocol=https;AccountName=account;AccountKey=a2V5==;EndpointSuffix=core.windows.net", BlobConnectionType.ConnectionString)]
        [DataRow("BlobEndpoint=https://account.blob.core.windows.net/;SharedAccessSignature=sv=2022&sig=abc;", BlobConnectionType.ConnectionStringWithSAS)]
        public void GetConnectionType(string connection, BlobConnectionType expected)
        {
            Assert.AreEqual(expected, ConnectionStore.GetConnectionType(connection));
        }
    }
}
