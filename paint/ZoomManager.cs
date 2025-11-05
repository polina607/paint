using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace paint
{
    // Управление масштабированием холста
    public class ZoomManager
    {
        private ScrollViewer _scrollViewer;
        private Canvas _canvas;
        private double _zoom = 1.0;

        // Настройки масштабирования
        private const double MinZoom = 0.1;
        private const double MaxZoom = 5.0;
        private const double ZoomStep = 0.1;

        public ZoomManager(ScrollViewer scrollViewer, Canvas canvas)
        {
            _scrollViewer = scrollViewer;
            _canvas = canvas;
            InitializeZoom();
        }

        // Увеличивает масштаб
        public void ZoomIn()
        {
            ChangeZoom(_zoom + ZoomStep);
        }

        // Уменьшает масштаб
        public void ZoomOut()
        {
            ChangeZoom(_zoom - ZoomStep);
        }

        // Сбрасывает масштаб к 100%
        public void ZoomReset()
        {
            ChangeZoom(1.0);
        }

        // Обрабатывает колесико мыши с зажатым Ctrl
        public void HandleMouseWheel(MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
                double zoomFactor = e.Delta > 0 ? ZoomStep : -ZoomStep;
                ChangeZoom(_zoom + zoomFactor);
            }
        }

        // Получает текущий уровень масштабирования
        public double GetZoomLevel()
        {
            return _zoom;
        }

        // Получает текстовое представление масштаба
        public string GetZoomText()
        {
            return $"{_zoom * 100:0}%";
        }

        private void InitializeZoom()
        {
            ApplyZoomTransform();
        }

        private void ChangeZoom(double newZoom)
        {
            // Ограничиваем масштаб минимальным и максимальным значениями
            newZoom = Math.Max(MinZoom, Math.Min(MaxZoom, newZoom));

            // Сохраняем центр прокрутки для плавного масштабирования
            Point scrollCenter = new Point(
                _scrollViewer.HorizontalOffset + _scrollViewer.ViewportWidth / 2,
                _scrollViewer.VerticalOffset + _scrollViewer.ViewportHeight / 2
            );

            _zoom = newZoom;
            ApplyZoomTransform();

            // Обновляем позицию прокрутки после масштабирования
            Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
            {
                _scrollViewer.ScrollToHorizontalOffset(scrollCenter.X * (_zoom / newZoom) - _scrollViewer.ViewportWidth / 2);
                _scrollViewer.ScrollToVerticalOffset(scrollCenter.Y * (_zoom / newZoom) - _scrollViewer.ViewportHeight / 2);
            }), DispatcherPriority.Background);
        }

        private void ApplyZoomTransform()
        {
            ScaleTransform scaleTransform = new ScaleTransform(_zoom, _zoom);
            _canvas.LayoutTransform = scaleTransform;
        }
    }
}