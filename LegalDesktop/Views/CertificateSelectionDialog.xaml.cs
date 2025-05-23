using System.Windows;

namespace LegalDesktop.Views
{
    public partial class CertificateSelectionDialog : Window
    {
        public string SelectedCertificate { get; private set; }

        public CertificateSelectionDialog(IEnumerable<string> certificates)
        {
            InitializeComponent();
            CertificatesList.ItemsSource = certificates;

            if (CertificatesList.Items.Count > 0)
                CertificatesList.SelectedIndex = 0;
        }

        private void Select_Click(object sender, RoutedEventArgs e)
        {
            if (CertificatesList.SelectedItem == null)
            {
                MessageBox.Show("Debe seleccionar un certificado.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SelectedCertificate = CertificatesList.SelectedItem.ToString();
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}