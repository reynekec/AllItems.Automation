namespace WpfAutomation.App.Docking.Contracts;

public interface IDockPanelLifecycleContract
{
    event EventHandler<DockPanelLifecycleEvent>? LifecycleChanged;

    ValueTask ApplyAsync(DockPanelLifecycleCommand command, CancellationToken cancellationToken = default);
}
