using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using LegalDesktop.Services;

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
            Process.Start(new ProcessStartInfo(AppConfig.DocumentationUrl) { UseShellExecute = true });
        }
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
