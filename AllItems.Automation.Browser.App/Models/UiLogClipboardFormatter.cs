namespace AllItems.Automation.Browser.App.Models;

public static class UiLogClipboardFormatter
{
    public static string Format(UiLogItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var prefix = $"[{item.TimestampUtc:O}] [{item.Level}] {item.Message}";
        return string.IsNullOrWhiteSpace(item.ScreenshotPath)
            ? prefix
            : $"{prefix}{Environment.NewLine}Screenshot: {item.ScreenshotPath}";
    }

    public static string FormatAll(IEnumerable<UiLogItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        return string.Join(
            $"{Environment.NewLine}{Environment.NewLine}",
            items.Select(Format));
    }
}
