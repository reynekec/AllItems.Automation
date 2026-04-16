using AllItems.Automation.Browser.App.Docking.Layout;

namespace AllItems.Automation.Browser.App.Docking.Services;

public interface IDockLayoutPersistenceService
{
    ValueTask<DockLayoutSnapshot?> RestoreAsync(CancellationToken cancellationToken = default);

    ValueTask SaveAsync(DockLayoutSnapshot snapshot, CancellationToken cancellationToken = default);

    void ScheduleSave(DockLayoutSnapshot snapshot, TimeSpan? delay = null);

    ValueTask ResetAsync(CancellationToken cancellationToken = default);
}
