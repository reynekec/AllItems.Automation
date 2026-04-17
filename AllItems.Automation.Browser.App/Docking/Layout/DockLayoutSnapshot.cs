namespace AllItems.Automation.Browser.App.Docking.Layout;

/// <summary>
/// Immutable root snapshot of dock layout state for persistence.
/// </summary>
public sealed record DockLayoutSnapshot
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public string? RootNodeId { get; init; }

    public IReadOnlyList<DockLayoutNodeSnapshot> Nodes { get; init; } = [];

    public IReadOnlyList<DockLayoutGroupSnapshot> Groups { get; init; } = [];

    public IReadOnlyList<DockLayoutFloatingHostSnapshot> FloatingHosts { get; init; } = [];

    public IReadOnlyList<DockLayoutAutoHideSnapshot> AutoHideItems { get; init; } = [];
}
