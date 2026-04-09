namespace WpfAutomation.Core.Inspection;

public sealed class PageInspectOptions
{
    public bool IncludeFrames { get; set; } = true;

    public bool IncludeShadowDom { get; set; } = true;

    public bool IncludeComputedStyles { get; set; }

    public bool ExportJson { get; set; }
}