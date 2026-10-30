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

        [TestMethod]
        [DataRow("https://account.blob.core.windows.net/")]
        [DataRow("AccountName=account;AccountKey=a2V5;DefaultEndpointsProtocol=http;BlobEndpoint=http://127.0.0.1:10000/account;")]
        public void Validate_AcceptsUsableConnections(string connection)
        {
            ConnectionStore.Validate(connection);
        }

        [TestMethod]
        [DataRow("AccountntName=account;AccountKey=a2V5;BlobEndpoint=http://127.0.0.1:10000/account;")]
        [DataRow("not a connection string")]
        public void Validate_RejectsInvalidConnectionStrings(string connection)
        {
            Assert.ThrowsExactly<System.FormatException>(() => ConnectionStore.Validate(connection));
        }
    }
}
