using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using PrintManager.Models;
using PrintManager.Services;

// Resolver ambigüedades WPF vs WinForms
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;
using RoutedEventArgs = System.Windows.RoutedEventArgs;
using Window = System.Windows.Window;
using WindowState = System.Windows.WindowState;


namespace PrintManager
{
    public partial class MainWindow : Window
    {
        private readonly PrinterService _printerService;
        private readonly GhostscriptService _gsService;
        private SpoolWatcherService? _spoolWatcher;

        // Cola de documentos
        private readonly ObservableCollection<PrintJob> _printQueue = new();
        private PrintJob? _selectedJob;
        private int _currentPage = 1;
        private int _totalPages = 0;

        // System tray
        private System.Windows.Forms.NotifyIcon? _trayIcon;

        public MainWindow()
        {
            InitializeComponent();
            _printerService = new PrinterService();
            _gsService = new GhostscriptService();

            DocumentList.ItemsSource = _printQueue;

            LoadPrinters();
            LoadColorOptions();
            SetupTrayIcon();

            // Cargar documento si se pasó por argumento
            if (!string.IsNullOrEmpty(App.InputFilePath) && File.Exists(App.InputFilePath))
            {
                AddDocumentToQueue(App.InputFilePath);
            }

            // Iniciar monitoreo de carpeta spool
            if (App.WatchSpool)
            {
                StartSpoolWatcher();
                StatusText.Text = "🟢 Vigilando cola de impresión...";
            }
        }

