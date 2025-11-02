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
            UpdateCurrentShapeButton();
        }

        // Обновляем текст кнопки текущей фигуры
        private void UpdateCurrentShapeButton()
        {
            CurrentShapeButton.Content = _currentTool switch
            {
                Tool.Line => "Линия",
                Tool.Rectangle => "Прямоугольник",
                Tool.Square => "Квадрат",
                Tool.Ellipse => "Эллипс",
                Tool.Circle => "Окружность",
                _ => "Фигура"
            };
        }

        // Обработчик для всех пунктов меню фигур
        private void ShapeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is string toolTag)
            {
                _currentTool = toolTag switch
                {
                    "Line" => Tool.Line,
                    "Rectangle" => Tool.Rectangle,
                    "Square" => Tool.Square,
                    "Ellipse" => Tool.Ellipse,
                    "Circle" => Tool.Circle,
                    _ => Tool.Line
                };
                UpdateCurrentShapeButton();
            }
        }

        // Клик по кнопке текущей фигуры - открывает меню
        private void CurrentShapeButton_Click(object sender, RoutedEventArgs e)
        {
            // Находим меню и открываем его
            var menu = FindVisualParent<Menu>(CurrentShapeButton);
            if (menu != null && menu.Items.Count > 0)
            {
                if (menu.Items[0] is MenuItem menuItem)
                {
                    menuItem.IsSubmenuOpen = true;
                }
            }
        }

        // Очистка холста
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            DrawCanvas.Children.Clear();
        }

        // Вспомогательный метод для поиска родительского элемента
        private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent)
                    return parent;

                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        // ---------- начало рисования ----------
        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(DrawCanvas);
            _isDrawing = true;

            _previewShape = CreateShape(_currentTool);
            if (_previewShape != null)
            {
                _previewShape.Stroke = GetSelectedBrush();
                _previewShape.StrokeThickness = 2;
                _previewShape.StrokeDashArray = new DoubleCollection { 2, 2 };

                if (FillCheck.IsChecked == true && _currentTool != Tool.Line)
                {
                    _previewShape.Fill = GetSelectedBrush();
                    _previewShape.Opacity = 0.7;
                }

                DrawCanvas.Children.Add(_previewShape);
            }
        }

        // ---------- перетаскивание мыши ----------
        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDrawing || _previewShape == null) return;

            Point current = e.GetPosition(DrawCanvas);
            UpdateShapeGeometry(_previewShape, _startPoint, current, _currentTool);
        }

        // ---------- отпустили мышь ----------
        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDrawing || _previewShape == null) return;

            _isDrawing = false;
            Point end = e.GetPosition(DrawCanvas);

            _previewShape.StrokeDashArray = null;

            if (FillCheck.IsChecked == true && _currentTool != Tool.Line)
            {
                _previewShape.Fill = GetSelectedBrush();
                _previewShape.Opacity = 1;
            }

            UpdateShapeGeometry(_previewShape, _startPoint, end, _currentTool);
            _previewShape = null;
        }

        // ---------- создаём фигуру по типу ----------
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

        // ---------- обновляем размеры и позицию фигуры ----------
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

        // ---------- получаем кисть из ComboBox ----------
        private Brush GetSelectedBrush()
        {
            if (ColorBox.SelectedItem is not ComboBoxItem item)
                return Brushes.Black;

            string? colorName = item.Tag as string;

            return colorName switch
            {
                "Red" => Brushes.Red,
                "Green" => Brushes.Green,
                "Blue" => Brushes.Blue,
                "Yellow" => Brushes.Yellow,
                "Purple" => Brushes.Purple,
                _ => Brushes.Black
            };
        }
    }
}