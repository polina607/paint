using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Linq;

namespace paint
{
    public class UndoManager
    {
        private Stack<CanvasState> _undoStack = new Stack<CanvasState>();
        private Stack<CanvasState> _redoStack = new Stack<CanvasState>();
        private const int MaxHistorySteps = 20;

        public void SaveState(Canvas canvas, string actionName = "")
        {
            // Сохраняем текущее состояние
            var currentState = new CanvasState(canvas, actionName);

            _undoStack.Push(currentState);
            _redoStack.Clear();

            // Ограничиваем размер истории
            if (_undoStack.Count > MaxHistorySteps)
            {
                var temp = new Stack<CanvasState>();
                for (int i = 0; i < MaxHistorySteps; i++)
                {
                    temp.Push(_undoStack.Pop());
                }
                _undoStack = temp;
            }

            System.Diagnostics.Debug.WriteLine($"=== SAVE STATE '{actionName}' ===");
            System.Diagnostics.Debug.WriteLine($"Shapes: {currentState.Shapes.Count}, Undo stack: {_undoStack.Count}");
        }

        public bool CanUndo => _undoStack.Count > 1; // Нужно минимум 2 состояния для отмены
        public bool CanRedo => _redoStack.Count > 0;

        public void Undo(Canvas canvas)
        {
            if (!CanUndo) return;

            System.Diagnostics.Debug.WriteLine($"=== UNDO ===");
            System.Diagnostics.Debug.WriteLine($"Before - Undo: {_undoStack.Count}, Redo: {_redoStack.Count}");

            // Сохраняем текущее состояние в redo
            var currentState = _undoStack.Pop();
            _redoStack.Push(currentState);

            // Берем предыдущее состояние (не текущее!)
            var previousState = _undoStack.Peek();

            System.Diagnostics.Debug.WriteLine($"Undo: {currentState.Shapes.Count} shapes -> {previousState.Shapes.Count} shapes");
            RestoreState(canvas, previousState);

            System.Diagnostics.Debug.WriteLine($"After - Undo: {_undoStack.Count}, Redo: {_redoStack.Count}");
        }

        public void Redo(Canvas canvas)
        {
            if (!CanRedo) return;

            System.Diagnostics.Debug.WriteLine($"=== REDO ===");
            System.Diagnostics.Debug.WriteLine($"Before - Undo: {_undoStack.Count}, Redo: {_redoStack.Count}");

            // Берем состояние из redo
            var nextState = _redoStack.Pop();

            // Сохраняем текущее состояние перед применением redo
            var currentState = new CanvasState(canvas, "Before Redo");
            _undoStack.Push(currentState);

            // Восстанавливаем состояние из redo
            RestoreState(canvas, nextState);

            System.Diagnostics.Debug.WriteLine($"Redo: {currentState.Shapes.Count} shapes -> {nextState.Shapes.Count} shapes");
            System.Diagnostics.Debug.WriteLine($"After - Undo: {_undoStack.Count}, Redo: {_redoStack.Count}");
        }

        public void ClearHistory()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }

        private void RestoreState(Canvas canvas, CanvasState state)
        {
            // Очищаем canvas
            canvas.Children.Clear();

            // Восстанавливаем все фигуры
            foreach (var shapeData in state.Shapes)
            {
                var shape = CreateShapeFromData(shapeData);
                if (shape != null)
                {
                    canvas.Children.Add(shape);
                }
            }
        }

