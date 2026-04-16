namespace AllItems.Automation.Browser.Core.Reports;

public sealed class FrameReport
{
    public string Name { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string ParentUrl { get; set; } = string.Empty;

    public IReadOnlyList<ElementNodeReport> RootNodes { get; set; } = Array.Empty<ElementNodeReport>();
}
