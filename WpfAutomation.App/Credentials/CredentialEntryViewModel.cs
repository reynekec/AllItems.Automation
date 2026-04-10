using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfAutomation.App.Credentials.Models;

namespace WpfAutomation.App.Credentials;

public sealed class CredentialEntryViewModel : INotifyPropertyChanged
{
    private readonly Dictionary<string, string> _fields;
    private string _name;
    private HostAuthKind _authType;
    private WebAuthKind _webAuthKind;

    private CredentialEntryViewModel(Guid id, string name, HostAuthKind authType, WebAuthKind webAuthKind, Dictionary<string, string> fields)
    {
        Id = id;
        _name = name;
        _authType = authType;
        _webAuthKind = webAuthKind;
        _fields = fields;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<WebAuthKind> WebAuthKinds { get; } = Enum.GetValues<WebAuthKind>();

    public Guid Id { get; }

    public string Name
    {
        get => _name;
        set
        {
            if (string.Equals(_name, value, StringComparison.Ordinal))
            {
                return;
            }

            _name = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "(Unnamed credential)" : Name;

    public HostAuthKind AuthType
    {
        get => _authType;
        set
        {
            if (_authType == value)
            {
                return;
            }

            _authType = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsWebSelected));
            OnPropertyChanged(nameof(IsWindowsSelected));
        }
    }

    public WebAuthKind WebAuthKind
    {
        get => _webAuthKind;
        set
        {
            if (_webAuthKind == value)
            {
                return;
            }

            _webAuthKind = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(WebAuthKindDisplayName));
            OnPropertyChanged(nameof(WebAuthKindDescription));
            RaiseWebKindVisibilityChanges();
        }
    }

    /// <summary>
    /// Gets the user-friendly display name for the selected auth kind.
    /// </summary>
    public string WebAuthKindDisplayName => WebAuthKindDisplayConverter.GetDisplayName(WebAuthKind);

    /// <summary>
    /// Gets the user-friendly description for the selected auth kind.
    /// </summary>
    public string WebAuthKindDescription => WebAuthKindDisplayConverter.GetDescription(WebAuthKind);

    public bool IsWebSelected
    {
        get => AuthType == HostAuthKind.Web;
        set
        {
            if (value)
            {
                AuthType = HostAuthKind.Web;
            }
        }
    }

    public bool IsWindowsSelected
    {
        get => AuthType == HostAuthKind.Windows;
        set
        {
            if (value)
            {
                AuthType = HostAuthKind.Windows;
            }
        }
    }

    public bool IsUsernamePasswordKind => WebAuthKind == WebAuthKind.UsernamePassword;
    public bool IsUsernameEmailOtpKind => WebAuthKind == WebAuthKind.UsernameEmailOtp;
    public bool IsUsernameSmsOtpKind => WebAuthKind == WebAuthKind.UsernameSmsOtp;
    public bool IsTotpKind => WebAuthKind == WebAuthKind.Totp;
    public bool IsOAuthSsoKind => WebAuthKind == WebAuthKind.OAuthSso;
    public bool IsHttpBasicAuthKind => WebAuthKind == WebAuthKind.HttpBasicAuth;
    public bool IsApiKeyBearerKind => WebAuthKind == WebAuthKind.ApiKeyBearer;
    public bool IsCertificateMtlsKind => WebAuthKind == WebAuthKind.CertificateMtls;
    public bool IsCustomKind => WebAuthKind == WebAuthKind.Custom;

    public string Username
    {
        get => GetField(WebCredentialEntry.FieldKeys.Username);
        set => SetField(WebCredentialEntry.FieldKeys.Username, value);
    }

    public string Password
    {
        get => GetField(WebCredentialEntry.FieldKeys.Password);
        set => SetField(WebCredentialEntry.FieldKeys.Password, value);
    }

    public string ImapHost
    {
        get => GetField(WebCredentialEntry.FieldKeys.ImapHost);
        set => SetField(WebCredentialEntry.FieldKeys.ImapHost, value);
    }

    public string ImapPort
    {
        get => GetField(WebCredentialEntry.FieldKeys.ImapPort);
        set => SetField(WebCredentialEntry.FieldKeys.ImapPort, value);
    }

    public string ImapUsername
    {
        get => GetField(WebCredentialEntry.FieldKeys.ImapUsername);
        set => SetField(WebCredentialEntry.FieldKeys.ImapUsername, value);
    }

    public string ImapPassword
    {
        get => GetField(WebCredentialEntry.FieldKeys.ImapPassword);
        set => SetField(WebCredentialEntry.FieldKeys.ImapPassword, value);
    }

    public string MailboxFolder
    {
        get => GetField(WebCredentialEntry.FieldKeys.MailboxFolder);
        set => SetField(WebCredentialEntry.FieldKeys.MailboxFolder, value);
    }

    public string SubjectContains
    {
        get => GetField(WebCredentialEntry.FieldKeys.SubjectContains);
        set => SetField(WebCredentialEntry.FieldKeys.SubjectContains, value);
    }

    public string PhoneHint
    {
        get => GetField(WebCredentialEntry.FieldKeys.PhoneHint);
        set => SetField(WebCredentialEntry.FieldKeys.PhoneHint, value);
    }

    public string TotpSecret
    {
        get => GetField(WebCredentialEntry.FieldKeys.TotpSecret);
        set => SetField(WebCredentialEntry.FieldKeys.TotpSecret, value);
    }

    public string ProviderName
    {
        get => GetField(WebCredentialEntry.FieldKeys.ProviderName);
        set => SetField(WebCredentialEntry.FieldKeys.ProviderName, value);
    }

    public string TokenName
    {
        get => GetField(WebCredentialEntry.FieldKeys.TokenName);
        set => SetField(WebCredentialEntry.FieldKeys.TokenName, value);
    }

