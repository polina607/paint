using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;
using System.Text;
using paint.Commands;

namespace paint
{
    public partial class MainWindow : Window
    {
        // Текущее состояние редактора
        private ShapeType _currentShape = ShapeType.Line;
        private Point _startPoint;
        private Shape? _previewShape;
        private bool _isDrawing = false;
        private ShapeProperties _currentProperties = new ShapeProperties();

        // Состояние многоугольника
        private PolygonState _polygonState = PolygonState.NotStarted;
        private List<Point> _polygonPoints = new List<Point>();
        private Polyline? _polygonPreview;

        // Для двойного клика
        private DateTime _lastClickTime = DateTime.MinValue;
        private Point _lastClickPoint;

        // Менеджеры
        private ShapeManager? _shapeManager;
        private ZoomManager? _zoomManager;

        // Режим работы
        private EditorMode _currentMode = EditorMode.Draw;

        // Текущий файл проекта
        private string _currentProjectFile = string.Empty;

        public MainWindow()
        {
            InitializeComponent();
            InitializeCommands();
        }

        // Инициализация команд приложения
        private void InitializeCommands()
        {
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Undo, (s, e) => Undo()));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Redo, (s, e) => Redo()));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.New, (s, e) => NewProject()));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Open, (s, e) => OpenProject()));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Save, (s, e) => SaveProject()));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.SaveAs, (s, e) => SaveProjectAs()));
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeManagers();
            UpdateUI();
        }

        // Инициализация менеджеров
        private void InitializeManagers()
        {
            UpdatePropertiesFromUI();
            _shapeManager = new ShapeManager(DrawCanvas);
            _zoomManager = new ZoomManager(MainScrollViewer, DrawCanvas);
        }

        // Обновление интерфейса
        private void UpdateUI()
        {
            UpdateStatusBar();
            UpdateZoomDisplay();
            UpdateEditModeButton();
        }

        // Обновление свойств из элементов управления
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

        // Обработчики выбора фигур и цветов
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
                _shapeManager?.ClearSelection();

                if (_currentShape != ShapeType.Polygon)
                {
                    ResetPolygon();
                }

                UpdateUI();
            }
        }

        private void StrokeColorBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Обновляем свойства только если есть выделенная фигура
            if (_shapeManager?.SelectedShape != null)
            {
                UpdateSelectedShapeProperties();
            }
            else
            {
                UpdatePropertiesFromUI();
            }
        }

        private void FillColorBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Обновляем свойства только если есть выделенная фигура
            if (_shapeManager?.SelectedShape != null)
            {
                UpdateSelectedShapeProperties();
            }
            else
            {
                UpdatePropertiesFromUI();
            }
        }

        // Обновление свойств выделенной фигуры
        private void UpdateSelectedShapeProperties()
        {
            if (_shapeManager?.SelectedShape != null)
            {
                var oldProperties = GetShapeProperties(_shapeManager.SelectedShape);

                // Создаем новые свойства из текущих значений UI
                var newProperties = new ShapeProperties
                {
                    Stroke = GetSelectedStrokeBrush(),
                    Fill = GetSelectedFillBrush(),
                    StrokeThickness = _currentProperties.StrokeThickness,
                    HasFill = (FillColorBox.SelectedItem as ComboBoxItem)?.Tag as string != "Transparent"
                };

                ShapeFactory.ApplyProperties(_shapeManager.SelectedShape, newProperties);

                var command = new ChangePropertiesCommand(_shapeManager.SelectedShape, oldProperties, newProperties, _shapeManager);
                UndoRedoManager.Instance.Execute(command);

                // Обновляем текущие свойства
                _currentProperties = newProperties;
            }
        }

        private ShapeProperties GetShapeProperties(Shape shape)
        {
            return new ShapeProperties
            {
                Stroke = shape.Stroke,
                Fill = shape.Fill,
                StrokeThickness = shape.StrokeThickness,
                HasFill = shape.Fill != Brushes.Transparent
            };
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

        // Кнопки управления
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Очистить весь холст?", "Очистка",
                                        MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                ClearCanvas();
            }
        }

        private void ClearCanvas()
        {
            var shapes = new List<Shape>();
            foreach (var child in DrawCanvas.Children)
            {
                if (child is Shape shape && !IsResizeHandle(shape))
                {
                    shapes.Add(shape);
                }
            }

            foreach (var shape in shapes)
            {
                var command = new RemoveShapeCommand(DrawCanvas, shape, _shapeManager);
                UndoRedoManager.Instance.Execute(command);
            }

            ResetPolygon();
            UpdateStatusBar();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_shapeManager?.SelectedShape != null)
            {
                var shape = _shapeManager.SelectedShape;
                var command = new RemoveShapeCommand(DrawCanvas, shape, _shapeManager);
                UndoRedoManager.Instance.Execute(command);
                UpdateStatusBar();
            }
        }

        private void EditModeButton_Click(object sender, RoutedEventArgs e)
        {
            _currentMode = _currentMode == EditorMode.Draw ? EditorMode.Edit : EditorMode.Draw;
            _shapeManager?.ClearSelection();
            UpdateUI();
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

        // Отмена/повтор
        private void Undo()
        {
            UndoRedoManager.Instance.Undo();
            _shapeManager = new ShapeManager(DrawCanvas);
            UpdateStatusBar();
        }

        private void Redo()
        {
            UndoRedoManager.Instance.Redo();
            _shapeManager = new ShapeManager(DrawCanvas);
            UpdateStatusBar();
        }

        // Управление многоугольником
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

        private void HandleEditModeMouseDown(Point currentPoint, MouseButton button)
        {
            if (button == MouseButton.Left && _shapeManager != null)
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
                    // Обновляем UI свойствами выделенной фигуры
                    UpdateUIWithSelectedShapeProperties();
                }
                else
                {
                    _shapeManager.ClearSelection();
                    // Сбрасываем UI к настройкам по умолчанию
                    UpdatePropertiesFromUI();
                }
                UpdateStatusBar();
            }
        }

        private void HandleDrawModeMouseDown(Point currentPoint, MouseButtonEventArgs e)
        {
            if (_currentShape == ShapeType.Polygon)
            {
                HandlePolygonMouseDown(currentPoint, e.ChangedButton);
                return;
            }

            if (e.ChangedButton == MouseButton.Left)
            {
                StartDrawing(currentPoint);
            }
        }

        private void StartDrawing(Point currentPoint)
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

        private void HandlePolygonMouseDown(Point currentPoint, MouseButton button)
        {
            if (button == MouseButton.Left)
            {
                bool isDoubleClick = CheckDoubleClick(currentPoint);

                if (isDoubleClick && _polygonState == PolygonState.Drawing)
                {
                    CompletePolygon();
                    return;
                }

                if (_polygonState == PolygonState.NotStarted)
                {
                    StartNewPolygon(currentPoint);
                }
                else if (_polygonState == PolygonState.Drawing)
                {
                    AddPolygonPoint(currentPoint);
                }

                _lastClickTime = DateTime.Now;
                _lastClickPoint = currentPoint;
            }
            else if (button == MouseButton.Right && _polygonState == PolygonState.Drawing)
            {
                ResetPolygon();
                UpdateStatusBar();
            }
        }

        private void StartNewPolygon(Point currentPoint)
        {
            _polygonPoints.Clear();
            _polygonPoints.Add(currentPoint);
            _polygonState = PolygonState.Drawing;

            _polygonPreview = new Polyline();
            ShapeFactory.ApplyProperties(_polygonPreview, _currentProperties);
            _polygonPreview.StrokeDashArray = new DoubleCollection { 2, 2 };
            _polygonPreview.Points = new PointCollection(_polygonPoints);
            DrawCanvas.Children.Add(_polygonPreview);
        }

        private void AddPolygonPoint(Point currentPoint)
        {
            _polygonPoints.Add(currentPoint);

            if (_polygonPreview != null)
            {
                _polygonPreview.Points = new PointCollection(_polygonPoints);
            }
        }

        private bool CheckDoubleClick(Point currentPoint)
        {
            TimeSpan timeSinceLastClick = DateTime.Now - _lastClickTime;
            double distance = Math.Sqrt(Math.Pow(currentPoint.X - _lastClickPoint.X, 2) +
                                       Math.Pow(currentPoint.Y - _lastClickPoint.Y, 2));

            return timeSinceLastClick.TotalMilliseconds < 300 && distance < 10;
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (DrawCanvas == null || _shapeManager == null) return;

            Point current = e.GetPosition(DrawCanvas);
            UpdateCoordinates(current);

            if (_currentMode == EditorMode.Edit)
            {
                HandleEditModeMouseMove(current);
            }
            else if (_currentShape == ShapeType.Polygon && _polygonState == PolygonState.Drawing)
            {
                UpdatePolygonPreview(current);
            }
            else if (_isDrawing && _previewShape != null)
            {
                UpdateShapeGeometry(_previewShape, _startPoint, current, _currentShape);
            }
        }

        private void UpdateCoordinates(Point current)
        {
            CoordinatesText.Text = $"X: {(int)current.X}, Y: {(int)current.Y}";
        }

        private void HandleEditModeMouseMove(Point current)
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

        private void UpdatePolygonPreview(Point current)
        {
            if (_polygonPreview != null && _polygonPoints.Count > 0)
            {
                var previewPoints = new List<Point>(_polygonPoints) { current };
                _polygonPreview.Points = new PointCollection(previewPoints);
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
                FinishDrawing(e);
            }
        }

        private void FinishDrawing(MouseButtonEventArgs e)
        {
            _isDrawing = false;
            Point end = e.GetPosition(DrawCanvas);

            _previewShape.StrokeDashArray = null;
            _previewShape.Opacity = 1;

            UpdateShapeGeometry(_previewShape, _startPoint, end, _currentShape);

            var command = new AddShapeCommand(DrawCanvas, _previewShape, _shapeManager);
            UndoRedoManager.Instance.Execute(command);

            DrawCanvas.Children.Remove(_previewShape);
            _previewShape = null;
            UpdateStatusBar();
        }

        private void CompletePolygon()
        {
            if (_polygonPoints.Count >= 3)
            {
                CreateFinalPolygon();
            }
            else
            {
                MessageBox.Show("Для многоугольника нужно как минимум 3 точки", "Недостаточно точек",
                               MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            ResetPolygon();
        }

        private void CreateFinalPolygon()
        {
            Polygon finalPolygon = new Polygon();
            ShapeFactory.ApplyProperties(finalPolygon, _currentProperties);
            finalPolygon.Points = new PointCollection(_polygonPoints);

            if (_polygonPreview != null)
            {
                DrawCanvas.Children.Remove(_polygonPreview);
                _polygonPreview = null;
            }

            var command = new AddShapeCommand(DrawCanvas, finalPolygon, _shapeManager);
            UndoRedoManager.Instance.Execute(command);

            UpdateStatusBar();
        }

        // Создание и обновление фигур
        private Shape? CreateShape(ShapeType tool)
        {
            return ShapeFactory.CreateShape(tool);
        }

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

        // Получение цветов из ComboBox
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
            string undoText = $" | Отмена: {(UndoRedoManager.Instance.CanUndo ? "✓" : "✗")}";
            string redoText = $" | Повтор: {(UndoRedoManager.Instance.CanRedo ? "✓" : "✗")}";

            if (StatusText != null)
            {
                StatusText.Text = $"{modeText}{shapeText}{selectionText}{zoomText}{polygonText}{undoText}{redoText}";
            }
        }

        // Файловые операции
        private void NewProject()
        {
            var result = MessageBox.Show("Создать новый проект? Несохраненные изменения будут потеряны.",
                                        "Новый проект", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                DrawCanvas.Children.Clear();
                ResetPolygon();
                _shapeManager = new ShapeManager(DrawCanvas);
                _currentProjectFile = string.Empty;
                UpdateUI();
                UpdateWindowTitle();

                UndoRedoManager.Instance.Clear();
            }
        }

        private void OpenProject()
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "Paint Project (*.paint)|*.paint|Все файлы (*.*)|*.*",
                    Title = "Открыть проект"
                };

                if (dialog.ShowDialog() == true)
                {
                    LoadProject(dialog.FileName);
                }
            }
            catch (Exception ex)
            {
                ShowError("Ошибка при открытии файла", ex.Message);
            }
        }

        private void SaveProject()
        {
            if (string.IsNullOrEmpty(_currentProjectFile))
            {
                SaveProjectAs();
            }
            else
            {
                SaveProjectToFile(_currentProjectFile);
            }
        }

        private void SaveProjectAs()
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "Paint Project (*.paint)|*.paint|Все файлы (*.*)|*.*",
                    Title = "Сохранить проект как",
                    DefaultExt = ".paint"
                };

                if (dialog.ShowDialog() == true)
                {
                    SaveProjectToFile(dialog.FileName);
                    _currentProjectFile = dialog.FileName;
                    UpdateWindowTitle();
                }
            }
            catch (Exception ex)
            {
                ShowError("Ошибка при сохранении файла", ex.Message);
            }
        }

        private void SaveProjectToFile(string filename)
        {
            ProjectSerializer.SaveProjectToFile(filename, DrawCanvas);
            UpdateWindowTitle();
            MessageBox.Show($"Проект успешно сохранен!\n{filename}",
                          "Сохранение завершено", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void LoadProject(string filename)
        {
            try
            {
                var project = ProjectSerializer.LoadProjectFromFile(filename);

                DrawCanvas.Children.Clear();
                ResetPolygon();

                foreach (var shapeData in project.Shapes)
                {
                    var shape = ProjectSerializer.CreateShapeFromData(shapeData);
                    if (shape != null)
                    {
                        DrawCanvas.Children.Add(shape);
                    }
                }

                _currentProjectFile = filename;
                _shapeManager = new ShapeManager(DrawCanvas);
                UpdateUI();

                UndoRedoManager.Instance.Clear();

                MessageBox.Show($"Проект успешно загружен!\n{filename}",
                              "Загрузка завершена", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowError("Ошибка при загрузке проекта", ex.Message);
            }
        }

        // Экспорт и выход
        private void ExportToSvg_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "SVG файлы (*.svg)|*.svg|Все файлы (*.*)|*.*",
                    Title = "Экспорт в SVG",
                    DefaultExt = ".svg"
                };

                if (dialog.ShowDialog() == true)
                {
                    string svgContent = SvgExporter.ExportToSvg(DrawCanvas);
                    File.WriteAllText(dialog.FileName, svgContent, Encoding.UTF8);

                    MessageBox.Show($"Проект успешно экспортирован в SVG!\n{dialog.FileName}",
                                  "Экспорт завершен", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                ShowError("Ошибка при экспорте в SVG", ex.Message);
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Выйти из приложения?", "Выход",
                                        MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                Application.Current.Shutdown();
            }
        }

        // Вспомогательные методы
        private void UpdateWindowTitle()
        {
            string filename = string.IsNullOrEmpty(_currentProjectFile) ?
                "Новый проект" : System.IO.Path.GetFileName(_currentProjectFile);
            this.Title = $"Vector Paint Editor - {filename}";
        }

        private void ShowError(string title, string message)
        {
            MessageBox.Show($"{title}: {message}", "Ошибка",
                           MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private bool IsResizeHandle(Shape shape)
        {
            return shape is Rectangle rect && rect.Width == 8 && rect.Height == 8;
        }

        // Обновляет UI свойствами выделенной фигуры
        private void UpdateUIWithSelectedShapeProperties()
        {
            if (_shapeManager?.SelectedShape == null)
            {
                // Если нет выделенной фигуры, используем текущие настройки
                UpdatePropertiesFromUI();
                return;
            }

            // Получаем свойства выделенной фигуры
            var shapeProperties = GetShapeProperties(_shapeManager.SelectedShape);

            // Обновляем UI в соответствии со свойствами фигуры
            UpdateColorBoxesFromProperties(shapeProperties);

            // Обновляем текущие свойства
            _currentProperties = shapeProperties;
        }

        // Обновляет комбобоксы цветов на основе свойств фигуры
        private void UpdateColorBoxesFromProperties(ShapeProperties properties)
        {
            if (StrokeColorBox == null || FillColorBox == null) return;

            // Временно отключаем обработчики событий чтобы избежать рекурсии
            StrokeColorBox.SelectionChanged -= StrokeColorBox_SelectionChanged;
            FillColorBox.SelectionChanged -= FillColorBox_SelectionChanged;

            try
            {
                // Обновляем комбобокс контура
                string strokeColorName = GetColorNameFromBrush(properties.Stroke);
                foreach (ComboBoxItem item in StrokeColorBox.Items)
                {
                    if (item.Tag as string == strokeColorName)
                    {
                        StrokeColorBox.SelectedItem = item;
                        break;
                    }
                }

                // Обновляем комбобокс заливки
                string fillColorName = properties.HasFill ? GetColorNameFromBrush(properties.Fill) : "Transparent";
                foreach (ComboBoxItem item in FillColorBox.Items)
                {
                    if (item.Tag as string == fillColorName)
                    {
                        FillColorBox.SelectedItem = item;
                        break;
                    }
                }
            }
            finally
            {
                // Восстанавливаем обработчики событий
                StrokeColorBox.SelectionChanged += StrokeColorBox_SelectionChanged;
                FillColorBox.SelectionChanged += FillColorBox_SelectionChanged;
            }
        }

        // Получает имя цвета из кисти
        private string GetColorNameFromBrush(Brush brush)
        {
            if (brush is SolidColorBrush solidBrush)
            {
                return solidBrush.Color.ToString() switch
                {
                    "#FFFF0000" => "Red",
                    "#FF008000" => "Green",
                    "#FF0000FF" => "Blue",
                    "#FFFFFF00" => "Yellow",
                    "#FF800080" => "Purple",
                    _ => "Black"
                };
            }
            return "Black";
        }

        // Обработчик изменения размера окна
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Можно добавить адаптацию интерфейса при изменении размера окна
            UpdateStatusBar();
        }
    }
}