using System.Windows;
using AllItems.Automation.Browser.App.Credentials;
using AllItems.Automation.Browser.App.Services.Diagnostics;
using AllItems.Automation.Browser.App.Services;

namespace AllItems.Automation.Browser.App.Services.Credentials;

public sealed class CredentialUnlockDialogService : ICredentialUnlockDialogService
{
    private readonly IUiDispatcherService _uiDispatcherService;

    public CredentialUnlockDialogService(IUiDispatcherService uiDispatcherService)
    {
        _uiDispatcherService = uiDispatcherService;
    }

    public bool ShowDialog(CredentialUnlockViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        if (Application.Current?.Dispatcher.CheckAccess() == true)
        {
            return ShowDialogCore(viewModel);
        }

        var dialogResult = false;
        _uiDispatcherService.InvokeAsync(() =>
        {
            dialogResult = ShowDialogCore(viewModel);
        }).GetAwaiter().GetResult();

        return dialogResult;
    }

    private static bool ShowDialogCore(CredentialUnlockViewModel viewModel)
    {
        AppCrashLogger.Info("Credential unlock dialog opening.");

        var window = new CredentialUnlockWindow(viewModel)
        {
            Owner = Application.Current?.MainWindow,
        };

        var dialogResult = window.ShowDialog();
        AppCrashLogger.Info($"Credential unlock dialog closed. Result={dialogResult}");
        return dialogResult == true;
    }
}
