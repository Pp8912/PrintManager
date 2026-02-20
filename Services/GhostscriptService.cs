using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using PrintManager.Models;

namespace PrintManager.Services
{
    public class GhostscriptService
    {
        // Posibles rutas de instalación
        private readonly string[] _gsPaths = new[]
        {
            @"C:\Program Files\gs\gs10.02.1\bin\gswin64c.exe",
            @"C:\Program Files\gs\gs10.02.0\bin\gswin64c.exe",
             @"C:\Program Files\gs\gs10.01.2\bin\gswin64c.exe",
            @"C:\Program Files\gs\gs10.01.1\bin\gswin64c.exe",
            @"C:\Program Files\gs\gs10.00.0\bin\gswin64c.exe",
             @"C:\Program Files\gs\gs9.56.1\bin\gswin64c.exe",
            "gswin64c.exe" // Si está en el PATH
        };

        private string GetGhostscriptPath()
        {
            foreach (var path in _gsPaths)
            {
                if (File.Exists(path)) return path;
            }
            return "gswin64c";
        }

        /// <summary>Cuenta las páginas de un PDF.</summary>
        public int GetPageCount(string? pdfPath)
        {
            if (!File.Exists(pdfPath)) return 0;

            try
            {
                // Intentar leer /Count directamente del PDF
                byte[] bytes = File.ReadAllBytes(pdfPath!);
                string text = System.Text.Encoding.ASCII.GetString(bytes);
                // Buscar /Type /Pages seguido de /Count N
                var match = Regex.Match(text, @"/Type\s*/Pages[^>]*/Count\s+(\d+)");
                if (match.Success)
                    return int.Parse(match.Groups[1].Value);
            }
            catch { }

            // Fallback: usar Ghostscript
            try
            {
                string gsPath = GetGhostscriptPath();
                var psi = new ProcessStartInfo
                {
                    FileName = gsPath,
                    Arguments = $"-dQUIET -dNODISPLAY -dNOSAFER -c \"({pdfPath!.Replace("\\", "/")}) (r) file runpdfbegin pdfpagecount = quit\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var p = Process.Start(psi);
                if (p != null)
                {
                    string output = p.StandardOutput.ReadToEnd().Trim();
                    p.WaitForExit();
                    if (int.TryParse(output, out int count))
                        return count;
                }
            }
            catch { }

            return 1; // Default
        }

        public BitmapImage? GeneratePreview(string? pdfPath, int pageNumber = 1)
        {
            if (!File.Exists(pdfPath)) return null;

            string gsPath = GetGhostscriptPath();
            string tempPng = Path.GetTempFileName() + ".png";

            try
            {
                var args = $"-dQUIET -dPARANOIDSAFER -dBATCH -dNOPAUSE -dNOPROMPT -sDEVICE=png16m -dTextAlphaBits=4 -dGraphicsAlphaBits=4 -r96 -dFirstPage={pageNumber} -dLastPage={pageNumber} -sOutputFile=\"{tempPng}\" \"{pdfPath}\"";

                RunGs(gsPath, args);

                if (File.Exists(tempPng))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(tempPng);
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error preview: " + ex.Message);
            }
            finally
            {
               Task.Run(async () => {
                   await Task.Delay(2000);
                   try { if(File.Exists(tempPng)) File.Delete(tempPng); } catch {}
               });
            }
            return null;
        }

        /// <summary>
        /// Detecta las dimensiones y orientación de la primera página del PDF.
        /// Intenta leer MediaBox del PDF directamente, si falla usa Ghostscript bbox.
        /// </summary>
        public PageInfo GetPageOrientation(string? pdfPath)
        {
            if (!File.Exists(pdfPath)) return new PageInfo();

            // Intento 1: Leer MediaBox directamente del PDF (rápido)
            var result = TryParseMediaBox(pdfPath!);
            if (result != null) return result;

            // Intento 2: Usar Ghostscript bbox (más lento pero funciona con PDFs comprimidos)
            return GetPageInfoViaBbox(pdfPath!);
        }

        private PageInfo? TryParseMediaBox(string pdfPath)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(pdfPath);
                string text = System.Text.Encoding.ASCII.GetString(bytes);

                var mbMatch = Regex.Match(text, @"/MediaBox\s*\[\s*([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s*\]");
                if (!mbMatch.Success) return null;

                double x1 = double.Parse(mbMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                double y1 = double.Parse(mbMatch.Groups[2].Value, CultureInfo.InvariantCulture);
                double x2 = double.Parse(mbMatch.Groups[3].Value, CultureInfo.InvariantCulture);
                double y2 = double.Parse(mbMatch.Groups[4].Value, CultureInfo.InvariantCulture);

                double widthPts = Math.Abs(x2 - x1);
                double heightPts = Math.Abs(y2 - y1);

                var rotMatch = Regex.Match(text, @"/Rotate\s+(\d+)");
                int rotation = rotMatch.Success ? int.Parse(rotMatch.Groups[1].Value) : 0;
                if (rotation == 90 || rotation == 270)
                    (widthPts, heightPts) = (heightPts, widthPts);

                return new PageInfo
                {
                    WidthPts = Math.Round(widthPts, 1),
                    HeightPts = Math.Round(heightPts, 1),
                    WidthMm = Math.Round(widthPts * 0.3528, 1),
                    HeightMm = Math.Round(heightPts * 0.3528, 1),
                    IsPortrait = heightPts >= widthPts
                };
            }
            catch { return null; }
        }

