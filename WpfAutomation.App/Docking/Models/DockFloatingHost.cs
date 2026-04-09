namespace WpfAutomation.App.Docking.Models;

/// <summary>
/// Represents a floating window host for dock panels.
/// </summary>
public sealed class DockFloatingHost
{
    public string HostId { get; init; } = string.Empty;

    public string GroupId { get; init; } = string.Empty;

    public double Left { get; init; }

    public double Top { get; init; }

    public double Width { get; init; } = 480;

    public double Height { get; init; } = 320;
}
