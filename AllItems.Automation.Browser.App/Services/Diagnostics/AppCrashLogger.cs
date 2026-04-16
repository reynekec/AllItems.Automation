using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace AllItems.Automation.Browser.App.Services.Diagnostics;

public static class AppCrashLogger
{
    private static readonly object Gate = new();
    private static bool _isInitialized;

    public static string LogsDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AllItems.Automation",
        "logs");

    public static string CurrentLogFilePath { get; private set; } = string.Empty;

    public static void Initialize()
    {
        lock (Gate)
        {
            if (_isInitialized)
            {
                return;
            }

            Directory.CreateDirectory(LogsDirectory);
            CurrentLogFilePath = Path.Combine(LogsDirectory, $"app-{DateTime.UtcNow:yyyyMMdd}.log");

            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            _isInitialized = true;
        }

        Info("Crash logger initialized.");
    }

    public static void RegisterDispatcher(Application application)
    {
        application.DispatcherUnhandledException += OnDispatcherUnhandledException;
        Info("Dispatcher unhandled exception hook registered.");
    }

    public static void Info(string message)
    {
        Write("INFO", message, null);
    }

    public static void Warn(string message)
    {
        Write("WARN", message, null);
    }

    public static void Error(string message, Exception? exception = null)
    {
        Write("ERROR", message, exception);
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
    {
        var exception = args.ExceptionObject as Exception;
        Error($"AppDomain unhandled exception. IsTerminating={args.IsTerminating}", exception);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args)
    {
        Error("TaskScheduler unobserved task exception.", args.Exception);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs args)
    {
        Error("Dispatcher unhandled exception.", args.Exception);
    }

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            var now = DateTime.UtcNow;
            var builder = new StringBuilder();
            builder.Append('[').Append(now.ToString("O")).Append("] [").Append(level).Append("] ").AppendLine(message);

            if (exception is not null)
            {
                builder.AppendLine(exception.ToString());
            }

            lock (Gate)
            {
                if (string.IsNullOrWhiteSpace(CurrentLogFilePath))
                {
                    Directory.CreateDirectory(LogsDirectory);
                    CurrentLogFilePath = Path.Combine(LogsDirectory, $"app-{DateTime.UtcNow:yyyyMMdd}.log");
                }

                File.AppendAllText(CurrentLogFilePath, builder.ToString());
            }
        }
        catch
        {
            // Never throw from logging.
        }
    }
}
