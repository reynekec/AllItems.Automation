namespace WpfAutomation.Core.Reports;

public sealed class ElementNodeReport
{
    public string TagName { get; set; } = string.Empty;

    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public string InnerText { get; set; } = string.Empty;

    public IReadOnlyList<string> Classes { get; set; } = Array.Empty<string>();

    public Dictionary<string, string> Attributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> Styles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string CssPath { get; set; } = string.Empty;

    public string XPath { get; set; } = string.Empty;

    public bool IsShadowHost { get; set; }

    public bool IsInShadowDom { get; set; }

    public BoundingBoxReport? BoundingBox { get; set; }

    public IReadOnlyList<ElementNodeReport> Children { get; set; } = Array.Empty<ElementNodeReport>();

    public IReadOnlyList<ElementNodeReport> ShadowChildren { get; set; } = Array.Empty<ElementNodeReport>();
}
