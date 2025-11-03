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
        NotStarted,    // Не начали рисовать
        Drawing,       // Рисуем линии
        Completing     // Завершаем (последняя точка к первой)
    }
}