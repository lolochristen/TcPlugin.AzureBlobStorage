using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TcPlugin.AzureBlobStorage
{
    public static class StreamExtensions
    {
        /// <summary>
        ///     Implementation of <see cref="Stream.CopyTo(System.IO.Stream)" /> with progress reporting
        /// </summary>
        /// <param name="fromStream"></param>
        /// <param name="destination"></param>
        /// <param name="bufferSize"></param>
        /// <param name="progressInfo"></param>
        internal static void CopyTo(this Stream fromStream, Stream destination, int bufferSize,
            CopyProgressInfo progressInfo)
        {
            var buffer = new byte[bufferSize];
            int count;
            while ((count = fromStream.Read(buffer, 0, buffer.Length)) != 0)
            {
                progressInfo.BytesTransfered += count;
                destination.Write(buffer, 0, count);
            }
        }

        /// <summary>
        ///     Implementation of <see cref="Stream.CopyToAsync(System.IO.Stream)" /> with progress reporting
        /// </summary>
        /// <param name="fromStream"></param>
        /// <param name="destination"></param>
        /// <param name="bufferSize"></param>
        /// <param name="progressInfo"></param>
        internal static async Task CopyToAsync(this Stream fromStream, Stream destination, int bufferSize,
            CopyProgressInfo progressInfo)
        {
            var buffer = new byte[bufferSize];
            int count;
            while ((count = await fromStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
            {
                progressInfo.BytesTransfered += count;
                await destination.WriteAsync(buffer, 0, count);
            }
        }

        /// <summary>
        ///     Implementation of <see cref="Stream.CopyToAsync(System.IO.Stream)" /> with progress reporting
        /// </summary>
        /// <param name="fromStream"></param>
        /// <param name="destination"></param>
        /// <param name="bufferSize"></param>
        /// <param name="progressInfo"></param>
        internal static async Task CopyToAsync(this Stream fromStream, Stream destination, int bufferSize,
            CopyProgressInfo progressInfo, CancellationToken cancellationToken)
        {
            var buffer = new byte[bufferSize];
            int count;
            while ((count = await fromStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) != 0)
            {
                progressInfo.BytesTransfered += count;
                await destination.WriteAsync(buffer, 0, count, cancellationToken);
            }
        }
    }

    public class CopyProgressInfo
    {
        public virtual long BytesTransfered
        {
            get;
            set;
        }
    }

    public class CopyProgressInfoCallback : CopyProgressInfo
    {
        private readonly Action<long> _callback;
        private long _bytesTransfered;

        public CopyProgressInfoCallback(Action<long> callback)
        {
            _callback = callback;
        }

        public override long BytesTransfered
        {
            get => _bytesTransfered;
            set
            {
                _bytesTransfered = value;
                _callback(_bytesTransfered);
            }
        }
    }
}
