using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace paint
{
    // Экспорт фигур в SVG формат
    public static class SvgExporter
    {
        public static string ExportToSvg(Canvas canvas)
        {
            var svg = new StringBuilder();

            svg.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            svg.AppendLine($"<svg width=\"{canvas.Width}\" height=\"{canvas.Height}\" xmlns=\"http://www.w3.org/2000/svg\">");

            // Белый фон
            svg.AppendLine("<rect width=\"100%\" height=\"100%\" fill=\"white\"/>");

            // Экспорт всех фигур
            foreach (var child in canvas.Children)
            {
                if (child is Shape shape && !IsResizeHandle(shape))
                {
                    svg.AppendLine(ShapeToSvg(shape));
                }
            }

            svg.AppendLine("</svg>");
            return svg.ToString();
        }

        // Конвертирует фигуру WPF в SVG
        private static string ShapeToSvg(Shape shape)
        {
            if (shape is Line line)
                return LineToSvg(line);
            else if (shape is Rectangle rect)
                return RectangleToSvg(rect);
            else if (shape is Ellipse ellipse)
                return EllipseToSvg(ellipse);
            else if (shape is Polygon polygon)
                return PolygonToSvg(polygon);

            return string.Empty;
        }

        // Конвертирует линию в SVG
        private static string LineToSvg(Line line)
        {
            var stroke = ColorToHex((line.Stroke as SolidColorBrush)?.Color ?? Colors.Black);
            return $"<line x1=\"{line.X1}\" y1=\"{line.Y1}\" x2=\"{line.X2}\" y2=\"{line.Y2}\" " +
                   $"stroke=\"{stroke}\" stroke-width=\"{line.StrokeThickness}\"/>";
        }

        // Конвертирует прямоугольник в SVG
        private static string RectangleToSvg(Rectangle rect)
        {
            var stroke = ColorToHex((rect.Stroke as SolidColorBrush)?.Color ?? Colors.Black);
            var fill = rect.Fill is SolidColorBrush fillBrush ?
                      ColorToHex(fillBrush.Color) : "none";

            var left = Canvas.GetLeft(rect);
            var top = Canvas.GetTop(rect);

            return $"<rect x=\"{left}\" y=\"{top}\" width=\"{rect.Width}\" height=\"{rect.Height}\" " +
                   $"stroke=\"{stroke}\" stroke-width=\"{rect.StrokeThickness}\" fill=\"{fill}\"/>";
        }

        // Конвертирует эллипс в SVG
        private static string EllipseToSvg(Ellipse ellipse)
        {
            var stroke = ColorToHex((ellipse.Stroke as SolidColorBrush)?.Color ?? Colors.Black);
            var fill = ellipse.Fill is SolidColorBrush fillBrush ?
                      ColorToHex(fillBrush.Color) : "none";

            var left = Canvas.GetLeft(ellipse);
            var top = Canvas.GetTop(ellipse);
            var centerX = left + ellipse.Width / 2;
            var centerY = top + ellipse.Height / 2;
            var radiusX = ellipse.Width / 2;
            var radiusY = ellipse.Height / 2;

            return $"<ellipse cx=\"{centerX}\" cy=\"{centerY}\" rx=\"{radiusX}\" ry=\"{radiusY}\" " +
                   $"stroke=\"{stroke}\" stroke-width=\"{ellipse.StrokeThickness}\" fill=\"{fill}\"/>";
        }

        // Конвертирует многоугольник в SVG
        private static string PolygonToSvg(Polygon polygon)
        {
            var stroke = ColorToHex((polygon.Stroke as SolidColorBrush)?.Color ?? Colors.Black);
            var fill = polygon.Fill is SolidColorBrush fillBrush ?
                      ColorToHex(fillBrush.Color) : "none";

            var points = new StringBuilder();
            foreach (var point in polygon.Points)
            {
                points.Append($"{point.X},{point.Y} ");
            }

            return $"<polygon points=\"{points}\" " +
                   $"stroke=\"{stroke}\" stroke-width=\"{polygon.StrokeThickness}\" fill=\"{fill}\"/>";
        }

        // Конвертирует цвет в HEX формат
        private static string ColorToHex(Color color)
        {
            return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        // Проверяет, является ли фигура маркером изменения размера
        private static bool IsResizeHandle(Shape shape)
        {
            return shape is Rectangle rect && rect.Width == 8 && rect.Height == 8;
        }
    }
}