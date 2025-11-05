using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Collections.Generic;

namespace paint.Commands
{
    // Команда добавления фигуры
    public class AddShapeCommand : IUndoCommand
    {
        private Canvas _canvas;
        private Shape _shape;
        private ShapeManager _shapeManager;

        public AddShapeCommand(Canvas canvas, Shape shape, ShapeManager shapeManager)
        {
            _canvas = canvas;
            _shape = CloneShape(shape);
            _shapeManager = shapeManager;
        }

        public void Execute()
        {
            _canvas.Children.Add(_shape);
            _shapeManager.AddShape(_shape);
        }

        public void Undo()
        {
            _canvas.Children.Remove(_shape);
            _shapeManager.ClearSelection();
        }

        // Создает копию фигуры для отмены/повтора
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
                    StrokeThickness = line.StrokeThickness,
                    Fill = line.Fill
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

    // Команда удаления фигуры
    public class RemoveShapeCommand : IUndoCommand
    {
        private Canvas _canvas;
        private Shape _shape;
        private ShapeManager _shapeManager;

        public RemoveShapeCommand(Canvas canvas, Shape shape, ShapeManager shapeManager)
        {
            _canvas = canvas;
            _shape = shape;
            _shapeManager = shapeManager;
        }

        public void Execute()
        {
            _canvas.Children.Remove(_shape);
            _shapeManager.ClearSelection();
        }

        public void Undo()
        {
            _canvas.Children.Add(_shape);
            _shapeManager.AddShape(_shape);
        }
    }

    // Команда перемещения фигуры
    public class MoveShapeCommand : IUndoCommand
    {
        private Shape _shape;
        private Point _oldPosition;
        private Point _newPosition;
        private ShapeManager _shapeManager;

        public MoveShapeCommand(Shape shape, Point oldPosition, Point newPosition, ShapeManager shapeManager)
        {
            _shape = shape;
            _oldPosition = oldPosition;
            _newPosition = newPosition;
            _shapeManager = shapeManager;
        }

        public void Execute()
        {
            MoveTo(_newPosition);
        }

        public void Undo()
        {
            MoveTo(_oldPosition);
        }

        private void MoveTo(Point position)
        {
            if (_shape is Line line)
            {
                double deltaX = position.X - _newPosition.X;
                double deltaY = position.Y - _newPosition.Y;

                line.X1 += deltaX;
                line.Y1 += deltaY;
                line.X2 += deltaX;
                line.Y2 += deltaY;
            }
            else if (_shape is Polygon polygon)
            {
                PointCollection newPoints = new PointCollection();
                foreach (Point point in polygon.Points)
                {
                    double deltaX = position.X - _newPosition.X;
                    double deltaY = position.Y - _newPosition.Y;
                    newPoints.Add(new Point(point.X + deltaX, point.Y + deltaY));
                }
                polygon.Points = newPoints;
            }
            else
            {
                Canvas.SetLeft(_shape, position.X);
                Canvas.SetTop(_shape, position.Y);
            }

            _shapeManager.UpdateSelectionMarkers();
        }
    }

    // Команда изменения размера фигуры
    public class ResizeShapeCommand : IUndoCommand
    {
        private Shape _shape;
        private Rect _oldBounds;
        private Rect _newBounds;
        private ShapeManager _shapeManager;

        public ResizeShapeCommand(Shape shape, Rect oldBounds, Rect newBounds, ShapeManager shapeManager)
        {
            _shape = shape;
            _oldBounds = oldBounds;
            _newBounds = newBounds;
            _shapeManager = shapeManager;
        }

        public void Execute()
        {
            ResizeTo(_newBounds);
        }

        public void Undo()
        {
            ResizeTo(_oldBounds);
        }

        private void ResizeTo(Rect bounds)
        {
            if (_shape is Line line)
            {
                line.X1 = bounds.Left;
                line.Y1 = bounds.Top;
                line.X2 = bounds.Right;
                line.Y2 = bounds.Bottom;
            }
            else if (_shape is Polygon polygon)
            {
                double scaleX = bounds.Width / _oldBounds.Width;
                double scaleY = bounds.Height / _oldBounds.Height;

                PointCollection newPoints = new PointCollection();
                foreach (Point point in polygon.Points)
                {
                    double newX = bounds.Left + (point.X - _oldBounds.Left) * scaleX;
                    double newY = bounds.Top + (point.Y - _oldBounds.Top) * scaleY;
                    newPoints.Add(new Point(newX, newY));
                }
                polygon.Points = newPoints;
            }
            else
            {
                Canvas.SetLeft(_shape, bounds.Left);
                Canvas.SetTop(_shape, bounds.Top);
                _shape.Width = bounds.Width;
                _shape.Height = bounds.Height;
            }

            _shapeManager.UpdateSelectionMarkers();
        }
    }

    // Команда изменения свойств фигуры
    public class ChangePropertiesCommand : IUndoCommand
    {
        private Shape _shape;
        private ShapeProperties _oldProperties;
        private ShapeProperties _newProperties;
        private ShapeManager _shapeManager;

        public ChangePropertiesCommand(Shape shape, ShapeProperties oldProperties, ShapeProperties newProperties, ShapeManager shapeManager)
        {
            _shape = shape;
            _oldProperties = oldProperties;
            _newProperties = newProperties;
            _shapeManager = shapeManager;
        }

        public void Execute()
        {
            ApplyProperties(_newProperties);
            _shapeManager.SelectShape(_shape);
        }

        public void Undo()
        {
            ApplyProperties(_oldProperties);
            _shapeManager.SelectShape(_shape);
        }

        private void ApplyProperties(ShapeProperties properties)
        {
            ShapeFactory.ApplyProperties(_shape, properties);
            _shapeManager.UpdateSelectionMarkers();
        }
    }
}