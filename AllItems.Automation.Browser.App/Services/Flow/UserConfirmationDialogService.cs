using System.Windows;
using AllItems.Automation.Browser.App.Views;

namespace AllItems.Automation.Browser.App.Services.Flow;

public sealed class UserConfirmationDialogService : IUserConfirmationDialogService
{
    private readonly IUiDispatcherService _uiDispatcherService;

    public UserConfirmationDialogService(IUiDispatcherService uiDispatcherService)
    {
        _uiDispatcherService = uiDispatcherService;
    }

    public async Task<bool> WaitForConfirmationAsync(string title, string message, CancellationToken cancellationToken = default)
    {
        var result = false;

        await _uiDispatcherService.InvokeAsync(() =>
        {
            var owner = Application.Current?.MainWindow;
            var window = new UserConfirmationDialogWindow
            {
                Title = string.IsNullOrWhiteSpace(title) ? "Confirmation" : title,
                Message = string.IsNullOrWhiteSpace(message)
                    ? "Click Continue to proceed with the automation flow."
                    : message,
            };

            if (owner is not null && owner.IsVisible)
            {
                window.Owner = owner;
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            result = window.ShowDialog() == true;
        });

        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }
}
