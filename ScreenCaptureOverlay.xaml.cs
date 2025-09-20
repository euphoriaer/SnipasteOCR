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
        private Point? _startPoint;
        private bool _isSelecting = false;
        private BitmapSource _frozenBackground; // ✅ 保存冻结画面
        public bool IsFrozenScreen=true;
        public event Action<BitmapSource, Rect> OnCaptureCompleted;

        public ScreenCaptureOverlay()
        {
            InitializeComponent();

            // 使用 SystemInformation 获取真实屏幕坐标（推荐）
            // 如果不想引用 WinForms，请用 SystemParameters（注意 DPI 问题）
            var screenWidth = SystemParameters.VirtualScreenWidth;
            var screenHeight = SystemParameters.VirtualScreenHeight;

            // ✅ 设置冻结画面为窗口背景
            if(IsFrozenScreen)
            {
                _frozenBackground = ScreenCaptureHelper.CaptureFullScreen();
                this.Background = new ImageBrush(_frozenBackground);
            }
            

            this.Left = 0;
            this.Top = 0;
            this.Width = screenWidth;
            this.Height = screenHeight;

            // 初始化遮罩
            this.Loaded += (s, e) => UpdateHoleMask();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _startPoint = e.GetPosition(this);
                _isSelecting = true;
            }
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isSelecting) return;

            var point = e.GetPosition(this);

            double x = Math.Min(_startPoint.Value.X, point.X);
            double y = Math.Min(_startPoint.Value.Y, point.Y);
            double width = Math.Abs(_startPoint.Value.X - point.X);
            double height = Math.Abs(_startPoint.Value.Y - point.Y);

            // 更新选区
            Canvas.SetLeft(SelectionRect, x);
            Canvas.SetTop(SelectionRect, y);
            SelectionRect.Width = width;
            SelectionRect.Height = height;
            SelectionRect.Visibility = Visibility.Visible;

            // 更新尺寸
            SizeText.Text = $"{(int)width} × {(int)height}";
            Canvas.SetLeft(SizeText, x);
            Canvas.SetTop(SizeText, y - 25);
            SizeText.Visibility = Visibility.Visible;

            // 更新“挖孔”遮罩
            UpdateHoleMask(x, y, width, height);
        }

        private void Border_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isSelecting && e.LeftButton == MouseButtonState.Released)
            {
                _isSelecting = false;

                var rect = GetSelectionRect();

                // ✅ 向内收缩 1px，避开边框
                rect.Inflate(-1, -1);

                if (rect.Width > 5 && rect.Height > 5)
                {
                    var bitmap = ScreenCaptureHelper.CaptureRegion(rect);
                    OnCaptureCompleted?.Invoke(bitmap, rect);
                }

                Close();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                OnCaptureCompleted?.Invoke(null, Rect.Empty);
                Close();
            }
        }

        // 创建“挖孔”遮罩：全屏暗色，中间挖出选区
        private void UpdateHoleMask(double x = 0, double y = 0, double width = 0, double height = 0)
        {
            var fullRect = new Rect(0, 0, this.ActualWidth, this.ActualHeight);
            var selectionRect = new Rect(x, y, width, height);

            var combined = new CombinedGeometry(
                GeometryCombineMode.Exclude,
                new RectangleGeometry(fullRect),
                new RectangleGeometry(selectionRect)
            );

            HoleMask.Data = combined;
        }

        private Rect GetSelectionRect()
        {
            return new Rect(
                Canvas.GetLeft(SelectionRect),
                Canvas.GetTop(SelectionRect),
                SelectionRect.Width,
                SelectionRect.Height
            );
        }
    }
}