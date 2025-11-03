namespace paint
{
    public enum ShapeType
    {
        Line,
        Rectangle,
        Square,
        Ellipse,
        Circle,
        Polygon
    }

    // Состояние рисования многоугольника
    public enum PolygonState
    {
        NotStarted,
        Drawing,
        Completing
    }
}