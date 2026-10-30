using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TcPlugin.AzureBlobStorage.Storage;

namespace TcPlugin.AzureBlobStorage.Test
{
    [TestClass]
    public class BlobClientFactoryTest
    {
        [TestMethod]
        public void RetriesStayShort_BecauseTcWaitsForThem()
        {
            var retry = BlobClientFactory.CreateOptions().Retry;

            Assert.AreEqual(2, retry.MaxRetries);
            Assert.AreEqual(TimeSpan.FromMilliseconds(500), retry.Delay);
            Assert.AreEqual(TimeSpan.FromSeconds(2), retry.MaxDelay);
        }
    }
}
