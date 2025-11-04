using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Collections.Generic;

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

        // Для двойного клика
        private DateTime _lastClickTime = DateTime.MinValue;
        private Point _lastClickPoint;

        // Менеджеры
        private ShapeManager? _shapeManager;
        private ZoomManager? _zoomManager;
        private UndoManager _undoManager = new UndoManager();

        // Режимы работы
        private EditorMode _currentMode = EditorMode.Draw;

        public MainWindow()
        {
            InitializeComponent();

            // Обработчики отмены/повтора
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Undo, (s, e) => Undo()));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Redo, (s, e) => Redo()));
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UpdatePropertiesFromUI();
            _shapeManager = new ShapeManager(DrawCanvas);
            _zoomManager = new ZoomManager(MainScrollViewer, DrawCanvas);
            UpdateStatusBar();
            UpdateZoomDisplay();

            // Сохраняем начальное пустое состояние
            _undoManager.SaveState(DrawCanvas, "Initial Empty State");
        }

        // Обновляем свойства из UI
        private void UpdatePropertiesFromUI()
        {
            if (_currentProperties == null)
                _currentProperties = new ShapeProperties();

            if (StrokeColorBox == null || FillColorBox == null)
                return;

            _currentProperties.Stroke = GetSelectedStrokeBrush();
            _currentProperties.Fill = GetSelectedFillBrush();
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

                _currentMode = EditorMode.Draw;
                UpdateEditModeButton();
                _shapeManager?.ClearSelection();

                if (_currentShape != ShapeType.Polygon)
                {
                    ResetPolygon();
                }

                UpdateStatusBar();
            }
        }

        // Обработчики цветов
        private void StrokeColorBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePropertiesFromUI();
            UpdateSelectedShapeProperties();
        }

        private void FillColorBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePropertiesFromUI();
            UpdateSelectedShapeProperties();
        }

        // Обновление свойств выделенной фигуры
        private void UpdateSelectedShapeProperties()
        {
            if (_shapeManager?.SelectedShape != null)
            {
                ShapeFactory.ApplyProperties(_shapeManager.SelectedShape, _currentProperties);
            }
        }

        // Обработчики масштабирования
        private void ZoomInButton_Click(object sender, RoutedEventArgs e)
        {
            _zoomManager?.ZoomIn();
            UpdateZoomDisplay();
        }

        private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
        {
            _zoomManager?.ZoomOut();
            UpdateZoomDisplay();
        }

        private void ZoomResetButton_Click(object sender, RoutedEventArgs e)
        {
            _zoomManager?.ZoomReset();
            UpdateZoomDisplay();
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            _zoomManager?.HandleMouseWheel(e);
            UpdateZoomDisplay();
        }

        private void UpdateZoomDisplay()
        {
            if (_zoomManager != null)
            {
                ZoomTextBlock.Text = _zoomManager.GetZoomText();
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            // ВАЖНО: Сохраняем состояние ДО очистки
            System.Diagnostics.Debug.WriteLine("=== SAVE STATE BEFORE CLEAR ===");
            _undoManager.SaveState(DrawCanvas, "Before Clear");

            if (DrawCanvas != null)
                DrawCanvas.Children.Clear();

            ResetPolygon();
            _shapeManager = new ShapeManager(DrawCanvas);
            UpdateStatusBar();
        }

        private void Undo()
        {
            _undoManager.Undo(DrawCanvas);
            _shapeManager = new ShapeManager(DrawCanvas);
            UpdateStatusBar();
        }

        private void Redo()
        {
            _undoManager.Redo(DrawCanvas);
            _shapeManager = new ShapeManager(DrawCanvas);
            UpdateStatusBar();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            // ВАЖНО: Сохраняем состояние ДО удаления
            System.Diagnostics.Debug.WriteLine("=== SAVE STATE BEFORE DELETE ===");
            _undoManager.SaveState(DrawCanvas, "Before Delete");

            _shapeManager?.DeleteSelectedShape();
            UpdateStatusBar();
        }

        private void EditModeButton_Click(object sender, RoutedEventArgs e)
        {
            _currentMode = _currentMode == EditorMode.Draw ? EditorMode.Edit : EditorMode.Draw;
            _shapeManager?.ClearSelection();
            UpdateStatusBar();
            UpdateEditModeButton();
        }

        private void UpdateEditModeButton()
        {
            if (EditModeButton != null)
            {
                if (_currentMode == EditorMode.Edit)
                {
                    EditModeButton.Content = "🎯 Рисовать";
                    EditModeButton.Background = Brushes.LightBlue;
                    EditModeButton.ToolTip = "Переключиться в режим рисования";
                }
                else
                {
                    EditModeButton.Content = "✏️ Редакт.";
                    EditModeButton.Background = Brushes.LightGreen;
                    EditModeButton.ToolTip = "Переключиться в режим редактирования";
                }
            }
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

        // Обработка мыши
        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (DrawCanvas == null || _shapeManager == null) return;

            Point currentPoint = e.GetPosition(DrawCanvas);

            if (_currentMode == EditorMode.Edit)
            {
                HandleEditModeMouseDown(currentPoint, e.ChangedButton);
            }
            else if (_currentShape == ShapeType.Polygon)
            {
                HandlePolygonMouseDown(currentPoint, e.ChangedButton);
            }
            else
            {
                HandleDrawModeMouseDown(currentPoint, e);
            }
        }

        // Режим редактирования
        private void HandleEditModeMouseDown(Point currentPoint, MouseButton button)
        {
            if (button == MouseButton.Left)
            {
                if (_shapeManager != null)
                {
                    var resizeHandle = _shapeManager.GetResizeHandleAtPoint(currentPoint);

                    if (resizeHandle.HasValue)
                    {
                        _shapeManager.StartResize(resizeHandle.Value, currentPoint);
                        UpdateStatusBar();
                        return;
                    }

                    var shape = _shapeManager.GetShapeAtPoint(currentPoint);
                    if (shape != null)
                    {
                        _shapeManager.SelectShape(shape);
                        _shapeManager.StartDrag(currentPoint);
                    }
                    else
                    {
                        // КЛИК НА ПУСТОМ МЕСТЕ - СНИМАЕМ ВЫДЕЛЕНИЕ
                        _shapeManager.ClearSelection();
                    }
                    UpdateStatusBar();
                }
            }
        }

        // Режим рисования (обычные фигуры)
        private void HandleDrawModeMouseDown(Point currentPoint, MouseButtonEventArgs e)
        {
            // Для многоугольника - отдельная обработка
            if (_currentShape == ShapeType.Polygon)
            {
                HandlePolygonMouseDown(currentPoint, e.ChangedButton);
                return;
            }

            // Для обычных фигур
            if (e.ChangedButton == MouseButton.Left)
            {
                UpdatePropertiesFromUI();
                _startPoint = currentPoint;
                _isDrawing = true;

                // ВАЖНО: Сохраняем состояние ПЕРЕД началом рисования
                System.Diagnostics.Debug.WriteLine("=== SAVE STATE BEFORE START DRAWING ===");
                _undoManager.SaveState(DrawCanvas, $"Before Start Draw {_currentShape}");

                _previewShape = CreateShape(_currentShape);
                if (_previewShape != null)
                {
                    ShapeFactory.ApplyProperties(_previewShape, _currentProperties);
                    _previewShape.StrokeDashArray = new DoubleCollection { 2, 2 };
                    _previewShape.Opacity = 0.7;
                    DrawCanvas.Children.Add(_previewShape);
                }
            }
        }

        // Многоугольник
        private void HandlePolygonMouseDown(Point currentPoint, MouseButton button)
        {
            if (button == MouseButton.Left)
            {
                // Проверка двойного клика для завершения многоугольника
                bool isDoubleClick = CheckDoubleClick(currentPoint);

                if (isDoubleClick && _polygonState == PolygonState.Drawing)
                {
                    CompletePolygon();
                    return;
                }

                if (_polygonState == PolygonState.NotStarted)
                {
                    // ВАЖНО: Сохраняем состояние ПЕРЕД началом рисования многоугольника
                    System.Diagnostics.Debug.WriteLine("=== SAVE STATE BEFORE START POLYGON ===");
                    _undoManager.SaveState(DrawCanvas, "Before Start Polygon");

                    _polygonPoints.Clear();
                    _polygonPoints.Add(currentPoint);
                    _polygonState = PolygonState.Drawing;

                    _polygonPreview = new Polyline();
                    ShapeFactory.ApplyProperties(_polygonPreview, _currentProperties);
                    _polygonPreview.StrokeDashArray = new DoubleCollection { 2, 2 };
                    _polygonPreview.Points = new PointCollection(_polygonPoints);
                    DrawCanvas.Children.Add(_polygonPreview);
                }
                else if (_polygonState == PolygonState.Drawing)
                {
                    // Добавляем новую точку к многоугольнику
                    _polygonPoints.Add(currentPoint);

                    if (_polygonPreview != null)
                    {
                        _polygonPreview.Points = new PointCollection(_polygonPoints);
                    }
                }

                // Обновляем время последнего клика
                _lastClickTime = DateTime.Now;
                _lastClickPoint = currentPoint;
            }
            else if (button == MouseButton.Right && _polygonState == PolygonState.Drawing)
            {
                // Правый клик - отмена рисования многоугольника
                ResetPolygon();
                UpdateStatusBar();
            }
        }

        // Проверка двойного клика
        private bool CheckDoubleClick(Point currentPoint)
        {
            TimeSpan timeSinceLastClick = DateTime.Now - _lastClickTime;
            double distance = Math.Sqrt(Math.Pow(currentPoint.X - _lastClickPoint.X, 2) +
                                       Math.Pow(currentPoint.Y - _lastClickPoint.Y, 2));

            // Считаем двойным кликом, если время между кликами < 300ms и расстояние < 10px
            return timeSinceLastClick.TotalMilliseconds < 300 && distance < 10;
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (DrawCanvas == null || _shapeManager == null) return;

            Point current = e.GetPosition(DrawCanvas);
            CoordinatesText.Text = $"X: {(int)current.X}, Y: {(int)current.Y}";

            if (_currentMode == EditorMode.Edit)
            {
                if (_shapeManager.IsDragging)
                {
                    _shapeManager.UpdateDrag(current);
                }
                else if (_shapeManager.IsResizing)
                {
                    _shapeManager.UpdateResize(current);
                }
            }
            else if (_currentShape == ShapeType.Polygon && _polygonState == PolygonState.Drawing)
            {
                // Обновляем предпросмотр многоугольника с текущей позицией мыши
                if (_polygonPreview != null && _polygonPoints.Count > 0)
                {
                    var previewPoints = new List<Point>(_polygonPoints) { current };
                    _polygonPreview.Points = new PointCollection(previewPoints);
                }
            }
            else if (_isDrawing && _previewShape != null)
            {
                UpdateShapeGeometry(_previewShape, _startPoint, current, _currentShape);
            }
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_shapeManager == null) return;

            if (_currentMode == EditorMode.Edit)
            {
                if (_shapeManager.IsDragging)
                {
                    _shapeManager.EndDrag();
                }
                else if (_shapeManager.IsResizing)
                {
                    _shapeManager.EndResize();
                }
            }
            else if (_currentShape != ShapeType.Polygon && _isDrawing && _previewShape != null)
            {
                _isDrawing = false;
                Point end = e.GetPosition(DrawCanvas);

                _previewShape.StrokeDashArray = null;
                _previewShape.Opacity = 1;

                UpdateShapeGeometry(_previewShape, _startPoint, end, _currentShape);

                // Добавляем фигуру в менеджер
                _shapeManager.AddShape(_previewShape);

                _previewShape = null;
                UpdateStatusBar();
            }
        }

        // Завершение многоугольника
        private void CompletePolygon()
        {
            if (_polygonPoints.Count >= 3)
            {
                // ВАЖНО: Сохраняем состояние ДО добавления фигуры
                System.Diagnostics.Debug.WriteLine("=== SAVE STATE BEFORE COMPLETING POLYGON ===");
                _undoManager.SaveState(DrawCanvas, "Before Complete Polygon");

                Polygon finalPolygon = new Polygon();
                ShapeFactory.ApplyProperties(finalPolygon, _currentProperties);
                finalPolygon.Points = new PointCollection(_polygonPoints);

                if (_polygonPreview != null)
                {
                    DrawCanvas.Children.Remove(_polygonPreview);
                    _polygonPreview = null;
                }

                DrawCanvas.Children.Add(finalPolygon);
                _shapeManager?.AddShape(finalPolygon);
                UpdateStatusBar();
            }
            else
            {
                // Если точек недостаточно, просто отменяем рисование
                MessageBox.Show("Для многоугольника нужно как минимум 3 точки", "Недостаточно точек",
                               MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            ResetPolygon();
        }

        // Создание фигуры
        private Shape? CreateShape(ShapeType tool)
        {
            return ShapeFactory.CreateShape(tool);
        }

        // Обновление геометрии фигуры
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

        // Получение кистей
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

        // Обновление статусной строки
        private void UpdateStatusBar()
        {
            string modeText = _currentMode == EditorMode.Draw ? "Режим рисования" : "Режим редактирования";
            string shapeText = _currentMode == EditorMode.Draw ? $" | Фигура: {_currentShape}" : "";
            string selectionText = _shapeManager?.SelectedShape != null ? " | Фигура выделена" : string.Empty;
            string zoomText = _zoomManager != null ? $" | Масштаб: {_zoomManager.GetZoomText()}" : string.Empty;
            string polygonText = _polygonState == PolygonState.Drawing ? $" | Точки: {_polygonPoints.Count}" : "";

            if (StatusText != null)
            {
                StatusText.Text = $"{modeText}{shapeText}{selectionText}{zoomText}{polygonText}";
            }
        }
    }

    
}