using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PrintManager.Services
{
    /// <summary>
    /// Monitorea la carpeta spool de la impresora virtual.
    /// Cuando el spooler escribe un archivo .prn, lo convierte a PDF y notifica a la app.
    /// </summary>
    public class SpoolWatcherService : IDisposable
    {
        private readonly string _spoolFolder;
        private readonly string _spoolFile;
        private readonly GhostscriptService _gsService;
        private FileSystemWatcher? _watcher;
        private bool _disposed;
        private bool _processing;
        private readonly object _lock = new();

        /// <summary>Se dispara cuando un nuevo PDF está listo: (pdfPath, documentName)</summary>
        public event Action<string, string>? DocumentReady;

        public SpoolWatcherService(GhostscriptService gsService, string spoolFolder = @"C:\PrintManagerSpool")
        {
            _gsService = gsService;
            _spoolFolder = spoolFolder;
            _spoolFile = Path.Combine(spoolFolder, "output.prn");
        }

        public void Start()
        {
            if (!Directory.Exists(_spoolFolder))
                Directory.CreateDirectory(_spoolFolder);

            if (File.Exists(_spoolFile))
            {
                _ = ProcessSpoolFileAsync();
            }

            _watcher = new FileSystemWatcher(_spoolFolder)
            {
                Filter = "output.prn",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                EnableRaisingEvents = true,
                IncludeSubdirectories = false
            };

            _watcher.Changed += OnSpoolFileChanged;
            _watcher.Created += OnSpoolFileChanged;
        }

        public void Stop()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Changed -= OnSpoolFileChanged;
                _watcher.Created -= OnSpoolFileChanged;
                _watcher.Dispose();
                _watcher = null;
            }
        }

        private async void OnSpoolFileChanged(object sender, FileSystemEventArgs e)
        {
            await ProcessSpoolFileAsync();
        }

        private async Task ProcessSpoolFileAsync()
        {
            lock (_lock)
            {
                if (_processing) return;
                _processing = true;
            }

            try
            {
                // IMPORTANTE: Capturar el nombre del documento AHORA, antes de que el
                // spooler termine y elimine el trabajo de la cola.
                // El trabajo aún está en estado "Printing" mientras se escribe el archivo.
                string documentName = GetDocumentNameFromSpooler();

                await Task.Delay(2000);
                await WaitForFileReady(_spoolFile);

                if (!File.Exists(_spoolFile)) return;

                var info = new FileInfo(_spoolFile);
                if (info.Length < 100) return;

                // Copiar archivo a ubicación temporal
                string tempPs = Path.Combine(Path.GetTempPath(), $"spool_{Guid.NewGuid():N}.ps");
                File.Copy(_spoolFile, tempPs, true);

                // Eliminar archivo del spool
                try { File.Delete(_spoolFile); } catch { }

                // Convertir PostScript a PDF
                string pdfPath = Path.Combine(Path.GetTempPath(), $"spool_{Guid.NewGuid():N}.pdf");
                try
                {
                    _gsService.ConvertPsToPdf(tempPs, pdfPath);
                }
                finally
                {
                    try { File.Delete(tempPs); } catch { }
                }

                // Notificar con nombre del documento
                if (File.Exists(pdfPath))
                {
                    DocumentReady?.Invoke(pdfPath, documentName);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error procesando spool: {ex.Message}");
            }
            finally
            {
                lock (_lock)
                {
                    _processing = false;
                }
            }
        }

        /// <summary>
        /// Obtiene el nombre del documento original consultando la cola de impresión
        /// de "PrintManager Color" en el spooler de Windows.
        /// Debe llamarse ANTES del delay, mientras el trabajo aún está en la cola.
        /// </summary>
        private string GetDocumentNameFromSpooler()
        {
            try
            {
                using var ps = new System.Printing.LocalPrintServer();
                var queues = ps.GetPrintQueues();
                var pmQueue = queues.FirstOrDefault(q =>
                    q.Name.Equals("PrintManager Color", StringComparison.OrdinalIgnoreCase));

                if (pmQueue != null)
                {
                    pmQueue.Refresh();
                    var jobs = pmQueue.GetPrintJobInfoCollection();
                    foreach (var job in jobs)
                    {
                        string name = job.Name;
                        // Ignorar nombres genéricos del driver
                        if (!string.IsNullOrWhiteSpace(name)
                            && !name.Equals("MSxpsPS", StringComparison.OrdinalIgnoreCase))
                        {
                            return name;
                        }
                    }
                }
            }
            catch { }

            return "Documento";
        }

        private async Task WaitForFileReady(string path, int maxWaitMs = 30000)
        {
            int waited = 0;
            int interval = 500;

            while (waited < maxWaitMs)
            {
                try
                {
                    if (!File.Exists(path)) return;
                    using var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
                    return;
                }
                catch (IOException)
                {
                    await Task.Delay(interval);
                    waited += interval;
                }
                catch
                {
                    return;
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Stop();
                _disposed = true;
            }
        }
    }
}
