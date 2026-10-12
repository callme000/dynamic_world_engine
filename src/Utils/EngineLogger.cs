using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace DynamicWorldEngine.Utils
{
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3,
        Fatal = 4
    }

    /// <summary>
    /// High-performance, crash-resilient logging and breadcrumb tracking system.
    /// Flushes synchronously to disk after every write with immediate lock release,
    /// ensuring that if GTA V or ScriptHookV terminates abruptly, the exact preceding
    /// statements are fully preserved on disk.
    /// Compatible with C# 5 / .NET 4.8.
    /// </summary>
    public static class Log
    {
        private static readonly object _fileLock = new object();
        private static string _logFilePath = null;
        private static LogLevel _minLevel = LogLevel.Debug;
        private static readonly Queue<string> _breadcrumbs = new Queue<string>();
        private static readonly int _maxBreadcrumbs = 100;
        private static bool _isInitialized = false;

        public static int MainThreadId { get; set; }

        public static string LogFilePath
        {
            get { return _logFilePath; }
        }

        static Log()
        {
            MainThreadId = Thread.CurrentThread.ManagedThreadId;
            Initialize();
        }

        public static void Initialize(string preferredDirectory = null)
        {
            lock (_fileLock)
            {
                if (_isInitialized && !string.IsNullOrEmpty(_logFilePath) && File.Exists(_logFilePath))
                {
                    return;
                }

                try
                {
                    string targetDir = null;

                    // 1. Try preferred directory if provided
                    if (!string.IsNullOrEmpty(preferredDirectory) && Directory.Exists(preferredDirectory))
                    {
                        targetDir = preferredDirectory;
                    }

                    // 2. Try GTA V scripts directory
                    if (string.IsNullOrEmpty(targetDir))
                    {
                        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                        string scriptsCandidate = Path.Combine(baseDir, "scripts");
                        if (Directory.Exists(scriptsCandidate))
                        {
                            targetDir = scriptsCandidate;
                        }
                        else
                        {
                            targetDir = baseDir;
                        }
                    }

                    _logFilePath = Path.Combine(targetDir, "DynamicWorldEngine.log");

                    // Check file size: if larger than 10MB, rotate to .old
                    if (File.Exists(_logFilePath))
                    {
                        var fi = new FileInfo(_logFilePath);
                        if (fi.Length > 10 * 1024 * 1024)
                        {
                            string backupPath = Path.Combine(targetDir, "DynamicWorldEngine.old.log");
                            try
                            {
                                if (File.Exists(backupPath)) File.Delete(backupPath);
                                File.Move(_logFilePath, backupPath);
                            }
                            catch { }
                        }
                    }

                    // Hook global unhandled exceptions to catch fatal crashes
                    AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

                    _isInitialized = true;

                    WriteInternal("SYSTEM", LogLevel.Info, "================================================================================");
                    WriteInternal("SYSTEM", LogLevel.Info, string.Format("Dynamic World Engine Logging System Initialized. Log file: {0}", _logFilePath));
                    WriteInternal("SYSTEM", LogLevel.Info, string.Format("OS: {0} | CLR: {1} | Main Thread ID: {2}", Environment.OSVersion, Environment.Version, MainThreadId));
                    WriteInternal("SYSTEM", LogLevel.Info, "================================================================================");
                }
                catch (Exception ex)
                {
                    // Fallback to local execution directory if permissions restrict scripts directory
                    try
                    {
                        _logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DynamicWorldEngine.log");
                        _isInitialized = true;
                    }
                    catch
                    {
                        Console.WriteLine("[Log] Failed to initialize logger: " + ex.Message);
                    }
                }
            }
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            string exMsg = ex != null ? ex.ToString() : (e.ExceptionObject != null ? e.ExceptionObject.ToString() : "Unknown fatal error");
            Fatal("CRASH", ex, string.Format("FATAL UNHANDLED CLR EXCEPTION (IsTerminating={0}): {1}", e.IsTerminating, exMsg));
            DumpBreadcrumbs();
        }

        /// <summary>
        /// Records an in-memory chronological breadcrumb.
        /// When a crash occurs, all recent breadcrumbs are immediately written to disk.
        /// </summary>
        public static void Breadcrumb(string action, params object[] args)
        {
            try
            {
                string msg = (args != null && args.Length > 0) ? string.Format(action, args) : action;
                string threadName = GetThreadTag();
                string timestamp = DateTime.UtcNow.ToString("HH:mm:ss.fff");
                string entry = string.Format("[{0}] [{1}] {2}", timestamp, threadName, msg);

                lock (_breadcrumbs)
                {
                    _breadcrumbs.Enqueue(entry);
                    while (_breadcrumbs.Count > _maxBreadcrumbs)
                    {
                        _breadcrumbs.Dequeue();
                    }
                }

                // Also log to debug stream for real-time tracking
                Debug("TRACE", msg);
            }
            catch { }
        }

        public static void DumpBreadcrumbs()
        {
            lock (_fileLock)
            {
                try
                {
                    WriteInternal("CRASH_DUMP", LogLevel.Fatal, "------------------- RECENT BREADCRUMBS TRAIL BEFORE CRASH -------------------");
                    lock (_breadcrumbs)
                    {
                        int index = 1;
                        foreach (var crumb in _breadcrumbs)
                        {
                            WriteInternal("BREADCRUMB", LogLevel.Fatal, string.Format("#{0:D2}: {1}", index++, crumb));
                        }
                    }
                    WriteInternal("CRASH_DUMP", LogLevel.Fatal, "-----------------------------------------------------------------------------");
                }
                catch { }
            }
        }

        public static void Debug(string tag, string message, params object[] args)
        {
            Write(tag, LogLevel.Debug, message, null, args);
        }

        public static void Info(string tag, string message, params object[] args)
        {
            Write(tag, LogLevel.Info, message, null, args);
        }

        public static void Warn(string tag, string message, params object[] args)
        {
            Write(tag, LogLevel.Warn, message, null, args);
        }

        public static void Error(string tag, Exception ex, string message = null, params object[] args)
        {
            Write(tag, LogLevel.Error, message, ex, args);
        }

        public static void Fatal(string tag, Exception ex, string message = null, params object[] args)
        {
            Write(tag, LogLevel.Fatal, message, ex, args);
            DumpBreadcrumbs();
        }

        private static void Write(string tag, LogLevel level, string message, Exception ex, object[] args)
        {
            if (level < _minLevel) return;

            string formattedMessage = message ?? "";
            try
            {
                if (args != null && args.Length > 0 && !string.IsNullOrEmpty(formattedMessage))
                {
                    formattedMessage = string.Format(formattedMessage, args);
                }
            }
            catch
            {
                // In case format tokens mismatch
            }

            if (ex != null)
            {
                if (!string.IsNullOrEmpty(formattedMessage))
                {
                    formattedMessage += "\nEXCEPTION: " + ex.ToString();
                }
                else
                {
                    formattedMessage = "EXCEPTION: " + ex.ToString();
                }
            }

            lock (_fileLock)
            {
                WriteInternal(tag, level, formattedMessage);
            }
        }

        private static void WriteInternal(string tag, LogLevel level, string message)
        {
            try
            {
                if (string.IsNullOrEmpty(_logFilePath)) return;

                string time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string thread = GetThreadTag();
                string levelStr = level.ToString().ToUpper().PadRight(5);

                string line = string.Format("[{0}] [{1}] [{2}] [{3}] {4}", time, levelStr, thread, tag, message);

                // Use FileStream with FileShare.ReadWrite and immediate Flush
                using (var fs = new FileStream(_logFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                using (var sw = new StreamWriter(fs, Encoding.UTF8))
                {
                    sw.WriteLine(line);
                    sw.Flush();
                    fs.Flush(true); // Force OS to flush buffer directly to disk hardware
                }
            }
            catch
            {
                // Silently avoid crashing if disk or permissions temporarily fail
            }
        }

        private static string GetThreadTag()
        {
            int tid = Thread.CurrentThread.ManagedThreadId;
            if (tid == MainThreadId) return "Main";
            if (Thread.CurrentThread.IsThreadPoolThread) return string.Format("Pool-{0}", tid);
            return string.Format("Thread-{0}", tid);
        }

        public static void SetLogLevel(LogLevel level)
        {
            _minLevel = level;
        }
    }
}
