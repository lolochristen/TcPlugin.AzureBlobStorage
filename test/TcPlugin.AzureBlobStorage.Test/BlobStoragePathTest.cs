using Microsoft.VisualStudio.TestTools.UnitTesting;
using TcPlugin.AzureBlobStorage.Paths;

namespace TcPlugin.AzureBlobStorage.Test
{
    [TestClass]
    public class BlobStoragePathTest
    {
        private const string DiscoveryBlob = "/contoso.com/MySubscription/myaccount/mycontainer/Folder1/Folder2/File.txt";
        private const string ConnectionBlob = "/#myconnection/mycontainer/Folder1/Folder2/File.txt";

        [TestMethod]
        public void Converts_Backslash()
        {
            BlobStoragePath path = @"\Test\Folder\Path\File.txt";
            Assert.AreEqual("/Test/Folder/Path/File.txt", (string)path);
        }

        [TestMethod]
        public void Removes_TrailingSlash()
        {
            BlobStoragePath forwardSlashes = "/some/folder/";
            BlobStoragePath backslashes = @"\some\folder\";

            Assert.AreEqual("/some/folder", forwardSlashes.Path);
            Assert.AreEqual("/some/folder", backslashes.Path);
        }

        [TestMethod]
        public void Root()
        {
            BlobStoragePath path = "/";
            Assert.AreEqual(0, path.Level);
            Assert.AreEqual("/", path.Path);
            Assert.IsFalse(path.IsConnectionPath);
            Assert.IsFalse(path.IsBlobPath);
        }

        [TestMethod]
        public void Default_IsRoot()
        {
            var path = default(BlobStoragePath);
            Assert.AreEqual(0, path.Level);
            Assert.AreEqual("/", path.Path);
        }

        [TestMethod]
        public void DiscoveryPath_Segments()
        {
            BlobStoragePath path = DiscoveryBlob;
            Assert.IsFalse(path.IsConnectionPath);
            Assert.AreEqual("contoso.com", path.Tenant);
            Assert.AreEqual("MySubscription", path.Subscription);
            Assert.AreEqual("myaccount", path.AccountName);
            Assert.AreEqual("mycontainer", path.ContainerName);
            Assert.AreEqual("Folder1/Folder2/File.txt", path.BlobPath);
            Assert.AreEqual("/contoso.com/MySubscription/myaccount/mycontainer", path.ContainerPath);
        }

        [TestMethod]
        public void ConnectionPath_Segments()
        {
            BlobStoragePath path = ConnectionBlob;
            Assert.IsTrue(path.IsConnectionPath);
            Assert.AreEqual("myconnection", path.AccountName);
            Assert.AreEqual("mycontainer", path.ContainerName);
            Assert.AreEqual("Folder1/Folder2/File.txt", path.BlobPath);
            Assert.AreEqual("/#myconnection/mycontainer", path.ContainerPath);
            Assert.AreEqual(string.Empty, path.Tenant);
        }

        [TestMethod]
        public void PathKinds()
        {
            Assert.IsTrue(((BlobStoragePath)"/#conn").IsConnectionRoot);
            Assert.IsTrue(((BlobStoragePath)"/#conn/container").IsContainerPath);
            Assert.IsTrue(((BlobStoragePath)"/#conn/container/blob").IsBlobPath);

            Assert.IsFalse(((BlobStoragePath)"/tenant").IsConnectionRoot);
            Assert.IsFalse(((BlobStoragePath)"/tenant/sub/account").IsContainerPath);
            Assert.IsTrue(((BlobStoragePath)"/tenant/sub/account/container").IsContainerPath);
            Assert.IsFalse(((BlobStoragePath)"/tenant/sub/account/container").IsBlobPath);
            Assert.IsTrue(((BlobStoragePath)DiscoveryBlob).IsBlobPath);
        }

        [TestMethod]
        public void Directory()
        {
            BlobStoragePath path = "/#conn/container/Folder1/File.txt";

            path = path.Directory;
            Assert.AreEqual("/#conn/container/Folder1", (string)path);

            path = path.Directory;
            Assert.AreEqual("/#conn/container", (string)path);

            path = path.Directory;
            Assert.AreEqual("/#conn", (string)path);

            path = path.Directory;
            Assert.AreEqual("/", (string)path);
        }

        [TestMethod]
        public void Level()
        {
            Assert.AreEqual(0, ((BlobStoragePath)@"\").Level);
            Assert.AreEqual(1, ((BlobStoragePath)@"\segment").Level);
            Assert.AreEqual(2, ((BlobStoragePath)@"\segment\segment").Level);
        }

        [TestMethod]
        public void GetSegment()
        {
            BlobStoragePath path = "/segment1/segment2/segment3/segment4";

            Assert.IsNull(path.GetSegment(0));
            Assert.AreEqual("segment1", path.GetSegment(1));
            Assert.AreEqual("segment4", path.GetSegment(4));
            Assert.IsNull(path.GetSegment(5));
        }
    }
}
