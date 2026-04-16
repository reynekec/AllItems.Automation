using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;

namespace SelectorDemo.Wpf;

/// <summary>
/// Interaction logic for ContentWindow.xaml
/// </summary>
public partial class BrowserWindow : Window
{
    private const double SelectorPanelDefaultWidth = 320;
    private const double DebugPanelDefaultWidth = 360;
    private const double DockSplitterWidth = 6;

    private readonly string _webViewUserDataPrimary = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AllItems",
        "SelectorDemo",
        "WebView2");

    private readonly string _webViewUserDataFallback = Path.Combine(
        Path.GetTempPath(),
        "AllItems",
        "SelectorDemo",
        "WebView2");

    private TaskCompletionSource<SelectionResult>? _inAppSelectionCompletion;
    private bool _webMessageHooked;
    private bool _debugProtocolHooked;
    private bool _debugCaptureActive;
    private bool _updatingDebugToggle;
    private bool _updatingDockTabs;
    private readonly List<CoreWebView2Frame> _allKnownFrames = [];
    private volatile bool _selectionActive;
    private CoreWebView2DevToolsProtocolEventReceiver? _networkResponseReceiver;
    private CoreWebView2DevToolsProtocolEventReceiver? _networkLoadingFailedReceiver;
    private CoreWebView2DevToolsProtocolEventReceiver? _consoleReceiver;
    private CoreWebView2DevToolsProtocolEventReceiver? _logEntryReceiver;

    private string? _initialUrl;
    private readonly bool _startWithDebug;
    private double _selectorPanelWidth = SelectorPanelDefaultWidth;
    private double _debugPanelWidth = DebugPanelDefaultWidth;

    public BrowserWindow()
    {
        InitializeComponent();
        Loaded += ContentWindow_Loaded;
    }

    public BrowserWindow(string url, bool startWithDebug = false) : this()
    {
        _initialUrl = url;
        _startWithDebug = startWithDebug;
    }

    private async void ContentWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await EnsureBrowserReadyAsync();

        if (_startWithDebug)
        {
            await StartDebugCaptureAsync();
        }

        if (!string.IsNullOrEmpty(_initialUrl))
        {
            NavigateTo(_initialUrl);
        }

        // Subscribe to tree node hover events
        SelectionPanel.TreeNodeHoverChanged += async (selector) =>
        {
            await HighlightElementBySelectorAsync(selector);
        };
    }

    private async Task EnsureBrowserReadyAsync()
    {
        if (BrowserView.CoreWebView2 is not null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(_webViewUserDataPrimary);
            var primaryEnvironment = await CoreWebView2Environment.CreateAsync(userDataFolder: _webViewUserDataPrimary);
            await BrowserView.EnsureCoreWebView2Async(primaryEnvironment);
        }
        catch (UnauthorizedAccessException)
        {
            Directory.CreateDirectory(_webViewUserDataFallback);
            var fallbackEnvironment = await CoreWebView2Environment.CreateAsync(userDataFolder: _webViewUserDataFallback);
            await BrowserView.EnsureCoreWebView2Async(fallbackEnvironment);
            StatusTextBox.Text = "WebView2 profile folder access was denied. Using fallback temp profile.";
        }

        BrowserView.CoreWebView2!.Settings.AreDefaultContextMenusEnabled = true;

        if (!_webMessageHooked)
        {
            BrowserView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
            BrowserView.CoreWebView2.FrameCreated += CoreWebView2_FrameCreated;
            BrowserView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
            BrowserView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
            BrowserView.CoreWebView2.WebResourceRequested += CoreWebView2_WebResourceRequested;
            BrowserView.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            _webMessageHooked = true;
        }

        await EnsureDebugProtocolAsync();
    }

    private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        => HandleWebMessage(e.WebMessageAsJson);

    private void CoreWebView2_FrameCreated(object? sender, CoreWebView2FrameCreatedEventArgs e)
    {
        var frame = e.Frame;

        lock (_allKnownFrames)
        {
            _allKnownFrames.Add(frame);
        }

        frame.Destroyed += (_, _) =>
        {
            lock (_allKnownFrames) { _allKnownFrames.Remove(frame); }
        };

        frame.WebMessageReceived += (_, args) => HandleWebMessage(args.WebMessageAsJson);

        if (_selectionActive)
        {
            _ = TryInjectFrameScriptAsync(frame);
        }

        if (_debugCaptureActive)
        {
            _ = TryInjectDebugFrameScriptAsync(frame);
        }
    }

    private static async Task TryInjectFrameScriptAsync(CoreWebView2Frame frame)
    {
        try { await frame.ExecuteScriptAsync(BuildFrameScript()); }
        catch { }
    }

    private static async Task TryInjectDebugFrameScriptAsync(CoreWebView2Frame frame)
    {
        try { await frame.ExecuteScriptAsync(BuildDebugCaptureScript()); }
        catch { }
    }

    private void HandleWebMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 512 });
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeNode) ||
                typeNode.ValueKind != JsonValueKind.String)
            {
                return;
            }

            var messageType = typeNode.GetString();

            if (messageType == "selector-capture-result" && _inAppSelectionCompletion is not null)
            {
                var result = ReadSelectionResult(root);
                _inAppSelectionCompletion.TrySetResult(result);
                return;
            }

            if (messageType == "browser-debug-event" && _debugCaptureActive)
            {
                var debugEvent = ReadDebugEvent(root);
                AddDebugEntry(debugEvent.Category, debugEvent.Message, debugEvent.Detail);
            }
        }
        catch (Exception ex)
        {
            if (_inAppSelectionCompletion is not null)
            {
                _inAppSelectionCompletion.TrySetResult(
                    new SelectionResult("error", ex.Message, string.Empty, string.Empty, string.Empty));
            }

            if (_debugCaptureActive)
            {
                AddDebugEntry("Debug", "Failed to read a browser debug message.", ex.Message);
            }
        }
    }

    public async void NavigateTo(string url)
    {
        try
        {
            await EnsureBrowserReadyAsync();
            BrowserView.CoreWebView2!.Navigate(url);
            StatusTextBox.Text = "Navigated. Use Start Selection (In-App) to pick an element.";
        }
        catch (Exception ex)
        {
            StatusTextBox.Text = $"Navigation error: {ex.Message}";
        }
    }

    public async Task HighlightElementBySelectorAsync(string? selector)
    {
        if (string.IsNullOrEmpty(selector))
        {
            // Clear highlight
            try
            {
                await BrowserView.CoreWebView2!.ExecuteScriptAsync("(function() { var h = document.querySelector('[data-highlight-temp]'); if (h) h.remove(); })();");
            }
            catch { }
            return;
        }

        try
        {
            await EnsureBrowserReadyAsync();
            
            // Escape the selector for JavaScript strings
            var escapedSelector = selector.Replace("\\", "\\\\").Replace("\"", "\\\"");
            
            var script = $@"
(function() {{
    // Remove previous highlight if exists
    var prev = document.querySelector('[data-highlight-temp]');
    if (prev) prev.remove();
    
    // Find element by selector
    try {{
        var el = document.querySelector(""{escapedSelector}"");
        if (el && el !== document.documentElement && el !== document.body) {{
            var rect = el.getBoundingClientRect();
            var highlight = document.createElement('div');
            highlight.setAttribute('data-highlight-temp', 'true');
            highlight.style.position = 'fixed';
            highlight.style.left = Math.max(rect.left, 0) + 'px';
            highlight.style.top = Math.max(rect.top, 0) + 'px';
            highlight.style.width = Math.max(rect.width, 2) + 'px';
            highlight.style.height = Math.max(rect.height, 2) + 'px';
            highlight.style.border = '2px solid #2ec27e';
            highlight.style.background = 'rgba(46, 194, 126, 0.16)';
            highlight.style.pointerEvents = 'none';
            highlight.style.zIndex = '2147483647';
            document.documentElement.appendChild(highlight);
        }}
    }} catch (e) {{
        console.error('Highlight error:', e);
    }}
}})();
";

            await BrowserView.CoreWebView2!.ExecuteScriptAsync(script);
        }
        catch (Exception ex)
        {
            // Silently fail - highlighting is not critical
        }
    }

    public async Task EnableDebugCaptureAsync()
    {
        if (_debugCaptureActive)
        {
            return;
        }

        await StartDebugCaptureAsync();
    }

    private async void btnSelectElement_Click(object sender, RoutedEventArgs e)
    {
        await StartInAppSelectionAsync();
    }

    private async void btnToggleDebug_Checked(object sender, RoutedEventArgs e)
    {
        if (_updatingDebugToggle)
        {
            return;
        }

        await StartDebugCaptureAsync();
    }

    private async void btnToggleDebug_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_updatingDebugToggle)
        {
            return;
        }

        await StopDebugCaptureAsync();
    }

    private void SelectorDockTab_Checked(object sender, RoutedEventArgs e)
        => SetSelectorDockOpen(true);

    private void SelectorDockTab_Unchecked(object sender, RoutedEventArgs e)
        => SetSelectorDockOpen(false);

    private void DebugDockTab_Checked(object sender, RoutedEventArgs e)
        => SetDebugDockOpen(true);

    private void DebugDockTab_Unchecked(object sender, RoutedEventArgs e)
        => SetDebugDockOpen(false);

    private async Task StartInAppSelectionAsync()
    {
        btnSelectElement.IsEnabled = false;

        StatusTextBox.Text = "Selection active — click any element, including inside embedded frames (Esc to cancel).";

        try
        {
            await EnsureBrowserReadyAsync();

            _selectionActive = true;
            _inAppSelectionCompletion = new TaskCompletionSource<SelectionResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Inject the main-frame capture script.
            await BrowserView.CoreWebView2!.ExecuteScriptAsync(BuildMainFrameScript());

            // Inject a capture script into every iframe already tracked.
            List<CoreWebView2Frame> snapshot;
            lock (_allKnownFrames) { snapshot = [.. _allKnownFrames]; }

            foreach (var frame in snapshot)
            {
                try { await frame.ExecuteScriptAsync(BuildFrameScript()); }
                catch { /* frame may already be gone */ }
            }

            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var result = await _inAppSelectionCompletion.Task.WaitAsync(timeout.Token);

            if (result.Status == "selected")
            {
                EnsureSelectionPanelVisible();
                SelectionPanel.SetSelectors(result.CssSelector, result.XPathSelector);
                SelectionPanel.SetElementBrowserInfo(result.HtmlSource, result.Attributes, result.ComputedStyles, result.DomTreeJson);

                var statusMsg = "Element selected.";
                if (!string.IsNullOrWhiteSpace(result.FrameUrl))
                {
                    statusMsg += $"  (inside frame: {result.FrameUrl})";
                }
                statusMsg += string.IsNullOrWhiteSpace(result.DomTreeJson)
                    ? "  [domTree: missing]"
                    : $"  [domTree: {result.DomTreeJson.Length} chars]";

                StatusTextBox.Text = statusMsg;
                SelectionPanel.SetStatus(statusMsg);
            }
            else
            {
                var message = string.IsNullOrWhiteSpace(result.Message)
                    ? "Selection was cancelled."
                    : result.Message;
                StatusTextBox.Text = message;
            }
        }
        catch (OperationCanceledException)
        {
            StatusTextBox.Text = "Timed out waiting for a click. Selection cancelled.";
        }
        catch (Exception ex)
        {
            StatusTextBox.Text = $"In-app selection failed: {ex.Message}";
        }
        finally
        {
            _selectionActive = false;
            _inAppSelectionCompletion = null;
            btnSelectElement.IsEnabled = true;
            await CleanupSelectionAsync();
        }
    }

    private async Task CleanupSelectionAsync()
    {
        var cleanup = BuildSelectionCleanupScript();

        try
        {
            if (BrowserView.CoreWebView2 is not null)
            {
                await BrowserView.CoreWebView2.ExecuteScriptAsync(cleanup);
            }
        }
        catch { }

        List<CoreWebView2Frame> snapshot;
        lock (_allKnownFrames) { snapshot = [.. _allKnownFrames]; }

        foreach (var frame in snapshot)
        {
            try { await frame.ExecuteScriptAsync(cleanup); }
            catch { }
        }
    }

    private async Task EnsureDebugProtocolAsync()
    {
        if (_debugProtocolHooked || BrowserView.CoreWebView2 is null)
        {
            return;
        }

        await BrowserView.CoreWebView2.CallDevToolsProtocolMethodAsync("Network.enable", "{}");
        await BrowserView.CoreWebView2.CallDevToolsProtocolMethodAsync("Runtime.enable", "{}");
        await BrowserView.CoreWebView2.CallDevToolsProtocolMethodAsync("Log.enable", "{}");

        _networkResponseReceiver = BrowserView.CoreWebView2.GetDevToolsProtocolEventReceiver("Network.responseReceived");
        _networkResponseReceiver.DevToolsProtocolEventReceived += NetworkResponseReceived;

        _networkLoadingFailedReceiver = BrowserView.CoreWebView2.GetDevToolsProtocolEventReceiver("Network.loadingFailed");
        _networkLoadingFailedReceiver.DevToolsProtocolEventReceived += NetworkLoadingFailed;

        _consoleReceiver = BrowserView.CoreWebView2.GetDevToolsProtocolEventReceiver("Runtime.consoleAPICalled");
        _consoleReceiver.DevToolsProtocolEventReceived += ConsoleMessageReceived;

        _logEntryReceiver = BrowserView.CoreWebView2.GetDevToolsProtocolEventReceiver("Log.entryAdded");
        _logEntryReceiver.DevToolsProtocolEventReceived += LogEntryReceived;

        _debugProtocolHooked = true;
    }

    private async Task StartDebugCaptureAsync()
    {
        btnToggleDebug.IsEnabled = false;

        try
        {
            await EnsureBrowserReadyAsync();
            await CleanupDebugCaptureAsync();

            _debugCaptureActive = true;
            EnsureDebugPanelVisible();
            DebugPanel.SetSessionStatus("Recording interactions, console output, and network activity.");
            StatusTextBox.Text = "Debugging active. Interactions, console messages, and network traffic are being recorded.";

            await BrowserView.CoreWebView2!.ExecuteScriptAsync(BuildDebugCaptureScript());

            List<CoreWebView2Frame> snapshot;
            lock (_allKnownFrames) { snapshot = [.. _allKnownFrames]; }

            foreach (var frame in snapshot)
            {
                try { await frame.ExecuteScriptAsync(BuildDebugCaptureScript()); }
                catch { }
            }

            AddDebugEntry("Session", "Debug capture started.", BrowserView.Source?.ToString());
            SetDebugToggleState(true);
        }
        catch (Exception ex)
        {
            _debugCaptureActive = false;
            EnsureDebugPanelVisible();
            DebugPanel.SetSessionStatus("Debug capture failed to start.");
            AddDebugEntry("Session", "Unable to start debug capture.", ex.Message);
            StatusTextBox.Text = $"Debugging could not start: {ex.Message}";
            SetDebugToggleState(false);
        }
        finally
        {
            btnToggleDebug.IsEnabled = true;
        }
    }

    private async Task StopDebugCaptureAsync()
    {
        btnToggleDebug.IsEnabled = false;

        try
        {
            _debugCaptureActive = false;
            await CleanupDebugCaptureAsync();

            EnsureDebugPanelVisible();
            DebugPanel.SetSessionStatus("Debug capture stopped. The recorded session remains available.");
            AddDebugEntry("Session", "Debug capture stopped.");
            StatusTextBox.Text = "Debugging stopped. The recorded session remains visible.";
            SetDebugToggleState(false);
        }
        finally
        {
            btnToggleDebug.IsEnabled = true;
        }
    }

    private async Task CleanupDebugCaptureAsync()
    {
        var cleanup = BuildDebugCleanupScript();

        try
        {
            if (BrowserView.CoreWebView2 is not null)
            {
                await BrowserView.CoreWebView2.ExecuteScriptAsync(cleanup);
            }
        }
        catch { }

        List<CoreWebView2Frame> snapshot;
        lock (_allKnownFrames) { snapshot = [.. _allKnownFrames]; }

        foreach (var frame in snapshot)
        {
            try { await frame.ExecuteScriptAsync(cleanup); }
            catch { }
        }
    }

    private void SetDebugToggleState(bool isDebugging)
    {
        _updatingDebugToggle = true;
        btnToggleDebug.IsChecked = isDebugging;
        btnToggleDebug.ToolTip = isDebugging ? "Stop debugging" : "Start debugging";
        DebugButtonStateGlyph.Data = Geometry.Parse(isDebugging
            ? "M5,5 H7 V11 H5 Z M9,5 H11 V11 H9 Z"
            : "M6,5 L11,8 L6,11 Z");
        DebugButtonStateGlyph.Fill = isDebugging ? Brushes.Firebrick : Brushes.Black;
        _updatingDebugToggle = false;
    }

    private void CoreWebView2_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!_debugCaptureActive)
        {
            return;
        }

        AddDebugEntry("Navigation", "Navigation starting.", e.Uri);
    }

    private async void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!_debugCaptureActive)
        {
            return;
        }

        var uri = BrowserView.Source?.ToString() ?? string.Empty;
        var message = e.IsSuccess ? "Navigation completed." : "Navigation failed.";
        var detail = e.IsSuccess ? uri : $"{uri}\n{e.WebErrorStatus}";

        AddDebugEntry("Navigation", message, detail);

        if (e.IsSuccess && BrowserView.CoreWebView2 is not null)
        {
            try
            {
                await BrowserView.CoreWebView2.ExecuteScriptAsync(BuildDebugCaptureScript());
            }
            catch (Exception ex)
            {
                AddDebugEntry("Debug", "Could not reattach page debug listeners after navigation.", ex.Message);
            }
        }
    }

    private void CoreWebView2_WebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (!_debugCaptureActive)
        {
            return;
        }

        var request = e.Request;
        var networkType = MapNetworkType(e.ResourceContext, request.Uri, string.Empty, string.Empty);
        AddDebugEntry("Network", $"{request.Method} {request.Uri}", e.ResourceContext.ToString(), "PENDING", Brushes.DimGray, networkType, null);
    }

    private void NetworkResponseReceived(object? sender, CoreWebView2DevToolsProtocolEventReceivedEventArgs e)
    {
        if (!_debugCaptureActive)
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(e.ParameterObjectAsJson);
            var response = doc.RootElement.GetProperty("response");
            var url = GetString(response, "url");
            var status = response.TryGetProperty("status", out var statusElement)
                ? statusElement.ToString()
                : string.Empty;
            var mimeType = GetString(response, "mimeType");
            var statusCode = TryReadStatusCode(status);
            var statusBrush = GetStatusBrush(statusCode);
            var statusText = statusCode.HasValue ? statusCode.Value.ToString() : "UNKNOWN";
            var devToolsType = GetString(doc.RootElement, "type");
            var networkType = MapNetworkType(CoreWebView2WebResourceContext.Other, url, mimeType, devToolsType);
            var headersText = response.TryGetProperty("headers", out var headers)
                ? FormatHeaders(headers)
                : string.Empty;

            AddDebugEntry("Network", $"Response {url}", mimeType, statusText, statusBrush, networkType, headersText);
        }
        catch (Exception ex)
        {
            AddDebugEntry("Debug", "Failed to parse a network response event.", ex.Message);
        }
    }

    private void NetworkLoadingFailed(object? sender, CoreWebView2DevToolsProtocolEventReceivedEventArgs e)
    {
        if (!_debugCaptureActive)
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(e.ParameterObjectAsJson);
            var root = doc.RootElement;
            var failedUrl = GetString(root, "url");
            var devToolsType = GetString(root, "type");
            var networkType = MapNetworkType(CoreWebView2WebResourceContext.Other, failedUrl, string.Empty, devToolsType);
            AddDebugEntry(
                "Network",
                $"Request failed: {failedUrl}",
                GetString(root, "errorText"),
                "FAILED",
                Brushes.Firebrick,
                networkType,
                null);
        }
        catch (Exception ex)
        {
            AddDebugEntry("Debug", "Failed to parse a network failure event.", ex.Message);
        }
    }

    private void ConsoleMessageReceived(object? sender, CoreWebView2DevToolsProtocolEventReceivedEventArgs e)
    {
        if (!_debugCaptureActive)
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(e.ParameterObjectAsJson);
            var root = doc.RootElement;
            var type = GetString(root, "type");
            var severity = NormalizeConsoleSeverity(type);
            var severityBrush = GetConsoleSeverityBrush(severity);

            var builder = new StringBuilder();
            if (root.TryGetProperty("args", out var argsElement) && argsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var arg in argsElement.EnumerateArray())
                {
                    if (builder.Length > 0)
                    {
                        builder.Append(' ');
                    }

                    if (arg.TryGetProperty("value", out var valueElement))
                    {
                        builder.Append(valueElement.ToString());
                    }
                    else if (arg.TryGetProperty("description", out var descriptionElement))
                    {
                        builder.Append(descriptionElement.ToString());
                    }
                    else
                    {
                        builder.Append(arg.ToString());
                    }
                }
            }

            AddDebugEntry("Console", builder.ToString(), GetString(root, "executionContextId"), severity, severityBrush);
        }
        catch (Exception ex)
        {
            AddDebugEntry("Debug", "Failed to parse a console event.", ex.Message);
        }
    }

    private void LogEntryReceived(object? sender, CoreWebView2DevToolsProtocolEventReceivedEventArgs e)
    {
        if (!_debugCaptureActive)
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(e.ParameterObjectAsJson);
            if (!doc.RootElement.TryGetProperty("entry", out var entry))
            {
                return;
            }

            var level = GetString(entry, "level");
            var source = GetString(entry, "source");
            var text = GetString(entry, "text");
            var url = GetString(entry, "url");
            var line = string.Empty;
            if (entry.TryGetProperty("lineNumber", out var lineNumber) && lineNumber.ValueKind == JsonValueKind.Number)
            {
                line = lineNumber.ToString();
            }

            var message = string.IsNullOrWhiteSpace(level)
                ? text
                : text;
            var severity = NormalizeConsoleSeverity(level);
            var severityBrush = GetConsoleSeverityBrush(severity);

            var detailBuilder = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(source))
            {
                detailBuilder.Append(source);
            }

            if (!string.IsNullOrWhiteSpace(url))
            {
                if (detailBuilder.Length > 0)
                {
                    detailBuilder.Append(" | ");
                }

                detailBuilder.Append(url);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    detailBuilder.Append(':');
                    detailBuilder.Append(line);
                }
            }

            AddDebugEntry("Console", message, detailBuilder.ToString(), severity, severityBrush);
        }
        catch (Exception ex)
        {
            AddDebugEntry("Debug", "Failed to parse a Log.entryAdded event.", ex.Message);
        }
    }

    private void AddDebugEntry(
        string category,
        string message,
        string? detail = null,
        string? statusText = null,
        Brush? statusBrush = null,
        string? networkType = null,
        string? headersText = null)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.InvokeAsync(() => AddDebugEntry(category, message, detail, statusText, statusBrush, networkType, headersText));
            return;
        }

        EnsureDebugPanelVisible();
        DebugPanel.AddEntry(category, message, detail, statusText, statusBrush, networkType, headersText);
    }

    private static string FormatHeaders(JsonElement headers)
    {
        if (headers.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var property in headers.EnumerateObject())
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append(property.Name);
            builder.Append(": ");
            builder.Append(property.Value.ToString());
        }

        return builder.ToString();
    }

    private static string MapNetworkType(
        CoreWebView2WebResourceContext resourceContext,
        string url,
        string mimeType,
        string devToolsType)
    {
        if (!string.IsNullOrWhiteSpace(devToolsType))
        {
            var normalized = devToolsType.Trim().ToLowerInvariant();
            return normalized switch
            {
                "fetch" or "xhr" => "Fetch/XHR",
                "document" => "Doc",
                "stylesheet" => "CSS",
                "script" => "JS",
                "font" => "Font",
                "image" => "Img",
                "media" => "Media",
                "manifest" => "Manifest",
                "websocket" or "eventsource" => "Socket",
                "wasm" => "Wasm",
                _ => "Other"
            };
        }

        if (mimeType.Contains("wasm", StringComparison.OrdinalIgnoreCase) ||
            url.EndsWith(".wasm", StringComparison.OrdinalIgnoreCase))
        {
            return "Wasm";
        }

        return resourceContext switch
        {
            CoreWebView2WebResourceContext.Fetch or CoreWebView2WebResourceContext.XmlHttpRequest => "Fetch/XHR",
            CoreWebView2WebResourceContext.Document => "Doc",
            CoreWebView2WebResourceContext.Stylesheet => "CSS",
            CoreWebView2WebResourceContext.Script => "JS",
            CoreWebView2WebResourceContext.Font => "Font",
            CoreWebView2WebResourceContext.Image => "Img",
            CoreWebView2WebResourceContext.Media => "Media",
            CoreWebView2WebResourceContext.Manifest => "Manifest",
            CoreWebView2WebResourceContext.Websocket or CoreWebView2WebResourceContext.EventSource => "Socket",
            _ => "Other"
        };
    }

    private static int? TryReadStatusCode(string statusText)
    {
        if (int.TryParse(statusText, out var parsed))
        {
            return parsed;
        }

        if (double.TryParse(statusText, out var parsedDouble))
        {
            return (int)Math.Truncate(parsedDouble);
        }

        return null;
    }

    private static Brush GetStatusBrush(int? statusCode)
    {
        if (!statusCode.HasValue)
        {
            return Brushes.DimGray;
        }

        return statusCode.Value switch
        {
            >= 200 and < 300 => Brushes.ForestGreen,
            >= 300 and < 400 => Brushes.SteelBlue,
            >= 400 and < 600 => Brushes.Firebrick,
            _ => Brushes.DimGray
        };
    }

    private static string NormalizeConsoleSeverity(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "info";
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "warn" => "warning",
            "verbose" => "info",
            _ => normalized
        };
    }

    private static Brush GetConsoleSeverityBrush(string severity)
    {
        return severity switch
        {
            "error" => Brushes.Firebrick,
            "warning" => Brushes.DarkOrange,
            "info" => Brushes.SteelBlue,
            "debug" => Brushes.MediumPurple,
            _ => Brushes.DimGray
        };
    }

    private void EnsureDebugPanelVisible()
    {
        SetDebugDockOpen(true);
    }

    private void EnsureSelectionPanelVisible()
    {
        SetSelectorDockOpen(true);
    }

    private void SetSelectorDockOpen(bool isOpen)
    {
        if (isOpen)
        {
            SelectionPanel.Visibility = Visibility.Visible;
            SelectorPanelSplitter.Visibility = Visibility.Visible;

            if (SelectorSplitterColumn.Width.Value <= 0)
            {
                SelectorSplitterColumn.Width = new GridLength(DockSplitterWidth);
            }

            if (SelectorPanelColumn.Width.Value > 0)
            {
                _selectorPanelWidth = SelectorPanelColumn.Width.Value;
            }

            SelectorPanelColumn.Width = new GridLength(_selectorPanelWidth > 0 ? _selectorPanelWidth : SelectorPanelDefaultWidth);
        }
        else
        {
            if (SelectorPanelColumn.Width.Value > 0)
            {
                _selectorPanelWidth = SelectorPanelColumn.Width.Value;
            }

            SelectionPanel.Visibility = Visibility.Collapsed;
            SelectorPanelSplitter.Visibility = Visibility.Collapsed;
            SelectorSplitterColumn.Width = new GridLength(0);
            SelectorPanelColumn.Width = new GridLength(0);
        }

        SyncDockTabState(SelectorDockTab, isOpen);
    }

    private void SetDebugDockOpen(bool isOpen)
    {
        if (isOpen)
        {
            DebugPanel.Visibility = Visibility.Visible;
            DebugPanelSplitter.Visibility = Visibility.Visible;

            if (DebugSplitterColumn.Width.Value <= 0)
            {
                DebugSplitterColumn.Width = new GridLength(DockSplitterWidth);
            }

            if (DebugPanelColumn.Width.Value > 0)
            {
                _debugPanelWidth = DebugPanelColumn.Width.Value;
            }

            DebugPanelColumn.Width = new GridLength(_debugPanelWidth > 0 ? _debugPanelWidth : DebugPanelDefaultWidth);
        }
        else
        {
            if (DebugPanelColumn.Width.Value > 0)
            {
                _debugPanelWidth = DebugPanelColumn.Width.Value;
            }

            DebugPanel.Visibility = Visibility.Collapsed;
            DebugPanelSplitter.Visibility = Visibility.Collapsed;
            DebugSplitterColumn.Width = new GridLength(0);
            DebugPanelColumn.Width = new GridLength(0);
        }

        SyncDockTabState(DebugDockTab, isOpen);
    }

    private void SyncDockTabState(System.Windows.Controls.Primitives.ToggleButton tab, bool isOpen)
    {
        if (_updatingDockTabs || tab.IsChecked == isOpen)
        {
            return;
        }

        _updatingDockTabs = true;
        tab.IsChecked = isOpen;
        _updatingDockTabs = false;
    }

    private static string BuildMainFrameScript()
    {
        return @"(() => {
    function sendResult(result) {
        if (window.chrome && window.chrome.webview) {
            window.chrome.webview.postMessage(Object.assign({ type: 'selector-capture-result' }, result));
        }
    }

    function escapeCss(value) {
        if (!value) return '';
        return value.replace(/([!""#$%&'()*+,./:;<=>?@[\\\]^`{|}~])/g, '\\\\$1');
    }

    function cssOf(el) {
        if (el.id) {
            return '#' + escapeCss(el.id);
        }
        const parts = [];
        let current = el;
        while (current && current.nodeType === Node.ELEMENT_NODE) {
            let part = current.tagName.toLowerCase();
            if (current.id) {
                part += '#' + escapeCss(current.id);
                parts.unshift(part);
                break;
            }
            const siblings = current.parentElement
                ? Array.from(current.parentElement.children).filter(x => x.tagName === current.tagName)
                : [];
            if (siblings.length > 1) {
                part += ':nth-of-type(' + (siblings.indexOf(current) + 1) + ')';
            }
            parts.unshift(part);
            current = current.parentElement;
            if (parts.length > 8) break;
        }
        return parts.join(' > ');
    }

    function xpathOf(el) {
        const parts = [];
        let current = el;
        while (current && current.nodeType === Node.ELEMENT_NODE) {
            let index = 1;
            let sibling = current.previousElementSibling;
            while (sibling) {
                if (sibling.tagName === current.tagName) index++;
                sibling = sibling.previousElementSibling;
            }
            parts.unshift(current.tagName.toLowerCase() + '[' + index + ']');
            current = current.parentElement;
        }
        return '/' + parts.join('/');
    }

    if (window.__selectorCaptureActive) {
        sendResult({ status: 'busy', message: 'Selection is already active.' });
        return;
    }
    window.__selectorCaptureActive = true;

    let highlight = null, shield = null;

    function swallow(e) {
        if (!e) return;
        if (typeof e.preventDefault === 'function') e.preventDefault();
        if (typeof e.stopPropagation === 'function') e.stopPropagation();
        if (typeof e.stopImmediatePropagation === 'function') e.stopImmediatePropagation();
    }

    function removeHighlight() {
        if (highlight) { highlight.remove(); highlight = null; }
    }

    function showHighlight(target) {
        if (!target || target === document.documentElement || target === document.body) {
            removeHighlight(); return;
        }
        removeHighlight();
        const rect = target.getBoundingClientRect();
        highlight = document.createElement('div');
        highlight.style.position = 'fixed';
        highlight.style.left = Math.max(rect.left, 0) + 'px';
        highlight.style.top = Math.max(rect.top, 0) + 'px';
        highlight.style.width = Math.max(rect.width, 2) + 'px';
        highlight.style.height = Math.max(rect.height, 2) + 'px';
        highlight.style.border = '2px solid #2ec27e';
        highlight.style.background = 'rgba(46, 194, 126, 0.16)';
        highlight.style.pointerEvents = 'none';
        highlight.style.zIndex = '2147483647';
        document.documentElement.appendChild(highlight);
    }

    function getElementAtPoint(x, y) {
        shield.style.display = 'none';
        const el = document.elementFromPoint(x, y);
        shield.style.display = 'block';
        return el;
    }

    function isFrameEl(el) {
        const tag = el?.tagName?.toLowerCase() ?? '';
        return tag === 'iframe' || tag === 'frame';
    }

    function cleanup() {
        if (shield) { shield.remove(); shield = null; }
        removeHighlight();
        window.__selectorCaptureActive = false;
        window.__selectorCaptureCleanup = null;
        document.removeEventListener('keydown', onKeyDown, true);
        window.removeEventListener('message', onChildMessage);
    }
    window.__selectorCaptureCleanup = cleanup;

    function onPointerMove(e) {
        swallow(e);
        const el = getElementAtPoint(e.clientX, e.clientY);
        if (isFrameEl(el)) {
            // Let pointer events pass through to the iframe's own capture shield.
            shield.style.pointerEvents = 'none';
        } else {
            shield.style.pointerEvents = 'auto';
        }
        showHighlight(el);
    }

    function onPointerDown(e) { swallow(e); }
    function onPointerUp(e) { swallow(e); }

    function buildDomTree(selectedEl) {
        const ancestors = [];
        let current = selectedEl;
        while (current && current.nodeType === Node.ELEMENT_NODE && current !== document.documentElement) {
            ancestors.unshift(current);
            current = current.parentElement;
        }
        if (document.documentElement && !ancestors.includes(document.documentElement)) {
            ancestors.unshift(document.documentElement);
        }

        function getNodeInnerText(el) {
            if (!el || typeof el.innerText !== 'string') return '';
            const normalized = el.innerText.replace(/\s+/g, ' ').trim();
            return normalized.length > 80 ? normalized.substring(0, 80) + '...' : normalized;
        }

        function buildNode(el, isSelected) {
            const node = {
                tagName: el.tagName.toLowerCase(),
                id: el.id || '',
                className: el.className || '',
                innerText: getNodeInnerText(el),
                isSelected: isSelected || false,
                children: []
            };
            if (isSelected) {
                for (let i = 0; i < Math.min(el.children.length, 20); i++) {
                    const childInfo = buildNode(el.children[i], false);
                    if (childInfo) node.children.push(childInfo);
                }
            }
            return node;
        }

        if (ancestors.length === 0) return null;
        let root = buildNode(ancestors[0], false);
        let currentNode = root;
        for (let i = 1; i < ancestors.length; i++) {
            const childNode = buildNode(ancestors[i], ancestors[i] === selectedEl);
            currentNode.children = [childNode];
            currentNode = childNode;
        }

        return root;
    }

    function onClick(e) {
        swallow(e);
        const el = getElementAtPoint(e.clientX, e.clientY);
        if (!el || el === document.documentElement || el === document.body) {
            cleanup();
            sendResult({ status: 'cancelled', message: 'No selectable element at click point.' });
            return;
        }
        // Clicks on iframe shells are handled by the injected frame script.
        if (isFrameEl(el)) return;

        const htmlSource = el.outerHTML.substring(0, 500);
        const attrs = [];
        for (let i = 0; i < el.attributes.length; i++) {
            const a = el.attributes[i];
            attrs.push(a.name + '=' + a.value);
        }
        const attributes = attrs.length > 0 ? attrs.join(String.fromCharCode(10)) : '(no attributes)';
        const style = window.getComputedStyle(el);
        const styleLines = [];
        for (let i = 0; i < Math.min(style.length, 30); i++) {
            const p = style[i];
            const v = style.getPropertyValue(p);
            if (v && v !== '') {
                styleLines.push(p + ': ' + v);
            }
        }
        const computedStyles = styleLines.length > 0 ? styleLines.join(String.fromCharCode(10)) : '(no computed styles)';
        const frameUrl = (typeof window !== 'undefined' && window.location) ? window.location.href : '';
        const domTree = buildDomTree(el) || {
            tagName: 'html',
            id: '',
            className: '',
            innerText: '',
            isSelected: false,
            children: [{
                tagName: el.tagName.toLowerCase(),
                id: el.id || '',
                className: el.className || '',
                innerText: getNodeInnerText(el),
                isSelected: true,
                children: []
            }]
        };

        cleanup();
        sendResult({
            status: 'selected',
            cssSelector: cssOf(el),
            xpathSelector: xpathOf(el),
            frameUrl: frameUrl,
            htmlSource: htmlSource,
            attributes: attributes,
            computedStyles: computedStyles,
            domTree: domTree
        });
    }

    function onChildMessage(e) {
        if (!e.data) return;
        if (e.data.type === 'capture-pointer-left-frame' && shield) {
            shield.style.pointerEvents = 'auto';
        }
        if (e.data.type === 'capture-selected') {
            let payload = e.data.payload;
            if ((!payload || typeof payload !== 'object') && typeof e.data.payloadJson === 'string') {
                try {
                    payload = JSON.parse(e.data.payloadJson);
                } catch (err) {
                    payload = null;
                }
            }
            if (payload && typeof payload === 'object') {
                cleanup();
                sendResult(payload);
                return;
            }
        }
        if (e.data.type === 'capture-cancel') {
            cleanup();
            sendResult({ status: 'cancelled', message: 'Selection cancelled.' });
        }
    }

    function onKeyDown(e) {
        if (e.key !== 'Escape') return;
        swallow(e);
        cleanup();
        sendResult({ status: 'cancelled', message: 'Selection cancelled by user.' });
    }

    shield = document.createElement('div');
    shield.style.position = 'fixed';
    shield.style.left = '0';
    shield.style.top = '0';
    shield.style.width = '100vw';
    shield.style.height = '100vh';
    shield.style.zIndex = '2147483646';
    shield.style.cursor = 'crosshair';
    shield.style.background = 'rgba(0,0,0,0)';
    shield.style.pointerEvents = 'auto';
    shield.setAttribute('aria-hidden', 'true');

    shield.addEventListener('pointermove', onPointerMove, true);
    shield.addEventListener('pointerdown', onPointerDown, true);
    shield.addEventListener('pointerup', onPointerUp, true);
    shield.addEventListener('click', onClick, true);
    document.addEventListener('keydown', onKeyDown, true);
    window.addEventListener('message', onChildMessage);

    document.documentElement.appendChild(shield);
})();";
    }

    private static string BuildFrameScript()
    {
        return @"(() => {
    function sendResult(result) {
        // Prefer bubbling to parent so iframe selections always flow through one path.
        try {
            if (window.parent && window.parent !== window) {
                window.parent.postMessage({
                    type: 'capture-selected',
                    payload: result,
                    payloadJson: JSON.stringify(result)
                }, '*');
                return;
            }
        } catch (err) {}

        // Fallback for top-level or isolated cases.
        if (window.chrome && window.chrome.webview) {
            window.chrome.webview.postMessage(Object.assign({ type: 'selector-capture-result' }, result));
        }
    }

    function escapeCss(value) {
        if (!value) return '';
        return value.replace(/([!""#$%&'()*+,./:;<=>?@[\\\]^`{|}~])/g, '\\\\$1');
    }

    function cssOf(el) {
        if (el.id) return '#' + escapeCss(el.id);
        const parts = [];
        let current = el;
        while (current && current.nodeType === Node.ELEMENT_NODE) {
            let part = current.tagName.toLowerCase();
            if (current.id) {
                part += '#' + escapeCss(current.id);
                parts.unshift(part);
                break;
            }
            const siblings = current.parentElement
                ? Array.from(current.parentElement.children).filter(x => x.tagName === current.tagName)
                : [];
            if (siblings.length > 1) part += ':nth-of-type(' + (siblings.indexOf(current) + 1) + ')';
            parts.unshift(part);
            current = current.parentElement;
            if (parts.length > 8) break;
        }
        return parts.join(' > ');
    }

    function xpathOf(el) {
        const parts = [];
        let current = el;
        while (current && current.nodeType === Node.ELEMENT_NODE) {
            let index = 1;
            let sibling = current.previousElementSibling;
            while (sibling) {
                if (sibling.tagName === current.tagName) index++;
                sibling = sibling.previousElementSibling;
            }
            parts.unshift(current.tagName.toLowerCase() + '[' + index + ']');
            current = current.parentElement;
        }
        return '/' + parts.join('/');
    }

    if (window.__selectorCaptureActive) return;
    window.__selectorCaptureActive = true;

    let highlight = null, shield = null;

    function swallow(e) {
        if (!e) return;
        if (typeof e.preventDefault === 'function') e.preventDefault();
        if (typeof e.stopPropagation === 'function') e.stopPropagation();
        if (typeof e.stopImmediatePropagation === 'function') e.stopImmediatePropagation();
    }

    function removeHighlight() {
        if (highlight) { highlight.remove(); highlight = null; }
    }

    function showHighlight(target) {
        if (!target || target === document.documentElement || target === document.body) {
            removeHighlight(); return;
        }
        removeHighlight();
        const rect = target.getBoundingClientRect();
        highlight = document.createElement('div');
        highlight.style.position = 'fixed';
        highlight.style.left = Math.max(rect.left, 0) + 'px';
        highlight.style.top = Math.max(rect.top, 0) + 'px';
        highlight.style.width = Math.max(rect.width, 2) + 'px';
        highlight.style.height = Math.max(rect.height, 2) + 'px';
        highlight.style.border = '2px solid #2ec27e';
        highlight.style.background = 'rgba(46, 194, 126, 0.16)';
        highlight.style.pointerEvents = 'none';
        highlight.style.zIndex = '2147483647';
        document.documentElement.appendChild(highlight);
    }

    function cleanup() {
        if (shield) { shield.remove(); shield = null; }
        removeHighlight();
        window.__selectorCaptureActive = false;
        window.__selectorCaptureCleanup = null;
        document.removeEventListener('keydown', onKeyDown, true);
    }
    window.__selectorCaptureCleanup = cleanup;

    function onPointerMove(e) {
        swallow(e);
        shield.style.display = 'none';
        const el = document.elementFromPoint(e.clientX, e.clientY);
        shield.style.display = 'block';
        showHighlight(el);
    }

    function onPointerLeave() {
        removeHighlight();
        try { window.parent.postMessage({ type: 'capture-pointer-left-frame' }, '*'); } catch (err) {}
    }

    function onPointerDown(e) { swallow(e); }
    function onPointerUp(e) { swallow(e); }

    function buildDomTree(selectedEl) {
        const ancestors = [];
        let current = selectedEl;
        while (current && current.nodeType === Node.ELEMENT_NODE && current !== document.documentElement) {
            ancestors.unshift(current);
            current = current.parentElement;
        }
        if (document.documentElement && !ancestors.includes(document.documentElement)) {
            ancestors.unshift(document.documentElement);
        }

        function getNodeInnerText(el) {
            if (!el || typeof el.innerText !== 'string') return '';
            const normalized = el.innerText.replace(/\s+/g, ' ').trim();
            return normalized.length > 80 ? normalized.substring(0, 80) + '...' : normalized;
        }

        function buildNode(el, isSelected) {
            const node = {
                tagName: el.tagName.toLowerCase(),
                id: el.id || '',
                className: el.className || '',
                innerText: getNodeInnerText(el),
                isSelected: isSelected || false,
                children: []
            };
            if (isSelected) {
                for (let i = 0; i < Math.min(el.children.length, 20); i++) {
                    const childInfo = buildNode(el.children[i], false);
                    if (childInfo) node.children.push(childInfo);
                }
            }
            return node;
        }

        if (ancestors.length === 0) return null;
        let root = buildNode(ancestors[0], false);
        let current_node = root;
        for (let i = 1; i < ancestors.length; i++) {
            const childNode = buildNode(ancestors[i], ancestors[i] === selectedEl);
            current_node.children = [childNode];
            current_node = childNode;
        }
        
        return root;
    }

    function onClick(e) {
        swallow(e);
        shield.style.display = 'none';
        const el = document.elementFromPoint(e.clientX, e.clientY);
        shield.style.display = 'block';
        if (!el || el === document.documentElement || el === document.body) {
            cleanup();
            sendResult({ status: 'cancelled', message: 'No selectable element.' });
            return;
        }
        
        const frameUrl = (typeof window !== 'undefined' && window.location) ? window.location.href : '';
        const htmlSource = el.outerHTML.substring(0, 500);
        
        // Get attributes
        let attributes = '';
        if (el.attributes.length > 0) {
            attributes = Array.from(el.attributes)
                .map(attr => attr.name + '=' + attr.value)
                .join('\n');
        } else {
            attributes = '(no attributes)';
        }
        
        // Get computed styles
        const style = window.getComputedStyle(el);
        let computedStyles = '';
        if (style.length > 0) {
            const styleProps = [];
            for (let i = 0; i < style.length; i++) {
                const prop = style[i];
                const value = style.getPropertyValue(prop);
                if (value && value !== '') {
                    styleProps.push(prop + ': ' + value);
                }
            }
            computedStyles = styleProps.slice(0, 30).join('\n');
        } else {
            computedStyles = '(no computed styles)';
        }
        
        cleanup();
        const domTree = buildDomTree(el) || {
            tagName: 'html',
            id: '',
            className: '',
            innerText: '',
            isSelected: false,
            children: [{
                tagName: el.tagName.toLowerCase(),
                id: el.id || '',
                className: el.className || '',
                innerText: getNodeInnerText(el),
                isSelected: true,
                children: []
            }]
        };
        sendResult({
            status: 'selected',
            cssSelector: cssOf(el),
            xpathSelector: xpathOf(el),
            frameUrl: frameUrl,
            htmlSource: htmlSource,
            attributes: attributes,
            computedStyles: computedStyles,
            domTree: domTree
        });
    }

    function onKeyDown(e) {
        if (e.key !== 'Escape') return;
        swallow(e);
        cleanup();
        try { window.parent.postMessage({ type: 'capture-cancel' }, '*'); } catch (err) {}
        sendResult({ status: 'cancelled', message: 'Selection cancelled.' });
    }

    shield = document.createElement('div');
    shield.style.position = 'fixed';
    shield.style.left = '0';
    shield.style.top = '0';
    shield.style.width = '100vw';
    shield.style.height = '100vh';
    shield.style.zIndex = '2147483646';
    shield.style.cursor = 'crosshair';
    shield.style.background = 'rgba(0,0,0,0)';
    shield.style.pointerEvents = 'auto';
    shield.setAttribute('aria-hidden', 'true');

    shield.addEventListener('pointermove', onPointerMove, true);
    shield.addEventListener('pointerleave', onPointerLeave, true);
    shield.addEventListener('pointerdown', onPointerDown, true);
    shield.addEventListener('pointerup', onPointerUp, true);
    shield.addEventListener('click', onClick, true);
    document.addEventListener('keydown', onKeyDown, true);

    document.documentElement.appendChild(shield);
})();";
    }

    private static string BuildDebugCaptureScript()
    {
        return @"(() => {
    function post(category, message, detail) {
        if (window.chrome && window.chrome.webview) {
            window.chrome.webview.postMessage({
                type: 'browser-debug-event',
                category: category,
                message: message,
                detail: detail || ''
            });
        }
    }

    function describeElement(target) {
        if (!target || !target.tagName) {
            return 'unknown element';
        }

        const name = target.tagName.toLowerCase();
        const id = target.id ? '#' + target.id : '';
        const className = typeof target.className === 'string' && target.className.trim()
            ? '.' + target.className.trim().replace(/\s+/g, '.')
            : '';
        return name + id + className;
    }

    function readValue(target) {
        if (!target) {
            return '';
        }

        if (typeof target.value === 'string') {
            return target.value;
        }

        if (typeof target.checked === 'boolean') {
            return String(target.checked);
        }

        return '';
    }

    if (typeof window.__browserDebugCleanup === 'function') {
        window.__browserDebugCleanup();
    }

    const subscriptions = [];

    function addListener(target, eventName, handler, options) {
        target.addEventListener(eventName, handler, options);
        subscriptions.push(() => target.removeEventListener(eventName, handler, options));
    }

    addListener(document, 'click', event => {
        post('Interaction', 'Clicked ' + describeElement(event.target), window.location.href);
    }, true);

    addListener(document, 'change', event => {
        post('Interaction', 'Changed ' + describeElement(event.target), readValue(event.target));
    }, true);

    addListener(document, 'submit', event => {
        post('Interaction', 'Submitted ' + describeElement(event.target), window.location.href);
    }, true);

    addListener(window, 'hashchange', () => {
        post('Navigation', 'Hash changed.', window.location.href);
    }, true);

    addListener(window, 'popstate', () => {
        post('Navigation', 'History navigation occurred.', window.location.href);
    }, true);

    window.__browserDebugCleanup = () => {
        for (const unsubscribe of subscriptions) {
            try { unsubscribe(); } catch (error) {}
        }
        window.__browserDebugCleanup = null;
    };
})();";
    }

    private static string BuildSelectionCleanupScript()
        => "if (typeof window.__selectorCaptureCleanup === 'function') { window.__selectorCaptureCleanup(); }";

    private static string BuildDebugCleanupScript()
        => "if (typeof window.__browserDebugCleanup === 'function') { window.__browserDebugCleanup(); }";

    private static SelectionResult ReadSelectionResult(JsonElement element)
    {
        string GetString(string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString() ?? string.Empty;
            }
            return string.Empty;
        }

        string GetJsonString(string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var value))
            {
                return value.GetRawText();
            }
            return string.Empty;
        }

        return new SelectionResult(
            GetString("status"),
            GetString("message"),
            GetString("cssSelector"),
            GetString("xpathSelector"),
            GetString("frameUrl"),
            GetString("htmlSource"),
            GetString("attributes"),
            GetString("computedStyles"),
            GetJsonString("domTree"));
    }

    private static DebugEventRecord ReadDebugEvent(JsonElement element)
    {
        return new DebugEventRecord(
            GetString(element, "category"),
            GetString(element, "message"),
            GetString(element, "detail"));
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    private sealed record SelectionResult(
        string Status,
        string Message,
        string CssSelector,
        string XPathSelector,
        string FrameUrl,
        string HtmlSource = "",
        string Attributes = "",
        string ComputedStyles = "",
        string DomTreeJson = "");

    private sealed record DebugEventRecord(string Category, string Message, string Detail);
}
