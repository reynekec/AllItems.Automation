namespace AllItems.Automation.Browser.App.Services.Flow;

public interface IUserConfirmationDialogService
{
    Task<bool> WaitForConfirmationAsync(string title, string message, CancellationToken cancellationToken = default);
}

public sealed class NullUserConfirmationDialogService : IUserConfirmationDialogService
{
    public Task<bool> WaitForConfirmationAsync(string title, string message, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }
}
