using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using TcPlugin.AzureBlobStorage.Authentication;

namespace TcPlugin.AzureBlobStorage.Management
{
    /// <summary>
    ///     Lists tenants, subscriptions and storage accounts through the Azure Resource Manager REST API.
    ///     Names shown in the file system are resolved to ids through cached lookups.
    /// </summary>
    internal class AzureResourceManagerClient
    {
        private const string BaseUrl = "https://management.azure.com";
        private const string TenantsUrl = "/tenants?api-version=2022-12-01";
        private const string SubscriptionsUrl = "/subscriptions?api-version=2022-12-01";

        private static readonly HttpClient HttpClient = new HttpClient();

        private readonly AzureCredentialProvider _credential;
        private readonly Dictionary<Guid, Dictionary<string, Guid>> _subscriptionsByTenant = new Dictionary<Guid, Dictionary<string, Guid>>();
        private Dictionary<string, Guid> _tenants;

        public AzureResourceManagerClient(AzureCredentialProvider credential)
        {
            _credential = credential;
        }

        /// <returns>Default domain names of all tenants accessible by the signed-in user.</returns>
        public IEnumerable<string> GetTenants()
        {
            return GetTenantLookup().Keys;
        }

        /// <returns>Display names of all subscriptions in the tenant.</returns>
        public IEnumerable<string> GetSubscriptions(string tenant)
        {
            return GetSubscriptionLookup(ResolveTenantId(tenant)).Keys;
        }

        /// <returns>Names of all storage accounts in the subscription.</returns>
        public IEnumerable<string> GetStorageAccounts(string tenant, string subscription)
        {
            return ListStorageAccounts(tenant, subscription)
                .Select(p => p.GetProperty("name").GetString())
                .ToList();
        }

        public JsonElement GetTenantDetails(string tenant)
        {
            var tenantId = ResolveTenantId(tenant);
            return GetValues(TenantsUrl, null).First(p => p.GetProperty("tenantId").GetGuid() == tenantId);
        }

        public JsonElement GetSubscriptionDetails(string tenant, string subscription)
        {
            var tenantId = ResolveTenantId(tenant);
            var subscriptionId = ResolveSubscriptionId(tenantId, subscription);
            return GetValues(SubscriptionsUrl, tenantId).First(p => p.GetProperty("subscriptionId").GetGuid() == subscriptionId);
        }

        public JsonElement GetStorageAccountDetails(string tenant, string subscription, string account)
        {
            return ListStorageAccounts(tenant, subscription).First(p => p.GetProperty("name").GetString() == account);
        }

        private List<JsonElement> ListStorageAccounts(string tenant, string subscription)
        {
            var tenantId = ResolveTenantId(tenant);
            var subscriptionId = ResolveSubscriptionId(tenantId, subscription);

            return GetValues(
                $"/subscriptions/{subscriptionId}/providers/Microsoft.Storage/storageAccounts?api-version=2023-01-01",
                tenantId);
        }

        private Dictionary<string, Guid> GetTenantLookup()
        {
            return _tenants ??= GetValues(TenantsUrl, null)
                .ToDictionary(p => p.GetProperty("defaultDomain").GetString(), p => p.GetProperty("tenantId").GetGuid());
        }

        private Dictionary<string, Guid> GetSubscriptionLookup(Guid tenantId)
        {
            if (!_subscriptionsByTenant.TryGetValue(tenantId, out var subscriptions))
            {
                subscriptions = GetValues(SubscriptionsUrl, tenantId)
                    .ToDictionary(p => p.GetProperty("displayName").GetString(), p => p.GetProperty("subscriptionId").GetGuid());
                _subscriptionsByTenant[tenantId] = subscriptions;
            }

            return subscriptions;
        }

        private Guid ResolveTenantId(string tenant)
        {
            if (Guid.TryParse(tenant, out var tenantId))
            {
                return tenantId;
            }

            return GetTenantLookup().TryGetValue(tenant, out tenantId) ? tenantId : Guid.Empty;
        }

        private Guid ResolveSubscriptionId(Guid tenantId, string subscription)
        {
            if (Guid.TryParse(subscription, out var subscriptionId))
            {
                return subscriptionId;
            }

            return GetSubscriptionLookup(tenantId).TryGetValue(subscription, out subscriptionId) ? subscriptionId : Guid.Empty;
        }

        /// <summary>Sends a GET request and returns the elements of the <c>value</c> array of the response.</summary>
        private List<JsonElement> GetValues(string relativeUrl, Guid? tenantId)
        {
            var tenant = tenantId.HasValue && tenantId.Value != Guid.Empty ? tenantId.Value.ToString() : null;
            var token = _credential.GetManagementToken(tenant);

            using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + relativeUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

            using var response = HttpClient.SendAsync(request).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();

            var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            using var document = JsonDocument.Parse(content);

            return document.RootElement.GetProperty("value").EnumerateArray().Select(p => p.Clone()).ToList();
        }
    }
}
