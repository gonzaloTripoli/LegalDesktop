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
using System.Xml.Linq;
using LegalDesktop.Models;
using LegalDesktop.Models.Dtos;
using Newtonsoft.Json;
using System.Text;
using LegalDesktop.Views;
using LegalDesktop.Services;
using System.Windows.Controls;



public class MainViewModel
{
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
    private readonly string _pdfsToSignFolder = Path.Combine(
       Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LegalDesktop", "PdfToSign");
    private TaskCompletionSource<bool> _signingCompleteTcs;
    private readonly string url = "https://localhost:7067/";

    public ObservableCollection<PdfModel> PdfFiles { get; set; }
    private FileSystemWatcher _watcher; // Observador de cambios en la carpeta

    private string _token;
    // Comandos
    public ICommand SelectAllCommand { get; }
    public ICommand SignCommand { get; }
    public ICommand UnselectAllCommand { get; }
    public ICommand DeclineSelectCommand { get; }
    public ICommand ViewPdfCommand { get; }

    public ICommand SkipCommand { get; }

    public ICommand ViewBackgroundCommand { get; }

    public MainViewModel(string token)
    {
        PdfFiles = new ObservableCollection<PdfModel>();
        _token = token;

        InitializeDocumentsAsync();
        
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
        var documentos = await GetDocumentsFromApiAsync();

        if (!Directory.Exists(_pdfFolderPath))
            Directory.CreateDirectory(_pdfFolderPath);


        if (!Directory.Exists(_pdfBackGroundFolderPath))
            Directory.CreateDirectory(_pdfBackGroundFolderPath);

        foreach (var doc in documentos)
        {

            var fileBackground = Path.Combine(_pdfBackGroundFolderPath, doc.BackgroundDcToSignDto.FileName);
            File.WriteAllBytes(fileBackground, doc.BackgroundDcToSignDto.Content);

            var filePath = Path.Combine(_pdfFolderPath, doc.FileName);
            File.WriteAllBytes(filePath, doc.Content);

            PdfFiles.Add(new PdfModel { Id = doc.Id, Name = doc.FileName, Path = filePath, PathBackGround = fileBackground, PrivateMessage = doc.PrivateMessage });

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
                    File.Delete(pdf.PathBackGround);
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
        var tokenService = new TokenSignerService();
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
            tokenService.Dispose();
        }
    }

    private void ModifyXolidoSignConfig(string signingFolder, string pdfReadyFolder)
    {
        try
        {
            string userName = Environment.UserName;
            string xmlPath = $@"C:\Users\{userName}\AppData\Roaming\Xolido_Systems,_S_A_\XolidoSign\sign.xml";

            if (!File.Exists(xmlPath))
                return;

            XDocument xmlDoc = XDocument.Load(xmlPath);

            var lastDirField = xmlDoc.Descendants("field")
                .FirstOrDefault(f => f.Attribute("name")?.Value == "lastDirectorySelectFiles");

            var nextDirField = xmlDoc.Descendants("field")
                .FirstOrDefault(f => f.Attribute("name")?.Value == "pathCarpetaSalida");

            var modoDeSalidaField = xmlDoc.Descendants("field")
                .FirstOrDefault(f => f.Attribute("name")?.Value == "modoDeSalida");

            bool modified = false;

            if (nextDirField != null && nextDirField.Value != pdfReadyFolder)
            {
                nextDirField.Value = pdfReadyFolder;
                modified = true;
            }

            if (lastDirField != null && lastDirField.Value != signingFolder)
            {
                lastDirField.Value = signingFolder;
                modified = true;
            }

            if (modoDeSalidaField != null)
            {
                // Reemplazar contenido con la estructura deseada
                var structElement = new XElement("struct",
                    new XAttribute("name", "Org.Xolido.XolidoSign.API_Firma.ModoDeSalida"),
                    new XElement("field",
                        new XAttribute("name", "_iModeExpression"),
                        "1"
                    ),
                    new XElement("field",
                        new XAttribute("name", "_expression"),
                        "%n%x"
                    )
                );

                modoDeSalidaField.RemoveNodes();
                modoDeSalidaField.Add(structElement);
                modified = true;
            }

            if (modified)
            {
                xmlDoc.Save(xmlPath);
            }
        }
        catch
        {
        }
    }


    private void StartXolidoSign()
    {
        try
        {
            string xolidoPath = @"C:\Program Files\XolidoSystems\XolidoSign\XolidoSign.exe";

            if (File.Exists(xolidoPath))
            {
                Process.Start(xolidoPath);
            }
            else
            {
                MessageBox.Show("No se encontró XolidoSign en la ruta esperada.");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al ejecutar XolidoSign: {ex.Message}");
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



    private void StartWatchingSignedFiles(List<PdfModel> expectedFiles)
    {
        if (!Directory.Exists(_signedPdfsFolder))
            Directory.CreateDirectory(_signedPdfsFolder);

        _signingCompleteTcs = new TaskCompletionSource<bool>();

        var expectedFileNames = expectedFiles.Select(f => Path.GetFileName(f.Path)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var foundFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        _watcher = new FileSystemWatcher
        {
            Path = _signedPdfsFolder,
            Filter = "*.pdf",
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
        };

        _watcher.Created += (s, e) =>
        {
            string signedFileName = Path.GetFileName(e.FullPath);

            var matched = expectedFiles.FirstOrDefault(p =>
     Path.GetFileName(e.FullPath).Equals(Path.GetFileName(p.Path), StringComparison.OrdinalIgnoreCase));


            if (matched != null)
            {
                matched.Path = e.FullPath;

                foundFiles.Add(signedFileName);

                if (foundFiles.Count == expectedFiles.Count)
                {
                    _signingCompleteTcs.TrySetResult(true);
                }
            }
        };

        _watcher.EnableRaisingEvents = true;

        Task.Run(async () =>
        {
            MessageBox.Show("Esperando la firma de los documentos en XolidoSign...");

            await _signingCompleteTcs.Task;

            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();

            Application.Current.Dispatcher.Invoke(async () =>
            {
                await UploadSignedFilesAsync(expectedFiles);
            });
        });
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
                    File.Delete(pdf.PathBackGround);
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

        if (!selectedFiles.Any())
        {
            MessageBox.Show("No hay archivos seleccionados para denegar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                var payload = new
                {
                    documentId = pdf.Id,
                    publicMessage = $"Denegado " 
                };

                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync("api/Documents/deny", content);

                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show($"Documento '{pdf.Name}' denegado correctamente.");
                    File.Delete(pdf.PathBackGround);
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

    private void UnselectAll()
    {
        foreach (var pdf in PdfFiles)
        {
            pdf.IsSelected = false;
        }
    }


}
