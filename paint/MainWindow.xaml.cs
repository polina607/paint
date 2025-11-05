using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Microsoft.Win32;
using System.Text;
using System.Windows.Markup;
using paint.Commands; // Добавьте этот using

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

        // Режимы работы
        private EditorMode _currentMode = EditorMode.Draw;

        // Текущий файл проекта
        private string _currentProjectFile = string.Empty;

        public MainWindow()
        {
            InitializeComponent();

            // Обработчики отмены/повтора
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Undo, (s, e) => Undo()));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Redo, (s, e) => Redo()));

            // Обработчики для меню файлов
            CommandBindings.Add(new CommandBinding(ApplicationCommands.New, (s, e) => NewProject()));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Open, (s, e) => OpenProject()));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Save, (s, e) => SaveProject()));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.SaveAs, (s, e) => SaveProjectAs()));
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UpdatePropertiesFromUI();
            _shapeManager = new ShapeManager(DrawCanvas);
            _zoomManager = new ZoomManager(MainScrollViewer, DrawCanvas);
            UpdateStatusBar();
            UpdateZoomDisplay();
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
                var oldProperties = GetShapeProperties(_shapeManager.SelectedShape);
                ShapeFactory.ApplyProperties(_shapeManager.SelectedShape, _currentProperties);

                // Создаем команду для изменения свойств
                var command = new ChangePropertiesCommand(_shapeManager.SelectedShape, oldProperties, _currentProperties, _shapeManager);
                UndoRedoManager.Instance.Execute(command);
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

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Очистить весь холст?", "Очистка",
                                        MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Создаем команду для очистки всех фигур
                var shapes = new List<Shape>();
                foreach (var child in DrawCanvas.Children)
                {
                    if (child is Shape shape && !IsResizeHandle(shape))
                    {
                        shapes.Add(shape);
                    }
                }

                // Выполняем команды удаления для каждой фигуры
                foreach (var shape in shapes)
                {
                    var command = new RemoveShapeCommand(DrawCanvas, shape, _shapeManager);
                    UndoRedoManager.Instance.Execute(command);
                }

                ResetPolygon();
                UpdateStatusBar();
            }
        }

        // Методы отмены/повтора
        private void Undo()
        {
            UndoRedoManager.Instance.Undo();
            UpdateStatusBar();
        }

        private void Redo()
        {
            UndoRedoManager.Instance.Redo();
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

                // Используем команду для добавления фигуры
                var command = new AddShapeCommand(DrawCanvas, _previewShape, _shapeManager);
                UndoRedoManager.Instance.Execute(command);

                // Удаляем preview фигуру с холста
                DrawCanvas.Children.Remove(_previewShape);
                _previewShape = null;
                UpdateStatusBar();
            }
        }

        // Завершение многоугольника
        private void CompletePolygon()
        {
            if (_polygonPoints.Count >= 3)
            {
                Polygon finalPolygon = new Polygon();
                ShapeFactory.ApplyProperties(finalPolygon, _currentProperties);
                finalPolygon.Points = new PointCollection(_polygonPoints);

                if (_polygonPreview != null)
                {
                    DrawCanvas.Children.Remove(_polygonPreview);
                    _polygonPreview = null;
                }

                // Используем команду для добавления фигуры
                var command = new AddShapeCommand(DrawCanvas, finalPolygon, _shapeManager);
                UndoRedoManager.Instance.Execute(command);

                UpdateStatusBar();
            }
            else
            {
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
            string undoText = $" | Отмена: {UndoRedoManager.Instance.CanUndo}, Повтор: {UndoRedoManager.Instance.CanRedo}";

            if (StatusText != null)
            {
                StatusText.Text = $"{modeText}{shapeText}{selectionText}{zoomText}{polygonText}{undoText}";
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
                UpdateStatusBar();
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
                MessageBox.Show($"Ошибка при открытии файла: {ex.Message}", "Ошибка",
                               MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show($"Ошибка при сохранении файла: {ex.Message}", "Ошибка",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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
                MessageBox.Show($"Ошибка при экспорте в SVG: {ex.Message}", "Ошибка",
                               MessageBoxButton.OK, MessageBoxImage.Error);
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

        private void LoadProject(string filename)
        {
            try
            {
                var project = LoadProjectFromFile(filename);

                DrawCanvas.Children.Clear();
                ResetPolygon();

                foreach (var shapeData in project.Shapes)
                {
                    var shape = CreateShapeFromData(shapeData);
                    if (shape != null)
                    {
                        DrawCanvas.Children.Add(shape);
                    }
                }

                _currentProjectFile = filename;
                _shapeManager = new ShapeManager(DrawCanvas);
                UpdateStatusBar();
                UpdateWindowTitle();

                UndoRedoManager.Instance.Clear();

                MessageBox.Show($"Проект успешно загружен!\n{filename}",
                              "Загрузка завершена", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке проекта: {ex.Message}", "Ошибка",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveProjectToFile(string filename)
        {
            var project = new PaintProject
            {
                CanvasWidth = DrawCanvas.Width,
                CanvasHeight = DrawCanvas.Height
            };

            foreach (var child in DrawCanvas.Children)
            {
                if (child is Shape shape && !IsResizeHandle(shape))
                {
                    project.Shapes.Add(CreateShapeData(shape));
                }
            }

            // Используем XML сериализацию для собственного формата
            var xmlSettings = new XmlWriterSettings
            {
                Indent = true,
                Encoding = Encoding.UTF8
            };

            using (var writer = XmlWriter.Create(filename, xmlSettings))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("PaintProject");
                writer.WriteAttributeString("CanvasWidth", project.CanvasWidth.ToString());
                writer.WriteAttributeString("CanvasHeight", project.CanvasHeight.ToString());

                foreach (var shapeData in project.Shapes)
                {
                    WriteShapeDataToXml(writer, shapeData);
                }

                writer.WriteEndElement();
                writer.WriteEndDocument();
            }

            UpdateWindowTitle();
            MessageBox.Show($"Проект успешно сохранен!\n{filename}",
                          "Сохранение завершено", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private PaintProject LoadProjectFromFile(string filename)
        {
            var project = new PaintProject();

            using (var reader = XmlReader.Create(filename))
            {
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.Name == "PaintProject")
                    {
                        project.CanvasWidth = double.Parse(reader.GetAttribute("CanvasWidth"));
                        project.CanvasHeight = double.Parse(reader.GetAttribute("CanvasHeight"));
                    }
                    else if (reader.NodeType == XmlNodeType.Element && reader.Name == "Shape")
                    {
                        project.Shapes.Add(ReadShapeDataFromXml(reader));
                    }
                }
            }

            return project;
        }

        private ShapeData CreateShapeData(Shape shape)
        {
            var data = new ShapeData
            {
                StrokeColor = ColorToHex((shape.Stroke as SolidColorBrush)?.Color ?? Colors.Black),
                FillColor = ColorToHex((shape.Fill as SolidColorBrush)?.Color ?? Colors.Transparent),
                StrokeThickness = shape.StrokeThickness,
                HasFill = shape.Fill != Brushes.Transparent && shape.Fill != null
            };

            if (shape is Line line)
            {
                data.Type = ShapeType.Line;
                data.X1 = line.X1;
                data.Y1 = line.Y1;
                data.X2 = line.X2;
                data.Y2 = line.Y2;
            }
            else if (shape is Rectangle rect)
            {
                data.Type = ShapeType.Rectangle;
                data.Left = Canvas.GetLeft(rect);
                data.Top = Canvas.GetTop(rect);
                data.Width = rect.Width;
                data.Height = rect.Height;
            }
            else if (shape is Ellipse ellipse)
            {
                data.Type = ShapeType.Ellipse;
                data.Left = Canvas.GetLeft(ellipse);
                data.Top = Canvas.GetTop(ellipse);
                data.Width = ellipse.Width;
                data.Height = ellipse.Height;
            }
            else if (shape is Polygon polygon)
            {
                data.Type = ShapeType.Polygon;
                foreach (var point in polygon.Points)
                {
                    data.Points.Add(new PointData(point.X, point.Y));
                }
            }

            return data;
        }

        private Shape CreateShapeFromData(ShapeData data)
        {
            var shape = ShapeFactory.CreateShape(data.Type);

            var properties = new ShapeProperties
            {
                Stroke = new SolidColorBrush(HexToColor(data.StrokeColor)),
                Fill = new SolidColorBrush(HexToColor(data.FillColor)),
                StrokeThickness = data.StrokeThickness,
                HasFill = data.HasFill
            };

            ShapeFactory.ApplyProperties(shape, properties);

            if (shape is Line line)
            {
                line.X1 = data.X1;
                line.Y1 = data.Y1;
                line.X2 = data.X2;
                line.Y2 = data.Y2;
            }
            else if (shape is Rectangle rect)
            {
                Canvas.SetLeft(rect, data.Left);
                Canvas.SetTop(rect, data.Top);
                rect.Width = data.Width;
                rect.Height = data.Height;
            }
            else if (shape is Ellipse ellipse)
            {
                Canvas.SetLeft(ellipse, data.Left);
                Canvas.SetTop(ellipse, data.Top);
                ellipse.Width = data.Width;
                ellipse.Height = data.Height;
            }
            else if (shape is Polygon polygon)
            {
                var points = new PointCollection();
                foreach (var pointData in data.Points)
                {
                    points.Add(new Point(pointData.X, pointData.Y));
                }
                polygon.Points = points;
            }

            return shape;
        }

        // Классы для сохранения проекта
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

        public class PointData
        {
            public double X { get; set; }
            public double Y { get; set; }

            public PointData() { }
            public PointData(double x, double y) { X = x; Y = y; }
        }

        // Класс для экспорта в SVG
        public static class SvgExporter
        {
            public static string ExportToSvg(Canvas canvas)
            {
                var svg = new StringBuilder();

                svg.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
                svg.AppendLine($"<svg width=\"{canvas.Width}\" height=\"{canvas.Height}\" xmlns=\"http://www.w3.org/2000/svg\">");

                // Фон с сеткой (опционально)
                svg.AppendLine("<rect width=\"100%\" height=\"100%\" fill=\"white\"/>");

                foreach (var child in canvas.Children)
                {
                    if (child is Shape shape && !IsResizeHandle(shape))
                    {
                        svg.AppendLine(ShapeToSvg(shape));
                    }
                }

                svg.AppendLine("</svg>");
                return svg.ToString();
            }

            private static string ShapeToSvg(Shape shape)
            {
                if (shape is Line line)
                    return LineToSvg(line);
                else if (shape is Rectangle rect)
                    return RectangleToSvg(rect);
                else if (shape is Ellipse ellipse)
                    return EllipseToSvg(ellipse);
                else if (shape is Polygon polygon)
                    return PolygonToSvg(polygon);

                return string.Empty;
            }

            private static string LineToSvg(Line line)
            {
                var stroke = ColorToHex((line.Stroke as SolidColorBrush)?.Color ?? Colors.Black);
                return $"<line x1=\"{line.X1}\" y1=\"{line.Y1}\" x2=\"{line.X2}\" y2=\"{line.Y2}\" " +
                       $"stroke=\"{stroke}\" stroke-width=\"{line.StrokeThickness}\"/>";
            }

            private static string RectangleToSvg(Rectangle rect)
            {
                var stroke = ColorToHex((rect.Stroke as SolidColorBrush)?.Color ?? Colors.Black);
                var fill = rect.Fill is SolidColorBrush fillBrush ?
                          ColorToHex(fillBrush.Color) : "none";

                var left = Canvas.GetLeft(rect);
                var top = Canvas.GetTop(rect);

                return $"<rect x=\"{left}\" y=\"{top}\" width=\"{rect.Width}\" height=\"{rect.Height}\" " +
                       $"stroke=\"{stroke}\" stroke-width=\"{rect.StrokeThickness}\" fill=\"{fill}\"/>";
            }

            private static string EllipseToSvg(Ellipse ellipse)
            {
                var stroke = ColorToHex((ellipse.Stroke as SolidColorBrush)?.Color ?? Colors.Black);
                var fill = ellipse.Fill is SolidColorBrush fillBrush ?
                          ColorToHex(fillBrush.Color) : "none";

                var left = Canvas.GetLeft(ellipse);
                var top = Canvas.GetTop(ellipse);
                var centerX = left + ellipse.Width / 2;
                var centerY = top + ellipse.Height / 2;
                var radiusX = ellipse.Width / 2;
                var radiusY = ellipse.Height / 2;

                return $"<ellipse cx=\"{centerX}\" cy=\"{centerY}\" rx=\"{radiusX}\" ry=\"{radiusY}\" " +
                       $"stroke=\"{stroke}\" stroke-width=\"{ellipse.StrokeThickness}\" fill=\"{fill}\"/>";
            }

            private static string PolygonToSvg(Polygon polygon)
            {
                var stroke = ColorToHex((polygon.Stroke as SolidColorBrush)?.Color ?? Colors.Black);
                var fill = polygon.Fill is SolidColorBrush fillBrush ?
                          ColorToHex(fillBrush.Color) : "none";

                var points = new StringBuilder();
                foreach (var point in polygon.Points)
                {
                    points.Append($"{point.X},{point.Y} ");
                }

                return $"<polygon points=\"{points}\" " +
                       $"stroke=\"{stroke}\" stroke-width=\"{polygon.StrokeThickness}\" fill=\"{fill}\"/>";
            }

            private static string ColorToHex(Color color)
            {
                return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
            }

            private static bool IsResizeHandle(Shape shape)
            {
                return shape is Rectangle rect && rect.Width == 8 && rect.Height == 8;
            }
        }

        // Вспомогательные методы для работы с проектом
        private void WriteShapeDataToXml(XmlWriter writer, ShapeData data)
        {
            writer.WriteStartElement("Shape");
            writer.WriteAttributeString("Type", data.Type.ToString());
            writer.WriteAttributeString("StrokeColor", data.StrokeColor);
            writer.WriteAttributeString("FillColor", data.FillColor);
            writer.WriteAttributeString("StrokeThickness", data.StrokeThickness.ToString());
            writer.WriteAttributeString("HasFill", data.HasFill.ToString());

            switch (data.Type)
            {
                case ShapeType.Line:
                    writer.WriteElementString("X1", data.X1.ToString());
                    writer.WriteElementString("Y1", data.Y1.ToString());
                    writer.WriteElementString("X2", data.X2.ToString());
                    writer.WriteElementString("Y2", data.Y2.ToString());
                    break;
                case ShapeType.Rectangle:
                case ShapeType.Square:
                case ShapeType.Ellipse:
                case ShapeType.Circle:
                    writer.WriteElementString("Left", data.Left.ToString());
                    writer.WriteElementString("Top", data.Top.ToString());
                    writer.WriteElementString("Width", data.Width.ToString());
                    writer.WriteElementString("Height", data.Height.ToString());
                    break;
                case ShapeType.Polygon:
                    writer.WriteStartElement("Points");
                    foreach (var point in data.Points)
                    {
                        writer.WriteStartElement("Point");
                        writer.WriteAttributeString("X", point.X.ToString());
                        writer.WriteAttributeString("Y", point.Y.ToString());
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                    break;
            }

            writer.WriteEndElement();
        }

        private ShapeData ReadShapeDataFromXml(XmlReader reader)
        {
            var data = new ShapeData
            {
                Type = (ShapeType)Enum.Parse(typeof(ShapeType), reader.GetAttribute("Type")),
                StrokeColor = reader.GetAttribute("StrokeColor"),
                FillColor = reader.GetAttribute("FillColor"),
                StrokeThickness = double.Parse(reader.GetAttribute("StrokeThickness")),
                HasFill = bool.Parse(reader.GetAttribute("HasFill"))
            };

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    switch (reader.Name)
                    {
                        case "X1": data.X1 = double.Parse(reader.ReadElementContentAsString()); break;
                        case "Y1": data.Y1 = double.Parse(reader.ReadElementContentAsString()); break;
                        case "X2": data.X2 = double.Parse(reader.ReadElementContentAsString()); break;
                        case "Y2": data.Y2 = double.Parse(reader.ReadElementContentAsString()); break;
                        case "Left": data.Left = double.Parse(reader.ReadElementContentAsString()); break;
                        case "Top": data.Top = double.Parse(reader.ReadElementContentAsString()); break;
                        case "Width": data.Width = double.Parse(reader.ReadElementContentAsString()); break;
                        case "Height": data.Height = double.Parse(reader.ReadElementContentAsString()); break;
                        case "Points":
                            if (!reader.IsEmptyElement)
                            {
                                ReadPointsFromXml(reader, data.Points);
                            }
                            break;
                    }
                }
                else if (reader.NodeType == XmlNodeType.EndElement && reader.Name == "Shape")
                {
                    break;
                }
            }

            return data;
        }

        private void ReadPointsFromXml(XmlReader reader, List<PointData> points)
        {
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.Name == "Point")
                {
                    double x = double.Parse(reader.GetAttribute("X"));
                    double y = double.Parse(reader.GetAttribute("Y"));
                    points.Add(new PointData(x, y));
                }
                else if (reader.NodeType == XmlNodeType.EndElement && reader.Name == "Points")
                {
                    break;
                }
            }
        }

        private string ColorToHex(Color color)
        {
            return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        private Color HexToColor(string hex)
        {
            hex = hex.Replace("#", "");

            byte a = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            byte r = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);

            return Color.FromArgb(a, r, g, b);
        }

        private bool IsResizeHandle(Shape shape)
        {
            return shape is Rectangle rect && rect.Width == 8 && rect.Height == 8;
        }

        private void UpdateWindowTitle()
        {
            string filename = string.IsNullOrEmpty(_currentProjectFile) ? "Новый проект" : System.IO.Path.GetFileName(_currentProjectFile);
            this.Title = $"Vector Paint Editor - {filename}";
        }
    }
}