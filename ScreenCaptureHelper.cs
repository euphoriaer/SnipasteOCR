// File: Utils/ScreenCaptureHelper.cs

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace SnipasteLikeApp.Utils
{
    public static class ScreenCaptureHelper
    {
        // --- P/Invoke 声明 ---
        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();
        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern IntPtr ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hDC);
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);
        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hDestDC, int x, int y, int nWidth, int nHeight, IntPtr hSrcDC, int xSrc, int ySrc, uint dwRop);
        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hDC);
        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private const uint SRCCOPY = 0x00CC0020; // 位图复制常量

        /// <summary>
        /// 截取指定区域的屏幕
        /// </summary>
        /// <param name="rect">要截取的区域（WPF 坐标）</param>
        /// <returns>BitmapSource 图像</returns>
        public static BitmapSource CaptureRegion(System.Windows.Rect rect)
        {
            if (rect.Width <= 0 || rect.Height <= 0)
                return null;

            IntPtr deskHwnd = GetDesktopWindow();
            IntPtr deskHdc = GetWindowDC(deskHwnd); // 获取整个屏幕的设备上下文

            if (deskHdc == IntPtr.Zero)
                return null;

            try
            {
                // 创建内存设备上下文
                IntPtr memHdc = CreateCompatibleDC(deskHdc);
                if (memHdc == IntPtr.Zero)
                    return null;

                try
                {
                    // 创建与屏幕兼容的位图
                    int width = (int)SystemParameters.VirtualScreenWidth;
                    int height = (int)SystemParameters.VirtualScreenHeight;
                    IntPtr hBitmap = CreateCompatibleBitmap(deskHdc, width, height);
                    if (hBitmap == IntPtr.Zero)
                        return null;

                    // 将位图选入内存 DC
                    IntPtr oldObj = SelectObject(memHdc, hBitmap);

                    // 执行位块传输（复制屏幕到内存位图）
                    bool result = BitBlt(memHdc, 0, 0, width, height, deskHdc, 0, 0, SRCCOPY);
                    if (!result)
                        return null;

                    // 创建 .NET Bitmap 来操作 GDI 位图
                    using var gdiBitmap =  System.Drawing.Image.FromHbitmap(hBitmap);
                    // 裁剪到指定区域
                    var cropRect = new System.Drawing.Rectangle(
                        (int)rect.X,
                        (int)rect.Y,
                        (int)rect.Width,
                        (int)rect.Height);

                    // 防止越界
                    cropRect.X = Math.Max(0, Math.Min(cropRect.X, gdiBitmap.Width - 1));
                    cropRect.Y = Math.Max(0, Math.Min(cropRect.Y, gdiBitmap.Height - 1));
                    cropRect.Width = Math.Max(1, Math.Min(cropRect.Width, gdiBitmap.Width - cropRect.X));
                    cropRect.Height = Math.Max(1, Math.Min(cropRect.Height, gdiBitmap.Height - cropRect.Y));

                    using (var cropped = gdiBitmap.Clone(cropRect, gdiBitmap.PixelFormat))
                    {
                        // 转为 WPF 的 BitmapSource
                        IntPtr hCroppedBitmap = cropped.GetHbitmap();
                        try
                        {
                            var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                                hCroppedBitmap,
                                IntPtr.Zero,
                                Int32Rect.Empty,
                                BitmapSizeOptions.FromEmptyOptions());

                            bitmapSource.Freeze(); // 冻结以跨线程使用
                            return bitmapSource;
                        }
                        finally
                        {
                            DeleteObject(hCroppedBitmap); // 必须释放 HBitmap
                        }
                    }
                }
                finally
                {
                    DeleteDC(memHdc);
                }
            }
            finally
            {
                ReleaseDC(deskHwnd, deskHdc);
            }
        }
    }
}