        private void SetupTrayIcon()
        {
            _trayIcon = new System.Windows.Forms.NotifyIcon
            {
                Text = "PrintManager Color",
                Icon = System.Drawing.SystemIcons.Application,
                Visible = false
            };

            _trayIcon.DoubleClick += (s, e) =>
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();
                _trayIcon.Visible = false;
            };

            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("Abrir PrintManager", null, (s, e) =>
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();
                _trayIcon.Visible = false;
            });
            menu.Items.Add("Salir", null, (s, e) =>
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                Application.Current.Shutdown();
            });
            _trayIcon.ContextMenuStrip = menu;
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
                if (_trayIcon != null)
                {
                    _trayIcon.Visible = true;
                    _trayIcon.ShowBalloonTip(2000, "PrintManager",
                        "La app sigue vigilando la cola de impresión.", 
                        System.Windows.Forms.ToolTipIcon.Info);
                }
            }
        }

        private void StartSpoolWatcher()
        {
            _spoolWatcher = new SpoolWatcherService(_gsService);
            _spoolWatcher.DocumentReady += OnSpoolDocumentReady;
            _spoolWatcher.Start();
        }

        private void OnSpoolDocumentReady(string pdfPath)
        {
            Dispatcher.Invoke(() =>
            {
                AddDocumentToQueue(pdfPath);

                // Mostrar la ventana si estaba minimizada
                Show();
                if (WindowState == WindowState.Minimized)
                    WindowState = WindowState.Normal;
                Activate();
                Topmost = true;
                Topmost = false;
                Focus();

                if (_trayIcon != null)
                    _trayIcon.Visible = false;
            });
        }

        /// <summary>Agrega un documento a la cola de impresión.</summary>
        private async void AddDocumentToQueue(string filePath)
        {
            var job = new PrintJob
            {
                FileName = Path.GetFileName(filePath),
                FilePath = filePath,
                Status = "Procesando..."
            };

            _printQueue.Add(job);
            DocumentList.SelectedItem = job;

            // Obtener info del documento en background
            try
            {
                var pageInfo = await Task.Run(() => _gsService.GetPageOrientation(filePath));
                var pageCount = await Task.Run(() => _gsService.GetPageCount(filePath));

                job.PageInfo = pageInfo;
                job.PageCount = pageCount;
                job.Status = "Listo";

                // Actualizar UI si este es el seleccionado
                if (_selectedJob == job)
                {
                    UpdateDocumentInfo(job);
                }
            }
            catch (Exception ex)
            {
                job.Status = $"Error: {ex.Message}";
            }
        }

        private async void DocumentList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedJob = DocumentList.SelectedItem as PrintJob;
            if (_selectedJob == null)
            {
                PreviewImage.Source = null;
                ClearDocumentInfo();
                return;
            }

            _currentPage = 1;
            _totalPages = _selectedJob.PageCount > 0 ? _selectedJob.PageCount : 1;
            UpdateDocumentInfo(_selectedJob);
            await LoadPreviewAsync(_selectedJob.FilePath, _currentPage);
        }

        private void UpdateDocumentInfo(PrintJob job)
        {
            if (job.PageInfo != null && job.PageInfo.WidthMm > 0)
            {
                string orientLabel = job.PageInfo.IsPortrait ? "Vertical (Portrait)" : "Horizontal (Landscape)";
                OrientationText.Text = $"📐 Orientación: {orientLabel}";
                PageSizeText.Text = $"📄 Tamaño: {job.PageInfo.WidthMm} × {job.PageInfo.HeightMm} mm";
            }
            else
            {
                OrientationText.Text = "📐 Orientación: —";
                PageSizeText.Text = "📄 Tamaño: —";
            }

            _totalPages = job.PageCount > 0 ? job.PageCount : 1;
            PageCountText.Text = $"📑 Páginas: {_totalPages}";
            UpdatePageIndicator();
        }

        private void ClearDocumentInfo()
        {
            OrientationText.Text = "📐 Orientación: —";
            PageSizeText.Text = "📄 Tamaño: —";
            PageCountText.Text = "📑 Páginas: —";
            PageIndicator.Text = "Página 0 / 0";
        }

        private void UpdatePageIndicator()
        {
            PageIndicator.Text = $"Página {_currentPage} / {_totalPages}";
            PrevPageBtn.IsEnabled = _currentPage > 1;
            NextPageBtn.IsEnabled = _currentPage < _totalPages;
        }

        private async Task LoadPreviewAsync(string filePath, int page)
        {
            StatusText.Text = $"Generando vista previa (pág. {page})...";
            try
            {
                var image = await Task.Run(() => _gsService.GeneratePreview(filePath, page));
                if (image != null)
                    PreviewImage.Source = image;
                StatusText.Text = "🟢 Vista previa lista.";
            }
            catch
            {
                StatusText.Text = "⚠ Error generando vista previa.";
            }
        }

        private async void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedJob == null || _currentPage <= 1) return;
            _currentPage--;
            UpdatePageIndicator();
            await LoadPreviewAsync(_selectedJob.FilePath, _currentPage);
        }

        private async void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedJob == null || _currentPage >= _totalPages) return;
            _currentPage++;
            UpdatePageIndicator();
            await LoadPreviewAsync(_selectedJob.FilePath, _currentPage);
        }

        private void LoadPrinters()
        {
            var printers = _printerService.GetInstalledPrinters();
            var filtered = printers.Where(p => !p.Equals("PrintManager Color", StringComparison.OrdinalIgnoreCase)).ToList();
            PrinterSelector.ItemsSource = filtered;

            string defaultPrinter = _printerService.GetDefaultPrinter();
            if (filtered.Contains(defaultPrinter))
                PrinterSelector.SelectedItem = defaultPrinter;
            else if (filtered.Count > 0)
                PrinterSelector.SelectedIndex = 0;
        }

        private void LoadColorOptions()
        {
            ColorSelector.ItemsSource = ColorOption.GetPresets();
            ColorSelector.SelectedIndex = 1;
        }

        private async void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedJob == null)
            {
                MessageBox.Show("Seleccione un documento de la cola.");
                return;
            }
            await PrintJobAsync(_selectedJob);
        }

        private async void PrintAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (_printQueue.Count == 0)
            {
                MessageBox.Show("No hay documentos en la cola.");
                return;
            }

            var jobs = _printQueue.ToList();
            foreach (var job in jobs)
            {
                await PrintJobAsync(job);
            }
        }

        private async Task PrintJobAsync(PrintJob job)
        {
            string? selectedPrinter = PrinterSelector.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedPrinter))
            {
                MessageBox.Show("Seleccione una impresora física.");
                return;
            }

            var colorOption = ColorSelector.SelectedItem as ColorOption;
            job.Status = "Imprimiendo...";
            StatusText.Text = $"Imprimiendo: {job.FileName}...";
            PrintButton.IsEnabled = false;

            try
            {
                var pageInfo = job.PageInfo;
                var filePath = job.FilePath;
                await Task.Run(() => _gsService.PrintDocument(filePath, selectedPrinter, colorOption, pageInfo));

                job.Status = "✅ Impreso";
                StatusText.Text = $"✅ {job.FileName} enviado a impresión.";

                // Remover de la cola después de imprimir
                await Task.Delay(1500);
                _printQueue.Remove(job);

                // Limpiar archivo temporal
                try { if (File.Exists(filePath)) File.Delete(filePath); } catch { }
            }
            catch (Exception ex)
            {
                job.Status = "❌ Error";
                MessageBox.Show($"Error al imprimir {job.FileName}: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "❌ Error en impresión.";
            }
            finally
            {
                PrintButton.IsEnabled = true;
            }
        }

        private void ClearQueueButton_Click(object sender, RoutedEventArgs e)
        {
            if (_printQueue.Count == 0) return;

            var result = MessageBox.Show("¿Limpiar toda la cola de impresión?", "Confirmar",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                foreach (var job in _printQueue)
                {
                    try { if (File.Exists(job.FilePath)) File.Delete(job.FilePath); } catch { }
                }
                _printQueue.Clear();
                PreviewImage.Source = null;
                ClearDocumentInfo();
                StatusText.Text = "🟢 Cola limpiada. Vigilando...";
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
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
                AddDocumentToQueue(openFileDialog.FileName);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _spoolWatcher?.Dispose();
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
            base.OnClosed(e);
        }
    }
}