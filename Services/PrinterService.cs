using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace PrintManager.Services
{
    public class PrinterService
    {
        public List<string> GetInstalledPrinters()
        {
            var printers = new List<string>();
            foreach (string printer in PrinterSettings.InstalledPrinters)
            {
                printers.Add(printer);
            }
            return printers;
        }

        public string GetDefaultPrinter()
        {
            PrinterSettings settings = new PrinterSettings();
            return settings.PrinterName;
        }

        // Método simple para enviar archivo RAW a la impresora
        // Nota: Para PDF, generalmente se necesita un intermediario como Ghostscript o Acrobat si la impresora no soporta impresión directa de PDF.
        // Sin embargo, si usamos Ghostscript para convertir, usaremos GS para imprimir también.
        // Este método queda como utilidad general.
        public void PrintRawFile(string printerName, string filePath, string jobName)
        {
             // Implementación básica usando RawPrint o similar. 
             // Por simplificación en esta fase, usaremos el wrapper de Ghostscript en el GhostscriptService para imprimir.
             // Aquí solo listamos impresoras por ahora.
        }
    }
}
