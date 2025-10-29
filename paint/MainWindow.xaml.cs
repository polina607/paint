using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace paint
{
    public partial class MainWindow : Window
    {
        private enum Tool
        {
            Line,
            Rectangle,
            Square,
            Ellipse,
            Circle
        }

        private Tool _currentTool = Tool.Line;
        private Point _startPoint;
        private Shape? _previewShape;
        private bool _isDrawing = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        // ====== Выбор инструмента ======
        private void BtnLine_Click(object sender, RoutedEventArgs e) => _currentTool = Tool.Line;
        private void BtnRectangle_Click(object sender, RoutedEventArgs e) => _currentTool = Tool.Rectangle;
        private void BtnSquare_Click(object sender, RoutedEventArgs e) => _currentTool = Tool.Square;
        private void BtnEllipse_Click(object sender, RoutedEventArgs e) => _currentTool = Tool.Ellipse;
        private void BtnCircle_Click(object sender, RoutedEventArgs e) => _currentTool = Tool.Circle;

        // ====== Рисование ======
        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(DrawCanvas);
            _isDrawing = true;

            _previewShape = CreateShape(_currentTool);
            if (_previewShape != null)
            {
                _previewShape.Stroke = Brushes.Black;
                _previewShape.StrokeThickness = 2;
                _previewShape.StrokeDashArray = new DoubleCollection { 2, 2 }; // пунктир для предпросмотра
                DrawCanvas.Children.Add(_previewShape);
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDrawing || _previewShape == null) return;

            Point current = e.GetPosition(DrawCanvas);
            UpdateShapeGeometry(_previewShape, _startPoint, current, _currentTool);
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDrawing || _previewShape == null) return;

            _isDrawing = false;
            Point end = e.GetPosition(DrawCanvas);
            _previewShape.StrokeDashArray = null; // сделать линию обычной
            UpdateShapeGeometry(_previewShape, _startPoint, end, _currentTool);
            _previewShape = null;
        }

        // ====== Создание фигуры ======
        private Shape? CreateShape(Tool tool)
        {
            return tool switch
            {
                Tool.Line => new Line(),
                Tool.Rectangle => new Rectangle(),
                Tool.Square => new Rectangle(),
                Tool.Ellipse => new Ellipse(),
                Tool.Circle => new Ellipse(),
                _ => null
            };
        }

        // ====== Обновление координат фигуры ======
        private void UpdateShapeGeometry(Shape shape, Point start, Point end, Tool tool)
        {
            double x = Math.Min(start.X, end.X);
            double y = Math.Min(start.Y, end.Y);
            double w = Math.Abs(end.X - start.X);
            double h = Math.Abs(end.Y - start.Y);

            switch (tool)
            {
                case Tool.Line:
                    if (shape is Line line)
                    {
                        line.X1 = start.X;
                        line.Y1 = start.Y;
                        line.X2 = end.X;
                        line.Y2 = end.Y;
                    }
                    break;

                case Tool.Rectangle:
                case Tool.Ellipse:
                    Canvas.SetLeft(shape, x);
                    Canvas.SetTop(shape, y);
                    shape.Width = w;
                    shape.Height = h;
                    break;

                case Tool.Square:
                case Tool.Circle:
                    double side = Math.Max(w, h);
                    Canvas.SetLeft(shape, start.X < end.X ? start.X : start.X - side);
                    Canvas.SetTop(shape, start.Y < end.Y ? start.Y : start.Y - side);
                    shape.Width = side;
                    shape.Height = side;
                    break;
            }
        }
    }
}
