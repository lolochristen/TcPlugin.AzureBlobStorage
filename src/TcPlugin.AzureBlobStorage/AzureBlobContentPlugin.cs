using System;
using System.Collections.Generic;
using System.Linq;
using Azure.Storage.Blobs.Models;
using TcPluginBase;
using TcPluginBase.Content;

namespace TcPlugin.AzureBlobStorage
{
    /// <summary>
    ///     Exposes the properties of <see cref="BlobItemProperties" /> as custom columns. Values come from the
    ///     properties cached by the last directory listing, so no additional requests are made.
    /// </summary>
    public class AzureBlobContentPlugin : ContentPlugin
    {
        private readonly BlobFileSystem _fileSystem;
        private readonly IReadOnlyList<BlobContentField> _fields;

        public AzureBlobContentPlugin(Settings pluginSettings, BlobFileSystem fileSystem) : base(pluginSettings)
        {
            _fileSystem = fileSystem;
            _fields = typeof(BlobItemProperties).GetProperties().Select(p => new BlobContentField(p)).ToList();
        }

        public override bool GetDefaultView(out string viewContents, out string viewHeaders, out string viewWidths,
            out string viewOptions, int maxLen)
        {
            viewContents = "[=tc.size]\\n[=tc.writedate]\\n[=<fs>.AccessTier]\\n[=<fs>.BlobType]";
            viewHeaders = "Size\\nDate\\nAccessTier\\nBlobType";
            viewWidths = "150,40,-40,40,40,40";
            viewOptions = "-1";
            return true;
        }

        public override ContentFieldType GetSupportedField(int fieldIndex, out string fieldName, out string units, int maxLen)
        {
            if (fieldIndex >= _fields.Count)
            {
                fieldName = null;
                units = null;
                return ContentFieldType.NoMoreFields;
            }

            var field = _fields[fieldIndex];
            fieldName = field.Name;
            units = string.Empty;
            return field.FieldType;
        }

        public override GetValueResult GetValue(string fileName, int fieldIndex, int unitIndex, int maxLen,
            GetValueFlags flags, out string fieldValue, out ContentFieldType fieldType)
        {
            var field = _fields[fieldIndex];
            var value = field.GetValue(_fileSystem.GetBlobItemProperties(fileName));

            if (value is null)
            {
                fieldValue = null;
                fieldType = ContentFieldType.String;
                return GetValueResult.FieldEmpty;
            }

            fieldValue = value.ToString();
            fieldType = field.FieldType;
            return GetValueResult.Success;
        }
    }
}
