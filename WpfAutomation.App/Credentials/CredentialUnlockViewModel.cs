using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security;
using System.Security.Cryptography;
using WpfAutomation.App.Services.Credentials;

namespace WpfAutomation.App.Credentials;

public sealed class CredentialUnlockViewModel : INotifyPropertyChanged
{
    private readonly ICredentialStore _credentialStore;
    private string? _errorMessage;

    public CredentialUnlockViewModel(ICredentialStore credentialStore)
    {
        _credentialStore = credentialStore;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (string.Equals(_errorMessage, value, StringComparison.Ordinal))
            {
                return;
            }

            _errorMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool TryUnlock(SecureString? masterPassword)
    {
        if (masterPassword is null || masterPassword.Length == 0)
        {
            ErrorMessage = "Master password is required.";
            return false;
        }

        try
        {
            _credentialStore.Unlock(masterPassword);
            ErrorMessage = null;
            return true;
        }
        catch (CryptographicException)
        {
            ErrorMessage = "Incorrect master password. Please try again.";
            return false;
        }
        catch (InvalidDataException)
        {
            ErrorMessage = "Incorrect master password. Please try again.";
            return false;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
