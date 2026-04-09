namespace WpfAutomation.App.Docking.Layout;

public sealed record DockLayoutFloatingHostSnapshot
{
    public string HostId { get; init; } = string.Empty;

    public string GroupId { get; init; } = string.Empty;

    public double Left { get; init; }

    public double Top { get; init; }

    public double Width { get; init; }

    public double Height { get; init; }
}
