using WpfAutomation.App.Docking.Layout;

namespace WpfAutomation.App.Docking.Services;

public interface IDockLayoutPersistenceService
{
    ValueTask<DockLayoutSnapshot?> RestoreAsync(CancellationToken cancellationToken = default);

    ValueTask SaveAsync(DockLayoutSnapshot snapshot, CancellationToken cancellationToken = default);

    void ScheduleSave(DockLayoutSnapshot snapshot, TimeSpan? delay = null);

    ValueTask ResetAsync(CancellationToken cancellationToken = default);
}
