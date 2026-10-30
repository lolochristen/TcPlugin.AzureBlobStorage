using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace TcPlugin.AzureBlobStorage.Details
{
    internal static class PropertyFormat
    {
        private const string Mask = "*****";

        private static readonly Regex Secrets = new Regex(@"(AccountKey=|\bsig=)[^;&]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly string[] SizeUnits = { "KB", "MB", "GB", "TB", "PB" };

        /// <returns>The display text, or null if the value has no content.</returns>
        public static string Format(object value)
        {
            switch (value)
            {
                case null:
                    return null;
                case bool flag:
                    return flag ? "Yes" : "No";
                case DateTimeOffset date:
                    return date == default ? null : date.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                case byte[] bytes:
                    return bytes.Length == 0 ? null : Convert.ToBase64String(bytes);
                default:
                    return value.ToString();
            }
        }

        public static string FormatSize(long bytes)
        {
            if (bytes < 1024)
            {
                return $"{bytes} bytes";
            }

            var size = (double)bytes;
            var unit = -1;
            while (size >= 1024 && unit < SizeUnits.Length - 1)
            {
                size /= 1024;
                unit++;
            }

            return string.Format(CultureInfo.InvariantCulture, "{0:0.#} {1} ({2:N0} bytes)", size, SizeUnits[unit], bytes);
        }

        /// <summary>Hides account keys and SAS signatures in a connection string or URL.</summary>
        public static string MaskSecrets(string connection)
        {
            return Secrets.Replace(connection, match => match.Groups[1].Value + Mask);
        }

        /// <summary>Removes the query, which holds the SAS token of clients created from a SAS URL.</summary>
        public static string WithoutQuery(Uri uri)
        {
            return uri.GetLeftPart(UriPartial.Path);
        }
    }
}
