using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Azure;
using Azure.Identity;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TcPlugin.AzureBlobStorage.Storage;

namespace TcPlugin.AzureBlobStorage.Test
{
    [TestClass]
    public class StorageErrorTest
    {
        [TestMethod]
        public void UnknownServiceError_UsesFirstLineOnly()
        {
            var error = new RequestFailedException(400, "The API version is not supported.\nRequestId:123\nHeaders: secret");

            Assert.AreEqual("The API version is not supported.", StorageError.Describe(error));
        }

        [TestMethod]
        [DataRow(401, "AuthenticationFailed", "Not authorized")]
        [DataRow(403, "AuthorizationPermissionMismatch", "Access denied")]
        [DataRow(404, "BlobNotFound", "does not exist")]
        [DataRow(409, "ContainerAlreadyExists", "already exists")]
        [DataRow(409, "ContainerBeingDeleted", "being deleted")]
        [DataRow(400, "OutOfRangeInput", "3-63 characters")]
        [DataRow(400, "InvalidResourceName", "3-63 characters")]
        public void KnownServiceError_IsExplained(int status, string errorCode, string expected)
        {
            var error = new RequestFailedException(status, "raw service text\nRequestId:123", errorCode, null);

            var message = StorageError.Describe(error);

            StringAssert.Contains(message, expected);
            Assert.IsFalse(message.Contains("RequestId"), message);
        }

        [TestMethod]
        public void UnreachableEndpoint_IsExplained()
        {
            var refused = new SocketException((int)SocketError.ConnectionRefused);
            var error = new AggregateException("Retry failed after 4 tries.", new RequestFailedException("Unable to connect.", refused));

            StringAssert.Contains(StorageError.Describe(error), "Cannot reach the storage endpoint");
        }

        [TestMethod]
        public void FailedTlsHandshake_IsExplained()
        {
            var handshake = new IOException("The handshake failed due to an unexpected packet format.");
            var web = new WebException("The underlying connection was closed.", handshake, WebExceptionStatus.SendFailure, null);
            var error = new AggregateException(new RequestFailedException("The underlying connection was closed.", web));

            var message = StorageError.Describe(error);

            StringAssert.Contains(message, "secure connection");
            StringAssert.Contains(message, "handshake failed");
        }

        [TestMethod]
        public void InvalidConnectionString_IsNamed()
        {
            var error = new FormatException("No valid combination of account information found.");

            Assert.AreEqual("Invalid connection string: No valid combination of account information found.", StorageError.Describe(error));
        }

        [TestMethod]
        public void FailedSignIn_ExplainsHowToSignIn()
        {
            var message = StorageError.Describe(new CredentialUnavailableException("DefaultAzureCredential failed.\nlong details"));

            StringAssert.Contains(message, "az login");
            StringAssert.Contains(message, "Connect-AzAccount");
            Assert.IsFalse(message.Contains("long details"), message);
        }

        [TestMethod]
        public void ManagementRequestFailure_IsNamed()
        {
            var message = StorageError.Describe(new HttpRequestException("Response status code does not indicate success: 403 (Forbidden)."));

            StringAssert.Contains(message, "Azure Resource Manager");
            StringAssert.Contains(message, "403");
        }

        [TestMethod]
        public void UnknownConnection_IsExplained()
        {
            StringAssert.Contains(StorageError.Describe(new KeyNotFoundException()), "no longer exists");
        }

        [TestMethod]
        public void LocalFileError_KeepsItsMessage()
        {
            var message = "The process cannot access the file 'C:\\a.txt'.";

            Assert.AreEqual(message, StorageError.Describe(new IOException(message)));
        }

        [TestMethod]
        public void UnexpectedError_ShowsNoStackTrace()
        {
            Exception error;
            try
            {
                throw new InvalidOperationException("Something odd.");
            }
            catch (Exception exp)
            {
                error = exp;
            }

            Assert.AreEqual("Unexpected error: Something odd.", StorageError.Describe(error));
        }

        [TestMethod]
        public void IsCancellation_SeesThroughAggregates()
        {
            Assert.IsTrue(StorageError.IsCancellation(new OperationCanceledException()));
            Assert.IsTrue(StorageError.IsCancellation(new AggregateException(new OperationCanceledException())));
            Assert.IsFalse(StorageError.IsCancellation(new IOException()));
        }
    }
}