        private PageInfo GetPageInfoViaBbox(string pdfPath)
        {
            try
            {
                string gsPath = GetGhostscriptPath();
                var args = $"-dBATCH -dNOPAUSE -dNOSAFER -dFirstPage=1 -dLastPage=1 -sDEVICE=bbox \"{pdfPath}\"";

                var psi = new ProcessStartInfo
                {
                    FileName = gsPath,
                    Arguments = args,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var p = Process.Start(psi);
                if (p == null) return new PageInfo();

                string stderr = p.StandardError.ReadToEnd();
                p.WaitForExit();

                // bbox escribe: %%HiResBoundingBox: x1 y1 x2 y2
                var match = Regex.Match(stderr, @"%%HiResBoundingBox:\s+([\d.]+)\s+([\d.]+)\s+([\d.]+)\s+([\d.]+)");
                if (match.Success)
                {
                    double x1 = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                    double y1 = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                    double x2 = double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
                    double y2 = double.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture);

                    double widthPts = x2 - x1;
                    double heightPts = y2 - y1;

                    return new PageInfo
                    {
                        WidthPts = Math.Round(widthPts, 1),
                        HeightPts = Math.Round(heightPts, 1),
                        WidthMm = Math.Round(widthPts * 0.3528, 1),
                        HeightMm = Math.Round(heightPts * 0.3528, 1),
                        IsPortrait = heightPts >= widthPts
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error detectando orientación via bbox: " + ex.Message);
            }

            return new PageInfo();
        }

        public void PrintDocument(string? pdfPath, string? printerName, ColorOption? colorOption, PageInfo? pageInfo)
        {
            if (!File.Exists(pdfPath)) throw new FileNotFoundException("PDF no encontrado");

            string gsPath = GetGhostscriptPath();
            bool applyColor = colorOption != null && !colorOption.IsNone;

            string pdfToPrint = pdfPath!;
            string? tempPdf = null;
            string? psFile = null;

            try
            {
                // Paso 1: Si hay conversión de color, generar PDF intermedio
                if (applyColor)
                {
                    tempPdf = Path.Combine(Path.GetTempPath(), $"color_{Guid.NewGuid():N}.pdf");
                    psFile = Path.Combine(Path.GetTempPath(), $"colorcvt_{Guid.NewGuid():N}.ps");

                    File.WriteAllText(psFile, colorOption!.ToPostScript());

                    string pass1Args = $"-dQUIET -dBATCH -dNOPAUSE -dNOSAFER -dNEWPDF=false " +
                                       $"-dAutoRotatePages=/None " +
                                       $"-sDEVICE=pdfwrite -sOutputFile=\"{tempPdf}\" " +
                                       $"-f \"{psFile}\" \"{pdfPath}\"";
                    RunGs(gsPath, pass1Args);

                    pdfToPrint = tempPdf;
                }

                // Paso 2: Renderizar PDF a imagen PNG a 300 DPI
                string tempPng = Path.Combine(Path.GetTempPath(), $"print_{Guid.NewGuid():N}.png");
                try
                {
                    string renderArgs = $"-dQUIET -dBATCH -dNOPAUSE -dNOSAFER " +
                                        $"-sDEVICE=png16m -r300 " +
                                        $"-dTextAlphaBits=4 -dGraphicsAlphaBits=4 " +
                                        $"-dFirstPage=1 -dLastPage=1 " +
                                        $"-sOutputFile=\"{tempPng}\" \"{pdfToPrint}\"";
                    RunGs(gsPath, renderArgs);

                    if (!File.Exists(tempPng))
                        throw new Exception("No se pudo renderizar el PDF a imagen.");

                    // Paso 3: Imprimir la imagen con WPF System.Printing
                    PrintImageViaWpf(tempPng, printerName!, pageInfo);
                }
                finally
                {
                    try { if (File.Exists(tempPng)) File.Delete(tempPng); } catch { }
                }
            }
            finally
            {
                try { if (psFile != null && File.Exists(psFile)) File.Delete(psFile); } catch { }
                try { if (tempPdf != null && File.Exists(tempPdf)) File.Delete(tempPdf); } catch { }
            }
        }

        /// <summary>
        /// Imprime una imagen PNG usando WPF System.Printing.
        /// Respeta la orientación y tamaño original del documento.
        /// </summary>
        private void PrintImageViaWpf(string imagePath, string printerName, PageInfo? pageInfo)
        {
            // Cargar la imagen
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(imagePath);
            bitmap.EndInit();
            bitmap.Freeze();

            // Configurar PrintServer y PrintQueue
            using var printServer = new System.Printing.LocalPrintServer();
            System.Printing.PrintQueue? printQueue = null;

            foreach (System.Printing.PrintQueue pq in printServer.GetPrintQueues())
            {
                if (pq.Name.Equals(printerName, StringComparison.OrdinalIgnoreCase))
                {
                    printQueue = pq;
                    break;
                }
            }

            if (printQueue == null)
                throw new Exception($"Impresora '{printerName}' no encontrada.");

            // Configurar PrintTicket con orientación correcta
            var ticket = printQueue.DefaultPrintTicket;
            bool isLandscape = pageInfo != null && !pageInfo.IsPortrait;
            ticket.PageOrientation = isLandscape
                ? System.Printing.PageOrientation.Landscape
                : System.Printing.PageOrientation.Portrait;

            // Calcular el tamaño real de la imagen en unidades WPF (96 DPI)
            // La imagen fue renderizada a 300 DPI, cada pixel = 1/300 pulgada
            // WPF usa 96 DPI: tamaño WPF = (pixels / imageDPI) * 96
            double imageDpiX = bitmap.DpiX > 0 ? bitmap.DpiX : 300;
            double imageDpiY = bitmap.DpiY > 0 ? bitmap.DpiY : 300;
            double imageWidthWpf = bitmap.PixelWidth / imageDpiX * 96.0;
            double imageHeightWpf = bitmap.PixelHeight / imageDpiY * 96.0;

            // Crear el visual para imprimir (imagen a tamaño original, sin escalado)
            var visual = new System.Windows.Media.DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawImage(bitmap, new System.Windows.Rect(0, 0, imageWidthWpf, imageHeightWpf));
            }

            // Crear XpsDocumentWriter e imprimir
            var writer = System.Printing.PrintQueue.CreateXpsDocumentWriter(printQueue);
            writer.Write(visual, ticket);
        }

        /// <summary>
        /// Convierte un archivo PostScript (.ps/.prn) a PDF.
        /// Usado por el SpoolWatcher para procesar archivos de la impresora virtual.
        /// </summary>
        public void ConvertPsToPdf(string psPath, string pdfOutputPath)
        {
            string gsPath = GetGhostscriptPath();
            string args = $"-dQUIET -dBATCH -dNOPAUSE -dNOSAFER " +
                          $"-dAutoRotatePages=/None " +
                          $"-sDEVICE=pdfwrite -sOutputFile=\"{pdfOutputPath}\" " +
                          $"\"{psPath}\"";
            RunGs(gsPath, args);
        }

        private void RunGs(string exe, string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (var p = Process.Start(psi))
            {
                if (p == null)
                {
                    throw new Exception("No se pudo iniciar el proceso de Ghostscript (Process.Start devolvió null).");
                }
                
                string output = p.StandardOutput.ReadToEnd();
                string err = p.StandardError.ReadToEnd();
                p.WaitForExit();
                
                if (p.ExitCode != 0)
                {
                   throw new Exception($"Ghostscript Error (Code {p.ExitCode}).\nArgs: {args}\nStdErr: {err}\nStdOut: {output}");
                }
            }
        }
    }
}
