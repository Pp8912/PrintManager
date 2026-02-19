using System.Windows;

namespace PrintManager
{
    public partial class App : Application
    {
        public static string? InputFilePath { get; set; }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            if (e.Args.Length > 0)
            {
                InputFilePath = e.Args[0];
            }
            
            // Para pruebas: si no hay argumento, usar un dummy si existe, o null.
            // InputFilePath = @"C:\Temp\test.pdf"; 

            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }
}
