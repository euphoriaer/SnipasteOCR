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
    public static async Task<string> Recognize(BitmapSource bitmap)
    {
        // TODO: 替换为您实际的 OCR 实现
        // 示例：Tesseract、Windows.Media.Ocr、PaddleOCR SDK 等

        FullOcrModel model = LocalFullModels.ChineseV5;

        byte[] sampleImageData = BitmapSourceToByteArray(bitmap);


        using (PaddleOcrAll all = new PaddleOcrAll(model, PaddleDevice.Mkldnn())
        {
            AllowRotateDetection = true, /* 允许识别有角度的文字 */
            Enable180Classification = false, /* 允许识别旋转角度大于90度的文字 */
        })
        {
            // Load local file by following code:
            // using (Mat src2 = Cv2.ImRead(@"C:\test.jpg"))
            using (Mat src = Cv2.ImDecode(sampleImageData, ImreadModes.Color))
            {
                PaddleOcrResult result = all.Run(src);
                Console.WriteLine("Detected all texts: \n" + result.Text);

                // ✅ 返回识别文本，而不是打印到控制台
                return FormatOcrResult(result);

                //foreach (PaddleOcrResultRegion region in result.Regions)
                //{
                //    Console.WriteLine($"Text: {region.Text}, Score: {region.Score}, RectCenter: {region.Rect.Center}, RectSize:    {region.Rect.Size}, Angle: {region.Rect.Angle}");
                //}
            }
        }


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