using System.Windows.Shapes;
using System.Windows.Media;

namespace paint
{
    // Фабрика для создания фигур
    public static class ShapeFactory
    {
        public static Shape CreateShape(ShapeType type)
        {
            return type switch
            {
                ShapeType.Line => new Line(),
                ShapeType.Rectangle => new Rectangle(),
                ShapeType.Square => new Rectangle(),
                ShapeType.Ellipse => new Ellipse(),
                ShapeType.Circle => new Ellipse(),
                ShapeType.Polygon => new Polygon(),
                _ => new Rectangle()
            };
        }

        public static void ApplyProperties(Shape shape, ShapeProperties properties)
        {
            if (shape == null || properties == null)
                return;

            shape.Stroke = properties.Stroke;
            shape.StrokeThickness = properties.StrokeThickness;

            if (properties.HasFill && !(shape is Line))
            {
                shape.Fill = properties.Fill;
            }
            else
            {
                shape.Fill = Brushes.Transparent;
            }
        }
    }
}