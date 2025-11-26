using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace CursorBackup.Services;

public static class Logger
{
    private static readonly string LogFilePath;
    private static readonly string MainLogFilePath;
    private static readonly object _lock = new object();
    private static readonly object _mainLogLock = new object();
    
    static Logger()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(localAppData, "CursorBackup");
        Directory.CreateDirectory(appFolder);
        LogFilePath = Path.Combine(appFolder, "app_logs.txt");
        MainLogFilePath = Path.Combine(appFolder, "main_log.txt");
        
        // Clear logs on startup to keep them clean and manageable
        try
        {
            if (File.Exists(LogFilePath))
            {
                File.Delete(LogFilePath);
            }
            // Keep main_log.txt for historical events, but limit its size
            if (File.Exists(MainLogFilePath))
            {
                var fileInfo = new FileInfo(MainLogFilePath);
                // If main log is larger than 1MB, truncate it (keep last 100 lines)
                if (fileInfo.Length > 1024 * 1024) // 1MB
                {
                    var lines = File.ReadAllLines(MainLogFilePath);
                    var lastLines = lines.Length > 100 ? lines.Skip(lines.Length - 100).ToArray() : lines;
                    File.WriteAllLines(MainLogFilePath, lastLines);
                }
            }
        }
        catch (Exception ex)
        {
            // If we can't clear logs, continue anyway
            Trace.WriteLine($"[LOGGER] Failed to clear logs on startup: {ex.Message}");
        }
        
        // Startup log
        LogInfo("========== APPLICATION STARTED ==========");
        LogInfo($"Log file path: {LogFilePath}");
        LogMainEvent("APP_START", "Application started");
    }
    
    public static void LogInfo(string message)
    {
        WriteLog("INFO", message);
    }
    
    public static void LogWarning(string message)
    {
        WriteLog("WARN", message);
    }
    
    public static void LogError(string message)
    {
        WriteLog("ERROR", message);
    }
    
    public static void LogError(Exception ex, string message)
    {
        WriteLog("ERROR", $"{message} | Exception: {ex.GetType().Name} | Message: {ex.Message} | StackTrace: {ex.StackTrace}");
    }
    
    [Conditional("DEBUG")]
    public static void LogDebug(string message)
    {
        WriteLog("DEBUG", message);
    }
    
    private static void WriteLog(string level, string message)
    {
        lock (_lock)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logEntry = $"[{timestamp}] [{level}] {message}";
                
                // Console output
                Trace.WriteLine(logEntry);
                Console.WriteLine(logEntry);
                
                // File output - APPEND MODE
                using (var fs = new FileStream(LogFilePath, FileMode.Append, FileAccess.Write, FileShare.Read))
                using (var sw = new StreamWriter(fs) { AutoFlush = true })
                {
                    sw.WriteLine(logEntry);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[LOGGER ERROR] Failed to write log: {ex.Message}");
            }
        }
    }
    
    public static string GetLogFilePath() => LogFilePath;
    public static string GetMainLogFilePath() => MainLogFilePath;
    
    /// <summary>
    /// Logs main events to main_log.txt (works in RELEASE mode too)
    /// Main events: login/logout, user name changes, exceptions, crashes
    /// </summary>
    public static void LogMainEvent(string eventType, string message, string? userName = null, string? platform = null)
    {
        lock (_mainLogLock)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logEntry = $"[{timestamp}] [{eventType}]";
                
                if (!string.IsNullOrEmpty(platform))
                {
                    logEntry += $" Platform: {platform}";
                }
                
                if (!string.IsNullOrEmpty(userName))
                {
                    logEntry += $" User: {userName}";
                }
                
                logEntry += $" | {message}";
                
                // File output - APPEND MODE (works in RELEASE too)
                using (var fs = new FileStream(MainLogFilePath, FileMode.Append, FileAccess.Write, FileShare.Read))
                using (var sw = new StreamWriter(fs) { AutoFlush = true })
                {
                    sw.WriteLine(logEntry);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[MAIN LOGGER ERROR] Failed to write main log: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// Logs exceptions to main log (for critical errors)
    /// </summary>
    public static void LogMainException(Exception ex, string context, string? platform = null)
    {
        LogMainEvent("EXCEPTION", 
            $"Context: {context} | Type: {ex.GetType().Name} | Message: {ex.Message}", 
            platform: platform);
    }
    
    /// <summary>
    /// Logs app crash to main log
    /// </summary>
    public static void LogMainCrash(Exception ex, string? additionalInfo = null)
    {
        var message = $"CRASH | Type: {ex.GetType().Name} | Message: {ex.Message}";
        if (!string.IsNullOrEmpty(additionalInfo))
        {
            message += $" | Info: {additionalInfo}";
        }
        LogMainEvent("CRASH", message);
    }
    
#if DEBUG
    public static void ClearLogs()
    {
        lock (_lock)
        {
            try
            {
                File.WriteAllText(LogFilePath, string.Empty);
                LogInfo("========== LOGS CLEARED ==========");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[LOGGER ERROR] Failed to clear logs: {ex.Message}");
            }
        }
    }
#endif
}

