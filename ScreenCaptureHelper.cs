using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.Runtime.InteropServices;

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

    private const uint SRCCOPY = 0x00CC0020; // 位图复制

    /// <summary>
    /// 截取指定区域的屏幕（精确、冻结、无遮罩污染）
    /// </summary>
    /// <param name="rect">要截取的区域（WPF 坐标）</param>
    /// <returns>BitmapSource 图像</returns>
    public static BitmapSource CaptureRegion(System.Windows.Rect rect)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
            return null;

        IntPtr deskHwnd = GetDesktopWindow();
        IntPtr deskHdc = GetWindowDC(deskHwnd);
        if (deskHdc == IntPtr.Zero)
            return null;

        IntPtr memHdc = IntPtr.Zero;
        IntPtr hBitmap = IntPtr.Zero;
        IntPtr hOldObj = IntPtr.Zero;
        BitmapSource result = null;

        try
        {
            memHdc = CreateCompatibleDC(deskHdc);
            if (memHdc == IntPtr.Zero)
                return null;

            int width = (int)SystemParameters.VirtualScreenWidth;
            int height = (int)SystemParameters.VirtualScreenHeight;

            hBitmap = CreateCompatibleBitmap(deskHdc, width, height);
            if (hBitmap == IntPtr.Zero)
                return null;

            hOldObj = SelectObject(memHdc, hBitmap);

            // ✅ 关键：BitBlt 从显卡帧缓冲复制，画面“冻结”
            bool success = BitBlt(memHdc, 0, 0, width, height, deskHdc, 0, 0, SRCCOPY);
            if (!success)
                return null;

            // 转为 .NET Bitmap
            using (var screenBitmap = System.Drawing.Image.FromHbitmap(hBitmap))
            {
                // 裁剪到目标区域
                var cropRect = new Rectangle(
                    (int)rect.X,
                    (int)rect.Y,
                    (int)rect.Width,
                    (int)rect.Height);

                // 边界保护
                cropRect.X = Math.Max(0, Math.Min(cropRect.X, screenBitmap.Width - 1));
                cropRect.Y = Math.Max(0, Math.Min(cropRect.Y, screenBitmap.Height - 1));
                cropRect.Width = Math.Max(1, Math.Min(cropRect.Width, screenBitmap.Width - cropRect.X));
                cropRect.Height = Math.Max(1, Math.Min(cropRect.Height, screenBitmap.Height - cropRect.Y));

                using (var cropped = screenBitmap.Clone(cropRect, screenBitmap.PixelFormat))
                {
                    IntPtr hCropped = cropped.GetHbitmap();
                    try
                    {
                        result = Imaging.CreateBitmapSourceFromHBitmap(
                            hCropped,
                            IntPtr.Zero,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());

                        result.Freeze(); // ✅ 冻结，支持跨线程使用
                    }
                    finally
                    {
                        DeleteObject(hCropped);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"截图失败: {ex.Message}");
            return null;
        }
        finally
        {
            if (hOldObj != IntPtr.Zero && hBitmap != IntPtr.Zero)
            {
                SelectObject(memHdc, hOldObj); // 恢复设备上下文
            }
            if (hBitmap != IntPtr.Zero)
            {
                DeleteObject(hBitmap);
            }
            if (memHdc != IntPtr.Zero)
            {
                DeleteDC(memHdc);
            }
            if (deskHdc != IntPtr.Zero)
            {
                ReleaseDC(deskHwnd, deskHdc);
            }
        }

        return result;
    }

    /// <summary>
    /// 截取全屏（返回冻结的 BitmapSource）
    /// </summary>
    public static BitmapSource CaptureFullScreen()
    {
        IntPtr deskHwnd = GetDesktopWindow();
        IntPtr deskHdc = GetWindowDC(deskHwnd);
        if (deskHdc == IntPtr.Zero) return null;

        IntPtr memHdc = IntPtr.Zero;
        IntPtr hBitmap = IntPtr.Zero;
        IntPtr hOldObj = IntPtr.Zero;
        BitmapSource result = null;

        try
        {
            int width = (int)SystemParameters.VirtualScreenWidth;
            int height = (int)SystemParameters.VirtualScreenHeight;

            memHdc = CreateCompatibleDC(deskHdc);
            hBitmap = CreateCompatibleBitmap(deskHdc, width, height);
            if (hBitmap == IntPtr.Zero) return null;

            hOldObj = SelectObject(memHdc, hBitmap);

            // ✅ 关键：立即复制全屏，画面“冻结”
            bool success = BitBlt(memHdc, 0, 0, width, height, deskHdc, 0, 0, SRCCOPY);
            if (!success) return null;

            // 转为 WPF 图像并冻结
            result = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(width, height));

            result.Freeze(); // ✅ 支持跨线程
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"全屏截图失败: {ex.Message}");
        }
        finally
        {
            if (hOldObj != IntPtr.Zero) SelectObject(memHdc, hOldObj);
            if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
            if (memHdc != IntPtr.Zero) DeleteDC(memHdc);
            if (deskHdc != IntPtr.Zero) ReleaseDC(deskHwnd, deskHdc);
        }

        return result;
    }
}