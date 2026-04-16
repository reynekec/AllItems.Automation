namespace AllItems.Automation.Browser.Core.Inspection;

public sealed class InspectOptions
{
    public bool IncludeDescendants { get; set; } = true;

    public bool IncludeAttributes { get; set; } = true;

    public bool IncludeComputedStyles { get; set; } = true;

    public bool IncludeAccessibility { get; set; } = true;

    public bool IncludeCssPath { get; set; } = true;

    public bool IncludeXPath { get; set; } = true;

    public bool IncludeScreenshot { get; set; }

    public bool IncludeFrames { get; set; } = true;

    public bool IncludeShadowDom { get; set; } = true;

    public bool ExportJson { get; set; }
}