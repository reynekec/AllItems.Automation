namespace AllItems.Automation.Browser.Core.Reports;

public sealed class PageInspectionReport
{
    public string? Url { get; set; }

    public ElementNodeReport? MainRoot { get; set; }

    public IReadOnlyList<FrameReport> Frames { get; set; } = Array.Empty<FrameReport>();

    public string? ScreenshotPath { get; set; }

    public string? JsonExportPath { get; set; }
}