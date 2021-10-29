using System.Windows;

namespace TcPlugin.AzureBlobStorage
{
    public partial class EnterConnectionInfoWindow : Window
    {
        public EnterConnectionInfoWindow()
        {
            InitializeComponent();
            ConnectionInfo.Focus();
        }

        public string Label
        {
            get => LabelText.Text;
            set => LabelText.Text = value;
        }

        public string ConnectionInfoText
        {
            get => ConnectionInfo.Text;
            set => ConnectionInfo.Text = value;
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
