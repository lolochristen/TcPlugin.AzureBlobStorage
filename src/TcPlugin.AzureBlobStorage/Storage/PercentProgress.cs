using System;

namespace TcPlugin.AzureBlobStorage.Storage
{
    /// <summary>
    ///     Converts transferred bytes into a percentage and reports it synchronously, only when it changes.
    /// </summary>
    internal sealed class PercentProgress : IProgress<long>
    {
        private readonly object _lock = new object();
        private readonly Action<int> _report;
        private readonly long _totalBytes;
        private int _lastPercent = -1;

        public PercentProgress(long totalBytes, Action<int> report)
        {
            _totalBytes = totalBytes;
            _report = report;
        }

        public void Report(long transferredBytes)
        {
            var percent = _totalBytes == 0 ? 0 : (int)(transferredBytes * 100 / _totalBytes);

            lock (_lock)
            {
                if (percent == _lastPercent)
                {
                    return;
                }

                _lastPercent = percent;
                _report(percent);
            }
        }
    }
}
