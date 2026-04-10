using System.Globalization;
using System.Windows.Data;
using WpfAutomation.App.Credentials.Models;

namespace WpfAutomation.App.Credentials;

/// <summary>
/// Converts <see cref="WebAuthKind"/> enum values to user-friendly display names.
/// </summary>
public sealed class WebAuthKindDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is WebAuthKind kind)
        {
            return GetDisplayName(kind);
        }

        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Gets a user-friendly display name for the given auth kind.
    /// </summary>
    public static string GetDisplayName(WebAuthKind kind) => kind switch
    {
        WebAuthKind.UsernamePassword => "Username & Password",
        WebAuthKind.UsernameEmailOtp => "Username & Email OTP",
        WebAuthKind.UsernameSmsOtp => "Username & SMS OTP",
        WebAuthKind.Totp => "Time-based OTP (TOTP)",
        WebAuthKind.OAuthSso => "OAuth / SSO",
        WebAuthKind.HttpBasicAuth => "HTTP Basic Authentication",
        WebAuthKind.ApiKeyBearer => "API Key / Bearer Token",
        WebAuthKind.CertificateMtls => "Certificate (mTLS)",
        WebAuthKind.Custom => "Custom",
        _ => kind.ToString(),
    };

    /// <summary>
    /// Gets a brief description for the given auth kind to use as a tooltip.
    /// </summary>
    public static string GetDescription(WebAuthKind kind) => kind switch
    {
        WebAuthKind.UsernamePassword => "Standard username and password authentication",
        WebAuthKind.UsernameEmailOtp => "Username with one-time password delivered via email",
        WebAuthKind.UsernameSmsOtp => "Username with one-time password delivered via SMS",
        WebAuthKind.Totp => "Time-based one-time password (Google Authenticator, Authy, etc.)",
        WebAuthKind.OAuthSso => "OAuth 2.0 or Single Sign-On (Google, Microsoft, etc.)",
        WebAuthKind.HttpBasicAuth => "HTTP Basic Authentication (username and password in headers)",
        WebAuthKind.ApiKeyBearer => "API key or Bearer token authentication",
        WebAuthKind.CertificateMtls => "Mutual TLS with certificate and private key",
        WebAuthKind.Custom => "Custom authentication method",
        _ => string.Empty,
    };
}
