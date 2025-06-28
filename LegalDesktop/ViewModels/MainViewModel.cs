using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using LegalDesktop.Services;
using System.Xml.Linq;
using LegalDesktop.Models;
using LegalDesktop.Models.Dtos;
using Newtonsoft.Json;
using System.Text;
using LegalDesktop.Views;
using LegalDesktop.Services;
using System.Windows.Controls;
using System.ComponentModel;



public class MainViewModel : INotifyPropertyChanged
{

    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    private readonly string _pdfFolderPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LegalDesktop",
        "FirmasFirmadas"
    );
    private readonly string _pdfBackGroundFolderPath = Path.Combine
     (Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "LegalDesktop",
    "FirmasFirmadasBackground"
);
    private readonly string _signedPdfsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LegalDesktop", "SignedPdfs");
    private readonly string url = AppConfig.BaseApiUrl;

    public ObservableCollection<PdfModel> PdfFiles { get; set; }
    private FileSystemWatcher _watcher; // Observador de cambios en la carpeta
    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            _isLoading = value;
            OnPropertyChanged(nameof(IsLoading)); // Asegúrate de tener INotifyPropertyChanged implementado
        }
    }
    private string _token;
    // Comandos
    public ICommand SelectAllCommand { get; }
    public ICommand SignCommand { get; }
    public ICommand OpenInfoCommand { get; }

    public ICommand UnselectAllCommand { get; }
    public ICommand DeclineSelectCommand { get; }
    public ICommand ViewPdfCommand { get; }

    public ICommand SkipCommand { get; }

    public ICommand ViewBackgroundCommand { get; }

    public ICommand RefreshCommand { get; }
    public MainViewModel(string token)
    {
        PdfFiles = new ObservableCollection<PdfModel>();
        _token = token;

        InitializeDocumentsAsync();
        OpenInfoCommand = new RelayCommand(OpenInfoWindow);

        RefreshCommand = new RelayCommand(Refresh);
        SelectAllCommand = new RelayCommand(SelectAll);
        SignCommand = new RelayCommand(SignSelectedFiles);
        SkipCommand = new RelayCommand(SkipSelectedFiles);
        UnselectAllCommand = new RelayCommand(UnselectAll);
        DeclineSelectCommand = new RelayCommand(DeclineSelectedFiles);
        ViewPdfCommand = new RelayCommand<string>(ViewPdf); // Inicializa el comando
        ViewBackgroundCommand = new RelayCommand<string>(ViewPdf);
        ConfigureFileWatcher();
    }
    public async Task InitializeDocumentsAsync()
    {
        await DownloadAndSavePdfsAsync();
    }
    private async  void Refresh()
    {
        await DownloadAndSavePdfsAsync();
    }

    private void OpenInfoWindow()
    {
        var infoWindow = new InfoWindow();

        // Previene el error si MainWindow no está correctamente seteado
        if (Application.Current?.MainWindow != infoWindow && Application.Current?.MainWindow != null)
        {
            infoWindow.Owner = Application.Current.MainWindow;
        }

        infoWindow.ShowDialog();
    }

    private void ConfigureFileWatcher()
    {
        if (!Directory.Exists(_pdfFolderPath))
            Directory.CreateDirectory(_pdfFolderPath);

        _watcher = new FileSystemWatcher
        {
            Path = _pdfFolderPath,
            Filter = "*.pdf", // Solo archivos PDF
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
        };

        // Suscribimos los eventos de cambios
        _watcher.Created += (s, e) => OnFileChanged();
        _watcher.Deleted += (s, e) => OnFileChanged();
        _watcher.Changed += (s, e) => OnFileChanged();
        _watcher.Renamed += (s, e) => OnFileChanged();

        _watcher.EnableRaisingEvents = true; // Activamos el FileSystemWatcher
    }

    private void OnFileChanged()
    {
        // Se ejecuta en el hilo de UI para actualizar la lista
    }

    private async Task DownloadAndSavePdfsAsync()
    {
        IsLoading = true;

        try
        {
           
            var documentos = await GetDocumentsFromApiAsync();

            if (!Directory.Exists(_pdfFolderPath))
                Directory.CreateDirectory(_pdfFolderPath);


            if (!Directory.Exists(_pdfBackGroundFolderPath))
                Directory.CreateDirectory(_pdfBackGroundFolderPath);

            if (documentos.Count>0 && PdfFiles.Count>0)
            {
                PdfFiles.Clear();
            }
            foreach (var doc in documentos)
            {
                var fileBackground = "";

                if (doc.BackgroundDcToSignDto != null){
                    fileBackground = Path.Combine(_pdfBackGroundFolderPath, doc.BackgroundDcToSignDto.FileName);
                    File.WriteAllBytes(fileBackground, doc.BackgroundDcToSignDto.Content);

                }

                var filePath = Path.Combine(_pdfFolderPath, doc.FileName);
                File.WriteAllBytes(filePath, doc.Content);

                PdfFiles.Add(new PdfModel { Id = doc.Id, Name = doc.FileName, Path = filePath, PathBackGround = fileBackground, PrivateMessage = doc.PrivateMessage , SecretaryId= doc.SecretaryId });

            }
        }
        finally
        {
            IsLoading = false;
        }
    }


    private async Task<List<DocumentToSignDto>> GetDocumentsFromApiAsync()
    {
        try
        {
            var client = new HttpClient();
            client.BaseAddress = new Uri(url);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);

            var response = await client.GetAsync("api/Documents/documents-to-sign");

            if (!response.IsSuccessStatusCode)
                return new List<DocumentToSignDto>();

            var content = await response.Content.ReadAsStringAsync();

            ;

            var documents = JsonConvert.DeserializeObject<List<DocumentToSignDto>>(content);

            return documents ?? new List<DocumentToSignDto>();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error al obtener documentos:");
            Console.WriteLine(ex.Message);
            return new List<DocumentToSignDto>();
        }
    }


    public async void SkipSelectedFiles()
    {
        var selectedFiles = PdfFiles.Where(p => p.IsSelected).ToList();
        if (selectedFiles.Count == 0)
        {
            MessageBox.Show("Ningún documento marcado.", "Falta selección", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        using var client = new HttpClient
        {
            BaseAddress = new Uri(url)
        };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        foreach (var pdf in selectedFiles)
        {
            try
            {
                var dialog = new ComentaryDialog(pdf.PrivateMessage);
                if (dialog.ShowDialog() != true)
                    continue;

                pdf.PrivateMessage = dialog.Comentario;

                var json = JsonConvert.SerializeObject(new
                {
                    documentId = pdf.Id,
                    privateMessage = pdf.PrivateMessage
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync("api/Documents/skip", content);

                if (response.IsSuccessStatusCode)
                {
                    if (pdf.PathBackGround != null)
                    {
                        File.Delete(pdf.PathBackGround);

                    }
                    File.Delete(pdf.Path);
                    PdfFiles.Remove(pdf);


                }
                else
                {
                    MessageBox.Show($"Fallo al marcar como omitido: '{pdf.Path}'. Código: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al marcar como omitido: '{pdf.Path}': {ex.Message}");
            }
        }
    }



    private async void SignSelectedFiles()
    {
        var selectedFiles = PdfFiles.Where(p => p.IsSelected).ToList();
        if (selectedFiles.Count == 0)
        {
            MessageBox.Show("Ningún documento marcado.", "Falta selección", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        var result = MessageBox.Show(
       $"¿Estás seguro que deseas Firmar {selectedFiles.Count} documento(s)? Esta acción no se puede deshacer.",
       "Confirmar firma",
       MessageBoxButton.YesNo,
       MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        var detector = new TokenDetectorService();

        var token = detector.DetectAvailableToken();


        if (token == TokenDetectorService.TokenType.StarSign)
        {


            var tokenService = new StarSignTokenService();

            tokenService.Initialize();

            try
            {
                // 1. Pedir PIN
                var pinDialog = new PinDialog();
                if (pinDialog.ShowDialog() != true) return;

                tokenService.Login(pinDialog.Pin);
                var certificates = tokenService.ListCertificates();
                var certDialog = new CertificateSelectionDialog(certificates);
                if (certDialog.ShowDialog() != true) return;

                // 3. Firmar cada PDF
                foreach (var pdf in selectedFiles)
                {
                    byte[] pdfBytes = File.ReadAllBytes(pdf.Path);
                    byte[] signedBytes = tokenService.SignPdf(
                        pdfBytes,
                        certDialog.SelectedCertificate,
                        pinDialog.Pin
                    );
                    Directory.CreateDirectory(_signedPdfsFolder); 
                    string signedPath = Path.Combine(_signedPdfsFolder, pdf.Name);
                    pdf.Path = signedPath;
                    File.WriteAllBytes(signedPath, signedBytes);

                    await UploadSignedFilesAsync(new List<PdfModel> { pdf });


                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
            finally
            {
                MessageBox.Show("Firmado correctamente.", "Estado de firma", MessageBoxButton.OK, MessageBoxImage.Information);

                tokenService.Dispose();
            }
        }
        else if (token == TokenDetectorService.TokenType.EPass2003)
        {
            var tokenService = new EPass2003TokenService();
            try
            {
                var certificates = tokenService.ListCertificates();
                if (certificates == null || certificates.Count == 0)
                {
                    MessageBox.Show("No se encontraron certificados válidos para ePass2003.");
                    return;
                }

                var certDialog = new CertificateSelectionDialog(certificates);
                if (certDialog.ShowDialog() != true) return;

                foreach (var pdf in selectedFiles)
                {
                    byte[] pdfBytes = File.ReadAllBytes(pdf.Path);
                    byte[] signedBytes = tokenService.SignPdf(
                        pdfBytes,
                        certDialog.SelectedCertificate
                    );
                    string signedPath = Path.Combine(_signedPdfsFolder, pdf.Name);
                    Directory.CreateDirectory(_signedPdfsFolder); // Asegura que la carpeta exista
                    File.WriteAllBytes(signedPath, signedBytes);
                    File.Delete(pdf.Path);
                    pdf.Path = signedPath;

                    await UploadSignedFilesAsync(new List<PdfModel> { pdf });
                    MessageBox.Show( "Firmado correctamente.", "Estado de firma.", MessageBoxButton.OK, MessageBoxImage.Information);

                }

            }
            catch (Exception ex)
            {
                MessageBox.Show("Error", "No se ha podido firmar correctamente ", MessageBoxButton.OK, MessageBoxImage.Warning);
                MessageBox.Show( "Error", "Ha habido un error al intentar firmar el documento. Por favor comuniquese con el area de sistemas si ya reinicio la aplicacion.", MessageBoxButton.OK, MessageBoxImage.Warning);

            }
        }
        else
        {
            MessageBox.Show("No se detectó un token válido para firmar.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }


    }


    private void ViewPdf(string filePath)
    {
        if (File.Exists(filePath))
        {
            try
            {
                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al abrir el PDF: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else
        {
            MessageBox.Show("El archivo no existe o fue eliminado.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }




    private async Task UploadSignedFilesAsync(List<PdfModel> signedFiles)
    {
        foreach (var pdf in signedFiles)
        {
            try
            {
                using var client = new HttpClient();
                client.BaseAddress = new Uri(url);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);

                using var form = new MultipartFormDataContent();

                var fileBytes = await File.ReadAllBytesAsync(pdf.Path);
                var fileContent = new ByteArrayContent(fileBytes);
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");

                form.Add(fileContent, "file", Path.GetFileName(pdf.Path));
                form.Add(new StringContent(pdf.Id.ToString()), "documentId");

                var response = await client.PostAsync("api/Documents/sign", form);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Archivo '{pdf.Path}' subido correctamente.");
                    if(pdf.PathBackGround != ""){
                        File.Delete(pdf.PathBackGround);

                    }
                    File.Delete(pdf.Path);
                    PdfFiles.Remove(pdf);

                }
                else
                {
                    Console.WriteLine($"Fallo la subida de '{pdf.Path}'. Código: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al subir el archivo '{pdf.Path}': {ex.Message}");
            }
        }
    }

    private async void DeclineSelectedFiles()
    {
        var selectedFiles = PdfFiles.Where(p => p.IsSelected).ToList();

        if (selectedFiles.Count == 0)
        {
            MessageBox.Show("Ningún documento marcado.", "Falta selección", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        var result = MessageBox.Show(
            $"¿Estás seguro que deseas denegar {selectedFiles.Count} documento(s)? Esta acción no se puede deshacer.",
            "Confirmar denegación",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return; 
        }

        using var client = new HttpClient
        {
            BaseAddress = new Uri(url)
        };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        foreach (var pdf in selectedFiles)
        {
            try
            {
                var dialog = new ComentaryDialog(pdf.PrivateMessage);
                if (dialog.ShowDialog() != true)
                    continue;

                pdf.PrivateMessage = dialog.Comentario;

            
                var payload = new
                {
                    documentId = pdf.Id,
                    publicMessage = pdf.PrivateMessage
                };

                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync("api/Documents/deny", content);

                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show($"Documento '{pdf.Name}' denegado correctamente.");
                    if (pdf.PathBackGround != "")
                    {
                        File.Delete(pdf.PathBackGround);

                    }
                    File.Delete(pdf.Path);
                    PdfFiles.Remove(pdf);
                }
                else
                {
                    MessageBox.Show($"Error al denegar '{pdf.Name}': {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Excepción al denegar '{pdf.Name}': {ex.Message}");
            }
        }
    }


    private void SelectAll()
    {
        foreach (var pdf in PdfFiles)
        {
            pdf.IsSelected = true;
        }
    }

    private void filterSecretary (int secretaryId)
    {

    }

    private void UnselectAll()
    {
        foreach (var pdf in PdfFiles)
        {
            pdf.IsSelected = false;
        }
    }


}
