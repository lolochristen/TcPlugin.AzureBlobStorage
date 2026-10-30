using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Azure;
using Azure.Identity;

namespace TcPlugin.AzureBlobStorage.Storage
{
    /// <summary>
    ///     Turns failures into a short reason for the user. Raw service messages are cut to their first line,
    ///     because the remaining lines contain request details and headers.
    /// </summary>
    internal static class StorageError
    {
        public static bool IsCancellation(Exception exception)
        {
            return exception is OperationCanceledException
                || (exception is AggregateException aggregate && aggregate.Flatten().InnerExceptions.All(IsCancellation));
        }

        public static string Describe(Exception exception)
        {
            if (exception is AggregateException aggregate)
            {
                return Describe(aggregate.Flatten().InnerExceptions.First());
            }

            return DescribeTransportFailure(exception) ?? DescribeFailure(exception);
        }

        private static string DescribeTransportFailure(Exception exception)
        {
            var chain = GetChain(exception).ToList();
            var webStatus = chain.OfType<WebException>().Select(web => (WebExceptionStatus?)web.Status).FirstOrDefault();

            if (chain.OfType<SocketException>().Any()
                || webStatus == WebExceptionStatus.ConnectFailure
                || webStatus == WebExceptionStatus.NameResolutionFailure)
            {
                return "Cannot reach the storage endpoint. Check the address and your network, and that a local emulator is running.";
            }

            if (webStatus == WebExceptionStatus.SendFailure
                || webStatus == WebExceptionStatus.SecureChannelFailure
                || webStatus == WebExceptionStatus.TrustFailure)
            {
                return "Could not establish a secure connection. The endpoint may not support HTTPS, or its certificate is not trusted."
                    + $"\n\nDetails: {FirstLine(chain.Last().Message)}";
            }

            return null;
        }

        private static string DescribeFailure(Exception exception)
        {
            switch (exception)
            {
                case RequestFailedException failed:
                    return DescribeServiceError(failed);
                case AuthenticationFailedException _:
                    return "Azure sign-in failed. Run 'az login' or 'Connect-AzAccount' and try again.";
                case HttpRequestException _:
                    return "The Azure Resource Manager request failed: " + FirstLine(exception.Message);
                case FormatException _:
                    return "Invalid connection string: " + FirstLine(exception.Message);
                case KeyNotFoundException _:
                    return "This saved connection no longer exists.";
                case IOException _:
                case UnauthorizedAccessException _:
                    return FirstLine(exception.Message);
                default:
                    return "Unexpected error: " + FirstLine(exception.Message);
            }
        }

        private static string DescribeServiceError(RequestFailedException failed)
        {
            switch (failed.ErrorCode)
            {
                case "ContainerAlreadyExists":
                    return "A container with this name already exists.";
                case "ContainerBeingDeleted":
                    return "A container with this name is being deleted. Try again in a moment.";
                case "OutOfRangeInput":
                case "InvalidResourceName":
                    return "The name is not valid. Container names must be 3-63 characters long and use only lowercase letters, numbers and single hyphens.";
            }

            switch (failed.Status)
            {
                case 401:
                    return "Not authorized. Sign in with 'az login' or 'Connect-AzAccount', or check the key or SAS token of the connection.";
                case 403:
                    return "Access denied. Your account needs a data role such as Storage Blob Data Reader or Contributor, or the key or SAS token lacks permission.";
                case 404:
                    return "The container or blob does not exist.";
                default:
                    return FirstLine(failed.Message);
            }
        }

        private static IEnumerable<Exception> GetChain(Exception exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
            {
                yield return current;
            }
        }

        private static string FirstLine(string message)
        {
            return message.Split('\r', '\n')[0];
        }
    }
}
