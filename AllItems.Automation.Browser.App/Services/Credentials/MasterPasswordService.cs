using AllItems.Automation.Browser.App.Credentials;
using AllItems.Automation.Browser.App.Services.Diagnostics;

namespace AllItems.Automation.Browser.App.Services.Credentials;

public sealed class MasterPasswordService : IMasterPasswordService
{
    private readonly ICredentialStore _credentialStore;
    private readonly ICredentialUnlockDialogService _unlockDialogService;

    public MasterPasswordService(
        ICredentialStore credentialStore,
        ICredentialUnlockDialogService unlockDialogService)
    {
        _credentialStore = credentialStore;
        _unlockDialogService = unlockDialogService;
    }

    public bool EnsureUnlockedBeforeRun()
    {
        if (_credentialStore.IsUnlocked)
        {
            AppCrashLogger.Info("Credential store already unlocked.");
            return true;
        }

        AppCrashLogger.Info("Credential store locked; requesting master password.");
        var viewModel = new CredentialUnlockViewModel(_credentialStore);
        var unlocked = _unlockDialogService.ShowDialog(viewModel);
        AppCrashLogger.Info($"Credential store unlock flow completed. Unlocked={unlocked}, IsUnlocked={_credentialStore.IsUnlocked}");
        return unlocked;
    }
}
