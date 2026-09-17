using System;
using System.Reflection;
using TcPluginBase.Content;

namespace TcPlugin.AzureBlobStorage
{
    /// <summary>A custom column backed by a property of the blob properties object.</summary>
    internal class BlobContentField
    {
        private readonly PropertyInfo _property;

        public BlobContentField(PropertyInfo property)
        {
            _property = property;
            FieldType = GetFieldType(property.PropertyType);
        }

        public string Name => _property.Name;

        public ContentFieldType FieldType { get; }

        public object GetValue(object source)
        {
            return source is null ? null : _property.GetValue(source);
        }

        private static ContentFieldType GetFieldType(Type type)
        {
            if (type == typeof(int) || type == typeof(uint))
            {
                return ContentFieldType.Numeric32;
            }

            if (type == typeof(long) || type == typeof(ulong))
            {
                return ContentFieldType.Numeric64;
            }

            if (type == typeof(bool))
            {
                return ContentFieldType.Boolean;
            }

            if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
            {
                return ContentFieldType.DateTime;
            }

            return ContentFieldType.String;
        }
    }
}
