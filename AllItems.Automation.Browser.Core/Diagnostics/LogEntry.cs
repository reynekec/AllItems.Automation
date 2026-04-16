namespace AllItems.Automation.Browser.Core.Diagnostics;

public sealed class LogEntry
{
    public DateTime TimestampUtc { get; init; }

    public string Level { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public Dictionary<string, string>? ContextData { get; init; }
}