using System;
using System.Linq;
using System.Text.Json.Serialization;
using Azure.Core;

namespace TcPlugin.AzureBlobStorage
{
    public class StorageConnection
    {
        public string ConnectionString
        {
            get;
            set;
        }

        public Uri Url
        {
            get;
            set;
        }

        [JsonIgnore]
        public bool IsStorageSAS => Url != null && Url.Host.EndsWith("blob.core.windows.net") &&
                                    Url.GetComponents(UriComponents.Path, UriFormat.Unescaped).Length == 0 &&
                                    Url.Query.Contains("&sig=");

        [JsonIgnore]
        public bool IsContainerSAS => Url != null && Url.Host.EndsWith("blob.core.windows.net") &&
                                      Url.GetComponents(UriComponents.Path, UriFormat.Unescaped).Length > 0 &&
                                      Url.Query.Contains("&sig=");

        [JsonIgnore]
        public bool IsContainerAad => Url != null && Url.Host.EndsWith("blob.core.windows.net") &&
                                      Url.GetComponents(UriComponents.Path, UriFormat.Unescaped).Length > 0 &&
                                      UseActiveDirectory;

        [JsonIgnore]
        public bool IsStorageAad => Url != null && Url.Host.EndsWith("blob.core.windows.net") &&
                                    Url.GetComponents(UriComponents.Path, UriFormat.Unescaped).Length == 0 &&
                                    UseActiveDirectory;


        [JsonIgnore] public bool IsConnectionString => ConnectionString?.Contains("AccountName=") ?? false;

        [JsonIgnore]
        public string ContainerName => IsContainerSAS || IsContainerAad
            ? Url.GetComponents(UriComponents.Path, UriFormat.Unescaped)
            : null;

        [JsonIgnore]
        public string AccountName
        {
            get
            {
                if (IsConnectionString)
                    return ConnectionString.Split(';').ToDictionary(p => p.Substring(0, p.IndexOf('=')),
                        p => p.Substring(p.IndexOf('=') + 1))["AccountName"];

                if (Url != null)
                {
                    if (IsContainerSAS)
                        return Url.Host.Substring(0, Url.Host.IndexOf('.')) + "." + ContainerName + "(SAS)";
                    if (IsStorageSAS)
                        return Url.Host.Substring(0, Url.Host.IndexOf('.')) + "(SAS)";
                    if (IsContainerAad)
                        return Url.Host.Substring(0, Url.Host.IndexOf('.')) + "." + ContainerName + "(Aad)";
                    if (IsStorageAad)
                        return Url.Host.Substring(0, Url.Host.IndexOf('.')) + "(Aad)";
                    return Url.Host;
                }

                return string.Empty;
            }
        }

        public string TenantId
        {
            get;
            set;
        }

        public bool UseActiveDirectory
        {
            get;
            set;
        }

        [JsonIgnore]
        public TokenCredential Credential
        {
            get;
            set;
        }

        [JsonIgnore] public bool RequiresAad => Url != null && IsContainerSAS == false && IsStorageSAS == false;

        public static StorageConnection FromAccountKey(string accountName, string accountKey,
            string endpointSuffix = "core.windows.net", string defaultEndpointsProtocol = "https")
        {
            return new StorageConnection
            {
                ConnectionString = $"DefaultEndpointsProtocol={defaultEndpointsProtocol};" +
                                   $"AccountName={accountName};" +
                                   $"AccountKey={accountKey};" +
                                   $"EndpointSuffix={endpointSuffix}"
            };
        }

        public Uri GetBaseUriFromConnectionString(string contnainerName = null)
        {
            var parts = ConnectionString.Split(';')
                .ToDictionary(p => p.Substring(0, p.IndexOf('=')), p => p.Substring(p.IndexOf('=') + 1));
            var url =
                $"{parts["DefaultEndpointsProtocol"]}://{parts["AccountName"]}.blob.{parts["EndpointSuffix"]}/{contnainerName ?? ""}";
            return new Uri(url);
        }

        public static StorageConnection Parse(string value)
        {
            if (value.StartsWith("https://"))
                return new StorageConnection { Url = new Uri(value) };
            return new StorageConnection { ConnectionString = value };
        }
    }
}
