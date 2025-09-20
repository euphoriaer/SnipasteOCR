// 可选：如果您还没有这个类
using OpenCvSharp;
using Sdcb.PaddleInference;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models;
using Sdcb.PaddleOCR.Models.Local;
using System.IO;
using System.Net.Http;
using System.Windows.Media.Imaging;
using System.IO;
using System.Windows.Media.Imaging;

public static class OcrHelper
{
    /// <summary>
    /// 异步执行 OCR 识别（在后台线程运行）
    /// </summary>
    public static async Task<string> Recognize(BitmapSource bitmap)
    {
        if (bitmap == null)
            throw new ArgumentNullException(nameof(bitmap));

        // 将整个 OCR 流程放到后台线程
        return await Task.Run(() =>
        {
            try
            {
                FullOcrModel model = LocalFullModels.ChineseV5;
                byte[] sampleImageData = BitmapSourceToByteArray(bitmap);

                using (PaddleOcrAll all = new PaddleOcrAll(model, PaddleDevice.Mkldnn())
                {
                    AllowRotateDetection = true,
                    Enable180Classification = false,
                })
                using (Mat src = Cv2.ImDecode(sampleImageData, ImreadModes.Color))
                {
                    // ✅ 同步调用，但在后台线程，不会卡 UI
                    PaddleOcrResult result = all.Run(src);
                    return FormatOcrResult(result);
                }
            }
            catch (Exception ex)
            {
                // 捕获异常并抛出，以便上层处理
                throw new InvalidOperationException("OCR 识别失败", ex);
            }
        });
    }


    /// <summary>
    /// 格式化 OCR 结果，保留段落结构
    /// </summary>
    private static string FormatOcrResult(PaddleOcrResult result)
    {
        if (result?.Regions == null || result.Regions.Length == 0)
            return "未识别到有效文字。";

        var lines = new System.Collections.Generic.List<string>();

        // 按 Y 坐标排序（从上到下）
        var sortedRegions = result.Regions
            .OrderBy(r => r.Rect.Center.Y)
            .ThenBy(r => r.Rect.Center.X)
            .ToArray();

        double? lastY = null;
        const double lineSpacingThreshold = 30; // 根据字体大小调整

        foreach (var region in sortedRegions)
        {
            double currentY = region.Rect.Center.Y;
            string text = region.Text?.Trim();

            if (string.IsNullOrEmpty(text))
                continue;

            // 判断是否换行（段落）
            if (lastY.HasValue && (currentY - lastY.Value) > lineSpacingThreshold)
            {
                lines.Add(""); // 换段
            }

            lines.Add(text);
            lastY = currentY;
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// 将 WPF BitmapSource 转为 byte[]
    /// </summary>
    private static byte[] BitmapSourceToByteArray(BitmapSource bitmapSource)
    {
        if (bitmapSource == null)
            return null;

        using (var stream = new MemoryStream())
        {
            var encoder = new PngBitmapEncoder(); // 使用 PNG 避免压缩失真
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
            encoder.Save(stream);
            return stream.ToArray();
        }
    }
}