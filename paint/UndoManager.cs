using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace paint
{
    public class UndoManager
    {
        private Stack<List<Shape>> _undoStack = new Stack<List<Shape>>();
        private Stack<List<Shape>> _redoStack = new Stack<List<Shape>>();
        private const int MaxHistorySteps = 5;

        public void SaveState(Canvas canvas, string actionName = "")
        {
            var shapes = GetCurrentShapes(canvas);

            System.Diagnostics.Debug.WriteLine($"=== SAVE STATE '{actionName}' ===");
            System.Diagnostics.Debug.WriteLine($"Shapes: {shapes.Count}, Undo stack: {_undoStack.Count + 1}");

            _undoStack.Push(shapes);
            _redoStack.Clear();

            if (_undoStack.Count > MaxHistorySteps)
            {
                var temp = new Stack<List<Shape>>();
                for (int i = 0; i < MaxHistorySteps; i++)
                {
                    temp.Push(_undoStack.Pop());
                }
                _undoStack = temp;
            }
        }

        public bool CanUndo => _undoStack.Count > 1; // Нужно минимум 2 состояния для отмены
        public bool CanRedo => _redoStack.Count > 0;

        public void Undo(Canvas canvas)
        {
            System.Diagnostics.Debug.WriteLine($"=== UNDO ===");
            System.Diagnostics.Debug.WriteLine($"Before - Undo: {_undoStack.Count}, Redo: {_redoStack.Count}");

            if (_undoStack.Count < 2)
            {
                System.Diagnostics.Debug.WriteLine("Need at least 2 states to undo");
                return;
            }

            // Убираем текущее состояние
            var currentState = _undoStack.Pop();

            // Берем предыдущее состояние
            var previousState = _undoStack.Peek();

            // Сохраняем текущее состояние в redo
            _redoStack.Push(currentState);

            System.Diagnostics.Debug.WriteLine($"Undo: {currentState.Count} shapes -> {previousState.Count} shapes");
            RestoreShapes(canvas, previousState);

            System.Diagnostics.Debug.WriteLine($"After - Undo: {_undoStack.Count}, Redo: {_redoStack.Count}");
        }

        public void Redo(Canvas canvas)
        {
            System.Diagnostics.Debug.WriteLine($"=== REDO ===");
            System.Diagnostics.Debug.WriteLine($"Before - Undo: {_undoStack.Count}, Redo: {_redoStack.Count}");

            if (_redoStack.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("Nothing to redo");
                return;
            }

            // Сохраняем текущее состояние в undo
            var currentState = GetCurrentShapes(canvas);
            _undoStack.Push(currentState);

            // Восстанавливаем из redo
            var nextState = _redoStack.Pop();

            System.Diagnostics.Debug.WriteLine($"Redo: {currentState.Count} shapes -> {nextState.Count} shapes");
            RestoreShapes(canvas, nextState);

            System.Diagnostics.Debug.WriteLine($"After - Undo: {_undoStack.Count}, Redo: {_redoStack.Count}");
        }

        private List<Shape> GetCurrentShapes(Canvas canvas)
        {
            var shapes = new List<Shape>();
            foreach (var child in canvas.Children)
            {
                if (child is Shape shape)
                {
                    if (shape is Rectangle rect && rect.Width == 8 && rect.Height == 8)
                        continue;
                    if (shape.StrokeDashArray != null && shape.StrokeDashArray.Count > 0)
                        continue;

                    shapes.Add(CloneShape(shape));
                }
            }
            return shapes;
        }

        private void RestoreShapes(Canvas canvas, List<Shape> shapes)
        {
            canvas.Children.Clear();
            foreach (var shape in shapes)
            {
                canvas.Children.Add(shape);
            }
        }

        private Shape CloneShape(Shape original)
        {
            if (original is Line line)
            {
                return new Line
                {
                    X1 = line.X1,
                    Y1 = line.Y1,
                    X2 = line.X2,
                    Y2 = line.Y2,
                    Stroke = line.Stroke,
                    StrokeThickness = line.StrokeThickness
                };
            }
            else if (original is Rectangle rect)
            {
                var newRect = new Rectangle
                {
                    Width = rect.Width,
                    Height = rect.Height,
                    Stroke = rect.Stroke,
                    Fill = rect.Fill,
                    StrokeThickness = rect.StrokeThickness
                };
                Canvas.SetLeft(newRect, Canvas.GetLeft(rect));
                Canvas.SetTop(newRect, Canvas.GetTop(rect));
                return newRect;
            }
            else if (original is Ellipse ellipse)
            {
                var newEllipse = new Ellipse
                {
                    Width = ellipse.Width,
                    Height = ellipse.Height,
                    Stroke = ellipse.Stroke,
                    Fill = ellipse.Fill,
                    StrokeThickness = ellipse.StrokeThickness
                };
                Canvas.SetLeft(newEllipse, Canvas.GetLeft(ellipse));
                Canvas.SetTop(newEllipse, Canvas.GetTop(ellipse));
                return newEllipse;
            }
            else if (original is Polygon polygon)
            {
                return new Polygon
                {
                    Points = new PointCollection(polygon.Points),
                    Stroke = polygon.Stroke,
                    Fill = polygon.Fill,
                    StrokeThickness = polygon.StrokeThickness
                };
            }

            return original;
        }
    }
}