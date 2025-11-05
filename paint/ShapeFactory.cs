using System.Windows.Shapes;
using System.Windows.Media;

namespace paint
{
    // Фабрика для создания и настройки фигур
    public static class ShapeFactory
    {
        // Создает фигуру указанного типа
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

        // Применяет свойства к фигуре
        public static void ApplyProperties(Shape shape, ShapeProperties properties)
        {
            if (shape == null || properties == null)
                return;

            shape.Stroke = properties.Stroke;
            shape.StrokeThickness = properties.StrokeThickness;

            // Линии не имеют заливки
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