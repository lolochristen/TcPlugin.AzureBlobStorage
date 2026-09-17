using System;
using System.Linq;
using TcPluginBase.FileSystem;

namespace TcPlugin.AzureBlobStorage.Paths
{
    /// <summary>
    ///     A path inside the plugin file system. Two layouts exist below the root:
    ///     <list type="bullet">
    ///         <item>Azure AD discovery: <c>/tenant/subscription/account/container/blob/path</c></item>
    ///         <item>Saved connection: <c>/#connection/container/blob/path</c></item>
    ///     </list>
    /// </summary>
    public readonly struct BlobStoragePath
    {
        public const string ConnectionPrefix = "#";

        private static readonly char[] Separators = { '\\', '/' };

        private readonly string[] _segments;

        /// <param name="path">Absolute path; may contain backslashes and forward slashes.</param>
        public BlobStoragePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                _segments = Array.Empty<string>();
                return;
            }

            path = path.Trim();

            if (!Separators.Contains(path[0]))
            {
                throw new NotSupportedException($"relative paths are not supported! path: '{path}'");
            }

            var trimmed = path.Substring(1).TrimEnd(Separators);
            _segments = trimmed.Length == 0 ? Array.Empty<string>() : trimmed.Split(Separators);
        }

        public string[] Segments => _segments ?? Array.Empty<string>();

        /// <summary>Number of segments: '/' is level 0, '/a' is level 1, '/a/b' is level 2.</summary>
        public int Level => Segments.Length;

        /// <summary>Normalized path with forward slashes and without trailing slash.</summary>
        public string Path => "/" + string.Join("/", Segments);

        public BlobStoragePath Directory => System.IO.Path.GetDirectoryName(Path);

        /// <summary>True for paths below a saved connection (<c>/#connection/...</c>).</summary>
        public bool IsConnectionPath => Level > 0 && Segments[0].StartsWith(ConnectionPrefix, StringComparison.Ordinal);

        public bool IsConnectionRoot => IsConnectionPath && Level == 1;

        /// <summary>Level at which the container segment is located.</summary>
        public int ContainerLevel => IsConnectionPath ? 2 : 4;

        public bool IsContainerPath => Level == ContainerLevel;

        public bool IsBlobPath => Level > ContainerLevel;

        public string Tenant => IsConnectionPath ? string.Empty : GetSegment(1) ?? string.Empty;

        public string Subscription => IsConnectionPath ? string.Empty : GetSegment(2) ?? string.Empty;

        /// <summary>Storage account name, or the connection name (without prefix) for saved connections.</summary>
        public string AccountName => IsConnectionPath
            ? Segments[0].Substring(ConnectionPrefix.Length)
            : GetSegment(3) ?? string.Empty;

        public string ContainerName => GetSegment(ContainerLevel) ?? string.Empty;

        /// <summary>Blob name relative to the container, e.g. <c>folder/file.txt</c>.</summary>
        public string BlobPath => string.Join("/", Segments.Skip(ContainerLevel));

        /// <summary>Path up to and including the container, e.g. <c>/#connection/container</c>.</summary>
        public string ContainerPath => "/" + string.Join("/", Segments.Take(ContainerLevel));

        /// <param name="level">1-based segment level.</param>
        /// <returns>The segment or null if the level does not exist.</returns>
        public string GetSegment(int level)
        {
            var index = level - 1;
            return index >= 0 && index < Segments.Length ? Segments[index] : null;
        }

        public override string ToString()
        {
            return Path;
        }

        public static implicit operator string(BlobStoragePath path)
        {
            return path.Path;
        }

        public static implicit operator BlobStoragePath(string path)
        {
            return new BlobStoragePath(path);
        }

        public static implicit operator BlobStoragePath(RemotePath path)
        {
            return new BlobStoragePath(path);
        }

        public static implicit operator RemotePath(BlobStoragePath path)
        {
            return new RemotePath(path.Path);
        }
    }
}
