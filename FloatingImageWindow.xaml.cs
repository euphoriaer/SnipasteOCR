// File: FloatingImageWindow.xaml.cs

using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SnipasteOCR
{
    public partial class FloatingImageWindow : Window
    {
        private double _zoomFactor = 1.0;
        private const double ZoomStep = 1.2;

        public BitmapSource Image => ImageControl.Source as BitmapSource;

        // ✅ 新增：构造函数支持传入屏幕坐标
        public FloatingImageWindow(BitmapSource image,Rect rect)
        {
            InitializeComponent();

            ImageControl.Source = image;

            // 设置窗口位置
            this.Left = rect.Left;
            this.Top = rect.Top;

            // 设置窗口大小为截图原始尺寸（考虑 DPI）
            if (image != null)
            {
                double dpiScaleX = image.DpiX / 96.0;
                double dpiScaleY = image.DpiY / 96.0;

                double width = image.PixelWidth * dpiScaleX;
                double height = image.PixelHeight * dpiScaleY;

                this.Width = width;
                this.Height = height;
            }

            // 滚轮缩放
            this.PreviewMouseWheel += (s, e) =>
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    e.Handled = true;
                    _zoomFactor = e.Delta > 0 ? _zoomFactor * ZoomStep : _zoomFactor / ZoomStep;
                    _zoomFactor = Math.Max(0.1, Math.Min(_zoomFactor, 10.0));
                    ZoomViewbox.LayoutTransform = new ScaleTransform(_zoomFactor, _zoomFactor);
                }
            };

            // 左键拖拽移动
            this.MouseDown += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    this.DragMove();
                }
            };

            // ESC 关闭
            this.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    this.Close();
                }
            };
        }

        // -----------------------------
        // 菜单事件（保持不变）
        // -----------------------------

        private void SaveImage_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PNG 文件 (*.png)|*.png|JPEG 文件 (*.jpg)|*.jpg|BMP 文件 (*.bmp)|*.bmp",
                FileName = $"Snip_{DateTime.Now:yyyyMMdd_HHmmss}.png",
                DefaultExt = ".png"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    using var fs = new FileStream(sfd.FileName, FileMode.Create);
                    BitmapEncoder encoder = sfd.FilterIndex switch
                    {
                        2 => new JpegBitmapEncoder(),
                        3 => new BmpBitmapEncoder(),
                        _ => new PngBitmapEncoder()
                    };
                    encoder.Frames.Add(BitmapFrame.Create(Image));
                    encoder.Save(fs);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"保存失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CopyToClipboard_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetImage(Image);
                MessageBox.Show("已复制到剪贴板", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"复制失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            _zoomFactor *= ZoomStep;
            _zoomFactor = Math.Max(0.1, Math.Min(_zoomFactor, 10.0));
            ZoomViewbox.LayoutTransform = new ScaleTransform(_zoomFactor, _zoomFactor);
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            _zoomFactor /= ZoomStep;
            _zoomFactor = Math.Max(0.1, Math.Min(_zoomFactor, 10.0));
            ZoomViewbox.LayoutTransform = new ScaleTransform(_zoomFactor, _zoomFactor);
        }

        private void ResetZoom_Click(object sender, RoutedEventArgs e)
        {
            _zoomFactor = 1.0;
            ZoomViewbox.LayoutTransform = new ScaleTransform(1.0, 1.0);
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // ESC 键关闭窗口
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.Close();
            }
            else if (e.Key == Key.OemPlus && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                ZoomIn_Click(sender, e);
            }
            else if (e.Key == Key.OemMinus && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                ZoomOut_Click(sender, e);
            }
            else if (e.Key == Key.D0 && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                ResetZoom_Click(sender, e);
            }
        }

    }
}