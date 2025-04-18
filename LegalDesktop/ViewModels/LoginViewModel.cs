using System.ComponentModel;
using System.Windows.Input;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using LegalDesktop.Services;
using LegalDesktop.Views;

namespace LegalDesktop.ViewModels
{
    public class LoginViewModel : INotifyPropertyChanged
    {
        private string _username;
        private string _errorMessage;
        private readonly ApiService _apiService;
        private string _password;

        public string Password
        {
            get => _password;
            set
            {
                _password = value;
                OnPropertyChanged(nameof(Password));
            }
        }
        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(nameof(Username)); }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(nameof(ErrorMessage)); }
        }

        public ICommand LoginCommand { get; }

        public event PropertyChangedEventHandler PropertyChanged;

        public LoginViewModel()
        {
            _apiService = new ApiService();
            LoginCommand = new RelayCommand(async () => await LoginAsync());
        }

        private async Task LoginAsync()
        {
            var password = _password; 

            var (success, token) = await _apiService.Login(Username, password);

            if (success == 1)
            {
                // Almacena el token en una propiedad
                Token = token;

                MainView mainView = new MainView();
                mainView.DataContext = new MainViewModel(Token); // Pasa el token al MainViewModel
                mainView.Show();
                Application.Current.Windows[0].Close();
            }
            else
            {
                ErrorMessage = "Usuario o contraseña incorrectos";
            }
        }

        // Agrega una propiedad para almacenar el token
        public string Token { get; private set; }

        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
