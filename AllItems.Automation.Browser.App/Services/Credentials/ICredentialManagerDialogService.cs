namespace AllItems.Automation.Browser.App.Services.Credentials;

public interface ICredentialManagerDialogService
{
    CredentialManagerDialogResult ShowDialog(Guid? selectedCredentialId = null, bool startWithNewCredential = false);
}

public sealed record CredentialManagerDialogResult(bool Accepted, Guid? SelectedCredentialId, string? SelectedCredentialName)
{
    public static CredentialManagerDialogResult Cancelled { get; } = new(false, null, null);
}

public sealed class NullCredentialManagerDialogService : ICredentialManagerDialogService
{
    public CredentialManagerDialogResult ShowDialog(Guid? selectedCredentialId = null, bool startWithNewCredential = false)
    {
        return CredentialManagerDialogResult.Cancelled;
    }
}
