using System;
using System.Collections.Generic;
using System.Windows;
using Microsoft.Azure.Management.ResourceManager.Fluent;

namespace TcPlugin.AzureBlobStorage
{
    public partial class SelectSubscriptionWindow : Window
    {
        public SelectSubscriptionWindow()
        {
            InitializeComponent();
            ConnectButton.Click += (sender, args) =>
            {
                if (OnConnect != null)
                {
                    var response = OnConnect.Invoke(SubscriptionComboBox.SelectedItem as ISubscription);
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
            SubscriptionComboBox.Focus();
        }

        public string Label
        {
            get => LabelText.Text;
            set => LabelText.Text = value;
        }

        public ISubscription SelectedSubscription => SubscriptionComboBox.SelectedItem as ISubscription;

        public Func<ISubscription, bool> OnConnect
        {
            get;
            set;
        }

        public void SetSubscriptions(IEnumerable<ISubscription> subscriptions)
        {
            SubscriptionComboBox.ItemsSource = subscriptions;
            SubscriptionComboBox.SelectedIndex = 0;
        }
    }
}
