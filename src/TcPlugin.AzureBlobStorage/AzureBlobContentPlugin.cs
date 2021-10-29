using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Azure.Storage.Blobs.Models;
using TcPluginBase;
using TcPluginBase.Content;

namespace TcPlugin.AzureBlobStorage
{
    public class AzureBlobContentPlugin : ContentPlugin
    {
        private readonly BlobFileSystem _blobFileSystem;
        private readonly IList<BlobContentField> _fields;

        public AzureBlobContentPlugin(Settings pluginSettings, BlobFileSystem blobFileSystem) : base(pluginSettings)
        {
            _blobFileSystem = blobFileSystem;

            _fields = typeof(BlobItemProperties).GetProperties().Select(p => new BlobContentField
                { FieldName = p.Name, FieldType = GetPropertyFieldType(p), PropertyInfo = p }).ToList();
        }

        public override bool GetDefaultView(out string viewContents, out string viewHeaders, out string viewWidths,
            out string viewOptions,
            int maxLen)
        {
            //return base.GetDefaultView(out viewContents, out viewHeaders, out viewOptions, out viewWidths, maxLen);
            viewContents = "[=tc.size]\\n[=tc.writedate]\\n[=<fs>.AccessTier]\\n[=<fs>.BlobType]";
            viewHeaders = "Size\\nDate\\nAccessTier\\nBlobType";
            viewWidths = "150,40,-40,40,40,40";
            viewOptions = "-1";
            return true;
        }

        public override ContentFieldType GetSupportedField(int fieldIndex, out string fieldName, out string units,
            int maxLen)
        {
            if (fieldIndex >= _fields.Count)
            {
                fieldName = null;
                units = null;
                return ContentFieldType.NoMoreFields;
            }

            var field = _fields[fieldIndex];
            fieldName = field.FieldName;
            units = "";
            return field.FieldType;
        }

        public override SupportedFieldOptions GetSupportedFieldFlags(int fieldIndex)
        {
            return base.GetSupportedFieldFlags(fieldIndex);
        }

        public override GetValueResult GetValue(string fileName, int fieldIndex, int unitIndex, int maxLen,
            GetValueFlags flags,
            out string fieldValue, out ContentFieldType fieldType)
        {
            var blobProperty = _blobFileSystem.GetBlobItemProperties(fileName);
            if (blobProperty == null)
            {
                fieldValue = null;
                fieldType = ContentFieldType.String;
                return GetValueResult.FieldEmpty;
            }

            var field = _fields[fieldIndex];
            var value = field.PropertyInfo.GetValue(blobProperty);
            if (value == null)
            {
                fieldValue = null;
                fieldType = ContentFieldType.String;
                return GetValueResult.FieldEmpty;
            }

            fieldValue = value.ToString();
            fieldType = field.FieldType;
            return GetValueResult.Success;
        }

        private ContentFieldType GetPropertyFieldType(PropertyInfo propertyInfo)
        {
            var contentFieldType = ContentFieldType.String;
            var type = propertyInfo.PropertyType;
            if (type == typeof(int) || type == typeof(uint))
                contentFieldType = ContentFieldType.Numeric32;
            if (type == typeof(long) || type == typeof(ulong))
                contentFieldType = ContentFieldType.Numeric64;
            if (type == typeof(bool))
                contentFieldType = ContentFieldType.Boolean;
            if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
                contentFieldType = ContentFieldType.DateTime;
            return contentFieldType;
        }
    }
}
