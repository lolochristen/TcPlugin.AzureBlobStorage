using System;
using System.Linq;
using Azure;

namespace TcPlugin.AzureBlobStorage.Storage
{
    /// <summary>
    ///     Turns storage failures into a short, readable reason. Only the first line of a service error is used,
    ///     because the remaining lines contain request details and headers.
    /// </summary>
    internal static class StorageError
    {
        /// <returns>
        ///     true for failures caused by the connection or the service: a rejected request, an unreachable
        ///     endpoint (the SDK reports these as <see cref="AggregateException" /> after its retries) or an
        ///     invalid connection string.
        /// </returns>
        public static bool IsHandled(Exception exception)
        {
            return exception is RequestFailedException
                || exception is FormatException
                || (exception is AggregateException aggregate && aggregate.Flatten().InnerExceptions.All(IsHandled));
        }

        public static string Describe(Exception exception)
        {
            switch (exception)
            {
                case AggregateException aggregate:
                    return Describe(aggregate.Flatten().InnerExceptions.First());
                case FormatException _:
                    return "Invalid connection string: " + FirstLine(exception.Message);
                default:
                    return FirstLine(GetInnermost(exception).Message);
            }
        }

        private static Exception GetInnermost(Exception exception)
        {
            while (exception.InnerException != null)
            {
                exception = exception.InnerException;
            }

            return exception;
        }

        private static string FirstLine(string message)
        {
            return message.Split('\r', '\n')[0];
        }
    }
}
