using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TcPluginBase.FileSystem;

namespace TcPlugin.AzureBlobStorage.Paths
{
    /// <summary>
    ///     Keeps virtual folders that do not exist in blob storage yet (blob storage has no empty folders),
    ///     so they stay visible until a blob is uploaded into them.
    /// </summary>
    internal class PathCache
    {
        private readonly List<BlobStoragePath> _paths = new List<BlobStoragePath>();
        private readonly object _lock = new object();

        public void Add(BlobStoragePath path)
        {
            lock (_lock)
            {
                RemoveInternal(path);
                _paths.Add(path);
            }
        }

        /// <summary>Removes the path and all of its sub folders.</summary>
        public bool Remove(BlobStoragePath path)
        {
            lock (_lock)
            {
                return RemoveInternal(path);
            }
        }

        /// <summary>
        ///     Merges the cached folders of <paramref name="parent" /> into the (ordinally sorted) listing.
        /// </summary>
        public IEnumerable<FindData> WithCached(BlobStoragePath parent, IEnumerable<FindData> list)
        {
            var folders = GetCachedFolders(parent);
            var comparer = StringComparer.Ordinal;

            foreach (var data in list)
            {
                for (var i = 0; i < folders.Count; i++)
                {
                    var cached = folders[i];
                    var isDuplicate = cached.FileName == data.FileName && cached.Attributes == data.Attributes;

                    if (isDuplicate || comparer.Compare(cached.FileName, data.FileName) < 0)
                    {
                        if (!isDuplicate)
                        {
                            yield return cached;
                        }

                        folders.RemoveAt(i);
                        i--;
                    }
                }

                yield return data;
            }

            foreach (var data in folders)
            {
                yield return data;
            }
        }

        private List<FindData> GetCachedFolders(BlobStoragePath parent)
        {
            lock (_lock)
            {
                return _paths
                    .Where(x => x.Path.StartsWith(parent.Path, StringComparison.Ordinal))
                    .Select(x => x.GetSegment(parent.Level + 1))
                    .Where(x => x != null)
                    .Distinct()
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .Select(x => new FindData(x, FileAttributes.Directory))
                    .ToList();
            }
        }

        private bool RemoveInternal(BlobStoragePath path)
        {
            return _paths.RemoveAll(x => x.Path == path.Path || x.Path.StartsWith(path.Path + "/", StringComparison.Ordinal)) > 0;
        }
    }
}
