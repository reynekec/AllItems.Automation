using AllItems.Automation.Browser.App.Docking.Layout;

namespace AllItems.Automation.Browser.App.Docking.Services;

public interface IDockLayoutStateStore
{
    DockLayoutSnapshot? Current { get; }

    event EventHandler<DockLayoutSnapshot>? LayoutChanged;

    ValueTask SaveAsync(DockLayoutSnapshot snapshot, CancellationToken cancellationToken = default);

    ValueTask<DockLayoutSnapshot?> GetAsync(CancellationToken cancellationToken = default);

    ValueTask ClearAsync(CancellationToken cancellationToken = default);
}
