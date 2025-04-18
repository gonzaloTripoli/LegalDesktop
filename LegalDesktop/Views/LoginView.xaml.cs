using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using LegalDesktop.ViewModels;

namespace LegalDesktop.Views    
{
    /// <summary>
    /// Lógica de interacción para LoginView.xaml
    /// </summary>
    public partial class LoginView : Window
    {
        public LoginView()
        {
            InitializeComponent();
            var viewModel = new LoginViewModel();
            DataContext = viewModel;

            PasswordBox.PasswordChanged += (s, e) =>
            {
                var pb = s as PasswordBox;
                viewModel.GetType().GetProperty("Password")?.SetValue(viewModel, pb.Password);
            };
        }
    }
}
