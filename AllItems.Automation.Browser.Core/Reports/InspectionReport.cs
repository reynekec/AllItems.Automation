namespace AllItems.Automation.Browser.Core.Reports;

public sealed class InspectionReport
{
    public string? Url { get; set; }

    public string? Selector { get; set; }

    public ElementNodeReport? RootElement { get; set; }

    public IReadOnlyList<FrameReport> Frames { get; set; } = Array.Empty<FrameReport>();

    public AccessibilityReport? Accessibility { get; set; }

    public string? ScreenshotPath { get; set; }

    public string? JsonExportPath { get; set; }
}