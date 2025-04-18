using System.Windows;
using LegalDesktop.Views;

namespace LegalSign
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            LoginView loginView = new LoginView();
            loginView.Show();
        }
    }
}
