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



public class MainViewModel
{
    private readonly string _pdfFolderPath = @"C:\firmasFirmadas"; // Ruta de la carpeta
    public ObservableCollection<PdfModel> PdfFiles { get; set; }
    private FileSystemWatcher _watcher; // Observador de cambios en la carpeta
    private string _token;
    // Comandos
    public ICommand SelectAllCommand { get; }
    public ICommand SignCommand { get; }
    public ICommand UnselectAllCommand { get; }
    public ICommand DeclineSelectCommand { get; }
    public ICommand ViewPdfCommand { get; } 

    public MainViewModel(string token)
    {
        PdfFiles = new ObservableCollection<PdfModel>();
        _token = token;

        InitializeDocumentsAsync();

        SelectAllCommand = new RelayCommand(SelectAll);
        SignCommand = new RelayCommand(SignSelectedFiles);
        UnselectAllCommand = new RelayCommand(UnselectAll);
        DeclineSelectCommand = new RelayCommand(DeclineSelectedFiles);
        ViewPdfCommand = new RelayCommand<string>(ViewPdf); // Inicializa el comando

        ConfigureFileWatcher();
    }
    public async Task InitializeDocumentsAsync()
    {
        await DownloadAndSavePdfsAsync();  // 1) Baja los archivos desde la API
        LoadPdfFiles();                    // 2) Los carga visualmente en la app
    }
    private void LoadPdfFiles()
    {
        if (!Directory.Exists(_pdfFolderPath))
            return;

        var files = Directory.GetFiles(_pdfFolderPath, "*.pdf");

        Application.Current.Dispatcher.Invoke(() =>
        {
            PdfFiles.Clear();
            foreach (var file in files)
            {
                FileInfo fileInfo = new FileInfo(file);
                PdfFiles.Add(new PdfModel
                {
                    Name = fileInfo.Name,
                    LastModified = fileInfo.LastWriteTime.ToString("dd/MM/yyyy HH:mm"),
                    IsSelected = false  ,
                    Path = fileInfo.FullName
                });
            }
        });
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
        Application.Current.Dispatcher.Invoke(LoadPdfFiles);
    }
    private async Task DownloadAndSavePdfsAsync()
    {
        var documentos = await GetDocumentsFromApiAsync();

        if (!Directory.Exists(_pdfFolderPath))
            Directory.CreateDirectory(_pdfFolderPath);

        foreach (var doc in documentos)
        {
            var filePath = Path.Combine(_pdfFolderPath, doc.FileName);
            File.WriteAllBytes(filePath, doc.Content);
        }
    }

    private async Task<List<DocumentToSignDto>> GetDocumentsFromApiAsync()
    {
        try
        {
            var client = new HttpClient();
            client.BaseAddress = new Uri("https://localhost:7067/");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);

            var response = await client.GetAsync("api/Documents/documents-to-sign");

            if (!response.IsSuccessStatusCode)
                return new List<DocumentToSignDto>();

            var content = await response.Content.ReadAsStringAsync();

            // Log temporal
            Console.WriteLine("JSON recibido:");
            Console.WriteLine(content);

            var documents = JsonConvert.DeserializeObject<List<DocumentToSignDto>>(content);

            return documents?? new List<DocumentToSignDto>();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error al obtener documentos:");
            Console.WriteLine(ex.Message);
            return new List<DocumentToSignDto>();
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


    private void SignSelectedFiles()
        {
        var selectedFiles = PdfFiles.Where(p => p.IsSelected).ToList();
        string destinationFolder = @"C:\Ruta\A\Carpeta\DeFirma";

        // Verificar si la carpeta de destino existe
        if (!Directory.Exists(destinationFolder))
        {
            Directory.CreateDirectory(destinationFolder);
        }

        foreach (var pdf in selectedFiles)
        {
            string sourceFilePath = pdf.Path;
            string destinationFilePath = Path.Combine(destinationFolder, Path.GetFileName(sourceFilePath));

            try
            {
                File.Move(sourceFilePath, destinationFilePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al mover el archivo: {ex.Message}");
                return; // Si hay un error, no seguimos con los siguientes pasos
            }
        }

        // Modificar el archivo XML de configuración
        ModifyXolidoSignConfig(destinationFolder);

        // Ejecutar XolidoSign
        StartXolidoSign();
    }

    private void ModifyXolidoSignConfig(string signingFolder)
    {
        try
        {
            string userName = Environment.UserName;
            string xmlPath = $@"C:\Users\{userName}\AppData\Roaming\Xolido_Systems,_S_A_\XolidoSign\sign.xml";

            if (File.Exists(xmlPath))
            {
                XDocument xmlDoc = XDocument.Load(xmlPath);

                // Buscar el campo 'lastDirectorySelectFiles'
                var lastDirField = xmlDoc.Descendants("field")
                    .FirstOrDefault(f => f.Attribute("name")?.Value == "lastDirectorySelectFiles");

                if (lastDirField != null)
                {
                    if (lastDirField.Value != signingFolder)
                    {
                        lastDirField.Value = signingFolder;
                        xmlDoc.Save(xmlPath);
                        MessageBox.Show("Configuración de firma actualizada en el XML.");
                    }
                    else
                    {
                        MessageBox.Show("El XML ya tiene la ruta correcta.");
                    }
                }
                else
                {
                    MessageBox.Show("No se encontró el campo 'lastDirectorySelectFiles' en el XML.");
                }
            }
            else
            {
                MessageBox.Show("El archivo de configuración sign.xml no existe.");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al modificar el XML: {ex.Message}");
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
    private void DeclineSelectedFiles()
    {
        var selectedFiles = PdfFiles.Where(p => p.IsSelected).ToList();

        if (!selectedFiles.Any())
        {
            MessageBox.Show("No hay archivos seleccionados para denegar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        foreach (var pdf in selectedFiles)
        {
            MessageBox.Show($"Denegando: {pdf.Name}");
        }

       
        // await HttpClient.PostAsync("URL_API_DENEGAR", new StringContent(JsonConvert.SerializeObject(selectedFiles), Encoding.UTF8, "application/json"));
    }

}
