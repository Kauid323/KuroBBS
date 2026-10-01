using System;
using System.Diagnostics;
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
    }
}
