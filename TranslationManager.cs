// TranslationManager.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace SnipasteOCR
{
    public static class TranslationManager
    {
        private static InferenceSession _session;
        private static HashSet<string> _sourceVocab;
        private static List<string> _targetVocab;
        private static bool _isInitialized = false;
        private static readonly object _lock = new object();

        // 模型路径
        private const string ModelPath = "Models/translator.onnx";
        private const string SrcVocabPath = "Models/src_vocabulary.txt";
        private const string TgtVocabPath = "Models/tgt_vocabulary.txt";

        public static void Initialize()
        {
            lock (_lock)
            {
                if (_isInitialized) return;

                try
                {
                    if (!File.Exists(ModelPath))
                        throw new FileNotFoundException($"ONNX 模型未找到: {ModelPath}");

                    _session = new InferenceSession(ModelPath);
                    _sourceVocab = LoadVocabulary(SrcVocabPath);
                    _targetVocab = LoadVocabularyList(TgtVocabPath);

                    _isInitialized = true;
                }
                catch (Exception ex)
                {
                    // 可以记录日志
                    System.Diagnostics.Debug.WriteLine($"ONNX 初始化失败: {ex.Message}");
                    // 不抛出，允许降级到词典法
                }
            }
        }

        /// <summary>
        /// 主翻译接口：优先 ONNX，失败则降级词典法
        /// </summary>
        public static async Task<string> TranslateAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            return await Task.Run(() =>
            {
                string result = "";

                // 1. 尝试 ONNX 翻译
                if (_isInitialized && _session != null)
                {
                    try
                    {
                        result = TranslateWithOnnx(text);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"ONNX 翻译失败: {ex.Message}");
                        result = ""; // 触发降级
                    }
                }

                // 2. 降级到词典法
                if (string.IsNullOrEmpty(result))
                {
                    result = FallbackTranslate(text);
                }

                return result;
            });
        }

        #region 🚀 ONNX 翻译核心逻辑

        private static string TranslateWithOnnx(string text)
        {
            // 1. 分词（简单空格分词，实际可用 Jieba.NET 中文分词）
            var tokens = Tokenize(text);
            if (tokens.Count == 0) return "";

            // 2. 转为 ID 序列
            var inputIds = tokens.Select(t => _sourceVocab.Contains(t) ? _sourceVocab.ToList().IndexOf(t) : 1) // 1 = UNK
                                .Prepend(2)  // 2 = BOS
                                .Append(3)   // 3 = EOS
                                .ToArray();

            // 3. 构造输入张量 [1, seq_len]
            var inputTensor = new DenseTensor<int>(inputIds, new int[] { 1, inputIds.Length });

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("src", inputTensor)
            };

            // 4. 推理
            using var results = _session.Run(inputs);
            var output = results.FirstOrDefault(x => x.Name == "tgt");
            if (output == null) return "";

            var outputTensor = output.AsTensor<int>();
            var outputIds = outputTensor.ToArray();

            // 5. 转为文本
            var translatedTokens = new List<string>();
            foreach (var id in outputIds)
            {
                if (id == 3) break; // EOS
                if (id >= 0 && id < _targetVocab.Count)
                    translatedTokens.Add(_targetVocab[id]);
            }

            // 6. 去除特殊标记
            var finalText = string.Join(" ", translatedTokens)
                                        .Replace("▁", " ") // BPE 分词空格
                                        .Trim();

            return finalText;
        }

        private static List<string> Tokenize(string text)
        {
            // 简化处理：英文空格分词 + 中文按字分（实际应使用 SentencePiece 或 Jieba）
            var tokens = new List<string>();

            var sb = new StringBuilder();
            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c))
                {
                    if (sb.Length > 0)
                    {
                        tokens.Add(sb.ToString());
                        sb.Clear();
                    }
                }
                else if (c >= 0x4E00 && c <= 0x9FFF) // 中文字符
                {
                    if (sb.Length > 0)
                    {
                        tokens.Add(sb.ToString());
                        sb.Clear();
                    }
                    tokens.Add(c.ToString());
                }
                else
                {
                    sb.Append(c);
                }
            }
            if (sb.Length > 0) tokens.Add(sb.ToString());

            return tokens;
        }

        #endregion

        #region 📚 词汇表加载

        private static HashSet<string> LoadVocabulary(string path)
        {
            var set = new HashSet<string>();
            foreach (var line in File.ReadLines(path))
            {
                set.Add(line.Trim());
            }
            return set;
        }

        private static List<string> LoadVocabularyList(string path)
        {
            return File.ReadLines(path).Select(l => l.Trim()).ToList();
        }

        #endregion

        #region 🔽 降级方案：词典翻译（保留原逻辑）

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

        private static string FallbackTranslate(string text)
        {
            if (ContainsChinese(text))
            {
                foreach (var kv in SimpleDict)
                    text = text.Replace(kv.Key, kv.Value);
            }
            else
            {
                foreach (var kv in SimpleDict)
                    text = text.Replace(kv.Value, kv.Key);
            }
            return text;
        }

        private static bool ContainsChinese(string text)
        {
            return text.Any(c => c >= 0x4E00 && c <= 0x9FFF);
        }

        #endregion
    }
}