namespace WpfAutomation.App.Models;

public sealed class UiLogItem
{
    public DateTime TimestampUtc { get; init; }

    public string Level { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string? ScreenshotPath { get; init; }
}
