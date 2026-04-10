using System.Diagnostics;
using System.Text.RegularExpressions;
using PlaywrightClientCertificate = Microsoft.Playwright.ClientCertificate;
using PlaywrightHttpCredentials = Microsoft.Playwright.HttpCredentials;
using WpfAutomation.App.Credentials.Models;
using WpfAutomation.App.Models.Flow;
using WpfAutomation.App.Services.Credentials;
using WpfAutomation.Core.Abstractions;
using WpfAutomation.Core.Browser;
using WpfAutomation.Core.Configuration;
using WpfAutomation.Core.Diagnostics;
using WpfAutomation.Core.Exceptions;

namespace WpfAutomation.App.Services.Flow;

public sealed class PlaywrightFlowExecutionBridge : IFlowExecutionBridge
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    private readonly IFlowRuntimeExecutor _runtimeExecutor;
    private readonly IBrowserLauncherFactory _browserLauncherFactory;
    private readonly BrowserOptions _baseBrowserOptions;
    private readonly DiagnosticsService _diagnosticsService;
    private readonly IMasterPasswordService _masterPasswordService;
    private readonly ICredentialStore? _credentialStore;
    private readonly IWebAuthExecutor _webAuthExecutor;
    private BrowserType _activeBrowserType = BrowserType.Chromium;
    private BrowserSession? _activeSession;
    private IPageWrapper? _currentPage;

    public PlaywrightFlowExecutionBridge(
        IFlowRuntimeExecutor runtimeExecutor,
        IBrowserLauncherFactory browserLauncherFactory,
        BrowserOptions baseBrowserOptions,
        DiagnosticsService diagnosticsService,
        IMasterPasswordService? masterPasswordService = null,
        ICredentialStore? credentialStore = null,
        IWebAuthExecutor? webAuthExecutor = null)
    {
        _runtimeExecutor = runtimeExecutor;
        _browserLauncherFactory = browserLauncherFactory;
        _baseBrowserOptions = baseBrowserOptions;
        _diagnosticsService = diagnosticsService;
        _masterPasswordService = masterPasswordService ?? new NullMasterPasswordService();
        _credentialStore = credentialStore;
        _webAuthExecutor = webAuthExecutor ?? new NullWebAuthExecutor();
    }

    public async Task PrepareRunAsync(ExecutionFlowGraph executionGraph, bool forceHeaded = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(executionGraph);

        if (!_masterPasswordService.EnsureUnlockedBeforeRun())
        {
            throw new OperationCanceledException("Credential store unlock was cancelled by the user.");
        }

        await CloseActiveSessionAsync();

        var runtimeResult = await _runtimeExecutor.ExecuteAsync(executionGraph, cancellationToken);
        var nodesBySourceId = executionGraph.Nodes.ToDictionary(node => node.SourceNodeId, StringComparer.Ordinal);

        _diagnosticsService.Info("Flow run start.", new Dictionary<string, string>
        {
            ["nodeCount"] = executionGraph.Nodes.Count.ToString(),
            ["edgeCount"] = executionGraph.Edges.Count.ToString(),
            ["executedActionCount"] = runtimeResult.ExecutedNodeIds.Count.ToString(),
        });

        foreach (var sourceNodeId in runtimeResult.ExecutedNodeIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!nodesBySourceId.TryGetValue(sourceNodeId, out var node) || node.NodeKind != FlowNodeKind.Action)
            {
                continue;
            }

            var context = CreateNodeContext(node);
            _diagnosticsService.Info("Flow action start.", context);

            try
            {
                await ExecuteActionAsync(node, forceHeaded, cancellationToken);
                _diagnosticsService.Info("Flow action complete.", context);
            }
            catch (OperationCanceledException)
            {
                _diagnosticsService.Warn("Flow action cancelled.", context);
                throw;
            }
            catch (Exception exception)
            {
                _diagnosticsService.Error("Flow action failed.", exception, context);
                throw;
            }
        }

        _diagnosticsService.Info("Flow run complete.");
    }

    public async Task CloseActiveSessionAsync()
    {
        _currentPage = null;

        if (_activeSession is null)
        {
            return;
        }

        await _activeSession.CloseAsync();
        _activeSession = null;
    }

    private async Task ExecuteActionAsync(IExecutionFlowNode node, bool forceHeaded, CancellationToken cancellationToken)
    {
        switch (node.ActionId)
        {
            case "open-browser":
                await OpenBrowserAsync(RequireParameters<OpenBrowserActionParameters>(node), forceHeaded, cancellationToken);
                return;
            case "new-page":
                await NewPageAsync(RequireParameters<NewPageActionParameters>(node), forceHeaded, cancellationToken);
                return;
            case "close-browser":
                await CloseBrowserAsync(RequireParameters<CloseBrowserActionParameters>(node));
                return;
            case "navigate-to-url":
                await NavigateToUrlAsync(RequireParameters<NavigateToUrlActionParameters>(node), forceHeaded, cancellationToken);
                return;
            case "click-element":
                await ClickElementAsync(RequireParameters<ClickElementActionParameters>(node), cancellationToken);
                return;
            case "fill-input":
                await FillInputAsync(RequireParameters<FillInputActionParameters>(node), cancellationToken);
                return;
            case "hover-element":
                await HoverElementAsync(RequireParameters<HoverElementActionParameters>(node), cancellationToken);
                return;
            case "select-option":
                await SelectOptionAsync(RequireParameters<SelectOptionActionParameters>(node), cancellationToken);
                return;
            case "expect-enabled":
                await ExpectEnabledAsync(RequireParameters<ExpectEnabledActionParameters>(node), cancellationToken);
                return;
            case "expect-hidden":
                await ExpectHiddenAsync(RequireParameters<ExpectHiddenActionParameters>(node), cancellationToken);
                return;
            case "expect-text":
                await ExpectTextAsync(RequireParameters<ExpectTextActionParameters>(node), cancellationToken);
                return;
            case "expect-visible":
                await ExpectVisibleAsync(RequireParameters<ExpectVisibleActionParameters>(node), cancellationToken);
                return;
            case "wait-for-url":
                await WaitForUrlAsync(RequireParameters<WaitForUrlActionParameters>(node), cancellationToken);
                return;
            default:
                throw new NotSupportedException($"Flow action '{node.ActionId}' is not yet bound to runtime execution.");
        }
    }

    private async Task OpenBrowserAsync(OpenBrowserActionParameters parameters, bool forceHeaded, CancellationToken cancellationToken)
    {
        await CloseActiveSessionAsync();

        var browserType = ResolveBrowserType(parameters.BrowserEngine);
        _activeBrowserType = browserType;
        var options = CreateBrowserOptions(parameters, forceHeaded);
        var launcher = _browserLauncherFactory.Create(browserType);

        _diagnosticsService.Info("Flow open-browser start.", new Dictionary<string, string>
        {
            ["browserEngine"] = parameters.BrowserEngine,
            ["headless"] = options.Headless.ToString(),
            ["timeoutMs"] = options.TimeoutMs.ToString(),
            ["retryCount"] = options.RetryCount.ToString(),
        });

        _activeSession = await launcher.StartAsync(options, cancellationToken);
        _currentPage = await _activeSession.NewPageAsync();
    }

    private async Task NewPageAsync(NewPageActionParameters parameters, bool forceHeaded, CancellationToken cancellationToken)
    {
        var session = await EnsureSessionAsync(forceHeaded, cancellationToken);
        _currentPage = await session.NewPageAsync();

        if (!string.IsNullOrWhiteSpace(parameters.InitialUrl))
        {
            _currentPage = await _currentPage.NavigateUrlAsync(parameters.InitialUrl, cancellationToken);
        }
    }

    private async Task CloseBrowserAsync(CloseBrowserActionParameters _)
    {
        await CloseActiveSessionAsync();
    }

    private async Task NavigateToUrlAsync(NavigateToUrlActionParameters parameters, bool forceHeaded, CancellationToken cancellationToken)
    {
        WebCredentialEntry? credential = null;
        if (parameters.EnableAuthentication && !string.IsNullOrWhiteSpace(parameters.CredentialId))
        {
            credential = await ResolveWebCredentialAsync(parameters.CredentialId);
            await ApplyPreNavigationCredentialConfigurationAsync(credential, parameters.Url, forceHeaded, cancellationToken);
        }

        var page = await EnsurePageAsync(forceHeaded, cancellationToken);
        var session = RequireActiveSession();
        _currentPage = await session.WithTemporaryOptionsAsync(
            parameters.TimeoutMs,
            parameters.WaitUntilNetworkIdle,
            () => page.NavigateUrlAsync(parameters.Url, cancellationToken));

        if (credential is not null && RequiresPostNavigationWebAuth(credential.WebAuthKind))
        {
            await _webAuthExecutor.ExecuteAsync(page, session, credential, cancellationToken);
        }
    }

    private async Task<WebCredentialEntry> ResolveWebCredentialAsync(string credentialId)
    {
        if (_credentialStore is null)
        {
            throw new InvalidOperationException("Credential auth requires ICredentialStore, but no store was configured.");
        }

        if (!Guid.TryParse(credentialId, out var parsedCredentialId))
        {
            throw new InvalidOperationException($"Credential id '{credentialId}' is not a valid Guid.");
        }

        var credential = await _credentialStore.GetByIdAsync(parsedCredentialId);
        if (credential is null)
        {
            throw new InvalidOperationException($"Credential '{parsedCredentialId}' was not found in the credential store.");
        }

        if (credential is not WebCredentialEntry webCredential)
        {
            throw new NotSupportedException($"Credential '{parsedCredentialId}' is not a supported web credential type.");
        }

        return webCredential;
    }

    private async Task ApplyPreNavigationCredentialConfigurationAsync(
        WebCredentialEntry credential,
        string targetUrl,
        bool forceHeaded,
        CancellationToken cancellationToken)
    {
        switch (credential.WebAuthKind)
        {
            case WebAuthKind.HttpBasicAuth:
                await RebuildSessionForHttpBasicAuthAsync(credential, forceHeaded, cancellationToken);
                return;
            case WebAuthKind.ApiKeyBearer:
                await ConfigureBearerHeaderAsync(credential, forceHeaded, cancellationToken);
                return;
            case WebAuthKind.CertificateMtls:
                await RebuildSessionForMtlsAsync(credential, targetUrl, forceHeaded, cancellationToken);
                return;
            default:
                return;
        }
    }

    private async Task RebuildSessionForHttpBasicAuthAsync(
        WebCredentialEntry credential,
        bool forceHeaded,
        CancellationToken cancellationToken)
    {
        var username = RequireCredentialField(credential, WebCredentialEntry.FieldKeys.Username);
        var password = RequireCredentialField(credential, WebCredentialEntry.FieldKeys.Password);

        var options = CloneBrowserOptions(forceHeaded);
        options.HttpCredentials = new PlaywrightHttpCredentials
        {
            Username = username,
            Password = password,
        };

        await RebuildSessionAsync(options, cancellationToken);
    }

    private async Task RebuildSessionForMtlsAsync(
        WebCredentialEntry credential,
        string targetUrl,
        bool forceHeaded,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out var parsedTargetUri))
        {
            throw new InvalidOperationException($"Target URL '{targetUrl}' is not a valid absolute URL for mTLS configuration.");
        }

        var certificatePath = RequireCredentialField(credential, WebCredentialEntry.FieldKeys.CertificatePath);
        var certificatePassword = RequireCredentialField(credential, WebCredentialEntry.FieldKeys.CertificatePassword);
        var privateKeyPath = GetCredentialField(credential, WebCredentialEntry.FieldKeys.PrivateKeyPath);

        var certificate = new PlaywrightClientCertificate
        {
            Origin = parsedTargetUri.GetLeftPart(UriPartial.Authority),
            Passphrase = certificatePassword,
        };

        if (!string.IsNullOrWhiteSpace(privateKeyPath))
        {
            certificate.CertPath = certificatePath;
            certificate.KeyPath = privateKeyPath;
        }
        else
        {
            certificate.PfxPath = certificatePath;
        }

        var options = CloneBrowserOptions(forceHeaded);
        options.ClientCertificates = [certificate];

        await RebuildSessionAsync(options, cancellationToken);
    }

    private async Task ConfigureBearerHeaderAsync(
        WebCredentialEntry credential,
        bool forceHeaded,
        CancellationToken cancellationToken)
    {
        var token = RequireCredentialField(credential, WebCredentialEntry.FieldKeys.Token);
        var session = await EnsureSessionAsync(forceHeaded, cancellationToken);
        await session.SetExtraHttpHeadersAsync(
        [
            new KeyValuePair<string, string>("Authorization", $"Bearer {token}"),
        ]);
    }

    private async Task RebuildSessionAsync(BrowserOptions options, CancellationToken cancellationToken)
    {
        await CloseActiveSessionAsync();

        var launcher = _browserLauncherFactory.Create(_activeBrowserType);
        _activeSession = await launcher.StartAsync(options, cancellationToken);
        _currentPage = null;
    }

    private static bool RequiresPostNavigationWebAuth(WebAuthKind webAuthKind)
    {
        return webAuthKind switch
        {
            WebAuthKind.HttpBasicAuth => false,
            WebAuthKind.ApiKeyBearer => false,
            WebAuthKind.CertificateMtls => false,
            _ => true,
        };
    }

    private static string RequireCredentialField(WebCredentialEntry credential, string key)
    {
        if (credential.Fields.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw new InvalidOperationException($"Credential '{credential.Name}' is missing required field '{key}'.");
    }

    private static string? GetCredentialField(WebCredentialEntry credential, string key)
    {
        return credential.Fields.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }

    private static bool IsStrictModeViolation(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current.Message.Contains("strict mode", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryBuildInteractiveIdSelector(string selector, out string interactiveSelector)
    {
        interactiveSelector = string.Empty;
        var trimmedSelector = selector.Trim();

        var shorthandIdMatch = Regex.Match(
            trimmedSelector,
            "^id\\s*=\\s*['\"]?(?<id>[^'\"\\s]+)['\"]?$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        if (shorthandIdMatch.Success)
        {
            var shorthandId = shorthandIdMatch.Groups["id"].Value;
            if (!string.IsNullOrWhiteSpace(shorthandId))
            {
                var escapedShorthandId = shorthandId.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
                interactiveSelector = $"input[id=\"{escapedShorthandId}\"], button[id=\"{escapedShorthandId}\"], a[id=\"{escapedShorthandId}\"], [role=\"button\"][id=\"{escapedShorthandId}\"]";
                return true;
            }
        }

        var idMatch = Regex.Match(
            trimmedSelector,
            "^\\s*\\[id\\s*=\\s*['\"]?(?<id>[^'\"\\]]+)['\"]?\\]\\s*$",
            RegexOptions.CultureInvariant);

        if (!idMatch.Success)
        {
            idMatch = Regex.Match(
                trimmedSelector,
                "^\\s*#(?<id>[^\\s]+)\\s*$",
                RegexOptions.CultureInvariant);
        }

        if (!idMatch.Success)
        {
            return false;
        }

        var id = idMatch.Groups["id"].Value;
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        var escaped = id.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
        interactiveSelector = $"input[id=\"{escaped}\"], button[id=\"{escaped}\"], a[id=\"{escaped}\"], [role=\"button\"][id=\"{escaped}\"]";
        return true;
    }

    private static string NormalizeSelectorForRuntime(string selector)
    {
        var trimmed = selector.Trim();

        var match = Regex.Match(
            trimmed,
            "^\\[class~=\\\"(?<token>(?:\\\\.|[^\\\"])*)\\\"\\]$",
            RegexOptions.CultureInvariant);

        if (!match.Success)
        {
            return trimmed;
        }

        var token = match.Groups["token"].Value
            .Replace("\\\"", "\"", StringComparison.Ordinal)
            .Replace("\\\\", "\\", StringComparison.Ordinal);

        if (token.IndexOfAny(['[', '#', '=', '>', '+', '~', ':']) < 0)
        {
            return trimmed;
        }

        return token;
    }

    private async Task ClickElementAsync(ClickElementActionParameters parameters, CancellationToken cancellationToken)
    {
        EnsureUnsupportedParameter(string.IsNullOrWhiteSpace(parameters.FrameSelector), "click-element", "FrameSelector");
        EnsureUnsupportedParameter(!parameters.Force, "click-element", "Force");

        var page = await RequirePageAsync(cancellationToken);
        await RequireActiveSession().WithTemporaryOptionsAsync(parameters.TimeoutMs, null, async () =>
        {
            await ClickWithIdStrictModeFallbackAsync(page, parameters.Selector, cancellationToken);
        });
    }

    private async Task ClickWithIdStrictModeFallbackAsync(IPageWrapper page, string selector, CancellationToken cancellationToken)
    {
        var normalizedSelector = NormalizeSelectorForRuntime(selector);
        if (!string.Equals(selector, normalizedSelector, StringComparison.Ordinal))
        {
            _diagnosticsService.Warn($"Normalized selector -> {normalizedSelector}");
        }

        var element = page.Search().ByCss(normalizedSelector, cancellationToken);

        try
        {
            await element.ClickAsync(cancellationToken);
        }
        catch (ElementInteractionException exception) when (TryBuildInteractiveIdSelector(normalizedSelector, out var interactiveSelector))
        {
            if (IsStrictModeViolation(exception))
            {
                _diagnosticsService.Warn($"Click strict-mode fallback -> {interactiveSelector}");
            }
            else
            {
                _diagnosticsService.Warn($"Click id fallback -> {interactiveSelector}");
            }

            var fallbackElement = page.Search().ByCss(interactiveSelector, cancellationToken);
            await fallbackElement.ClickAsync(cancellationToken);
        }
    }

    private async Task FillInputAsync(FillInputActionParameters parameters, CancellationToken cancellationToken)
    {
        var page = await RequirePageAsync(cancellationToken);
        await RequireActiveSession().WithTemporaryOptionsAsync(parameters.TimeoutMs, null, async () =>
        {
            var element = page.Search().ByCss(parameters.Selector, cancellationToken);

            if (parameters.ClearFirst)
            {
                await element.FillAsync(parameters.Value, cancellationToken);
                return;
            }

            await element.TypeAsync(parameters.Value, cancellationToken);
        });
    }

    private async Task HoverElementAsync(HoverElementActionParameters parameters, CancellationToken cancellationToken)
    {
        var page = await RequirePageAsync(cancellationToken);
        await RequireActiveSession().WithTemporaryOptionsAsync(parameters.TimeoutMs, null, async () =>
        {
            var element = page.Search().ByCss(parameters.Selector, cancellationToken);
            await element.HoverAsync(cancellationToken);
        });
    }

    private async Task SelectOptionAsync(SelectOptionActionParameters parameters, CancellationToken cancellationToken)
    {
        var page = await RequirePageAsync(cancellationToken);
        await RequireActiveSession().WithTemporaryOptionsAsync(parameters.TimeoutMs, null, async () =>
        {
            var element = page.Search().ByCss(parameters.Selector, cancellationToken);
            await element.SelectAsync(parameters.OptionValue, cancellationToken);
        });
    }

    private async Task ExpectEnabledAsync(ExpectEnabledActionParameters parameters, CancellationToken cancellationToken)
    {
        var page = await RequirePageAsync(cancellationToken);
        await RequireActiveSession().WithTemporaryOptionsAsync(parameters.TimeoutMs, null, async () =>
        {
            var element = page.Search().ByCss(parameters.Selector, cancellationToken);
            await WaitUntilAsync(() => element.IsEnabledAsync(cancellationToken), parameters.TimeoutMs, $"Element '{parameters.Selector}' did not become enabled.", cancellationToken);
        });
    }

    private async Task ExpectHiddenAsync(ExpectHiddenActionParameters parameters, CancellationToken cancellationToken)
    {
        var page = await RequirePageAsync(cancellationToken);
        await RequireActiveSession().WithTemporaryOptionsAsync(parameters.TimeoutMs, null, async () =>
        {
            var element = page.Search().ByCss(parameters.Selector, cancellationToken);
            await WaitUntilAsync(async () => !await element.IsVisibleAsync(cancellationToken), parameters.TimeoutMs, $"Element '{parameters.Selector}' did not become hidden.", cancellationToken);
        });
    }

    private async Task ExpectTextAsync(ExpectTextActionParameters parameters, CancellationToken cancellationToken)
    {
        var page = await RequirePageAsync(cancellationToken);
        await RequireActiveSession().WithTemporaryOptionsAsync(parameters.TimeoutMs, null, async () =>
        {
            var element = page.Search().ByCss(parameters.Selector, cancellationToken);
            await WaitUntilAsync(async () =>
            {
                var text = await element.GetTextAsync(cancellationToken);
                return string.Equals(text, parameters.ExpectedText,
                    parameters.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
            }, parameters.TimeoutMs, $"Element '{parameters.Selector}' text did not match the expected value.", cancellationToken);
        });
    }

    private async Task ExpectVisibleAsync(ExpectVisibleActionParameters parameters, CancellationToken cancellationToken)
    {
        var page = await RequirePageAsync(cancellationToken);
        await RequireActiveSession().WithTemporaryOptionsAsync(parameters.TimeoutMs, null, async () =>
        {
            var element = page.Search().ByCss(parameters.Selector, cancellationToken);
            await WaitUntilAsync(() => element.IsVisibleAsync(cancellationToken), parameters.TimeoutMs, $"Element '{parameters.Selector}' did not become visible.", cancellationToken);
        });
    }

    private async Task WaitForUrlAsync(WaitForUrlActionParameters parameters, CancellationToken cancellationToken)
    {
        var page = await RequirePageAsync(cancellationToken);
        await WaitUntilAsync(
            () => Task.FromResult(UrlMatches(page.CurrentUrl, parameters.UrlPattern, parameters.IsRegex)),
            parameters.TimeoutMs,
            $"URL did not match '{parameters.UrlPattern}'.",
            cancellationToken);
    }

    private async Task<BrowserSession> EnsureSessionAsync(bool forceHeaded, CancellationToken cancellationToken)
    {
        if (_activeSession is not null)
        {
            return _activeSession;
        }

        var launcher = _browserLauncherFactory.Create(_activeBrowserType);
        var options = CloneBrowserOptions(forceHeaded);
        _activeSession = await launcher.StartAsync(options, cancellationToken);
        return _activeSession;
    }

    private async Task<IPageWrapper> EnsurePageAsync(bool forceHeaded, CancellationToken cancellationToken)
    {
        if (_currentPage is not null)
        {
            return _currentPage;
        }

        var session = await EnsureSessionAsync(forceHeaded, cancellationToken);
        _currentPage = await session.NewPageAsync();
        return _currentPage;
    }

    private async Task<IPageWrapper> RequirePageAsync(CancellationToken cancellationToken)
    {
        return await EnsurePageAsync(forceHeaded: false, cancellationToken);
    }

    private BrowserSession RequireActiveSession()
    {
        return _activeSession
            ?? throw new InvalidOperationException("Flow runtime requires an active browser session.");
    }

    private static TParameters RequireParameters<TParameters>(IExecutionFlowNode node)
        where TParameters : ActionParameters
    {
        return node.ActionParameters as TParameters
            ?? throw new InvalidOperationException($"Action '{node.ActionId}' is missing typed parameters of '{typeof(TParameters).Name}'.");
    }

    private static Dictionary<string, string> CreateNodeContext(IExecutionFlowNode node)
    {
        return new Dictionary<string, string>
        {
            ["sourceNodeId"] = node.SourceNodeId,
            ["actionId"] = node.ActionId ?? string.Empty,
            ["displayLabel"] = node.DisplayLabel,
        };
    }

    private static void EnsureUnsupportedParameter(bool condition, string actionId, string parameterName)
    {
        if (condition)
        {
            return;
        }

        throw new NotSupportedException($"Action '{actionId}' does not yet support the '{parameterName}' option at runtime.");
    }

    private BrowserOptions CreateBrowserOptions(OpenBrowserActionParameters parameters, bool forceHeaded)
    {
        var options = CloneBrowserOptions(forceHeaded);
        options.Headless = forceHeaded ? false : parameters.Headless;
        options.TimeoutMs = parameters.TimeoutMs;
        options.RetryCount = parameters.RetryCount;
        return options;
    }

    private BrowserOptions CloneBrowserOptions(bool forceHeaded)
    {
        return new BrowserOptions
        {
            Headless = forceHeaded ? false : _baseBrowserOptions.Headless,
            TimeoutMs = _baseBrowserOptions.TimeoutMs,
            RetryCount = _baseBrowserOptions.RetryCount,
            NavigationWaitUntilNetworkIdle = _baseBrowserOptions.NavigationWaitUntilNetworkIdle,
            ScreenshotDirectory = _baseBrowserOptions.ScreenshotDirectory,
            InspectionExportDirectory = _baseBrowserOptions.InspectionExportDirectory,
            HttpCredentials = _baseBrowserOptions.HttpCredentials is null
                ? null
                : new PlaywrightHttpCredentials
                {
                    Username = _baseBrowserOptions.HttpCredentials.Username,
                    Password = _baseBrowserOptions.HttpCredentials.Password,
                    Origin = _baseBrowserOptions.HttpCredentials.Origin,
                    Send = _baseBrowserOptions.HttpCredentials.Send,
                },
            ClientCertificates = _baseBrowserOptions.ClientCertificates is null
                ? null
                : [.. _baseBrowserOptions.ClientCertificates],
            ExtraHttpHeaders = _baseBrowserOptions.ExtraHttpHeaders is null
                ? null
                : [.. _baseBrowserOptions.ExtraHttpHeaders],
        };
    }

    private static BrowserType ResolveBrowserType(string browserEngine)
    {
        return browserEngine.Trim().ToLowerInvariant() switch
        {
            "chromium" => BrowserType.Chromium,
            "firefox" => BrowserType.Firefox,
            "webkit" => BrowserType.WebKit,
            _ => throw new InvalidOperationException($"Unsupported browser engine '{browserEngine}'."),
        };
    }

    private static bool UrlMatches(string currentUrl, string pattern, bool isRegex)
    {
        if (isRegex)
        {
            return Regex.IsMatch(currentUrl, pattern);
        }

        return string.Equals(currentUrl, pattern, StringComparison.OrdinalIgnoreCase)
            || currentUrl.Contains(pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> predicate, int timeoutMs, string failureMessage, CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromMilliseconds(timeoutMs);
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await predicate())
            {
                return;
            }

            var remaining = timeout - stopwatch.Elapsed;
            await Task.Delay(remaining < PollInterval ? remaining : PollInterval, cancellationToken);
        }

        throw new TimeoutException(failureMessage);
    }

    private sealed class NullWebAuthExecutor : IWebAuthExecutor
    {
        public Task ExecuteAsync(IPageWrapper page, BrowserSession session, WebCredentialEntry credential, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Web auth execution requires IWebAuthExecutor, but no executor was configured.");
        }
    }
}
