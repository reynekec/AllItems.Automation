using System.Security.Cryptography;
using System.Text;
using System.Windows;
using AllItems.Automation.Browser.App.Credentials;
using AllItems.Automation.Browser.App.Credentials.Models;
using AllItems.Automation.Browser.App.Services;
using AllItems.Automation.Browser.Core.Abstractions;
using AllItems.Automation.Browser.Core.Browser;
using AllItems.Automation.Browser.Core.Diagnostics;

namespace AllItems.Automation.Browser.App.Services.Flow;

public sealed class WebAuthExecutor : IWebAuthExecutor
{
    private const string UsernameSelectorKey = "UsernameSelector";
    private const string PasswordSelectorKey = "PasswordSelector";
    private const string SubmitSelectorKey = "SubmitSelector";
    private const string OtpSelectorKey = "OtpSelector";

    private readonly DiagnosticsService _diagnosticsService;
    private readonly IUiDispatcherService _uiDispatcherService;

    public WebAuthExecutor(DiagnosticsService diagnosticsService, IUiDispatcherService uiDispatcherService)
    {
        _diagnosticsService = diagnosticsService;
        _uiDispatcherService = uiDispatcherService;
    }

    public async Task ExecuteAsync(
        IPageWrapper page,
        BrowserSession session,
        WebCredentialEntry credential,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(credential);

        _diagnosticsService.Info("Web auth start.", new Dictionary<string, string>
        {
            ["credentialId"] = credential.Id.ToString(),
            ["authKind"] = credential.WebAuthKind.ToString(),
        });

        switch (credential.WebAuthKind)
        {
            case WebAuthKind.UsernamePassword:
                await ExecuteUsernamePasswordAsync(page, credential, cancellationToken);
                return;
            case WebAuthKind.UsernameEmailOtp:
                await ExecuteUsernamePasswordWithOtpAsync(page, credential, useEmailOtp: true, cancellationToken);
                return;
            case WebAuthKind.UsernameSmsOtp:
                await ExecuteUsernamePasswordWithOtpAsync(page, credential, useEmailOtp: false, cancellationToken);
                return;
            case WebAuthKind.Totp:
                await ExecuteTotpAsync(page, credential, cancellationToken);
                return;
            case WebAuthKind.OAuthSso:
                await ExecuteUsernamePasswordAsync(page, credential, cancellationToken);
                return;
            case WebAuthKind.HttpBasicAuth:
                await ExecuteHttpBasicAuthAsync(session, credential, cancellationToken);
                return;
            case WebAuthKind.ApiKeyBearer:
                await ExecuteApiKeyBearerAsync(session, credential, cancellationToken);
                return;
            case WebAuthKind.CertificateMtls:
                await ExecuteCertificateMtlsAsync(credential, cancellationToken);
                return;
            case WebAuthKind.Custom:
                _diagnosticsService.Warn("Custom web auth credential is informational only; no runtime actions were executed.",
                    new Dictionary<string, string> { ["credentialId"] = credential.Id.ToString() });
                return;
        }

        throw new NotSupportedException($"Unsupported web auth kind '{credential.WebAuthKind}'.");
    }

    private async Task ExecuteUsernamePasswordAsync(IPageWrapper page, WebCredentialEntry credential, CancellationToken cancellationToken)
    {
        var username = RequireField(credential, WebCredentialEntry.FieldKeys.Username);
        var password = RequireField(credential, WebCredentialEntry.FieldKeys.Password);

        var usernameSelector = GetField(credential, UsernameSelectorKey, "input[name='username']");
        var passwordSelector = GetField(credential, PasswordSelectorKey, "input[type='password']");
        var submitSelector = GetField(credential, SubmitSelectorKey, "button[type='submit']");

        await page.Search().ByCss(usernameSelector, cancellationToken).FillAsync(username, cancellationToken);
        await page.Search().ByCss(passwordSelector, cancellationToken).FillAsync(password, cancellationToken);
        await page.Search().ByCss(submitSelector, cancellationToken).ClickAsync(cancellationToken);
    }

    private async Task ExecuteUsernamePasswordWithOtpAsync(
        IPageWrapper page,
        WebCredentialEntry credential,
        bool useEmailOtp,
        CancellationToken cancellationToken)
    {
        await ExecuteUsernamePasswordAsync(page, credential, cancellationToken);

        if (useEmailOtp)
        {
            await TryResolveEmailOtpViaImapAsync(credential, cancellationToken);
        }

        var promptTarget = GetOtpPromptTarget(credential);
        var otpCode = await ShowOtpPromptAsync(promptTarget);
        if (string.IsNullOrWhiteSpace(otpCode))
        {
            throw new InvalidOperationException("OTP entry is required to continue authentication.");
        }

        var otpSelector = GetField(credential, OtpSelectorKey, "input[name='otp']");
        await page.Search().ByCss(otpSelector, cancellationToken).FillAsync(otpCode.Trim(), cancellationToken);
        var submitSelector = GetField(credential, SubmitSelectorKey, "button[type='submit']");
        await page.Search().ByCss(submitSelector, cancellationToken).ClickAsync(cancellationToken);
    }

