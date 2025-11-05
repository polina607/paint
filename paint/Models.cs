using System.Windows.Media;
using System.Collections.Generic;

namespace paint
{
    // Свойства для отображения фигур
    public class ShapeProperties
    {
        public Brush Stroke { get; set; } = Brushes.Black;
        public Brush Fill { get; set; } = Brushes.Transparent;
        public double StrokeThickness { get; set; } = 2;
        public bool HasFill { get; set; } = false;
    }

    // Проект для сохранения/загрузки
    public class PaintProject
    {
        public List<ShapeData> Shapes { get; set; } = new List<ShapeData>();
        public double CanvasWidth { get; set; } = 2000;
        public double CanvasHeight { get; set; } = 2000;
    }

    // Данные фигуры для сериализации
    public class ShapeData
    {
        public ShapeType Type { get; set; }
        public string StrokeColor { get; set; } = "#FF000000";
        public string FillColor { get; set; } = "#00FFFFFF";
        public double StrokeThickness { get; set; } = 2;
        public bool HasFill { get; set; }

        // Для Line
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }

        // Для Rectangle, Ellipse, Square, Circle
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

        // Для Polygon
        public List<PointData> Points { get; set; } = new List<PointData>();
    }

    // Данные точки для сериализации
    public class PointData
    {
        public double X { get; set; }
        public double Y { get; set; }

        public PointData() { }
        public PointData(double x, double y) { X = x; Y = y; }
    }
}