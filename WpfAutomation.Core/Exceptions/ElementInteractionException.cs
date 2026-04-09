namespace WpfAutomation.Core.Exceptions;

public sealed class ElementInteractionException : AutomationException
{
    public ElementInteractionException(
        string message,
        string? actionName = null,
        string? url = null,
        string? selector = null,
        int? timeoutMs = null,
        string? screenshotPath = null,
        Exception? innerException = null)
        : base(message, actionName, url, selector, timeoutMs, screenshotPath, innerException)
    {
    }
}