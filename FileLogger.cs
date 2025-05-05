using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace AlexAssistant
{
    public static class FileLogger
    {
                private static readonly string logFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),             "AlexAssistant",             $"AlexAssistant_Startup_{DateTime.Now:yyyyMMdd_HHmmss}.log"); 
        private static readonly object _lock = new object();         private static int initialized = 0; 
                public static void Initialize()
        {
                        if (Interlocked.CompareExchange(ref initialized, 1, 0) == 0)
            {
                try
                {
                    string logDirectory = Path.GetDirectoryName(logFilePath);
                    if (!string.IsNullOrEmpty(logDirectory))
                    {
                        Directory.CreateDirectory(logDirectory);
                                                File.WriteAllText(logFilePath, $"--- Log Started: {DateTime.Now} ---\n" +
                                                      $"--- Base Directory: {AppContext.BaseDirectory} ---\n" +
                                                      $"--- Log File Path: {logFilePath} ---\n\n", Encoding.UTF8);
                        Debug.WriteLine($"FileLogger initialized. Logging to: {logFilePath}");
                        Log("FileLogger Initialized.");                     }
                    else
                    {
                        Debug.WriteLine($"FATAL: Could not determine log directory for path: {logFilePath}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"FATAL: Could not initialize file logger at {logFilePath}. Error: {ex.Message}");
                    initialized = 0;                 }
            }
        }

                public static void Log(string message,
                              [CallerMemberName] string memberName = "",
                              [CallerFilePath] string sourceFilePath = "",
                              [CallerLineNumber] int sourceLineNumber = 0)
        {
                        if (initialized == 0) Initialize();
            if (initialized == 0) return; 
            try
            {
                lock (_lock)
                {
                    string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} " +
                                        $"[{Path.GetFileName(sourceFilePath)}:{memberName}:{sourceLineNumber}] " +
                                        $"- {message}\n";

                    File.AppendAllText(logFilePath, logMessage, Encoding.UTF8);
                    Debug.WriteLine($"LOG: {logMessage.Trim()}");                 }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR writing to log file '{logFilePath}': {ex.Message}");
            }
        }

                public static void LogProcessOutput(string processIdentifier, string data, bool isError = false)
        {
            if (string.IsNullOrWhiteSpace(data)) return;
                        Log($"[{processIdentifier} {(isError ? "ERR" : "OUT")}]: {data.TrimEnd()}");
        }

                public static void LogError(string message, Exception? ex = null,                                    [CallerMemberName] string memberName = "",
                                   [CallerFilePath] string sourceFilePath = "",
                                   [CallerLineNumber] int sourceLineNumber = 0)
        {
            string fullMessage = message;
            if (ex != null)
            {
                fullMessage += $"\nException: {ex?.GetType().Name}: {ex?.Message}\nStackTrace:\n{ex?.StackTrace}";
            }
            Log($"ERROR: {fullMessage}", memberName, sourceFilePath, sourceLineNumber);
        }
    }
}
