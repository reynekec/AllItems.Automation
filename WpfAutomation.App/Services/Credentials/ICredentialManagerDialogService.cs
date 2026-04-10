namespace WpfAutomation.App.Services.Credentials;

public interface ICredentialManagerDialogService
{
    CredentialManagerDialogResult ShowDialog(Guid? selectedCredentialId = null);
}

public sealed record CredentialManagerDialogResult(bool Accepted, Guid? SelectedCredentialId, string? SelectedCredentialName)
{
    public static CredentialManagerDialogResult Cancelled { get; } = new(false, null, null);
}

public sealed class NullCredentialManagerDialogService : ICredentialManagerDialogService
{
    public CredentialManagerDialogResult ShowDialog(Guid? selectedCredentialId = null)
    {
        return CredentialManagerDialogResult.Cancelled;
    }
}