        private Shape CreateShapeFromData(ShapeData data)
        {
            Shape shape = data.Type switch
            {
                ShapeType.Line => new Line(),
                ShapeType.Rectangle => new Rectangle(),
                ShapeType.Ellipse => new Ellipse(),
                ShapeType.Polygon => new Polygon(),
                _ => new Rectangle()
            };

            // Применяем свойства
            shape.Stroke = new SolidColorBrush(data.StrokeColor);
            shape.StrokeThickness = data.StrokeThickness;

            if (data.HasFill && !(shape is Line))
            {
                shape.Fill = new SolidColorBrush(data.FillColor);
            }
            else
            {
                shape.Fill = Brushes.Transparent;
            }

            // Устанавливаем геометрию
            switch (shape)
            {
                case Line line:
                    line.X1 = data.X1;
                    line.Y1 = data.Y1;
                    line.X2 = data.X2;
                    line.Y2 = data.Y2;
                    break;
                case Rectangle rect:
                    Canvas.SetLeft(rect, data.Left);
                    Canvas.SetTop(rect, data.Top);
                    rect.Width = data.Width;
                    rect.Height = data.Height;
                    break;
                case Ellipse ellipse:
                    Canvas.SetLeft(ellipse, data.Left);
                    Canvas.SetTop(ellipse, data.Top);
                    ellipse.Width = data.Width;
                    ellipse.Height = data.Height;
                    break;
                case Polygon polygon:
                    polygon.Points = new PointCollection(data.Points.Select(p => new Point(p.X, p.Y)));
                    break;
            }

            return shape;
        }
    }

    // Класс для хранения состояния canvas
    public class CanvasState
    {
        public List<ShapeData> Shapes { get; } = new List<ShapeData>();
        public string ActionName { get; }
        public DateTime Timestamp { get; }

        public CanvasState(Canvas canvas, string actionName)
        {
            ActionName = actionName;
            Timestamp = DateTime.Now;

            // Сохраняем все фигуры с canvas (исключая маркеры изменения размера)
            foreach (var child in canvas.Children)
            {
                if (child is Shape shape && !IsResizeHandle(shape) && !IsPreviewShape(shape))
                {
                    Shapes.Add(CreateShapeData(shape));
                }
            }
        }

        private ShapeData CreateShapeData(Shape shape)
        {
            var data = new ShapeData
            {
                StrokeColor = (shape.Stroke as SolidColorBrush)?.Color ?? Colors.Black,
                FillColor = (shape.Fill as SolidColorBrush)?.Color ?? Colors.Transparent,
                StrokeThickness = shape.StrokeThickness,
                HasFill = shape.Fill != Brushes.Transparent && shape.Fill != null
            };

            switch (shape)
            {
                case Line line:
                    data.Type = ShapeType.Line;
                    data.X1 = line.X1;
                    data.Y1 = line.Y1;
                    data.X2 = line.X2;
                    data.Y2 = line.Y2;
                    break;
                case Rectangle rect:
                    data.Type = ShapeType.Rectangle;
                    data.Left = Canvas.GetLeft(rect);
                    data.Top = Canvas.GetTop(rect);
                    data.Width = rect.Width;
                    data.Height = rect.Height;
                    break;
                case Ellipse ellipse:
                    data.Type = ShapeType.Ellipse;
                    data.Left = Canvas.GetLeft(ellipse);
                    data.Top = Canvas.GetTop(ellipse);
                    data.Width = ellipse.Width;
                    data.Height = ellipse.Height;
                    break;
                case Polygon polygon:
                    data.Type = ShapeType.Polygon;
                    data.Points = polygon.Points.Select(p => new PointData(p.X, p.Y)).ToList();
                    break;
            }

            return data;
        }

        private bool IsResizeHandle(Shape shape)
        {
            return shape is Rectangle rect && rect.Width == 8 && rect.Height == 8;
        }

        private bool IsPreviewShape(Shape shape)
        {
            // Игнорируем фигуры в режиме предпросмотра (пунктирные, полупрозрачные)
            return shape.StrokeDashArray != null && shape.StrokeDashArray.Count > 0 && shape.Opacity < 1.0;
        }
    }

    // Классы данных для сериализации состояния
    public class ShapeData
    {
        public ShapeType Type { get; set; }
        public Color StrokeColor { get; set; } = Colors.Black;
        public Color FillColor { get; set; } = Colors.Transparent;
        public double StrokeThickness { get; set; } = 2;
        public bool HasFill { get; set; }

        // Для Line
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }

        // Для Rectangle, Ellipse
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
}