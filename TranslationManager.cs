// TranslationManager.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SnipasteOCR
{
    public static class TranslationManager
    {
        private static bool _isInitialized = false;
        private static readonly object _lock = new object();

        // 初始化（可加载模型等）
        public static void Initialize()
        {
            lock (_lock)
            {
                if (_isInitialized) return;

                // 如果你有模型加载逻辑，放在这里
                // 例如：检查 Models/ 目录是否存在模型文件

                _isInitialized = true;
            }
        }

        /// <summary>
        /// 翻译文本（中英互译）
        /// </summary>
        public static async Task<string> TranslateAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            return await Task.Run(() =>
            {
                try
                {
                    // 初始化（首次调用）
                    if (!_isInitialized)
                        Initialize();

                    // 这里是“伪翻译”逻辑，你可以替换为真实模型
                    return FallbackTranslate(text);
                }
                catch
                {
                    // 万一出错，返回一个基础翻译
                    return "[翻译失败] " + text;
                }
            });
        }

        #region ✅ 离线翻译逻辑（当前为“规则+词典”保底方案）

        // 一个简单的中英词典（可用于演示）
        private static readonly Dictionary<string, string> SimpleDict = new Dictionary<string, string>
        {
            { "今天", "Today" },
            { "天气", "weather" },
            { "真", "really" },
            { "好", "good" },
            { "昨天", "Yesterday" },
            { "不错", "nice" },
            { "谢谢", "Thank you" },
            { "你好", "Hello" },
            { "电脑", "computer" },
            { "图片", "image" },
            { "识别", "recognize" },
            { "翻译", "translate" },
            { "钉图", "Pinned Image" },
            { "OCR", "OCR" },
            { "Snipaste", "Snipaste" }
        };

        /// <summary>
        /// 保底翻译：基于词典的简单替换（适合无模型时测试）
        /// </summary>
        private static string FallbackTranslate(string text)
        {
            // 判断是中文还是英文
            if (ContainsChinese(text))
            {
                // 中文 → 英文
                foreach (var kv in SimpleDict)
                {
                    text = text.Replace(kv.Key, kv.Value);
                }
                return text;
            }
            else
            {
                // 英文 → 中文（反向查找）
                foreach (var kv in SimpleDict)
                {
                    text = text.Replace(kv.Value, kv.Key);
                }
                return text;
            }
        }

        private static bool ContainsChinese(string text)
        {
            foreach (char c in text)
            {
                if (c >= 0x4E00 && c <= 0x9FFF) // 中文 Unicode 范围
                    return true;
            }
            return false;
        }

        #endregion
    }
}