using System;
using System.Collections.Generic;

namespace TcPlugin.AzureBlobStorage.Storage
{
    /// <summary>
    ///     Keeps operations on many files from showing one dialog per file for the same reason. A reason stays quiet
    ///     until it has not occurred for the length of the window.
    /// </summary>
    internal class ErrorThrottle
    {
        private readonly Dictionary<string, DateTime> _lastSeen = new Dictionary<string, DateTime>();
        private readonly TimeSpan _window;

        public ErrorThrottle(TimeSpan window)
        {
            _window = window;
        }

        public bool ShouldShow(string reason, DateTime now)
        {
            lock (_lastSeen)
            {
                var isRepeat = _lastSeen.TryGetValue(reason, out var lastSeen) && now - lastSeen < _window;
                _lastSeen[reason] = now;
                return !isRepeat;
            }
        }
    }
}
