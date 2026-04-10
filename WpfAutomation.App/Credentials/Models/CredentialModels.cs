using System.Text.Json.Serialization;

namespace WpfAutomation.App.Credentials.Models;

public enum HostAuthKind
{
    Web,
    Windows,
}

public enum WebAuthKind
{
    UsernamePassword,
    UsernameEmailOtp,
    UsernameSmsOtp,
    Totp,
    OAuthSso,
    HttpBasicAuth,
    ApiKeyBearer,
    CertificateMtls,
    Custom,
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(WebCredentialEntry), typeDiscriminator: "web")]
public abstract record CredentialEntry(Guid Id, string Name, HostAuthKind AuthType);

public sealed record WebCredentialEntry(
    Guid Id,
    string Name,
    WebAuthKind WebAuthKind,
    Dictionary<string, string> Fields)
    : CredentialEntry(Id, Name, HostAuthKind.Web)
{
    /// <summary>
    /// Returns the set of field keys that contain secrets for the given auth kind.
    /// These values must never appear in logs or flow JSON.
    /// </summary>
    public static IReadOnlySet<string> SensitiveFieldNames(WebAuthKind kind) => kind switch
    {
        WebAuthKind.UsernamePassword => new HashSet<string>(StringComparer.Ordinal) { "Password" },
        WebAuthKind.UsernameEmailOtp => new HashSet<string>(StringComparer.Ordinal) { "Password", "ImapPassword" },
        WebAuthKind.UsernameSmsOtp   => new HashSet<string>(StringComparer.Ordinal) { "Password" },
        WebAuthKind.Totp             => new HashSet<string>(StringComparer.Ordinal) { "Password", "TotpSecret" },
        WebAuthKind.OAuthSso         => new HashSet<string>(StringComparer.Ordinal) { "Password" },
        WebAuthKind.HttpBasicAuth    => new HashSet<string>(StringComparer.Ordinal) { "Password" },
        WebAuthKind.ApiKeyBearer     => new HashSet<string>(StringComparer.Ordinal) { "Token" },
        WebAuthKind.CertificateMtls  => new HashSet<string>(StringComparer.Ordinal) { "CertificatePassword" },
        _                            => new HashSet<string>(StringComparer.Ordinal),
    };

    /// <summary>Known field key constants for each auth type.</summary>
    public static class FieldKeys
    {
        // Shared
        public const string Username = "Username";
        public const string Password = "Password";

        // UsernameEmailOtp
        public const string ImapHost            = "ImapHost";
        public const string ImapPort            = "ImapPort";
        public const string ImapUsername        = "ImapUsername";
        public const string ImapPassword        = "ImapPassword";
        public const string MailboxFolder       = "MailboxFolder";
        public const string SubjectContains     = "SubjectContains";

        // UsernameSmsOtp
        public const string PhoneHint = "PhoneHint";

        // Totp
        public const string TotpSecret = "TotpSecret";

        // OAuthSso
        public const string ProviderName = "ProviderName";

        // ApiKeyBearer
        public const string TokenName = "TokenName";
        public const string Token     = "Token";

        // CertificateMtls
        public const string CertificatePath     = "CertificatePath";
        public const string CertificatePassword = "CertificatePassword";
        public const string PrivateKeyPath      = "PrivateKeyPath";

        // Custom
        public const string Label = "Label";
        public const string Notes = "Notes";
    }
}
