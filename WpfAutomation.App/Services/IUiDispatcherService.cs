namespace WpfAutomation.App.Services;

public interface IUiDispatcherService
{
    Task InvokeAsync(Action action);
}
