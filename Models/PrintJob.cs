using System.ComponentModel;

namespace PrintManager.Models
{
    /// <summary>
    /// Representa un documento en la cola de impresión.
    /// </summary>
    public class PrintJob : INotifyPropertyChanged
    {
        private string _status = "Pendiente";
        private int _copies = 1;

        public string Id { get; set; } = System.Guid.NewGuid().ToString("N")[..8];
        public string FileName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public int PageCount { get; set; }
        public PageInfo? PageInfo { get; set; }
        public System.DateTime ReceivedAt { get; set; } = System.DateTime.Now;

        public int Copies
        {
            get => _copies;
            set { _copies = value < 1 ? 1 : value; OnPropertyChanged(nameof(Copies)); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(nameof(Status)); }
        }

        /// <summary>Texto para mostrar en la lista</summary>
        public string DisplayText => $"{FileName}";
        public string DetailText => PageCount > 0
            ? $"{PageCount} pág. | {PageInfo?.WidthMm}×{PageInfo?.HeightMm} mm | {(PageInfo?.IsPortrait == true ? "Vertical" : "Horizontal")}"
            : "Procesando...";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
