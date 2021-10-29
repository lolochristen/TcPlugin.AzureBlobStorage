using System;
using System.Collections.Generic;
using System.Windows;

namespace TcPlugin.AzureBlobStorage
{
    public partial class SelectTenantWindow : Window
    {
        public SelectTenantWindow()
        {
            InitializeComponent();
            ConnectButton.Click += (sender, args) =>
            {
                if (OnConnect != null)
                {
                    var response = OnConnect.Invoke(SelectedTenant.Key);
                    if (response)
                    {
                        DialogResult = true;
                        Close();
                    }
                }
                else
                {
                    DialogResult = true;
                    Close();
                }
            };
            TenantComboBox.Focus();
        }

        public KeyValuePair<string, string> SelectedTenant => (KeyValuePair<string, string>)TenantComboBox.SelectedItem;

        public string Label
        {
            get => LabelText.Text;
            set => LabelText.Text = value;
        }

        public Func<string, bool> OnConnect
        {
            get;
            set;
        }

        public void SetTenants(Dictionary<string, string> tenants)
        {
            TenantComboBox.ItemsSource = tenants;
            TenantComboBox.SelectedIndex = 0;
        }
    }
}
