using System.Windows.Media;

namespace paint
{
    // Конвертер цветов между разными форматами
    public static class ColorConverter
    {
        // Конвертирует цвет в HEX строку
        public static string ColorToHex(Color color)
        {
            return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        // Конвертирует HEX строку в цвет
        public static Color HexToColor(string hex)
        {
            hex = hex.Replace("#", "");

            byte a = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            byte r = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);

            return Color.FromArgb(a, r, g, b);
        }
    }
}