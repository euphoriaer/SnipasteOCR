using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SnipasteOCR
{
    public partial class FloatingImageWindow : Window
    {
        private double _zoomFactor = 1.0;
        private const double ZoomStep = 1.2;
        private Rect _rect;

        public BitmapSource Image => ImageControl.Source as BitmapSource;

        // 构造函数，接收图像和屏幕矩形
        public FloatingImageWindow(BitmapSource image, Rect screenRect)
        {
            InitializeComponent();

            // ✅ 确保阴影默认开启（即使 XAML 已设置）
            ToggleGlowMenuItem.IsChecked = true;

            // 设置窗口位置
            this.Left = screenRect.X;
            this.Top = screenRect.Y;
            _rect=screenRect;
            // 设置窗口大小为原始像素尺寸（考虑 DPI）
            if (image != null)
            {
                double dpiScaleX = image.DpiX / 96.0;
                double dpiScaleY = image.DpiY / 96.0;

                double width = image.PixelWidth * dpiScaleX;
                double height = image.PixelHeight * dpiScaleY;

                this.Width = width;
                this.Height = height;
            }

            // 设置图像源
            ImageControl.Source = image;

            // 注册事件
            this.PreviewMouseWheel += FloatingImageWindow_PreviewMouseWheel;
            this.MouseDown += Window_MouseDown;
            this.KeyDown += Window_KeyDown; // 确保注册了 KeyDown 事件
        }

        // 处理鼠标滚轮缩放
        private void FloatingImageWindow_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {

            e.Handled = true;

            if (e.Delta > 0)
            {
                _zoomFactor *= ZoomStep;
            }
            else
            {
                _zoomFactor /= ZoomStep;
            }

            ApplyZoom();

        }

        private void ApplyZoom()
        {
            _zoomFactor = Math.Max(0.1, Math.Min(_zoomFactor, 10.0)); // 限制缩放范围

            // ✅ 1. 设置缩放变换：以左上角 (0,0) 为中心
            var transform = new ScaleTransform(_zoomFactor, _zoomFactor)
            {
                CenterX = 0,
                CenterY = 0
            };

            // ✅ 2. 更新窗口大小
            if (Image != null)
            {
                double newWidth = Image.PixelWidth * _zoomFactor;
                double newHeight = Image.PixelHeight * _zoomFactor;

                this.Width = newWidth;
                this.Height = newHeight;
                this.Left = _rect.X;
                this.Top = _rect.Y;
            }
            ImageControl.RenderTransformOrigin = new Point(0, 0); // 确保变换原点是左上角
            ImageControl.RenderTransform = transform;
            
            // ✅ 3. 可选：显示缩放百分比（右上角）
            ShowZoomPercentage((int)(_zoomFactor * 100));
        }

        private void ShowZoomPercentage(int percentage)
        {
            ZoomPercentageText.Text = $"{percentage}%";
            ZoomPercentageText.Visibility = Visibility.Visible;

            // 清除之前的定时器
            if (_zoomTimer != null)
            {
                _zoomTimer.Stop();
                _zoomTimer = null;
            }

            _zoomTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _zoomTimer.Tick += (s, e) =>
            {
                ZoomPercentageText.Visibility = Visibility.Collapsed;
                _zoomTimer.Stop();
                _zoomTimer = null;
            };
            _zoomTimer.Start();
        }

        private DispatcherTimer _zoomTimer;

        // 拖拽移动窗口
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        // ESC 键关闭窗口
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.Close();
            }
            else if (e.Key == Key.OemPlus)
            {
                ZoomIn_Click(sender, e);
            }
            else if (e.Key == Key.OemMinus)
            {
                ZoomOut_Click(sender, e);
            }
            else if (e.Key == Key.D0)
            {
                ResetZoom_Click(sender, e);
            }
        }

        // 菜单点击事件处理
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
                
            }
            catch (Exception ex)
            {
                MessageBox.Show($"复制失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            _zoomFactor *= ZoomStep;
            ApplyZoom();
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            _zoomFactor /= ZoomStep;
            ApplyZoom();
        }

        private void ResetZoom_Click(object sender, RoutedEventArgs e)
        {
            _zoomFactor = 1.0;
            ApplyZoom();
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private TextPopupWindow ShowOcrResult(string text)
        {
            var popup = new TextPopupWindow(text);
            popup.Owner = this; // 可选：设置所有者
            popup.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            popup.Show(); // 非模态，允许其他操作
            return popup;
        }

        private async void OcrRecognize_Click(object sender, RoutedEventArgs e)
        {
            if (Image == null) return;

            // ✅ 1. 显示“正在识别”提示（非模态）
            var progressPopup = ShowOcrResult("正在识别文字..."); 
            

            try
            {
                // ✅ 2. 异步调用 OCR（已在后台线程）
                string result = await OcrHelper.Recognize(Image);

                // ✅ 3. 关闭进度提示
                

                if (string.IsNullOrWhiteSpace(result))
                {
                    MessageBox.Show("未识别到任何文字。", "OCR 结果", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // ✅ 4. 显示结果（富文本）
                progressPopup.SetText(result);

            }
            catch (Exception ex)
            {
                progressPopup.Close();
                MessageBox.Show($"OCR 识别失败：{ex.Message}\n\n详情：{ex.InnerException?.Message}",
                               "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        // 右键菜单：切换边缘阴影
        private void ToggleGlow_Click(object sender, RoutedEventArgs e)
        {
            bool showGlow = ToggleGlowMenuItem.IsChecked;

            if (showGlow)
            {
                GlowBorder.Effect = FindResource("GlowEffect") as DropShadowEffect;
            }
            else
            {
                GlowBorder.Effect = null;
            }
        }

        private async void Transform_Click(object sender, RoutedEventArgs e)
        {
            var progressPopup = ShowOcrResult("正在翻译...");
            var result = await OcrHelper.Recognize(Image);
            var translated = await TranslationManager.TranslateAsync(result);
            progressPopup.SetText($"原文：{result}\n\n\n译文：{translated}");
      
        }
    }
}