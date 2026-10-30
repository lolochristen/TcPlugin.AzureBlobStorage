using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Azure.Storage.Blobs;

namespace TcPlugin.AzureBlobStorage.Connections
{
    /// <summary>
    ///     Named blob connections (connection strings or URLs), persisted encrypted with DPAPI for the current user.
    /// </summary>
    public class ConnectionStore
    {
        private const string ContainerNameSetting = "ContainerName=";

        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FsAzureStorage", "connections.bin");

        private Dictionary<string, string> _connections = new Dictionary<string, string>();

        public event EventHandler Changed;

        public IEnumerable<string> Names => _connections.Keys;

        public bool Contains(string name)
        {
            return _connections.ContainsKey(name);
        }

        /// <exception cref="KeyNotFoundException">No connection with this name exists.</exception>
        public string Get(string name)
        {
            return _connections[name];
        }

        public void AddOrUpdate(string name, string connection)
        {
            _connections[name] = connection;
            Save();
        }

        public bool Remove(string name)
        {
            if (!_connections.Remove(name))
            {
                return false;
            }

            Save();
            return true;
        }

        public void Load()
        {
            if (!File.Exists(SettingsPath))
            {
                return;
            }

            var decrypted = ProtectedData.Unprotect(File.ReadAllBytes(SettingsPath), null, DataProtectionScope.CurrentUser);
            _connections = JsonSerializer.Deserialize<Dictionary<string, string>>(decrypted) ?? new Dictionary<string, string>();
            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <exception cref="FormatException">The connection is neither a URL nor a valid connection string.</exception>
        public static void Validate(string connection)
        {
            if (!Uri.TryCreate(connection, UriKind.Absolute, out _))
            {
                _ = new BlobServiceClient(GetSdkConnectionString(connection));
            }
        }

        /// <returns>The only container this connection gives access to, or null if it covers a whole account.</returns>
        public static string GetScopedContainer(string connection)
        {
            if (Uri.TryCreate(connection, UriKind.Absolute, out var uri))
            {
                var container = uri.AbsolutePath.Trim('/');
                return container.Length == 0 ? null : container;
            }

            var value = connection.Split(';').FirstOrDefault(IsContainerNameSetting)?.Trim().Substring(ContainerNameSetting.Length);
            return string.IsNullOrEmpty(value) ? null : value;
        }

        /// <summary>
        ///     Removes the <c>ContainerName</c> setting that tools such as .NET Aspire add to connection strings. The SDK
        ///     rejects every setting it doesn't know.
        /// </summary>
        public static string GetSdkConnectionString(string connection)
        {
            return string.Join(";", connection.Split(';').Where(segment => !IsContainerNameSetting(segment)));
        }

        private static bool IsContainerNameSetting(string segment)
        {
            return segment.Trim().StartsWith(ContainerNameSetting, StringComparison.OrdinalIgnoreCase);
        }

        public static BlobConnectionType GetConnectionType(string connection)
        {
            if (Uri.TryCreate(connection, UriKind.Absolute, out var uri))
            {
                var hasSas = uri.Query.Contains("sig=");
                if (uri.AbsolutePath == "/")
                {
                    return hasSas ? BlobConnectionType.ServiceUrlWithSAS : BlobConnectionType.ServiceUrl;
                }

                return hasSas ? BlobConnectionType.ContainerUrlWithSAS : BlobConnectionType.ContainerUrl;
            }

            var hasSharedAccessSignature = connection
                .Split(';')
                .Any(p => p.Trim().StartsWith("SharedAccessSignature=", StringComparison.OrdinalIgnoreCase));

            return hasSharedAccessSignature ? BlobConnectionType.ConnectionStringWithSAS : BlobConnectionType.ConnectionString;
        }

        private void Save()
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(_connections);
            var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            File.WriteAllBytes(SettingsPath, encrypted);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
