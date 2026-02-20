using System.Windows;

namespace PrintManager
{
    public partial class App : System.Windows.Application
    {
        public static string? InputFilePath { get; set; }

        /// <summary>Indica que la app debe iniciar el monitoreo de la carpeta spool.</summary>
        public static bool WatchSpool { get; set; } = true;

        private void Application_Startup(object sender, System.Windows.StartupEventArgs e)
        {
            // Procesar argumentos de línea de comandos
            foreach (var arg in e.Args)
            {
                if (arg.Equals("--no-spool", System.StringComparison.OrdinalIgnoreCase))
                {
                    WatchSpool = false;
                }
                else if (!arg.StartsWith("-"))
                {
                    // Es una ruta de archivo
                    InputFilePath = arg;
                }
            }

            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }
}
