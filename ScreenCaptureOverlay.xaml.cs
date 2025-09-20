using SnipasteLikeApp.Utils;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SnipasteOCR
{
    public partial class ScreenCaptureOverlay : Window
    {

        private Point _startPoint;
        private Point _endPoint;
        private bool _isSelecting = false;

        public event Action<BitmapSource,Rect> OnCaptureCompleted;

        public ScreenCaptureOverlay()
        {
            InitializeComponent();
            Left = 0;
            Top = 0;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _startPoint = e.GetPosition(this);
                _isSelecting = true;
            }
        }

        private void Border_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isSelecting) return;

            _endPoint = e.GetPosition(this);

            double x = Math.Min(_startPoint.X, _endPoint.X);
            double y = Math.Min(_startPoint.Y, _endPoint.Y);
            double width = Math.Abs(_startPoint.X - _endPoint.X);
            double height = Math.Abs(_startPoint.Y - _endPoint.Y);

            SelectionRect.SetValue(Canvas.LeftProperty, x);
            SelectionRect.SetValue(Canvas.TopProperty, y);
            SelectionRect.Width = width;
            SelectionRect.Height = height;
            SelectionRect.Visibility = Visibility.Visible;

            SizeText.Text = $"{(int)width} × {(int)height}";
            SizeText.SetValue(Canvas.LeftProperty, x);
            SizeText.SetValue(Canvas.TopProperty, y - 15);
            SizeText.Visibility = Visibility.Visible;

            UpdateMask(x, y, width, height);
        }

        private void Border_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isSelecting && e.LeftButton == MouseButtonState.Released)
            {
                _isSelecting = false;

                var rect = GetSelectionRect();
                if (rect.Width > 5 && rect.Height > 5)
                {
                    var bitmap = ScreenCaptureHelper.CaptureRegion(rect);
                    OnCaptureCompleted?.Invoke(bitmap,rect);
                }

                Close();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                OnCaptureCompleted?.Invoke(null,Rect.Empty);
                Close();
            }
        }

        private void UpdateMask(double x, double y, double width, double height)
        {
            var regions = new[]
            {
                new Rect(0, 0, Width, y),
                new Rect(0, y, x, height),
                new Rect(x + width, y, Width - x - width, height),
                new Rect(0, y + height, Width, Height - y - height)
            };

            var geometry = new CombinedGeometry();
            foreach (var r in regions)
            {
                geometry = new CombinedGeometry(GeometryCombineMode.Union, geometry, new RectangleGeometry(r));
            }

            Mask.Clip = geometry;
        }

        private Rect GetSelectionRect()
        {
            double x = Math.Min(_startPoint.X, _endPoint.X);
            double y = Math.Min(_startPoint.Y, _endPoint.Y);
            double width = Math.Abs(_startPoint.X - _endPoint.X);
            double height = Math.Abs(_startPoint.Y - _endPoint.Y);
            return new Rect(x, y, width, height);
        }
    }
}