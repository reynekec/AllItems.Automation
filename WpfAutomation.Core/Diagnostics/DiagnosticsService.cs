namespace WpfAutomation.Core.Diagnostics;

public sealed class DiagnosticsService
{
    private readonly List<LogEntry> _entries = [];

    public event Action<LogEntry>? EntryAdded;

    public void Info(string message, Dictionary<string, string>? contextData = null)
    {
        Add("INFO", message, contextData);
    }

    public void Warn(string message, Dictionary<string, string>? contextData = null)
    {
        Add("WARN", message, contextData);
    }

    public void Error(string message, Exception? exception = null, Dictionary<string, string>? contextData = null)
    {
        var fullMessage = exception is null ? message : $"{message}: {exception.Message}";
        Add("ERROR", fullMessage, contextData);
    }

    public IReadOnlyList<LogEntry> GetLogs()
    {
        return _entries.AsReadOnly();
    }

    public void ClearLogs()
    {
        _entries.Clear();
    }

    private void Add(string level, string message, Dictionary<string, string>? contextData)
    {
        var timestamp = DateTime.UtcNow;
        var entry = new LogEntry
        {
            TimestampUtc = timestamp,
            Level = level,
            Message = message,
            ContextData = contextData,
        };

        _entries.Add(entry);
        EntryAdded?.Invoke(entry);

        var line = $"[{timestamp:O}] [{level}] {message}";
        Console.WriteLine(line);
    }
}