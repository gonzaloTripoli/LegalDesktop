using System.Windows;

namespace LegalDesktop.Views
{
    public partial class PinDialog : Window
    {
        public string Pin => PinBox.Password;

        public PinDialog()
        {
            InitializeComponent();
            PinBox.Focus();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Pin))
            {
                MessageBox.Show("El PIN no puede estar vacío.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}