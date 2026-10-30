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
        [DataRow("AccountName=a;AccountKey=a2V5;BlobEndpoint=http://x/a;;ContainerName=package-blob", "package-blob")]
        [DataRow("AccountName=a;containername=package-blob;AccountKey=a2V5", "package-blob")]
        [DataRow("AccountName=a;AccountKey=a2V5;ContainerName=", null)]
        [DataRow("AccountName=a;AccountKey=a2V5", null)]
        [DataRow("https://account.blob.core.windows.net/container?sv=2022&sig=abc", "container")]
        [DataRow("https://account.blob.core.windows.net/", null)]
        public void GetScopedContainer(string connection, string expected)
        {
            Assert.AreEqual(expected, ConnectionStore.GetScopedContainer(connection));
        }

        [TestMethod]
        public void GetSdkConnectionString_RemovesOnlyContainerName()
        {
            Assert.AreEqual("AccountName=a;AccountKey=a2V5;BlobEndpoint=http://x/a;",
                ConnectionStore.GetSdkConnectionString("AccountName=a;AccountKey=a2V5;BlobEndpoint=http://x/a;;ContainerName=package-blob"));
        }

        [TestMethod]
        [DataRow("https://account.blob.core.windows.net/")]
        [DataRow("AccountName=account;AccountKey=a2V5;DefaultEndpointsProtocol=http;BlobEndpoint=http://127.0.0.1:10000/account;")]
        [DataRow("DefaultEndpointsProtocol=http;AccountName=account;AccountKey=a2V5;BlobEndpoint=http://127.0.0.1:55344/account;;ContainerName=package-blob")]
        public void Validate_AcceptsUsableConnections(string connection)
        {
            ConnectionStore.Validate(connection);
        }

        [TestMethod]
        [DataRow("AccountntName=account;AccountKey=a2V5;BlobEndpoint=http://127.0.0.1:10000/account;")]
        [DataRow("AccountName=account;AccountKey=a2V5;BlobEndpoint=http://127.0.0.1:10000/account;Foo=bar")]
        [DataRow("not a connection string")]
        public void Validate_RejectsInvalidConnectionStrings(string connection)
        {
            Assert.ThrowsExactly<System.FormatException>(() => ConnectionStore.Validate(connection));
        }
    }
}
