using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using PrintManager.Models;
using PrintManager.Services;

namespace PrintManager
{
    public partial class MainWindow : Window
    {
        private readonly PrinterService _printerService;
        private readonly GhostscriptService _gsService;
        private SpoolWatcherService? _spoolWatcher;
        private string? _currentPdfPath;
        private PageInfo? _pageInfo;

        public MainWindow()
        {
            InitializeComponent();
            _printerService = new PrinterService();
            _gsService = new GhostscriptService();
            
            LoadPrinters();
            LoadColorOptions();
            LoadDocument();

            // Iniciar monitoreo de carpeta spool si está habilitado
            if (App.WatchSpool)
            {
                StartSpoolWatcher();
            }
        }

        private void StartSpoolWatcher()
        {
            _spoolWatcher = new SpoolWatcherService(_gsService);
            _spoolWatcher.DocumentReady += OnSpoolDocumentReady;
            _spoolWatcher.Start();
        }

        /// <summary>
        /// Se ejecuta cuando el SpoolWatcher detecta un nuevo documento de la impresora virtual.
        /// </summary>
        private void OnSpoolDocumentReady(string pdfPath)
        {
            // El evento viene de otro hilo, despachar al hilo UI
            Dispatcher.Invoke(() =>
            {
                LoadDocumentFromPath(pdfPath);

                // Traer la ventana al frente
                if (WindowState == WindowState.Minimized)
                    WindowState = WindowState.Normal;
                Activate();
                Topmost = true;
                Topmost = false;
                Focus();
            });
        }

        /// <summary>
        /// Carga un documento desde una ruta específica (usado por el SpoolWatcher y aperturas directas).
        /// </summary>
        public void LoadDocumentFromPath(string path)
        {
            App.InputFilePath = path;
            _currentPdfPath = path;
            LoadDocument();
        }

        private void LoadPrinters()
        {
            var printers = _printerService.GetInstalledPrinters();
            
            // Filtrar la impresora virtual de la lista (no queremos imprimir a nosotros mismos)
            var filtered = printers.Where(p => !p.Equals("PrintManager Color", StringComparison.OrdinalIgnoreCase)).ToList();
            PrinterSelector.ItemsSource = filtered;
            
            string defaultPrinter = _printerService.GetDefaultPrinter();
            if (filtered.Contains(defaultPrinter))
            {
                PrinterSelector.SelectedItem = defaultPrinter;
            }
            else if (filtered.Count > 0)
            {
                PrinterSelector.SelectedIndex = 0;
            }
        }

        private void LoadColorOptions()
        {
            ColorSelector.ItemsSource = ColorOption.GetPresets();
            ColorSelector.SelectedIndex = 1; // Cian por defecto
        }

        private async void LoadDocument()
        {
            _currentPdfPath = App.InputFilePath;

            if (string.IsNullOrEmpty(_currentPdfPath) || !File.Exists(_currentPdfPath))
            {
                StatusText.Text = "No se ha cargado ningún documento.";
                OrientationText.Text = "📐 Orientación: —";
                PageSizeText.Text = "📄 Tamaño: —";
                return;
            }

            StatusText.Text = $"Cargando: {Path.GetFileName(_currentPdfPath)}...";

            try
            {
                // Cargar preview y orientación en paralelo
                var previewTask = Task.Run(() => _gsService.GeneratePreview(_currentPdfPath));
                var orientationTask = Task.Run(() => _gsService.GetPageOrientation(_currentPdfPath));

                var image = await previewTask;
                _pageInfo = await orientationTask;

                if (image != null)
                {
                    PreviewImage.Source = image;
                }

                // Mostrar información de orientación
                if (_pageInfo.WidthMm > 0 && _pageInfo.HeightMm > 0)
                {
                    string orientLabel = _pageInfo.IsPortrait ? "Vertical (Portrait)" : "Horizontal (Landscape)";
                    OrientationText.Text = $"📐 Orientación: {orientLabel}";
                    PageSizeText.Text = $"📄 Tamaño: {_pageInfo.WidthMm} × {_pageInfo.HeightMm} mm";
                }
                else
                {
                    OrientationText.Text = "📐 Orientación: No detectada";
                    PageSizeText.Text = "📄 Tamaño: —";
                }

                StatusText.Text = "Vista previa lista.";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error cargando documento: " + ex.Message);
            }
        }

        private async void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentPdfPath))
            {
                MessageBox.Show("No hay documento para imprimir.");
                return;
            }

            string? selectedPrinter = PrinterSelector.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedPrinter))
            {
                MessageBox.Show("Seleccione una impresora física.");
                return;
            }

            var colorOption = ColorSelector.SelectedItem as ColorOption;
            StatusText.Text = "Enviando a impresión...";
            PrintButton.IsEnabled = false;

            try
            {
                var pageInfo = _pageInfo;
                await Task.Run(() => 
                {
                    _gsService.PrintDocument(_currentPdfPath, selectedPrinter, colorOption, pageInfo);
                });

                MessageBox.Show("Documento enviado a la cola de impresión exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                StatusText.Text = "Impresión enviada correctamente.";
                PrintButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al imprimir: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Error en impresión.";
                PrintButton.IsEnabled = true;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf|All Files (*.*)|*.*",
                Title = "Seleccionar documento PDF"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                LoadDocumentFromPath(openFileDialog.FileName);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _spoolWatcher?.Dispose();
            base.OnClosed(e);
        }
    }
}