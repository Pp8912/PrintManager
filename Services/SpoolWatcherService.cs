using System;
using System.IO;
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

        /// <summary>Se dispara cuando un nuevo PDF está listo para preview.</summary>
        public event Action<string>? DocumentReady;

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

            // Procesar archivo existente si hay
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
            // Evitar procesamiento simultáneo (FileSystemWatcher puede disparar múltiples eventos)
            lock (_lock)
            {
                if (_processing) return;
                _processing = true;
            }

            try
            {
                // Esperar un momento para que el spooler termine de escribir
                await Task.Delay(2000);

                // Esperar a que el archivo esté disponible
                await WaitForFileReady(_spoolFile);

                if (!File.Exists(_spoolFile)) return;

                var info = new FileInfo(_spoolFile);
                if (info.Length < 100) return;

                // Copiar el archivo a una ubicación temporal antes de procesarlo
                string tempPs = Path.Combine(Path.GetTempPath(), $"spool_{Guid.NewGuid():N}.ps");
                File.Copy(_spoolFile, tempPs, true);

                // Eliminar el archivo del spool para liberar la cola de impresión
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

                // Notificar que el PDF está listo
                if (File.Exists(pdfPath))
                {
                    DocumentReady?.Invoke(pdfPath);
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
        /// Espera hasta que el archivo esté disponible (no bloqueado por el spooler).
        /// </summary>
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
                    return; // Archivo disponible
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
