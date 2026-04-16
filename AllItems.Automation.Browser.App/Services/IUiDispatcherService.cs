namespace AllItems.Automation.Browser.App.Services;

public interface IUiDispatcherService
{
    Task InvokeAsync(Action action);
}
