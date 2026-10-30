using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace TcPlugin.AzureBlobStorage.Details
{
    /// <summary>
    ///     Shows a property sheet as a two-column list. Group rows are plain bold rows, because list view groups
    ///     depend on the visual styles of the host process.
    /// </summary>
    internal sealed class PropertySheetDialog : Form
    {
        private const double MaxScreenShare = 0.8;
        private const int ColumnPadding = 24;
        private const int MinWidth = 420;

        private readonly ListView _list;
        private readonly PropertySheet _sheet;

        public PropertySheetDialog(PropertySheet sheet)
        {
            _sheet = sheet;

            Text = sheet.Title;
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Font;
            ShowIcon = false;
            ShowInTaskbar = false;
            MinimizeBox = false;
            MaximizeBox = false;
            KeyPreview = true;
            StartPosition = FormStartPosition.Manual;

            _list = CreateList();

            var copy = new Button { Text = "&Copy", AutoSize = true };
            copy.Click += (sender, args) => CopyToClipboard();

            var close = new Button { Text = "Close", AutoSize = true, DialogResult = DialogResult.OK };
            CancelButton = close;
            AcceptButton = close;

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(6)
            };
            buttons.Controls.Add(close);
            buttons.Controls.Add(copy);

            Controls.Add(_list);
            Controls.Add(buttons);

            KeyDown += OnKeyDown;
        }

        public static void Show(IWin32Window owner, PropertySheet sheet)
        {
            using (var dialog = new PropertySheetDialog(sheet))
            {
                dialog.FitTo(Screen.FromHandle(owner?.Handle ?? IntPtr.Zero).WorkingArea);
                dialog.ShowDialog(owner);
            }
        }

        /// <summary>Sizes the window to its content and centers it on the given area.</summary>
        internal void FitTo(Rectangle area)
        {
            CreateControl();
            _list.AutoResizeColumn(0, ColumnHeaderAutoResizeStyle.ColumnContent);
            _list.AutoResizeColumn(1, ColumnHeaderAutoResizeStyle.ColumnContent);
            _list.Columns[0].Width += ColumnPadding;
            _list.Columns[1].Width += ColumnPadding;

            var rowHeight = _list.Items.Count > 0 ? _list.GetItemRect(0).Height : Font.Height;
            var chromeWidth = Width - ClientSize.Width + SystemInformation.VerticalScrollBarWidth;
            var chromeHeight = Height - ClientSize.Height + _list.Top + Controls.OfType<FlowLayoutPanel>().Single().Height;

            var width = Math.Max(MinWidth, _list.Columns[0].Width + _list.Columns[1].Width + chromeWidth);
            var height = (rowHeight * (_list.Items.Count + 2)) + chromeHeight;

            Width = Math.Min(width, (int)(area.Width * MaxScreenShare));
            Height = Math.Min(height, (int)(area.Height * MaxScreenShare));
            Location = new Point(area.Left + ((area.Width - Width) / 2), area.Top + ((area.Height - Height) / 2));
            MinimumSize = new Size(MinWidth, Math.Min(Height, 200));
        }

        private ListView CreateList()
        {
            var list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                HideSelection = false,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                ShowItemToolTips = true,
                BorderStyle = BorderStyle.None
            };
            list.Columns.Add("Property");
            list.Columns.Add("Value");

            var boldFont = new Font(Font, FontStyle.Bold);
            Disposed += (sender, args) => boldFont.Dispose();

            string group = null;
            foreach (var item in _sheet.Items)
            {
                if (item.Group != group)
                {
                    if (group != null)
                    {
                        list.Items.Add(new ListViewItem(string.Empty));
                    }

                    group = item.Group;
                    list.Items.Add(new ListViewItem(group) { Font = boldFont });
                }

                var row = new ListViewItem("  " + item.Name) { ToolTipText = item.Value, Tag = item };
                row.SubItems.Add(item.Value);
                list.Items.Add(row);
            }

            return list;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.C)
            {
                CopyToClipboard();
                e.Handled = true;
            }
        }

        /// <summary>Copies the values of the selected rows, or the whole sheet if nothing is selected.</summary>
        private void CopyToClipboard()
        {
            var selected = _list.SelectedItems.Cast<ListViewItem>().Select(row => row.Tag).OfType<PropertyItem>().ToList();
            var text = selected.Count == 0
                ? _sheet.ToText()
                : string.Join(Environment.NewLine, selected.Select(item => item.Value));

            try
            {
                Clipboard.SetText(text);
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                // another process holds the clipboard; the user can simply copy again
            }
        }
    }
}
