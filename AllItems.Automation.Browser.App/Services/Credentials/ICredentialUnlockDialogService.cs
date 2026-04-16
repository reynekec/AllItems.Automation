using AllItems.Automation.Browser.App.Credentials;

namespace AllItems.Automation.Browser.App.Services.Credentials;

public interface ICredentialUnlockDialogService
{
    bool ShowDialog(CredentialUnlockViewModel viewModel);
}
