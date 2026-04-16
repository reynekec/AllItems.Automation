using System.Windows;
using AllItems.Automation.Browser.App.Credentials;
using AllItems.Automation.Browser.App.Services.Diagnostics;

namespace AllItems.Automation.Browser.App.Services.Credentials;

public sealed class CredentialManagerDialogService : ICredentialManagerDialogService
{
    private readonly ICredentialStore _credentialStore;
    private readonly IMasterPasswordService _masterPasswordService;
    private readonly IUiDispatcherService _uiDispatcherService;

    public CredentialManagerDialogService(
        ICredentialStore credentialStore,
        IMasterPasswordService masterPasswordService,
        IUiDispatcherService uiDispatcherService)
    {
        _credentialStore = credentialStore;
        _masterPasswordService = masterPasswordService;
        _uiDispatcherService = uiDispatcherService;
    }

    public CredentialManagerDialogResult ShowDialog(Guid? selectedCredentialId = null, bool startWithNewCredential = false)
    {
        if (!_credentialStore.IsUnlocked)
        {
            AppCrashLogger.Info("Credential manager requested while store is locked; prompting unlock.");
            var unlocked = _masterPasswordService.EnsureUnlockedBeforeRun();
            if (!unlocked || !_credentialStore.IsUnlocked)
            {
                AppCrashLogger.Warn("Credential manager launch canceled because credential store remained locked.");
                return CredentialManagerDialogResult.Cancelled;
            }
        }

        var result = CredentialManagerDialogResult.Cancelled;

        try
        {
            _uiDispatcherService.InvokeAsync(() =>
            {
                AppCrashLogger.Info("Credential manager dialog opening.");
                var viewModel = new CredentialManagerViewModel(_credentialStore, selectedCredentialId, startWithNewCredential);
                var window = CreateWindow(viewModel);

                var dialogResult = window.ShowDialog();
                if (dialogResult == true)
                {
                    result = new CredentialManagerDialogResult(
                        true,
                        viewModel.SelectedCredentialId,
                        viewModel.SelectedCredentialName);
                }

                AppCrashLogger.Info($"Credential manager dialog closed. Result={dialogResult}");
            }).GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            AppCrashLogger.Error("Credential manager dialog failed.", exception);
            return CredentialManagerDialogResult.Cancelled;
        }

        return result;
    }

    private static Window CreateWindow(CredentialManagerViewModel viewModel)
    {
        var owner = Application.Current?.MainWindow;
        var window = new CredentialManagerWindow(viewModel);
        if (owner is not null && owner.IsVisible)
        {
            window.Owner = owner;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        return window;
    }
}
