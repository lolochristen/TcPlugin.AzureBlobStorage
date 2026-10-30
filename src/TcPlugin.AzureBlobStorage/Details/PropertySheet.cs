using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TcPlugin.AzureBlobStorage.Details
{
    internal sealed class PropertyItem
    {
        public PropertyItem(string group, string name, string value)
        {
            Group = group;
            Name = name;
            Value = value;
        }

        public string Group { get; }

        public string Name { get; }

        public string Value { get; }
    }

    /// <summary>
    ///     The properties of one item, ready to be displayed. Values without content are left out, so builders can add
    ///     everything an item might have.
    /// </summary>
    internal sealed class PropertySheet
    {
        private const string DefaultGroup = "General";

        private readonly List<PropertyItem> _items = new List<PropertyItem>();
        private string _group = DefaultGroup;

        public PropertySheet(string title)
        {
            Title = title;
        }

        public string Title { get; }

        public IReadOnlyList<PropertyItem> Items => _items;

        public PropertySheet Group(string name)
        {
            _group = name;
            return this;
        }

        public PropertySheet Add(string name, object value)
        {
            var text = PropertyFormat.Format(value);
            if (!string.IsNullOrEmpty(text))
            {
                _items.Add(new PropertyItem(_group, name, text));
            }

            return this;
        }

        public string ToText()
        {
            var width = _items.Count == 0 ? 0 : _items.Max(item => item.Name.Length);
            var text = new StringBuilder(Title);
            string group = null;

            foreach (var item in _items)
            {
                if (item.Group != group)
                {
                    group = item.Group;
                    text.Append(Environment.NewLine).Append(Environment.NewLine).Append(group);
                }

                text.Append(Environment.NewLine).Append("  ").Append(item.Name.PadRight(width)).Append("  ").Append(item.Value);
            }

            return text.ToString();
        }
    }
}
