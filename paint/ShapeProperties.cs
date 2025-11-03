using System.Windows.Media;

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
}