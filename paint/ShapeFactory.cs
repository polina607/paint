using System.Windows.Shapes;
using System.Windows.Media;

namespace paint
{
    /// Фабрика для создания фигур с применением свойств

    public static class ShapeFactory
    {

        // Создает фигуру указанного типа

        /// <param name="type">Тип фигуры</param>
        
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

        /// Применяет свойства к фигуре

        /// <param name="shape">Фигура</param>
        /// <param name="properties">Свойства</param>
        public static void ApplyProperties(Shape shape, ShapeProperties properties)
        {
            if (shape == null || properties == null)
                return;

            shape.Stroke = properties.Stroke;
            shape.StrokeThickness = properties.StrokeThickness;

            // Для линий заливка не применяется
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