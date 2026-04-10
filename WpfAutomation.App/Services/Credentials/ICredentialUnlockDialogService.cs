using WpfAutomation.App.Credentials;

namespace WpfAutomation.App.Services.Credentials;

public interface ICredentialUnlockDialogService
{
    bool ShowDialog(CredentialUnlockViewModel viewModel);
}