    public string Token
    {
        get => GetField(WebCredentialEntry.FieldKeys.Token);
        set => SetField(WebCredentialEntry.FieldKeys.Token, value);
    }

    public string CertificatePath
    {
        get => GetField(WebCredentialEntry.FieldKeys.CertificatePath);
        set => SetField(WebCredentialEntry.FieldKeys.CertificatePath, value);
    }

    public string CertificatePassword
    {
        get => GetField(WebCredentialEntry.FieldKeys.CertificatePassword);
        set => SetField(WebCredentialEntry.FieldKeys.CertificatePassword, value);
    }

    public string PrivateKeyPath
    {
        get => GetField(WebCredentialEntry.FieldKeys.PrivateKeyPath);
        set => SetField(WebCredentialEntry.FieldKeys.PrivateKeyPath, value);
    }

    public string Label
    {
        get => GetField(WebCredentialEntry.FieldKeys.Label);
        set => SetField(WebCredentialEntry.FieldKeys.Label, value);
    }

    public string Notes
    {
        get => GetField(WebCredentialEntry.FieldKeys.Notes);
        set => SetField(WebCredentialEntry.FieldKeys.Notes, value);
    }

    public static CredentialEntryViewModel CreateNew()
    {
        return new CredentialEntryViewModel(
            Guid.NewGuid(),
            string.Empty,
            HostAuthKind.Web,
            WebAuthKind.UsernamePassword,
            new Dictionary<string, string>(StringComparer.Ordinal));
    }

    public static CredentialEntryViewModel FromModel(CredentialEntry model)
    {
        if (model is WebCredentialEntry web)
        {
            return new CredentialEntryViewModel(
                web.Id,
                web.Name,
                web.AuthType,
                web.WebAuthKind,
                new Dictionary<string, string>(web.Fields, StringComparer.Ordinal));
        }

        return CreateNew();
    }

    public CredentialEntry ToModel()
    {
        var fields = _fields
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        return new WebCredentialEntry(Id, Name.Trim(), WebAuthKind, fields);
    }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
        {
            errors.Add("Credential name is required.");
        }

        if (AuthType == HostAuthKind.Windows)
        {
            errors.Add("Windows authentication is coming soon. Select Web.");
            return errors;
        }

        switch (WebAuthKind)
        {
            case WebAuthKind.UsernamePassword:
            case WebAuthKind.UsernameEmailOtp:
            case WebAuthKind.UsernameSmsOtp:
            case WebAuthKind.HttpBasicAuth:
                Require(Username, "Username", errors);
                Require(Password, "Password", errors);
                break;
            case WebAuthKind.Totp:
                Require(Username, "Username", errors);
                Require(Password, "Password", errors);
                Require(TotpSecret, "TOTP secret", errors);
                break;
            case WebAuthKind.OAuthSso:
                Require(ProviderName, "Provider name", errors);
                Require(Username, "Username", errors);
                Require(Password, "Password", errors);
                break;
            case WebAuthKind.ApiKeyBearer:
                Require(TokenName, "Token name", errors);
                Require(Token, "Token", errors);
                break;
            case WebAuthKind.CertificateMtls:
                Require(CertificatePath, "Certificate path", errors);
                break;
            case WebAuthKind.Custom:
                Require(Label, "Label", errors);
                break;
        }

        return errors;
    }

    private static void Require(string value, string label, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{label} is required.");
        }
    }

    private string GetField(string key)
    {
        return _fields.TryGetValue(key, out var value) ? value : string.Empty;
    }

    private void SetField(string key, string value)
    {
        var newValue = value ?? string.Empty;
        if (_fields.TryGetValue(key, out var current) && string.Equals(current, newValue, StringComparison.Ordinal))
        {
            return;
        }

        _fields[key] = newValue;
        OnPropertyChanged(GetPropertyNameForFieldKey(key));
    }

    private static string GetPropertyNameForFieldKey(string key)
    {
        return key switch
        {
            nameof(Username) => nameof(Username),
            nameof(Password) => nameof(Password),
            nameof(ImapHost) => nameof(ImapHost),
            nameof(ImapPort) => nameof(ImapPort),
            nameof(ImapUsername) => nameof(ImapUsername),
            nameof(ImapPassword) => nameof(ImapPassword),
            nameof(MailboxFolder) => nameof(MailboxFolder),
            nameof(SubjectContains) => nameof(SubjectContains),
            nameof(PhoneHint) => nameof(PhoneHint),
            nameof(TotpSecret) => nameof(TotpSecret),
            nameof(ProviderName) => nameof(ProviderName),
            nameof(TokenName) => nameof(TokenName),
            nameof(Token) => nameof(Token),
            nameof(CertificatePath) => nameof(CertificatePath),
            nameof(CertificatePassword) => nameof(CertificatePassword),
            nameof(PrivateKeyPath) => nameof(PrivateKeyPath),
            nameof(Label) => nameof(Label),
            nameof(Notes) => nameof(Notes),
            _ => key,
        };
    }

    private void RaiseWebKindVisibilityChanges()
    {
        OnPropertyChanged(nameof(IsUsernamePasswordKind));
        OnPropertyChanged(nameof(IsUsernameEmailOtpKind));
        OnPropertyChanged(nameof(IsUsernameSmsOtpKind));
        OnPropertyChanged(nameof(IsTotpKind));
        OnPropertyChanged(nameof(IsOAuthSsoKind));
        OnPropertyChanged(nameof(IsHttpBasicAuthKind));
        OnPropertyChanged(nameof(IsApiKeyBearerKind));
        OnPropertyChanged(nameof(IsCertificateMtlsKind));
        OnPropertyChanged(nameof(IsCustomKind));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
