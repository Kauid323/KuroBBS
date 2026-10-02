using System;
using System.Diagnostics;
using Windows.Storage;
using Windows.UI.Core;

namespace KuroBBS.Services
{
    public enum LogLevel
    {
        Info,
        Network,
        Thread,
        Loading,
        Warning,
        Error
    }

    public class LogEntry
    {
        public string Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Category { get; set; }
        public int ThreadId { get; set; }
        public bool IsUiThread { get; set; }
        public string Message { get; set; }
        public string Details { get; set; }

        public string DisplayHeader
        {
            get
            {
                return string.Format("[{0}] [{1}] [Thread #{2}{3}]", 
                    Timestamp, 
                    Category, 
                    ThreadId, 
                    IsUiThread ? "(UI)" : "(BG)");
            }
        }
    }

    public class KuroLogger
    {
        private static KuroLogger _instance;
        public static KuroLogger Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new KuroLogger();
                }
                return _instance;
            }
        }

        public KuroLogger()
        {
        }

        public void SetDispatcher(CoreDispatcher dispatcher)
        {
            // Kept for backward compatibility
        }

        public void Log(LogLevel level, string category, string message, string details = null)
        {
            int threadId = Environment.CurrentManagedThreadId;
            string timeStr = DateTime.Now.ToString("HH:mm:ss.fff");

            var entry = new LogEntry
            {
                Timestamp = timeStr,
                Level = level,
                Category = category,
                ThreadId = threadId,
                IsUiThread = false,
                Message = message,
                Details = details
            };

            // Visual Studio Debug Output Window / Output Stream
            string formatted = string.Format("{0} {1}{2}", 
                entry.DisplayHeader, 
                message, 
                string.IsNullOrEmpty(details) ? "" : "\nDetails: " + details);
            Debug.WriteLine(formatted);

            // 【关键】同时落盘到 LocalFolder\kuro.log。
            // 之前只写 Debug.WriteLine —— 一旦脱离调试器（或调试器未抓 Output），
            // 崩溃现场就完全没有记录，导致「闪退且无日志、无法定位」。
            WriteToFile(level, formatted);
        }

        // ---------- 持久化日志（落盘） ----------

        private static readonly object _fileLock = new object();
        private static bool _fileDisabled;

        /// <summary>应用启动时立即创建的日志文件（LocalFolder\kuro.log）。</summary>
        private static StorageFile _logFile;

        /// <summary>
        /// 在应用启动（无调试器场景）时调用，确保 kuro.log 文件存在。
        /// 不会抛异常：任何失败都静默降级为「仅 Debug 输出」。
        /// </summary>
        public static void EnsureFileCreated(string appVersion = null)
        {
            try
            {
                var folder = ApplicationData.Current.LocalFolder;
                var file = folder.CreateFileAsync("kuro.log", CreationCollisionOption.OpenIfExists)
                                 .AsTask().GetAwaiter().GetResult();
                _logFile = file;

                string header = "\r\n===== KuroBBS started " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                              + " (v" + (appVersion ?? "?") + ") =====\r\n";
                AppendRaw(header);
            }
            catch
            {
                _fileDisabled = true;
            }
        }

        private static void WriteToFile(LogLevel level, string text)
        {
            // 只落盘关键级别，避免正常 Info 刷爆文件。
            if (level == LogLevel.Info || level == LogLevel.Loading || level == LogLevel.Thread)
            {
                return;
            }
            AppendRaw("[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] " + text + "\r\n");
        }

        /// <summary>强制写一行（任何级别都用，供崩溃/阶段追踪使用）。</summary>
        public static void Trace(string text)
        {
            try { Debug.WriteLine("[TRACE] " + text); } catch { }
            AppendRaw("[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] [TRACE] " + text + "\r\n");
        }

        private static void AppendRaw(string text)
        {
            if (_fileDisabled) return;
            lock (_fileLock)
            {
                try
                {
                    if (_logFile == null)
                    {
                        _logFile = ApplicationData.Current.LocalFolder
                            .CreateFileAsync("kuro.log", CreationCollisionOption.OpenIfExists)
                            .AsTask().GetAwaiter().GetResult();
                    }

                    // 每次追加都独立打开/关闭，保证即使进程被强杀（StackOverflow / fail-fast），
                    // 之前写入的每一行也已经 flush 到磁盘，不会随进程一起丢失。
                    // WP8.1 没有 System.IO.File，统一使用 WinRT 的 FileIO.AppendTextAsync。
                    Windows.Storage.FileIO.AppendTextAsync(_logFile, text)
                        .AsTask().GetAwaiter().GetResult();
                }
                catch
                {
                    _fileDisabled = true;
                }
            }
        }

        public void Clear()
        {
        }

        // Convenience Helpers
        public static void Network(string method, string url, string details = null)
        {
            Instance.Log(LogLevel.Network, "NET", string.Format("{0} {1}", method, url), details);
        }

        public static void ThreadInfo(string action, string details = null)
        {
            Instance.Log(LogLevel.Thread, "THREAD", action, details);
        }

        public static void Loading(string phase, string message)
        {
            Instance.Log(LogLevel.Loading, "LOAD", string.Format("[{0}] {1}", phase, message));
        }

        public static void Info(string category, string message)
        {
            Instance.Log(LogLevel.Info, category, message);
        }

        public static void Warn(string category, string message)
        {
            Instance.Log(LogLevel.Warning, category, message);
        }

        public static void Error(string category, string message, Exception ex = null)
        {
            string details = ex != null ? ex.ToString() : null;
            Instance.Log(LogLevel.Error, category, message, details);
        }

        /// <summary>
        /// 记录一次「当前进程内存占用」，用于定位 WP8.1 的 OOM 死点。
        ///
        /// 背景：Wiki 条目详情页会「解析完整跑完 → 在 XAML 实现阶段无托管异常地 exit code 1」，
        /// 这正是 WP8.1 内存上限被杀的特征。光靠 `MEM_USAGE_INCREASED`（只在跨档时触发）
        /// 无法看出内存是在哪一步涨起来的，因此在关键阶段主动采样一次。
        ///
        /// `Windows.System.MemoryManager` **只存在于 WP8.1**，Win8.1 桌面 head 没有该类型，
        /// 所以必须条件编译；桌面 head 上此方法是空实现（不产生日志）。
        /// </summary>
        public static void Mem(string tag)
        {
#if WINDOWS_PHONE_APP
            try
            {
                Trace(string.Format("MEM[{0}] usage={1} bytes level={2}",
                    tag,
                    Windows.System.MemoryManager.AppMemoryUsage,
                    Windows.System.MemoryManager.AppMemoryUsageLevel));
            }
            catch { }
#endif
        }
    }
}
