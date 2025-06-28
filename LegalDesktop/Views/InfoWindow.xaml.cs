using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace LegalDesktop.Views
{
    public partial class InfoWindow : Window
    {
        public InfoWindow()
        {
            InitializeComponent();
        }
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
        private void DocumentationButton_Click(object sender, RoutedEventArgs e)
        {
            string url = "https://ruta-a-la-documentacion"; // Reemplazalo por tu URL real o path local
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
