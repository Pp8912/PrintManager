using System.Collections.Generic;
using System.Windows.Media;

namespace PrintManager.Models
{
    public class ColorOption
    {
        public string Name { get; set; } = "";
        public double R { get; set; }
        public double G { get; set; }
        public double B { get; set; }
        public bool IsNone { get; set; }

        /// <summary>Brush para mostrar la muestra de color en el ComboBox</summary>
        public System.Windows.Media.Brush DisplayBrush => IsNone
            ? System.Windows.Media.Brushes.Transparent
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(
                (byte)(R * 255), (byte)(G * 255), (byte)(B * 255)));

        /// <summary>Genera el PostScript para setcolortransfer con estos valores RGB</summary>
        public string ToPostScript()
        {
            // Fórmula por canal: output = T + input × (1-T)
            // Así: negro (input=0) → T (color destino), blanco (input=1) → 1 (se mantiene)
            // En PostScript: { complement mul target add }
            // Casos especiales: T=0 → { } (identidad), T=1 → { pop 1 }
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            string rProc = BuildChannelProc(R, ci);
            string gProc = BuildChannelProc(G, ci);
            string bProc = BuildChannelProc(B, ci);
            return $"{rProc} {gProc} {bProc} {{ }} setcolortransfer";
        }

        private static string BuildChannelProc(double target, System.Globalization.CultureInfo ci)
        {
            // T=0: identidad (negro queda 0, blanco queda 1)
            if (target <= 0.001) return "{ }";
            // T=1: forzar a 1 (negro y blanco → 1)
            if (target >= 0.999) return "{ pop 1 }";
            // Valor intermedio: output = (1-T)*input + T
            string complement = (1.0 - target).ToString("F3", ci);
            string t = target.ToString("F3", ci);
            return $"{{ {complement} mul {t} add }}";
        }

        public static List<ColorOption> GetPresets()
        {
            return new List<ColorOption>
            {
                new() { Name = "Sin conversión", IsNone = true },
                new() { Name = "Cian",     R = 0.0, G = 1.0, B = 1.0 },
                new() { Name = "Magenta",  R = 1.0, G = 0.0, B = 1.0 },
                new() { Name = "Rojo",     R = 1.0, G = 0.0, B = 0.0 },
                new() { Name = "Azul",     R = 0.0, G = 0.0, B = 1.0 },
                new() { Name = "Verde",    R = 0.0, G = 0.5, B = 0.0 },
                new() { Name = "Naranja",  R = 1.0, G = 0.5, B = 0.0 },
            };
        }

        public override string ToString() => Name;
    }
}
