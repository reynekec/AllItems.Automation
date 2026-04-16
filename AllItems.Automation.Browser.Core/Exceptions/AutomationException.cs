namespace AllItems.Automation.Browser.Core.Exceptions;

public class AutomationException : Exception
{
    public AutomationException(
        string message,
        string? actionName = null,
        string? url = null,
        string? selector = null,
        int? timeoutMs = null,
        string? screenshotPath = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ActionName = actionName;
        Url = url;
        Selector = selector;
        TimeoutMs = timeoutMs;
        ScreenshotPath = screenshotPath;
    }

    public string? ActionName { get; }

    public string? Url { get; }

    public string? Selector { get; }

    public int? TimeoutMs { get; }

    public string? ScreenshotPath { get; }

    public override string ToString()
    {
        var details = new List<string>
        {
            base.ToString(),
        };

        if (!string.IsNullOrWhiteSpace(ActionName))
        {
            details.Add($"Action: {ActionName}");
        }

        if (!string.IsNullOrWhiteSpace(Url))
        {
            details.Add($"Url: {Url}");
        }

        if (!string.IsNullOrWhiteSpace(Selector))
        {
            details.Add($"Selector: {Selector}");
        }

        if (TimeoutMs.HasValue)
        {
            details.Add($"TimeoutMs: {TimeoutMs.Value}");
        }

        if (!string.IsNullOrWhiteSpace(ScreenshotPath))
        {
            details.Add($"ScreenshotPath: {ScreenshotPath}");
        }

        if (InnerException is not null)
        {
            details.Add($"InnerException: {InnerException.GetType().Name}: {InnerException.Message}");
        }

        return string.Join(Environment.NewLine, details);
    }
}