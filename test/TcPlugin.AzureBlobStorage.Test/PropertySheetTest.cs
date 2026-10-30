using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TcPlugin.AzureBlobStorage.Details;

namespace TcPlugin.AzureBlobStorage.Test
{
    [TestClass]
    public class PropertySheetTest
    {
        [TestMethod]
        public void EmptyValues_AreLeftOut()
        {
            var sheet = new PropertySheet("title")
                .Add("Null", null)
                .Add("Empty", "")
                .Add("Date", default(DateTimeOffset))
                .Add("Text", "value");

            Assert.AreEqual("Text", sheet.Items.Single().Name);
        }

        [TestMethod]
        public void Groups_ApplyToFollowingItems()
        {
            var sheet = new PropertySheet("title").Add("A", "1").Group("Storage").Add("B", "2");

            Assert.AreEqual("General", sheet.Items[0].Group);
            Assert.AreEqual("Storage", sheet.Items[1].Group);
        }

        [TestMethod]
        public void ToText_AlignsValuesAndNamesGroups()
        {
            var sheet = new PropertySheet("Properties").Add("A", "1").Add("Longer", "2").Group("Storage").Add("B", "3");

            var expected = string.Join(Environment.NewLine,
                "Properties", "", "General", "  A       1", "  Longer  2", "", "Storage", "  B       3");

            Assert.AreEqual(expected, sheet.ToText());
        }

        [TestMethod]
        [DataRow(0L, "0 bytes")]
        [DataRow(512L, "512 bytes")]
        [DataRow(1536L, "1.5 KB (1,536 bytes)")]
        [DataRow(1572864L, "1.5 MB (1,572,864 bytes)")]
        public void FormatSize(long bytes, string expected)
        {
            Assert.AreEqual(expected, PropertyFormat.FormatSize(bytes));
        }

        [TestMethod]
        public void Format_ShowsLocalDateAndYesNo()
        {
            var date = new DateTimeOffset(2024, 3, 1, 10, 0, 0, TimeSpan.Zero);

            Assert.AreEqual(date.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"), PropertyFormat.Format(date));
            Assert.AreEqual("Yes", PropertyFormat.Format(true));
            Assert.AreEqual("No", PropertyFormat.Format(false));
        }

        [TestMethod]
        [DataRow("AccountName=a;AccountKey=c2VjcmV0==;BlobEndpoint=http://x/a;", "AccountName=a;AccountKey=*****;BlobEndpoint=http://x/a;")]
        [DataRow("https://a.blob.core.windows.net/?sv=2022&sig=abc%3D&sp=rl", "https://a.blob.core.windows.net/?sv=2022&sig=*****&sp=rl")]
        [DataRow("BlobEndpoint=http://x/a;SharedAccessSignature=sv=2022&sig=abc", "BlobEndpoint=http://x/a;SharedAccessSignature=sv=2022&sig=*****")]
        [DataRow("https://a.blob.core.windows.net/", "https://a.blob.core.windows.net/")]
        public void MaskSecrets(string connection, string expected)
        {
            Assert.AreEqual(expected, PropertyFormat.MaskSecrets(connection));
        }

        [TestMethod]
        public void WithoutQuery_RemovesSas()
        {
            Assert.AreEqual("https://a.blob.core.windows.net/c/a.txt",
                PropertyFormat.WithoutQuery(new Uri("https://a.blob.core.windows.net/c/a.txt?sv=2022&sig=abc")));
        }
    }
}
