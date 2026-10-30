using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TcPluginBase.FileSystem;

namespace TcPlugin.AzureBlobStorage.Test
{
    [TestClass]
    public class DirectoryListingTest
    {
        [TestMethod]
        public void EmptyDirectory_SignalsNoMoreFiles()
        {
            Assert.ThrowsExactly<NoMoreFilesException>(() => AzureBlobFsPlugin.ToListing(new List<FindData>()));
        }

        [TestMethod]
        public void Items_AreReturned()
        {
            var listing = AzureBlobFsPlugin.ToListing(new List<FindData> { new FindData("container", FileAttributes.Directory) });

            Assert.AreEqual("container", listing.Single().FileName);
        }
    }
}
