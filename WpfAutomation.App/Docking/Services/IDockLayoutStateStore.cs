using WpfAutomation.App.Docking.Layout;

namespace WpfAutomation.App.Docking.Services;

public interface IDockLayoutStateStore
{
    DockLayoutSnapshot? Current { get; }

    event EventHandler<DockLayoutSnapshot>? LayoutChanged;

    ValueTask SaveAsync(DockLayoutSnapshot snapshot, CancellationToken cancellationToken = default);

    ValueTask<DockLayoutSnapshot?> GetAsync(CancellationToken cancellationToken = default);

    ValueTask ClearAsync(CancellationToken cancellationToken = default);
}
