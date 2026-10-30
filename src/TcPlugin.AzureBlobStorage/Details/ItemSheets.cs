using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Azure.Storage.Blobs.Models;
using TcPlugin.AzureBlobStorage.Connections;

namespace TcPlugin.AzureBlobStorage.Details
{
    /// <summary>Builds the property sheet of each kind of item from the data Azure returns for it.</summary>
    internal static class ItemSheets
    {
        public static PropertySheet Connection(string name, string connection)
        {
            return new PropertySheet("Connection " + name)
                .Add("Name", name)
                .Add("Type", ConnectionStore.GetConnectionType(connection).ToString())
                .Add("Connection", PropertyFormat.MaskSecrets(connection));
        }

        public static PropertySheet Tenant(JsonElement tenant)
        {
            var domains = Find(tenant, "domains");
            var allDomains = domains?.ValueKind == JsonValueKind.Array
                ? string.Join(", ", domains.Value.EnumerateArray().Select(domain => domain.GetString()))
                : null;

            return new PropertySheet("Tenant " + Text(tenant, "defaultDomain"))
                .Add("Name", Text(tenant, "displayName"))
                .Add("Tenant ID", Text(tenant, "tenantId"))
                .Add("Default domain", Text(tenant, "defaultDomain"))
                .Add("Domains", allDomains)
                .Add("Category", Text(tenant, "tenantCategory"))
                .Add("Type", Text(tenant, "tenantType"))
                .Add("Country", Text(tenant, "countryCode"));
        }

        public static PropertySheet Subscription(JsonElement subscription)
        {
            return new PropertySheet("Subscription " + Text(subscription, "displayName"))
                .Add("Name", Text(subscription, "displayName"))
                .Add("Subscription ID", Text(subscription, "subscriptionId"))
                .Add("State", Text(subscription, "state"))
                .Add("Tenant ID", Text(subscription, "tenantId"))
                .Add("Authorization source", Text(subscription, "authorizationSource"))
                .Add("Spending limit", Text(subscription, "subscriptionPolicies", "spendingLimit"));
        }

        public static PropertySheet StorageAccount(JsonElement account)
        {
            return new PropertySheet("Storage account " + Text(account, "name"))
                .Add("Name", Text(account, "name"))
                .Add("Resource group", GetResourceGroup(Text(account, "id")))
                .Add("Location", Text(account, "location"))
                .Add("Kind", Text(account, "kind"))
                .Add("SKU", Text(account, "sku", "name"))
                .Add("Access tier", Text(account, "properties", "accessTier"))
                .Add("Created", Date(account, "properties", "creationTime"))
                .Add("Status", Text(account, "properties", "provisioningState"))
                .Add("Blob endpoint", Text(account, "properties", "primaryEndpoints", "blob"))
                .Group("Security")
                .Add("HTTPS only", Flag(account, "properties", "supportsHttpsTrafficOnly"))
                .Add("Minimum TLS version", Text(account, "properties", "minimumTlsVersion"))
                .Add("Public blob access", Flag(account, "properties", "allowBlobPublicAccess"))
                .Add("Shared key access", Flag(account, "properties", "allowSharedKeyAccess"))
                .Add("Public network access", Text(account, "properties", "publicNetworkAccess"))
                .Add("Hierarchical namespace", Flag(account, "properties", "isHnsEnabled"));
        }

        public static PropertySheet Account(PropertySheet sheet, Uri endpoint, AccountInfo info)
        {
            return sheet.Group("Storage account")
                .Add("Blob endpoint", PropertyFormat.WithoutQuery(endpoint))
                .Add("Kind", info.AccountKind.ToString())
                .Add("SKU", info.SkuName.ToString())
                .Add("Hierarchical namespace", info.IsHierarchicalNamespaceEnabled);
        }

