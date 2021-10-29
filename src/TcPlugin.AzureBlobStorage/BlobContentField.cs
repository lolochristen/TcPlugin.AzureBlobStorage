using System.Reflection;
using TcPluginBase.Content;

namespace TcPlugin.AzureBlobStorage
{
    public class BlobContentField
    {
        public string FieldName
        {
            get;
            set;
        }

        public ContentFieldType FieldType
        {
            get;
            set;
        }

        public PropertyInfo PropertyInfo
        {
            get;
            set;
        }
    }
}
