using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Collections.Generic;
using System.Windows.Threading;

namespace paint
{
    public partial class MainWindow : Window
    {
        private ShapeType _currentShape = ShapeType.Line;
        private Point _startPoint;
        private Shape? _previewShape;
        private bool _isDrawing = false;
        private ShapeProperties _currentProperties = new ShapeProperties();

        // Для многоугольника
        private PolygonState _polygonState = PolygonState.NotStarted;
        private List<Point> _polygonPoints = new List<Point>();
        private Polyline? _polygonPreview;

        // Для определения двойного клика
        private DateTime _lastClickTime = DateTime.MinValue;
        private Point _lastClickPoint;

        public MainWindow()
        {
            InitializeComponent();
        }

        // Обновляем свойства из UI элементов
        private void UpdatePropertiesFromUI()
        {
            if (_currentProperties == null)
                _currentProperties = new ShapeProperties();

            if (StrokeColorBox == null || FillColorBox == null)
                return;

            _currentProperties.Stroke = GetSelectedStrokeBrush();
            _currentProperties.Fill = GetSelectedFillBrush();

            // Автоматически определяем, есть ли заливка
            _currentProperties.HasFill = (FillColorBox.SelectedItem as ComboBoxItem)?.Tag as string != "Transparent";
        }

        // Обработчик выбора фигуры
        private void ShapeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ShapeBox?.SelectedItem is ComboBoxItem item && item.Tag is string shapeTag)
            {
                _currentShape = shapeTag switch
                {
                    "Line" => ShapeType.Line,
                    "Rectangle" => ShapeType.Rectangle,
                    "Square" => ShapeType.Square,
                    "Ellipse" => ShapeType.Ellipse,
                    "Circle" => ShapeType.Circle,
                    "Polygon" => ShapeType.Polygon,
                    _ => ShapeType.Line
                };

                // Сбрасываем состояние многоугольника при переключении инструмента
                if (_currentShape != ShapeType.Polygon)
                {
                    ResetPolygon();
                }
            }
        }

        // Обработчик выбора цвета контура
        private void StrokeColorBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePropertiesFromUI();
        }

        // Обработчик выбора цвета заливки
        private void FillColorBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePropertiesFromUI();
        }

        // Сброс состояния многоугольника
        private void ResetPolygon()
        {
            _polygonState = PolygonState.NotStarted;
            _polygonPoints.Clear();
            if (_polygonPreview != null)
            {
                DrawCanvas.Children.Remove(_polygonPreview);
                _polygonPreview = null;
            }
        }

        // Очистка холста
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            if (DrawCanvas != null)
                DrawCanvas.Children.Clear();
            ResetPolygon();
        }

        // Начало рисования 
        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (DrawCanvas == null) return;

            Point currentPoint = e.GetPosition(DrawCanvas);

            // Проверяем двойной клик для многоугольника
            if (_currentShape == ShapeType.Polygon && e.ChangedButton == MouseButton.Left)
            {
                TimeSpan timeSinceLastClick = DateTime.Now - _lastClickTime;
                double distance = Point.Subtract(_lastClickPoint, currentPoint).Length;

                // Если время между кликами мало и точки близко - это двойной клик
                if (timeSinceLastClick.TotalMilliseconds < 500 && distance < 10)
                {
                    if (_polygonState == PolygonState.Drawing)
                    {
                        CompletePolygon();
                        e.Handled = true;
                        return;
                    }
                }

                _lastClickTime = DateTime.Now;
                _lastClickPoint = currentPoint;
            }

            if (_currentShape == ShapeType.Polygon)
            {
                HandlePolygonMouseDown(currentPoint, e.ChangedButton);
            }
            else
            {
                HandleRegularShapeMouseDown(currentPoint);
            }
        }

        // Обработка мыши для обычных фигур
        private void HandleRegularShapeMouseDown(Point currentPoint)
        {
            UpdatePropertiesFromUI();

            _startPoint = currentPoint;
            _isDrawing = true;

            _previewShape = CreateShape(_currentShape);
            if (_previewShape != null)
            {
                ShapeFactory.ApplyProperties(_previewShape, _currentProperties);
                _previewShape.StrokeDashArray = new DoubleCollection { 2, 2 };
                _previewShape.Opacity = 0.7;
                DrawCanvas.Children.Add(_previewShape);
            }
        }

        // Обработка мыши для многоугольника
        private void HandlePolygonMouseDown(Point currentPoint, MouseButton button)
        {
            if (button == MouseButton.Left)
            {
                // Левый клик - добавление точки
                if (_polygonState == PolygonState.NotStarted)
                {
                    // Начало нового многоугольника
                    _polygonPoints.Clear();
                    _polygonPoints.Add(currentPoint);
                    _polygonState = PolygonState.Drawing;

                    // Создаем превью
                    _polygonPreview = new Polyline();
                    ShapeFactory.ApplyProperties(_polygonPreview, _currentProperties);
                    _polygonPreview.StrokeDashArray = new DoubleCollection { 2, 2 };
                    _polygonPreview.Points = new PointCollection(_polygonPoints);
                    DrawCanvas.Children.Add(_polygonPreview);
                }
                else if (_polygonState == PolygonState.Drawing)
                {
                    // Добавляем новую точку
                    _polygonPoints.Add(currentPoint);
                    if (_polygonPreview != null)
                    {
                        _polygonPreview.Points = new PointCollection(_polygonPoints);
                    }
                }
            }
            else if (button == MouseButton.Right && _polygonState == PolygonState.Drawing)
            {
                // Правый клик - завершение многоугольника
                CompletePolygon();
            }
        }

        // Завершение многоугольника
        private void CompletePolygon()
        {
            if (_polygonPoints.Count >= 3)
            {
                // Создаем финальный многоугольник
                Polygon finalPolygon = new Polygon();
                ShapeFactory.ApplyProperties(finalPolygon, _currentProperties);
                finalPolygon.Points = new PointCollection(_polygonPoints);

                // Убираем превью и добавляем финальную фигуру
                if (_polygonPreview != null)
                {
                    DrawCanvas.Children.Remove(_polygonPreview);
                }
                DrawCanvas.Children.Add(finalPolygon);
            }

            ResetPolygon();
        }

        // Перетаскивание мыши
        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (DrawCanvas == null) return;

            Point current = e.GetPosition(DrawCanvas);

            if (_currentShape == ShapeType.Polygon && _polygonState == PolygonState.Drawing)
            {
                // Для многоугольника - обновляем превью с текущей позицией мыши
                if (_polygonPreview != null && _polygonPoints.Count > 0)
                {
                    var previewPoints = new List<Point>(_polygonPoints) { current };
                    _polygonPreview.Points = new PointCollection(previewPoints);
                }
            }
            else if (_isDrawing && _previewShape != null)
            {
                // Для обычных фигур
                UpdateShapeGeometry(_previewShape, _startPoint, current, _currentShape);
            }
        }

        // Отпустили мышь
        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_currentShape != ShapeType.Polygon && _isDrawing && _previewShape != null)
            {
                _isDrawing = false;
                Point end = e.GetPosition(DrawCanvas);

                _previewShape.StrokeDashArray = null;
                _previewShape.Opacity = 1;

                UpdateShapeGeometry(_previewShape, _startPoint, end, _currentShape);
                _previewShape = null;
            }
        }

        // Создаём фигуру по типу
        private Shape? CreateShape(ShapeType tool)
        {
            return ShapeFactory.CreateShape(tool);
        }

        // Обновляем размеры и позицию фигуры
        private void UpdateShapeGeometry(Shape shape, Point start, Point end, ShapeType shapeType)
        {
            if (shape == null) return;

            double x = Math.Min(start.X, end.X);
            double y = Math.Min(start.Y, end.Y);
            double w = Math.Abs(end.X - start.X);
            double h = Math.Abs(end.Y - start.Y);

            switch (shapeType)
            {
                case ShapeType.Line:
                    if (shape is Line line)
                    {
                        line.X1 = start.X;
                        line.Y1 = start.Y;
                        line.X2 = end.X;
                        line.Y2 = end.Y;
                    }
                    break;

                case ShapeType.Rectangle:
                case ShapeType.Ellipse:
                    Canvas.SetLeft(shape, x);
                    Canvas.SetTop(shape, y);
                    shape.Width = w;
                    shape.Height = h;
                    break;

                case ShapeType.Square:
                case ShapeType.Circle:
                    double side = Math.Max(w, h);
                    Canvas.SetLeft(shape, start.X < end.X ? start.X : start.X - side);
                    Canvas.SetTop(shape, start.Y < end.Y ? start.Y : start.Y - side);
                    shape.Width = side;
                    shape.Height = side;
                    break;
            }
        }

        // Получаем кисть для контура
        private Brush GetSelectedStrokeBrush()
        {
            if (StrokeColorBox?.SelectedItem is not ComboBoxItem item)
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

        // Получаем кисть для заливки
        private Brush GetSelectedFillBrush()
        {
            if (FillColorBox?.SelectedItem is not ComboBoxItem item)
                return Brushes.Transparent;

            string? colorName = item.Tag as string;

            return colorName switch
            {
                "Red" => Brushes.Red,
                "Green" => Brushes.Green,
                "Blue" => Brushes.Blue,
                "Yellow" => Brushes.Yellow,
                "Purple" => Brushes.Purple,
                "Black" => Brushes.Black,
                _ => Brushes.Transparent
            };
        }

        // Обработчик загрузки окна
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UpdatePropertiesFromUI();
        }
    }
}