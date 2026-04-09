namespace WpfAutomation.Core.Reports;

public sealed class AccessibilityReport
{
    public string Role { get; set; } = string.Empty;

    public string AriaLabel { get; set; } = string.Empty;

    public string AriaDescription { get; set; } = string.Empty;

    public string AriaLabelledBy { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
