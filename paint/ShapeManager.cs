using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace paint
{
    // Управление фигурами на холсте: выделение, перемещение, изменение размера
    public class ShapeManager
    {
        private Canvas _canvas;
        private Shape? _selectedShape;
        private Point _dragStartPoint;
        private bool _isDragging = false;
        private bool _isResizing = false;
        private ResizeHandle _activeResizeHandle;

        // Маркеры изменения размера
        private const double ResizeHandleSize = 8;
        private Dictionary<ResizeHandle, Rectangle> _resizeHandles = new Dictionary<ResizeHandle, Rectangle>();

        // Для хранения исходных значений при изменении размера
        private double _originalX1, _originalY1, _originalX2, _originalY2;
        private PointCollection _originalPolygonPoints;

        // Событие для уведомления об изменении выделения
        public event Action<Shape?>? SelectionChanged;

        public ShapeManager(Canvas canvas)
        {
            _canvas = canvas;
            InitializeResizeHandles();
        }

        // Добавляет фигуру и выделяет ее
        public void AddShape(Shape shape)
        {
            SelectShape(shape);
        }

        // Выделяет фигуру и показывает маркеры
        public void SelectShape(Shape shape)
        {
            ClearSelection();
            _selectedShape = shape;

            // Устанавливаем позицию для многоугольника, если не установлена
            if (shape is Polygon polygon && double.IsNaN(Canvas.GetLeft(polygon)))
            {
                Canvas.SetLeft(polygon, 0);
                Canvas.SetTop(polygon, 0);
            }

            ShowSelectionMarkers();
            SelectionChanged?.Invoke(_selectedShape);
        }

        // Снимает выделение с фигуры
        public void ClearSelection()
        {
            HideSelectionMarkers();
            _selectedShape = null;
            SelectionChanged?.Invoke(null);
        }

        // Удаляет выделенную фигуру
        public void DeleteSelectedShape()
        {
            if (_selectedShape != null)
            {
                _canvas.Children.Remove(_selectedShape);
                ClearSelection();
            }
        }

        // Начинает перемещение фигуры
        public void StartDrag(Point startPoint)
        {
            if (_selectedShape != null)
            {
                _isDragging = true;
                _dragStartPoint = startPoint;
            }
        }

        // Обновляет позицию при перемещении
        public void UpdateDrag(Point currentPoint)
        {
            if (_isDragging && _selectedShape != null)
            {
                double deltaX = currentPoint.X - _dragStartPoint.X;
                double deltaY = currentPoint.Y - _dragStartPoint.Y;

                if (_selectedShape is Line line)
                {
                    // Перемещение линии
                    line.X1 += deltaX;
                    line.Y1 += deltaY;
                    line.X2 += deltaX;
                    line.Y2 += deltaY;
                }
                else if (_selectedShape is Polygon polygon)
                {
                    // Перемещение многоугольника
                    PointCollection newPoints = new PointCollection();
                    foreach (Point point in polygon.Points)
                    {
                        newPoints.Add(new Point(point.X + deltaX, point.Y + deltaY));
                    }
                    polygon.Points = newPoints;
                }
                else
                {
                    // Перемещение обычных фигур
                    Canvas.SetLeft(_selectedShape, Canvas.GetLeft(_selectedShape) + deltaX);
                    Canvas.SetTop(_selectedShape, Canvas.GetTop(_selectedShape) + deltaY);
                }

                _dragStartPoint = currentPoint;
                UpdateResizeHandlesPosition();
            }
        }

        // Завершает перемещение
        public void EndDrag()
        {
            _isDragging = false;
        }

        // Начинает изменение размера
        public void StartResize(ResizeHandle handle, Point startPoint)
        {
            if (_selectedShape != null)
            {
                _isResizing = true;
                _activeResizeHandle = handle;
                _dragStartPoint = startPoint;

                // Сохраняем исходные значения
                if (_selectedShape is Line line)
                {
                    _originalX1 = line.X1;
                    _originalY1 = line.Y1;
                    _originalX2 = line.X2;
                    _originalY2 = line.Y2;
                }
                else if (_selectedShape is Polygon polygon)
                {
                    _originalPolygonPoints = new PointCollection(polygon.Points);
                }
                else
                {
                    // Для обычных фигур сохраняем текущие размеры
                    _originalX1 = Canvas.GetLeft(_selectedShape);
                    _originalY1 = Canvas.GetTop(_selectedShape);
                    _originalX2 = _originalX1 + _selectedShape.Width;
                    _originalY2 = _originalY1 + _selectedShape.Height;
                }
            }
        }

        // Обновляет размер при изменении
        public void UpdateResize(Point currentPoint)
        {
            if (_isResizing && _selectedShape != null)
            {
                double deltaX = currentPoint.X - _dragStartPoint.X;
                double deltaY = currentPoint.Y - _dragStartPoint.Y;

                if (_selectedShape is Line line)
                {
                    // Изменение размера линии
                    switch (_activeResizeHandle)
                    {
                        case ResizeHandle.TopLeft:
                            line.X1 = _originalX1 + deltaX;
                            line.Y1 = _originalY1 + deltaY;
                            break;
                        case ResizeHandle.TopRight:
                            line.X2 = _originalX2 + deltaX;
                            line.Y2 = _originalY2 + deltaY;
                            break;
                        case ResizeHandle.BottomLeft:
                            line.X1 = _originalX1 + deltaX;
                            line.Y1 = _originalY1 + deltaY;
                            break;
                        case ResizeHandle.BottomRight:
                            line.X2 = _originalX2 + deltaX;
                            line.Y2 = _originalY2 + deltaY;
                            break;
                    }
                }
                else if (_selectedShape is Polygon polygon && _originalPolygonPoints != null)
                {
                    // Масштабирование многоугольника относительно центра
                    Point center = GetPolygonCenter(_originalPolygonPoints);

                    // Вычисляем коэффициенты масштабирования
                    double scaleX = 1.0;
                    double scaleY = 1.0;

                    switch (_activeResizeHandle)
                    {
                        case ResizeHandle.TopLeft:
                            scaleX = 1.0 - deltaX / 100;
                            scaleY = 1.0 - deltaY / 100;
                            break;
                        case ResizeHandle.TopRight:
                            scaleX = 1.0 + deltaX / 100;
                            scaleY = 1.0 - deltaY / 100;
                            break;
                        case ResizeHandle.BottomLeft:
                            scaleX = 1.0 - deltaX / 100;
                            scaleY = 1.0 + deltaY / 100;
                            break;
                        case ResizeHandle.BottomRight:
                            scaleX = 1.0 + deltaX / 100;
                            scaleY = 1.0 + deltaY / 100;
                            break;
                    }

                    // Ограничиваем минимальный размер
                    scaleX = Math.Max(0.1, scaleX);
                    scaleY = Math.Max(0.1, scaleY);

                    PointCollection newPoints = new PointCollection();
                    foreach (Point point in _originalPolygonPoints)
                    {
                        double newX = center.X + (point.X - center.X) * scaleX;
                        double newY = center.Y + (point.Y - center.Y) * scaleY;
                        newPoints.Add(new Point(newX, newY));
                    }
                    polygon.Points = newPoints;
                }
                else
                {
                    // Изменение размера обычных фигур
                    double left = Canvas.GetLeft(_selectedShape);
                    double top = Canvas.GetTop(_selectedShape);
                    double width = _selectedShape.Width;
                    double height = _selectedShape.Height;

                    switch (_activeResizeHandle)
                    {
                        case ResizeHandle.TopLeft:
                            double newLeft = Math.Min(left + deltaX, left + width - 10);
                            double newTop = Math.Min(top + deltaY, top + height - 10);
                            double newWidth = Math.Max(10, width - deltaX);
                            double newHeight = Math.Max(10, height - deltaY);

                            Canvas.SetLeft(_selectedShape, newLeft);
                            Canvas.SetTop(_selectedShape, newTop);
                            _selectedShape.Width = newWidth;
                            _selectedShape.Height = newHeight;
                            break;

                        case ResizeHandle.TopRight:
                            newTop = Math.Min(top + deltaY, top + height - 10);
                            newWidth = Math.Max(10, width + deltaX);
                            newHeight = Math.Max(10, height - deltaY);

                            Canvas.SetTop(_selectedShape, newTop);
                            _selectedShape.Width = newWidth;
                            _selectedShape.Height = newHeight;
                            break;

                        case ResizeHandle.BottomLeft:
                            newLeft = Math.Min(left + deltaX, left + width - 10);
                            newWidth = Math.Max(10, width - deltaX);
                            newHeight = Math.Max(10, height + deltaY);

                            Canvas.SetLeft(_selectedShape, newLeft);
                            _selectedShape.Width = newWidth;
                            _selectedShape.Height = newHeight;
                            break;

                        case ResizeHandle.BottomRight:
                            newWidth = Math.Max(10, width + deltaX);
                            newHeight = Math.Max(10, height + deltaY);

                            _selectedShape.Width = newWidth;
                            _selectedShape.Height = newHeight;
                            break;
                    }
                }

                _dragStartPoint = currentPoint;
                UpdateResizeHandlesPosition();
            }
        }

        // Завершает изменение размера
        public void EndResize()
        {
            _isResizing = false;
            _originalPolygonPoints = null;
        }

        // Обновляет маркеры выделения
        public void UpdateSelectionMarkers()
        {
            UpdateResizeHandlesPosition();
        }

        // Находит центр многоугольника
        private Point GetPolygonCenter(PointCollection points)
        {
            if (points == null || points.Count == 0)
                return new Point(0, 0);

            double sumX = 0, sumY = 0;
            foreach (Point point in points)
            {
                sumX += point.X;
                sumY += point.Y;
            }
            return new Point(sumX / points.Count, sumY / points.Count);
        }

        // Проверяет, находится ли точка в маркере изменения размера
        public ResizeHandle? GetResizeHandleAtPoint(Point point)
        {
            foreach (var handle in _resizeHandles)
            {
                if (IsPointInRectangle(point, handle.Value))
                {
                    return handle.Key;
                }
            }
            return null;
        }

        // Проверяет, находится ли точка внутри фигуры
        public bool IsPointInShape(Point point, Shape shape)
        {
            if (shape is Line line)
            {
                return IsPointNearLine(point, line);
            }

            if (shape is Polygon polygon)
            {
                return IsPointInPolygon(point, polygon);
            }

            // Для прямоугольников и эллипсов
            double left = Canvas.GetLeft(shape);
            double top = Canvas.GetTop(shape);

            // Если позиция не установлена, считаем что фигура в (0,0)
            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;

            Rect rect = new Rect(left, top, shape.Width, shape.Height);
            return rect.Contains(point);
        }

        // Проверяет, находится ли точка внутри многоугольника
        private bool IsPointInPolygon(Point point, Polygon polygon)
        {
            PointCollection points = polygon.Points;
            if (points == null || points.Count < 3)
                return false;

            int count = points.Count;
            bool inside = false;

            // Алгоритм проверки точки в многоугольнике (ray casting)
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                if (((points[i].Y > point.Y) != (points[j].Y > point.Y)) &&
                    (point.X < (points[j].X - points[i].X) * (point.Y - points[i].Y) / (points[j].Y - points[i].Y) + points[i].X))
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        // Находит фигуру в указанной точке
        public Shape? GetShapeAtPoint(Point point)
        {
            // Проверяем с конца (верхние фигуры сначала)
            for (int i = _canvas.Children.Count - 1; i >= 0; i--)
            {
                if (_canvas.Children[i] is Shape shape &&
                    shape != _selectedShape &&
                    !_resizeHandles.Values.Contains(shape) &&
                    IsPointInShape(point, shape))
                {
                    return shape;
                }
            }
            return null;
        }

        // Получает границы фигуры
        public Rect GetShapeBounds(Shape shape)
        {
            if (shape is Line line)
            {
                double minX = Math.Min(line.X1, line.X2);
                double minY = Math.Min(line.Y1, line.Y2);
                double maxX = Math.Max(line.X1, line.X2);
                double maxY = Math.Max(line.Y1, line.Y2);
                return new Rect(minX, minY, maxX - minX, maxY - minY);
            }
            else if (shape is Polygon polygon)
            {
                return GetPolygonBounds(polygon);
            }
            else
            {
                double left = Canvas.GetLeft(shape);
                double top = Canvas.GetTop(shape);
                if (double.IsNaN(left)) left = 0;
                if (double.IsNaN(top)) top = 0;
                return new Rect(left, top, shape.Width, shape.Height);
            }
        }

        // Свойства для отслеживания состояния
        public bool IsDragging => _isDragging;
        public bool IsResizing => _isResizing;
        public Shape? SelectedShape => _selectedShape;

        // Инициализация маркеров изменения размера
        private void InitializeResizeHandles()
        {
            foreach (ResizeHandle handle in Enum.GetValues(typeof(ResizeHandle)))
            {
                var rect = new Rectangle
                {
                    Width = ResizeHandleSize,
                    Height = ResizeHandleSize,
                    Fill = Brushes.White,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                    Visibility = Visibility.Collapsed,
                    Cursor = Cursors.SizeAll
                };
                _resizeHandles[handle] = rect;
                _canvas.Children.Add(rect);
            }
        }

        // Показывает маркеры выделения
        private void ShowSelectionMarkers()
        {
            if (_selectedShape != null)
            {
                UpdateResizeHandlesPosition();
                foreach (var handle in _resizeHandles.Values)
                {
                    handle.Visibility = Visibility.Visible;
                }
            }
        }

        // Скрывает маркеры выделения
        private void HideSelectionMarkers()
        {
            foreach (var handle in _resizeHandles.Values)
                handle.Visibility = Visibility.Collapsed;
        }

        // Обновляет позиции маркеров изменения размера
        private void UpdateResizeHandlesPosition()
        {
            if (_selectedShape == null) return;

            Rect bounds = GetShapeBounds(_selectedShape);

            // Для линии используем только два маркера
            if (_selectedShape is Line)
            {
                SetResizeHandlePosition(ResizeHandle.TopLeft, bounds.Left - ResizeHandleSize / 2, bounds.Top - ResizeHandleSize / 2);
                SetResizeHandlePosition(ResizeHandle.TopRight, bounds.Right - ResizeHandleSize / 2, bounds.Bottom - ResizeHandleSize / 2);
                SetResizeHandlePosition(ResizeHandle.BottomLeft, -100, -100); // Скрываем
                SetResizeHandlePosition(ResizeHandle.BottomRight, -100, -100); // Скрываем
            }
            else
            {
                // Для всех остальных фигур - 4 маркера
                SetResizeHandlePosition(ResizeHandle.TopLeft, bounds.Left - ResizeHandleSize / 2, bounds.Top - ResizeHandleSize / 2);
                SetResizeHandlePosition(ResizeHandle.TopRight, bounds.Right - ResizeHandleSize / 2, bounds.Top - ResizeHandleSize / 2);
                SetResizeHandlePosition(ResizeHandle.BottomLeft, bounds.Left - ResizeHandleSize / 2, bounds.Bottom - ResizeHandleSize / 2);
                SetResizeHandlePosition(ResizeHandle.BottomRight, bounds.Right - ResizeHandleSize / 2, bounds.Bottom - ResizeHandleSize / 2);
            }
        }

        // Вычисляет ограничивающий прямоугольник для многоугольника
        private Rect GetPolygonBounds(Polygon polygon)
        {
            if (polygon.Points == null || polygon.Points.Count == 0)
                return new Rect(0, 0, 0, 0);

            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;

            foreach (Point point in polygon.Points)
            {
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }

            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        // Устанавливает позицию маркера изменения размера
        private void SetResizeHandlePosition(ResizeHandle handle, double x, double y)
        {
            if (_resizeHandles.TryGetValue(handle, out var rect))
            {
                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, y);
            }
        }

        // Проверяет, находится ли точка внутри прямоугольника
        private bool IsPointInRectangle(Point point, Rectangle rect)
        {
            double left = Canvas.GetLeft(rect);
            double top = Canvas.GetTop(rect);
            Rect rectBounds = new Rect(left, top, rect.Width, rect.Height);
            return rectBounds.Contains(point);
        }

        // Проверяет, находится ли точка рядом с линией
        private bool IsPointNearLine(Point point, Line line)
        {
            double distance = DistanceToLine(point, new Point(line.X1, line.Y1), new Point(line.X2, line.Y2));
            return distance < 10; // Пороговое расстояние 10 пикселей
        }

        // Вычисляет расстояние от точки до линии
        private double DistanceToLine(Point point, Point lineStart, Point lineEnd)
        {
            double A = point.X - lineStart.X;
            double B = point.Y - lineStart.Y;
            double C = lineEnd.X - lineStart.X;
            double D = lineEnd.Y - lineStart.Y;

            double dot = A * C + B * D;
            double lenSq = C * C + D * D;
            double param = (lenSq != 0) ? dot / lenSq : -1;

            double xx, yy;

            if (param < 0)
            {
                xx = lineStart.X;
                yy = lineStart.Y;
            }
            else if (param > 1)
            {
                xx = lineEnd.X;
                yy = lineEnd.Y;
            }
            else
            {
                xx = lineStart.X + param * C;
                yy = lineStart.Y + param * D;
            }

            double dx = point.X - xx;
            double dy = point.Y - yy;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}