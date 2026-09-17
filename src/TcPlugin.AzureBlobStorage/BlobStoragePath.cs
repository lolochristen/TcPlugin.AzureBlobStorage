using System;
using System.Linq;
using TcPluginBase.FileSystem;

namespace TcPlugin.AzureBlobStorage
{
    public struct BlobStoragePath
    {
        public string[] Segments
        {
            get;
            set;
        }

        // Path = '/some/segments/without/trailing/slash'
        public string Path => $"/{string.Join("/", Segments)}";

        public string Tenant => GetSegment(1) ?? string.Empty;
        public string Subscription => GetSegment(2) ?? string.Empty;

        public string AccountName => IsConfigurationBased ? Segments[0]?.Substring(1) ?? string.Empty : GetSegment(3) ?? string.Empty;

        public string ContainerName => (IsConfigurationBased ? GetSegment(2) : GetSegment(4)) ?? string.Empty;

        public string BlobPath => string.Join("/", Segments.Skip(IsConfigurationBased ? 2 : 4)); // Segments.Count() > 4 ? "/" + string.Join("/", Segments.Skip(4)) : string.Empty;

        public string BlobName => BlobPath;

        /// <summary>
        ///     '\'                 level 0
        ///     '\segment'          level 1
        ///     '\segment\segment'  level 2
        /// </summary>
        public int Level => Segments.Length;

        //public bool IsAccountPath => !string.IsNullOrEmpty(AccountName);
        //public bool IsContainerPath => !string.IsNullOrEmpty(ContainerName);
        public bool IsBlobPath => (IsConfigurationBased && Level > 2) || (!IsConfigurationBased && Level > 4);

        public bool IsConfigurationBased => Segments[0].StartsWith("#");

        public BlobStoragePath Directory => System.IO.Path.GetDirectoryName(Path);

        /// <param name="path"> can contain backslashes and forward slashes </param>
        public BlobStoragePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                Segments = new string[0];
                return;
            }

            var separators = new[] { '\\', '/' };

            path = path.Trim();

            if (!separators.Contains(path[0]))
                throw new NotSupportedException($"relative paths are not supported! path: '{path}'");

            var lastChar = path.Length > 1 ? path[path.Length - 1] : (char)0;
            if (separators.Contains(lastChar)) path = path.Substring(0, path.Length - 1);

            var substring = path.Substring(1);
            Segments = string.IsNullOrEmpty(substring)
                ? new string[0]
                : substring.Split(separators);
        }

        public string GetSegment(int level)
        {
            var index = level - 1;
            if (index < 0) return null;

            return index < Segments.Length ? Segments[index] : null;
        }

        public static implicit operator string(BlobStoragePath path)
        {
            return path.ToString();
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
            return new RemotePath(path);
        }

        public override string ToString()
        {
            return Path;
        }
    }
}