    private async Task ExecuteHttpBasicAuthAsync(BrowserSession session, WebCredentialEntry credential, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var username = RequireField(credential, WebCredentialEntry.FieldKeys.Username);
        var password = RequireField(credential, WebCredentialEntry.FieldKeys.Password);
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));

        await session.SetExtraHttpHeadersAsync(new[]
        {
            new KeyValuePair<string, string>("Authorization", $"Basic {encoded}"),
        });
    }

    private async Task ExecuteApiKeyBearerAsync(BrowserSession session, WebCredentialEntry credential, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var token = RequireField(credential, WebCredentialEntry.FieldKeys.Token);
        await session.SetExtraHttpHeadersAsync(new[]
        {
            new KeyValuePair<string, string>("Authorization", $"Bearer {token}"),
        });
    }

    private Task ExecuteCertificateMtlsAsync(WebCredentialEntry credential, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _ = RequireField(credential, WebCredentialEntry.FieldKeys.CertificatePath);
        _ = RequireField(credential, WebCredentialEntry.FieldKeys.CertificatePassword);

        _diagnosticsService.Info(
            "Certificate mTLS credential detected. Client certificate configuration is applied during pre-navigation session bootstrap.",
            new Dictionary<string, string>
            {
                ["credentialId"] = credential.Id.ToString(),
                ["authKind"] = credential.WebAuthKind.ToString(),
            });

        return Task.CompletedTask;
    }

    private Task TryResolveEmailOtpViaImapAsync(WebCredentialEntry credential, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var hasImapHost = credential.Fields.TryGetValue(WebCredentialEntry.FieldKeys.ImapHost, out var imapHost)
            && !string.IsNullOrWhiteSpace(imapHost);
        var hasImapUser = credential.Fields.TryGetValue(WebCredentialEntry.FieldKeys.ImapUsername, out var imapUsername)
            && !string.IsNullOrWhiteSpace(imapUsername);
        var hasImapPassword = credential.Fields.TryGetValue(WebCredentialEntry.FieldKeys.ImapPassword, out var imapPassword)
            && !string.IsNullOrWhiteSpace(imapPassword);

        if (hasImapHost && hasImapUser && hasImapPassword)
        {
            _diagnosticsService.Warn(
                "Email OTP IMAP settings were provided, but IMAP polling is not yet enabled in this runtime; prompting for manual OTP entry.",
                new Dictionary<string, string> { ["credentialId"] = credential.Id.ToString() });
        }

        return Task.CompletedTask;
    }

    private async Task ExecuteTotpAsync(IPageWrapper page, WebCredentialEntry credential, CancellationToken cancellationToken)
    {
        await ExecuteUsernamePasswordAsync(page, credential, cancellationToken);

        var secret = RequireField(credential, WebCredentialEntry.FieldKeys.TotpSecret);
        var code = ComputeTotp(secret, DateTimeOffset.UtcNow);

        var otpSelector = GetField(credential, OtpSelectorKey, "input[name='otp']");
        await page.Search().ByCss(otpSelector, cancellationToken).FillAsync(code, cancellationToken);

        var submitSelector = GetField(credential, SubmitSelectorKey, "button[type='submit']");
        await page.Search().ByCss(submitSelector, cancellationToken).ClickAsync(cancellationToken);
    }

    private Task<string?> ShowOtpPromptAsync(string target)
    {
        return ShowOtpPromptCoreAsync(target);
    }

    private async Task<string?> ShowOtpPromptCoreAsync(string target)
    {
        string? result = null;

        await _uiDispatcherService.InvokeAsync(() =>
        {
            var window = new OtpInputWindow(target)
            {
                Owner = Application.Current?.MainWindow,
            };

            var dialogResult = window.ShowDialog();
            result = dialogResult == true ? window.OtpCode : null;
        });

        return result;
    }

    private static string GetOtpPromptTarget(WebCredentialEntry credential)
    {
        var phoneHint = GetField(credential, WebCredentialEntry.FieldKeys.PhoneHint, string.Empty);
        if (!string.IsNullOrWhiteSpace(phoneHint))
        {
            return phoneHint;
        }

        var email = GetField(credential, WebCredentialEntry.FieldKeys.ImapUsername, string.Empty);
        if (!string.IsNullOrWhiteSpace(email))
        {
            return email;
        }

        return "your authenticator";
    }

    private static string RequireField(WebCredentialEntry credential, string key)
    {
        if (credential.Fields.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw new InvalidOperationException($"Credential '{credential.Name}' is missing required field '{key}'.");
    }

    private static string GetField(WebCredentialEntry credential, string key, string defaultValue)
    {
        return credential.Fields.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : defaultValue;
    }

    private static string ComputeTotp(string base32Secret, DateTimeOffset timestamp)
    {
        var key = DecodeBase32(base32Secret);
        var counter = timestamp.ToUnixTimeSeconds() / 30;

        Span<byte> counterBytes = stackalloc byte[8];
        for (var i = 7; i >= 0; i--)
        {
            counterBytes[i] = (byte)(counter & 0xFF);
            counter >>= 8;
        }

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(counterBytes.ToArray());
        var offset = hash[^1] & 0x0F;
        var binaryCode =
            ((hash[offset] & 0x7F) << 24) |
            ((hash[offset + 1] & 0xFF) << 16) |
            ((hash[offset + 2] & 0xFF) << 8) |
            (hash[offset + 3] & 0xFF);

        var otp = binaryCode % 1_000_000;
        return otp.ToString("D6");
    }

    private static byte[] DecodeBase32(string input)
    {
        var normalized = input.Trim().TrimEnd('=').ToUpperInvariant();
        if (normalized.Length == 0)
        {
            throw new InvalidOperationException("TOTP secret cannot be empty.");
        }

        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bits = 0;
        var value = 0;
        var bytes = new List<byte>();

        foreach (var character in normalized)
        {
            if (char.IsWhiteSpace(character))
            {
                continue;
            }

            var index = alphabet.IndexOf(character);
            if (index < 0)
            {
                throw new InvalidOperationException("TOTP secret must be a valid Base32 string.");
            }

            value = (value << 5) | index;
            bits += 5;

            if (bits >= 8)
            {
                bits -= 8;
                bytes.Add((byte)((value >> bits) & 0xFF));
            }
        }

        return bytes.ToArray();
    }
}
