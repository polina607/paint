using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml;
using System.Collections.Generic;

namespace paint
{
    // Сериализация и десериализация проектов
    public static class ProjectSerializer
    {
        // Сохраняет проект в файл
        public static void SaveProjectToFile(string filename, Canvas canvas)
        {
            var project = new PaintProject
            {
                CanvasWidth = canvas.Width,
                CanvasHeight = canvas.Height
            };

            // Собираем все фигуры с холста
            foreach (var child in canvas.Children)
            {
                if (child is Shape shape && !IsResizeHandle(shape))
                {
                    project.Shapes.Add(CreateShapeData(shape));
                }
            }

            // Сохраняем в XML
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
        }

        // Загружает проект из файла
        public static PaintProject LoadProjectFromFile(string filename)
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

        // Создает данные фигуры для сериализации
        public static ShapeData CreateShapeData(Shape shape)
        {
            var data = new ShapeData
            {
                StrokeColor = ColorToHex((shape.Stroke as SolidColorBrush)?.Color ?? Colors.Black),
                FillColor = ColorToHex((shape.Fill as SolidColorBrush)?.Color ?? Colors.Transparent),
                StrokeThickness = shape.StrokeThickness,
                HasFill = shape.Fill != Brushes.Transparent && shape.Fill != null
            };

            // Заполняем данные в зависимости от типа фигуры
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

        // Создает фигуру из данных
        public static Shape CreateShapeFromData(ShapeData data)
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

            // Восстанавливаем геометрию в зависимости от типа фигуры
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

        // Записывает данные фигуры в XML
        private static void WriteShapeDataToXml(XmlWriter writer, ShapeData data)
        {
            writer.WriteStartElement("Shape");
            writer.WriteAttributeString("Type", data.Type.ToString());
            writer.WriteAttributeString("StrokeColor", data.StrokeColor);
            writer.WriteAttributeString("FillColor", data.FillColor);
            writer.WriteAttributeString("StrokeThickness", data.StrokeThickness.ToString());
            writer.WriteAttributeString("HasFill", data.HasFill.ToString());

            // Записываем данные в зависимости от типа фигуры
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

        // Читает данные фигуры из XML
        private static ShapeData ReadShapeDataFromXml(XmlReader reader)
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

        // Читает точки многоугольника из XML
        private static void ReadPointsFromXml(XmlReader reader, List<PointData> points)
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

        // Конвертирует цвет в HEX строку
        private static string ColorToHex(Color color)
        {
            return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        // Конвертирует HEX строку в цвет
        private static Color HexToColor(string hex)
        {
            hex = hex.Replace("#", "");

            byte a = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            byte r = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);

            return Color.FromArgb(a, r, g, b);
        }

        // Проверяет, является ли фигура маркером изменения размера
        private static bool IsResizeHandle(Shape shape)
        {
            return shape is Rectangle rect && rect.Width == 8 && rect.Height == 8;
        }
    }
}