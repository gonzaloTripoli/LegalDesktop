using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
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

namespace LegalDesktop.Views
{
    /// <summary>
    /// Lógica de interacción para ComentaryDialog.xaml
    /// </summary>
    public partial class ComentaryDialog : Window
    {
        private string _comentario;
        public string Comentario
        {
            get => _comentario;
            set
            {
                _comentario = value;
                OnPropertyChanged();
            }
        }

        public ComentaryDialog(string actualComentario = "")
        {
            InitializeComponent();
            Comentario = actualComentario;
            DataContext = this;
        }

        private void Aceptar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
