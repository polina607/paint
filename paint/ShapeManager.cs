using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace paint
{
    // Управление фигурами на холсте
    public class ShapeManager
    {
        private Canvas _canvas;
        private Shape? _selectedShape;
        private Point _dragStartPoint;
        private bool _isDragging = false;
        private bool _isResizing = false;
        private ResizeHandle _activeResizeHandle;

        private const double ResizeHandleSize = 8;
        private Dictionary<ResizeHandle, Rectangle> _resizeHandles = new Dictionary<ResizeHandle, Rectangle>();

        // Для хранения оригинальных координат Line и Polygon
        private double _originalX1, _originalY1, _originalX2, _originalY2;
        private PointCollection _originalPolygonPoints;

        public ShapeManager(Canvas canvas)
        {
            _canvas = canvas;
            InitializeResizeHandles();
        }

        // Добавляет фигуру в менеджер
        public void AddShape(Shape shape)
        {
            SelectShape(shape);
        }

        // Выделяет фигуру на холсте
        public void SelectShape(Shape shape)
        {
            ClearSelection();
            _selectedShape = shape;

            if (shape is Polygon polygon && double.IsNaN(Canvas.GetLeft(polygon)))
            {
                Canvas.SetLeft(polygon, 0);
                Canvas.SetTop(polygon, 0);
            }

            ShowSelectionMarkers();
        }

        // Снимает выделение с фигуры
        public void ClearSelection()
        {
            HideSelectionMarkers();
            _selectedShape = null;
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

        // Обновляет позицию фигуры при перемещении
        public void UpdateDrag(Point currentPoint)
        {
            if (_isDragging && _selectedShape != null)
            {
                double deltaX = currentPoint.X - _dragStartPoint.X;
                double deltaY = currentPoint.Y - _dragStartPoint.Y;

                if (_selectedShape is Line line)
                {
                    line.X1 += deltaX;
                    line.Y1 += deltaY;
                    line.X2 += deltaX;
                    line.Y2 += deltaY;
                }
                else if (_selectedShape is Polygon polygon)
                {
                    // Перемещаем весь полигон
                    PointCollection newPoints = new PointCollection();
                    foreach (Point point in polygon.Points)
                    {
                        newPoints.Add(new Point(point.X + deltaX, point.Y + deltaY));
                    }
                    polygon.Points = newPoints;
                }
                else
                {
                    Canvas.SetLeft(_selectedShape, Canvas.GetLeft(_selectedShape) + deltaX);
                    Canvas.SetTop(_selectedShape, Canvas.GetTop(_selectedShape) + deltaY);
                }

                _dragStartPoint = currentPoint;
                UpdateResizeHandlesPosition();
            }
        }

        // Завершает перемещение фигуры
        public void EndDrag()
        {
            _isDragging = false;
        }

        // Начинает изменение размера фигуры
        public void StartResize(ResizeHandle handle, Point startPoint)
        {
            if (_selectedShape != null)
            {
                _isResizing = true;
                _activeResizeHandle = handle;
                _dragStartPoint = startPoint;

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
            }
        }

        // Обновляет размер фигуры
        public void UpdateResize(Point currentPoint)
        {
            if (_isResizing && _selectedShape != null)
            {
                double deltaX = currentPoint.X - _dragStartPoint.X;
                double deltaY = currentPoint.Y - _dragStartPoint.Y;

                if (_selectedShape is Line line)
                {
                    // Для Line - изменяем конкретную конечную точку
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
                    // Для Polygon - масштабируем все точки относительно центра
                    Point center = GetPolygonCenter(_originalPolygonPoints);
                    double scaleX = 1.0 + deltaX / 100;
                    double scaleY = 1.0 + deltaY / 100;

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
                    // Для Rectangle и Ellipse
                    double left = Canvas.GetLeft(_selectedShape);
                    double top = Canvas.GetTop(_selectedShape);
                    double width = _selectedShape.Width;
                    double height = _selectedShape.Height;

                    switch (_activeResizeHandle)
                    {
                        case ResizeHandle.TopLeft:
                            Canvas.SetLeft(_selectedShape, left + deltaX);
                            Canvas.SetTop(_selectedShape, top + deltaY);
                            _selectedShape.Width = Math.Max(10, width - deltaX);
                            _selectedShape.Height = Math.Max(10, height - deltaY);
                            break;
                        case ResizeHandle.TopRight:
                            Canvas.SetTop(_selectedShape, top + deltaY);
                            _selectedShape.Width = Math.Max(10, width + deltaX);
                            _selectedShape.Height = Math.Max(10, height - deltaY);
                            break;
                        case ResizeHandle.BottomLeft:
                            Canvas.SetLeft(_selectedShape, left + deltaX);
                            _selectedShape.Width = Math.Max(10, width - deltaX);
                            _selectedShape.Height = Math.Max(10, height + deltaY);
                            break;
                        case ResizeHandle.BottomRight:
                            _selectedShape.Width = Math.Max(10, width + deltaX);
                            _selectedShape.Height = Math.Max(10, height + deltaY);
                            break;
                    }
                }

                _dragStartPoint = currentPoint;
                UpdateResizeHandlesPosition();
            }
        }

        // Завершает изменение размера фигуры
        public void EndResize()
        {
            _isResizing = false;
            _originalPolygonPoints = null;
        }

        // Находит центр полигона
        private Point GetPolygonCenter(PointCollection points)
        {
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

            double left = Canvas.GetLeft(shape);
            double top = Canvas.GetTop(shape);
            Rect rect = new Rect(left, top, shape.Width, shape.Height);
            return rect.Contains(point);
        }

        // Проверяет, находится ли точка внутри полигона
        private bool IsPointInPolygon(Point point, Polygon polygon)
        {
            PointCollection points = polygon.Points;
            int count = points.Count;
            bool inside = false;

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

        // Получает фигуру под указанной точкой
        public Shape? GetShapeAtPoint(Point point)
        {
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

        public bool IsDragging => _isDragging;
        public bool IsResizing => _isResizing;
        public Shape? SelectedShape => _selectedShape;

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
                    Visibility = Visibility.Collapsed
                };
                _resizeHandles[handle] = rect;
                _canvas.Children.Add(rect);
            }
        }

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

        private void HideSelectionMarkers()
        {
            foreach (var handle in _resizeHandles.Values)
                handle.Visibility = Visibility.Collapsed;
        }

        private void UpdateResizeHandlesPosition()
        {
            if (_selectedShape == null) return;

            if (_selectedShape is Line line)
            {
                // Для Line - маркеры только на концах
                SetResizeHandlePosition(ResizeHandle.TopLeft, line.X1 - ResizeHandleSize / 2, line.Y1 - ResizeHandleSize / 2);
                SetResizeHandlePosition(ResizeHandle.TopRight, line.X2 - ResizeHandleSize / 2, line.Y2 - ResizeHandleSize / 2);
                SetResizeHandlePosition(ResizeHandle.BottomLeft, -100, -100); // Скрыть
                SetResizeHandlePosition(ResizeHandle.BottomRight, -100, -100); // Скрыть
            }
            else if (_selectedShape is Polygon polygon)
            {
                // Для Polygon - маркеры вокруг bounding box
                Rect bounds = GetPolygonBounds(polygon);
                SetResizeHandlePosition(ResizeHandle.TopLeft, bounds.Left - ResizeHandleSize / 2, bounds.Top - ResizeHandleSize / 2);
                SetResizeHandlePosition(ResizeHandle.TopRight, bounds.Right - ResizeHandleSize / 2, bounds.Top - ResizeHandleSize / 2);
                SetResizeHandlePosition(ResizeHandle.BottomLeft, bounds.Left - ResizeHandleSize / 2, bounds.Bottom - ResizeHandleSize / 2);
                SetResizeHandlePosition(ResizeHandle.BottomRight, bounds.Right - ResizeHandleSize / 2, bounds.Bottom - ResizeHandleSize / 2);
            }
            else
            {
                // Для Rectangle и Ellipse
                double left = Canvas.GetLeft(_selectedShape);
                double top = Canvas.GetTop(_selectedShape);
                double width = _selectedShape.Width;
                double height = _selectedShape.Height;

                SetResizeHandlePosition(ResizeHandle.TopLeft, left - ResizeHandleSize / 2, top - ResizeHandleSize / 2);
                SetResizeHandlePosition(ResizeHandle.TopRight, left + width - ResizeHandleSize / 2, top - ResizeHandleSize / 2);
                SetResizeHandlePosition(ResizeHandle.BottomLeft, left - ResizeHandleSize / 2, top + height - ResizeHandleSize / 2);
                SetResizeHandlePosition(ResizeHandle.BottomRight, left + width - ResizeHandleSize / 2, top + height - ResizeHandleSize / 2);
            }
        }

        // Находит bounding box полигона
        private Rect GetPolygonBounds(Polygon polygon)
        {
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

        private void SetResizeHandlePosition(ResizeHandle handle, double x, double y)
        {
            if (_resizeHandles.TryGetValue(handle, out var rect))
            {
                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, y);
            }
        }

        private bool IsPointInRectangle(Point point, Rectangle rect)
        {
            double left = Canvas.GetLeft(rect);
            double top = Canvas.GetTop(rect);
            Rect rectBounds = new Rect(left, top, rect.Width, rect.Height);
            return rectBounds.Contains(point);
        }

        private bool IsPointNearLine(Point point, Line line)
        {
            double distance = DistanceToLine(point, new Point(line.X1, line.Y1), new Point(line.X2, line.Y2));
            return distance < 10;
        }

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

    // Маркеры изменения размера
    public enum ResizeHandle
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    // Режимы работы редактора
    public enum EditorMode
    {
        Draw,
        Edit
    }
}