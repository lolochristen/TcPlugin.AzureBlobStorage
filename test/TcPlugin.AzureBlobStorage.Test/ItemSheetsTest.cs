using System;
using System.Linq;
using System.Text.Json;
using Azure.Storage.Blobs.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TcPlugin.AzureBlobStorage.Connections;
using TcPlugin.AzureBlobStorage.Details;

namespace TcPlugin.AzureBlobStorage.Test
{
    [TestClass]
    public class ItemSheetsTest
    {
        private static JsonElement Json(string json)
        {
            return JsonDocument.Parse(json).RootElement;
        }

        private static string Value(PropertySheet sheet, string name)
        {
            return sheet.Items.SingleOrDefault(item => item.Name == name)?.Value;
        }

        [TestMethod]
        public void Tenant_ShowsIdAndDomain()
        {
            var sheet = ItemSheets.Tenant(Json(@"{
                ""tenantId"": ""11111111-1111-1111-1111-111111111111"", ""defaultDomain"": ""contoso.com"", ""displayName"": ""Contoso"",
                ""tenantCategory"": ""Home"", ""countryCode"": ""CH"", ""domains"": [""contoso.com"", ""contoso.ch""] }"));

            Assert.AreEqual("11111111-1111-1111-1111-111111111111", Value(sheet, "Tenant ID"));
            Assert.AreEqual("contoso.com", Value(sheet, "Default domain"));
            Assert.AreEqual("Contoso", Value(sheet, "Name"));
            Assert.AreEqual("contoso.com, contoso.ch", Value(sheet, "Domains"));
        }

        [TestMethod]
        public void Subscription_ShowsNameAndId()
        {
            var sheet = ItemSheets.Subscription(Json(@"{
                ""subscriptionId"": ""22222222-2222-2222-2222-222222222222"", ""displayName"": ""Production"", ""state"": ""Enabled"",
                ""tenantId"": ""11111111-1111-1111-1111-111111111111"", ""subscriptionPolicies"": { ""spendingLimit"": ""Off"" } }"));

            Assert.AreEqual("Production", Value(sheet, "Name"));
            Assert.AreEqual("22222222-2222-2222-2222-222222222222", Value(sheet, "Subscription ID"));
            Assert.AreEqual("Enabled", Value(sheet, "State"));
            Assert.AreEqual("Off", Value(sheet, "Spending limit"));
        }

        [TestMethod]
        public void StorageAccount_ReadsNestedProperties()
        {
            var sheet = ItemSheets.StorageAccount(Json(@"{
                ""name"": ""myaccount"", ""location"": ""switzerlandnorth"", ""kind"": ""StorageV2"",
                ""id"": ""/subscriptions/22222222/resourceGroups/my-rg/providers/Microsoft.Storage/storageAccounts/myaccount"",
                ""sku"": { ""name"": ""Standard_LRS"", ""tier"": ""Standard"" },
                ""properties"": {
                    ""accessTier"": ""Hot"", ""supportsHttpsTrafficOnly"": true, ""allowBlobPublicAccess"": false, ""minimumTlsVersion"": ""TLS1_2"",
                    ""primaryEndpoints"": { ""blob"": ""https://myaccount.blob.core.windows.net/"" }, ""creationTime"": ""2024-03-01T10:00:00Z"" } }"));

            Assert.AreEqual("myaccount", Value(sheet, "Name"));
            Assert.AreEqual("my-rg", Value(sheet, "Resource group"));
            Assert.AreEqual("Standard_LRS", Value(sheet, "SKU"));
            Assert.AreEqual("Hot", Value(sheet, "Access tier"));
            Assert.AreEqual("Yes", Value(sheet, "HTTPS only"));
            Assert.AreEqual("No", Value(sheet, "Public blob access"));
            Assert.AreEqual("https://myaccount.blob.core.windows.net/", Value(sheet, "Blob endpoint"));
            Assert.AreEqual(PropertyFormat.Format(DateTimeOffset.Parse("2024-03-01T10:00:00Z")), Value(sheet, "Created"));
        }

        [TestMethod]
        public void MissingJsonValues_AreLeftOut()
        {
            var sheet = ItemSheets.StorageAccount(Json(@"{ ""name"": ""myaccount"" }"));

            Assert.AreEqual(1, sheet.Items.Count);
        }

        [TestMethod]
        public void Connection_NeverShowsSecrets()
        {
            var sheet = ItemSheets.Connection("emu", "AccountName=a;AccountKey=c2VjcmV0;BlobEndpoint=http://127.0.0.1:10000/a;");

            Assert.AreEqual("emu", Value(sheet, "Name"));
            Assert.AreEqual(nameof(BlobConnectionType.ConnectionString), Value(sheet, "Type"));
            Assert.IsFalse(sheet.ToText().Contains("c2VjcmV0"), sheet.ToText());
        }

        [TestMethod]
        public void Blob_WithoutValues_DoesNotFail()
        {
            var sheet = ItemSheets.Blob("a.txt", new Uri("https://a.blob.core.windows.net/c/a.txt?sig=secret"), new BlobProperties());

            Assert.AreEqual("a.txt", Value(sheet, "Name"));
            Assert.AreEqual("https://a.blob.core.windows.net/c/a.txt", Value(sheet, "URL"));
        }

        [TestMethod]
        public void Container_ListsMetadataInItsOwnGroup()
        {
            var properties = BlobsModelFactory.BlobContainerProperties(
                DateTimeOffset.Parse("2024-03-01T10:00:00Z"), new Azure.ETag("0x1"),
                metadata: new System.Collections.Generic.Dictionary<string, string> { { "owner", "me" } });

            var sheet = ItemSheets.Container("cont", new Uri("https://a.blob.core.windows.net/cont?sig=secret"), properties);

            Assert.AreEqual("https://a.blob.core.windows.net/cont", Value(sheet, "URL"));
            Assert.AreEqual("Metadata", sheet.Items.Single(item => item.Name == "owner").Group);
        }
    }
}
