using System;
using System.IO;
using Azure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TcPlugin.AzureBlobStorage.Storage;

namespace TcPlugin.AzureBlobStorage.Test
{
    [TestClass]
    public class StorageErrorTest
    {
        [TestMethod]
        public void ServiceError_UsesFirstLineOnly()
        {
            var error = new RequestFailedException(400, "The API version is not supported.\nRequestId:123\nHeaders: secret");

            Assert.AreEqual("The API version is not supported.", StorageError.Describe(error));
        }

        [TestMethod]
        public void TransportError_UsesInnermostReason()
        {
            var handshake = new IOException("The handshake failed due to an unexpected packet format.");
            var error = new RequestFailedException("The underlying connection was closed.", new InvalidOperationException("send failed", handshake));

            Assert.AreEqual("The handshake failed due to an unexpected packet format.", StorageError.Describe(error));
        }

        [TestMethod]
        public void InvalidConnectionString_IsNamed()
        {
            var error = new FormatException("No valid combination of account information found.");

            Assert.AreEqual("Invalid connection string: No valid combination of account information found.", StorageError.Describe(error));
        }

        [TestMethod]
        public void RetriedTransportError_UsesInnermostReason()
        {
            var refused = new IOException("Connection refused.");
            var error = new AggregateException("Retry failed after 4 tries.", new RequestFailedException("Unable to connect.", refused));

            Assert.AreEqual("Connection refused.", StorageError.Describe(error));
        }

        [TestMethod]
        public void IsHandled_OnlyForExpectedErrors()
        {
            Assert.IsTrue(StorageError.IsHandled(new RequestFailedException("failed")));
            Assert.IsTrue(StorageError.IsHandled(new FormatException()));
            Assert.IsTrue(StorageError.IsHandled(new AggregateException(new RequestFailedException("failed"))));
            Assert.IsFalse(StorageError.IsHandled(new AggregateException(new InvalidOperationException())));
            Assert.IsFalse(StorageError.IsHandled(new InvalidOperationException()));
        }
    }
}