        public static PropertySheet Container(string name, Uri uri, BlobContainerProperties properties)
        {
            var sheet = new PropertySheet("Container " + name)
                .Add("Name", name)
                .Add("URL", PropertyFormat.WithoutQuery(uri))
                .Add("Last modified", properties.LastModified)
                .Add("ETag", properties.ETag)
                .Group("Access")
                .Add("Public access", properties.PublicAccess)
                .Add("Lease state", properties.LeaseState)
                .Add("Lease status", properties.LeaseStatus)
                .Add("Immutability policy", properties.HasImmutabilityPolicy)
                .Add("Legal hold", properties.HasLegalHold)
                .Add("Default encryption scope", properties.DefaultEncryptionScope);

            return AddMetadata(sheet, properties.Metadata);
        }

        public static PropertySheet Blob(string blobPath, Uri uri, BlobProperties properties)
        {
            var sheet = new PropertySheet("Blob " + blobPath)
                .Add("Name", blobPath)
                .Add("URL", PropertyFormat.WithoutQuery(uri))
                .Add("Size", PropertyFormat.FormatSize(properties.ContentLength))
                .Add("Content type", properties.ContentType)
                .Add("Created", properties.CreatedOn)
                .Add("Last modified", properties.LastModified)
                .Add("Last accessed", properties.LastAccessed)
                .Group("Storage")
                .Add("Blob type", properties.BlobType)
                .Add("Access tier", properties.AccessTier)
                .Add("Tier inferred", properties.AccessTierInferred)
                .Add("Archive status", properties.ArchiveStatus)
                .Add("Lease state", properties.LeaseState)
                .Add("Lease status", properties.LeaseStatus)
                .Add("Server encrypted", properties.IsServerEncrypted)
                .Add("Version ID", properties.VersionId)
                .Group("Content")
                .Add("MD5", properties.ContentHash)
                .Add("ETag", properties.ETag)
                .Add("Encoding", properties.ContentEncoding)
                .Add("Language", properties.ContentLanguage)
                .Add("Disposition", properties.ContentDisposition)
                .Add("Cache control", properties.CacheControl);

            if (!string.IsNullOrEmpty(properties.CopyId))
            {
                sheet.Group("Copy")
                    .Add("Status", properties.CopyStatus)
                    .Add("Progress", properties.CopyProgress)
                    .Add("Source", properties.CopySource == null ? null : PropertyFormat.WithoutQuery(properties.CopySource))
                    .Add("Completed", properties.CopyCompletedOn);
            }

            return AddMetadata(sheet, properties.Metadata);
        }

        /// <summary>Blob storage has no real folders: a name without a blob is a prefix of other blobs, or a folder not uploaded to yet.</summary>
        public static PropertySheet Folder(string containerName, string blobPath)
        {
            return new PropertySheet("Folder " + blobPath)
                .Add("Name", blobPath)
                .Add("Type", "Folder")
                .Add("Container", containerName);
        }

        private static PropertySheet AddMetadata(PropertySheet sheet, IDictionary<string, string> metadata)
        {
            if (metadata?.Count > 0)
            {
                sheet.Group("Metadata");
                foreach (var entry in metadata.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
                {
                    sheet.Add(entry.Key, entry.Value);
                }
            }

            return sheet;
        }

        private static string GetResourceGroup(string resourceId)
        {
            var segments = resourceId?.Split('/') ?? Array.Empty<string>();
            var index = Array.FindIndex(segments, segment => string.Equals(segment, "resourceGroups", StringComparison.OrdinalIgnoreCase));
            return index >= 0 && index + 1 < segments.Length ? segments[index + 1] : null;
        }

        private static JsonElement? Find(JsonElement element, params string[] path)
        {
            foreach (var name in path)
            {
                if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out element))
                {
                    return null;
                }
            }

            return element;
        }

        private static string Text(JsonElement element, params string[] path)
        {
            var value = Find(element, path);
            return value?.ValueKind == JsonValueKind.String ? value.Value.GetString() : null;
        }

        private static bool? Flag(JsonElement element, params string[] path)
        {
            var value = Find(element, path);
            return value?.ValueKind == JsonValueKind.True || value?.ValueKind == JsonValueKind.False
                ? value.Value.GetBoolean()
                : (bool?)null;
        }

        private static DateTimeOffset? Date(JsonElement element, params string[] path)
        {
            return DateTimeOffset.TryParse(Text(element, path), out var date) ? date : (DateTimeOffset?)null;
        }
    }
}
