namespace PrintManager.Models
{
    /// <summary>
    /// Información de dimensiones y orientación de una página PDF.
    /// </summary>
    public class PageInfo
    {
        public double WidthMm { get; set; }
        public double HeightMm { get; set; }
        public double WidthPts { get; set; }
        public double HeightPts { get; set; }
        public bool IsPortrait { get; set; } = true;
    }
}
