using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AllItems.Automation.Browser.Actions.Browser;
using AllItems.Automation.Browser.App.Docking.Layout;
using AllItems.Automation.Browser.App.Docking.Models;
using AllItems.Automation.Browser.App.Docking.Services;
using AllItems.Automation.Browser.App.Commands;
using AllItems.Automation.Browser.App.Models;
using AllItems.Automation.Browser.App.Services;
using AllItems.Automation.Browser.App.Services.Diagnostics;
using AllItems.Automation.Browser.App.Services.Flow;
using AllItems.Automation.Browser.Core.Configuration;
using AllItems.Automation.Browser.Core.Diagnostics;

namespace AllItems.Automation.Browser.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IUiActionsSidebarCommandContract
{
    private readonly IAutomationOrchestrator _orchestrator;
    private readonly IActionCatalogBuilder _actionCatalogBuilder;
    private readonly DiagnosticsService _diagnosticsService;
    private readonly BrowserOptions _browserOptions;
    private readonly IFlowExecutionBridge _flowExecutionBridge;
    private readonly IUiDispatcherService _uiDispatcherService;
    private readonly ITestDockWindowService _testDockWindowService;
    private readonly IDockLayoutPersistenceService? _dockLayoutPersistenceService;
    private readonly FlowCanvasViewModel _flowCanvasViewModel;
    private readonly ObservableCollection<UiActionCategory> _actionCatalog;
    private readonly UiActionsSidebarState _actionsSidebarState;
    private readonly RelayCommand _startCommand;
    private readonly AsyncRelayCommand _debugCommand;
    private readonly RelayCommand _stopCommand;
    private readonly RelayCommand _invokeActionCommand;
    private readonly RelayCommand _toggleCategoryCommand;
    private readonly RelayCommand _startDragCommand;
    private readonly RelayCommand _openDockLabCommand;
    private readonly RelayCommand _layoutChangedCommand;
    private readonly StatusBarItemModel _runStateStatusItem;
    private readonly StatusBarItemModel _targetHostStatusItem;
    private readonly StatusBarItemModel _executionProfileStatusItem;
    private readonly StatusBarItemModel _diagnosticsCountStatusItem;
    private readonly StatusBarItemModel _runStateBadgeItem;
    private readonly StatusBarItemModel _stopStatusActionItem;
    private CancellationTokenSource? _runCancellationTokenSource;
    private IFlowRunExecutionControl? _activeRunExecutionControl;
    private DockLayoutSnapshot? _activeLayout;
    private UiActionItem? _selectedSidebarAction;
    private string _url = "https://example.com";
    private string _status = "Idle";
    private bool _isRunning;
    private UiRunState _runState = UiRunState.Idle;

    public MainViewModel(
        IAutomationOrchestrator orchestrator,
        IActionCatalogBuilder actionCatalogBuilder,
        DiagnosticsService diagnosticsService,
        IUiDispatcherService uiDispatcherService)
        : this(orchestrator, actionCatalogBuilder, diagnosticsService, uiDispatcherService, new BrowserOptions(), new NullTestDockWindowService(), FlowCanvasViewModel.CreateDefault(diagnosticsService), null, new NullFlowExecutionBridge())
    {
    }

    public MainViewModel(
        IAutomationOrchestrator orchestrator,
        IActionCatalogBuilder actionCatalogBuilder,
        DiagnosticsService diagnosticsService,
        IUiDispatcherService uiDispatcherService,
        ITestDockWindowService testDockWindowService)
        : this(orchestrator, actionCatalogBuilder, diagnosticsService, uiDispatcherService, new BrowserOptions(), testDockWindowService, FlowCanvasViewModel.CreateDefault(diagnosticsService), null, new NullFlowExecutionBridge())
    {
    }

    public MainViewModel(
        IAutomationOrchestrator orchestrator,
        IActionCatalogBuilder actionCatalogBuilder,
        DiagnosticsService diagnosticsService,
        IUiDispatcherService uiDispatcherService,
        BrowserOptions browserOptions,
        ITestDockWindowService testDockWindowService)
        : this(orchestrator, actionCatalogBuilder, diagnosticsService, uiDispatcherService, browserOptions, testDockWindowService, FlowCanvasViewModel.CreateDefault(diagnosticsService), null, new NullFlowExecutionBridge())
    {
    }

    public MainViewModel(
        IAutomationOrchestrator orchestrator,
        IActionCatalogBuilder actionCatalogBuilder,
        DiagnosticsService diagnosticsService,
        IUiDispatcherService uiDispatcherService,
        BrowserOptions browserOptions,
        ITestDockWindowService testDockWindowService,
        IDockLayoutPersistenceService dockLayoutPersistenceService,
        FlowCanvasViewModel flowCanvasViewModel)
        : this(orchestrator, actionCatalogBuilder, diagnosticsService, uiDispatcherService, browserOptions, testDockWindowService, flowCanvasViewModel, dockLayoutPersistenceService, new NullFlowExecutionBridge())
    {
    }

    public MainViewModel(
        IAutomationOrchestrator orchestrator,
        IActionCatalogBuilder actionCatalogBuilder,
        DiagnosticsService diagnosticsService,
        IUiDispatcherService uiDispatcherService,
        BrowserOptions browserOptions,
        ITestDockWindowService testDockWindowService,
        FlowCanvasViewModel flowCanvasViewModel,
        IDockLayoutPersistenceService? dockLayoutPersistenceService)
        : this(orchestrator, actionCatalogBuilder, diagnosticsService, uiDispatcherService, browserOptions, testDockWindowService, flowCanvasViewModel, dockLayoutPersistenceService, new NullFlowExecutionBridge())
    {
    }

    public MainViewModel(
        IAutomationOrchestrator orchestrator,
        IActionCatalogBuilder actionCatalogBuilder,
        DiagnosticsService diagnosticsService,
        IUiDispatcherService uiDispatcherService,
        BrowserOptions browserOptions,
        ITestDockWindowService testDockWindowService,
        FlowCanvasViewModel flowCanvasViewModel,
        IDockLayoutPersistenceService? dockLayoutPersistenceService,
        IFlowExecutionBridge flowExecutionBridge)
    {
        _orchestrator = orchestrator;
        _actionCatalogBuilder = actionCatalogBuilder;
        _diagnosticsService = diagnosticsService;
        _browserOptions = browserOptions;
        _flowExecutionBridge = flowExecutionBridge;
        _uiDispatcherService = uiDispatcherService;
        _testDockWindowService = testDockWindowService;
        _dockLayoutPersistenceService = dockLayoutPersistenceService;
        _flowCanvasViewModel = flowCanvasViewModel;

        Logs = [];
        _actionCatalog = [];
        _actionsSidebarState = new UiActionsSidebarState();

        _diagnosticsService.ClearLogs();
        _diagnosticsService.Info("Ready");
        foreach (var entry in _diagnosticsService.GetLogs())
        {
            Logs.Add(new UiLogItem
            {
                TimestampUtc = entry.TimestampUtc,
                Level = entry.Level,
                Message = entry.Message,
            });
        }

        _startCommand = new RelayCommand(ToggleRunPauseResume, CanUseStartCommand);
        _debugCommand = new AsyncRelayCommand(DebugAsync, () => !IsRunning);
        _stopCommand = new RelayCommand(Stop, () => IsRunning || IsPaused);
        _invokeActionCommand = new RelayCommand(InvokeAction, parameter => parameter is UiActionInvokeRequest);
        _toggleCategoryCommand = new RelayCommand(ToggleCategory, parameter => parameter is UiActionCategoryToggleRequest);
        _startDragCommand = new RelayCommand(StartDragAction, parameter => parameter is UiActionDragRequest);
        _openDockLabCommand = new RelayCommand(OpenDockLab);
        _layoutChangedCommand = new RelayCommand(parameter => OnDockLayoutChanged(parameter as DockLayoutSnapshot));

        StartCommand = _startCommand;
        DebugCommand = _debugCommand;
        StopCommand = _stopCommand;
        InvokeActionCommand = _invokeActionCommand;
        ToggleCategoryCommand = _toggleCategoryCommand;
        StartDragCommand = _startDragCommand;
        OpenDockLabCommand = _openDockLabCommand;
        LayoutChangedCommand = _layoutChangedCommand;

        Panels =
        [
            new DockPanelDescriptor { PanelId = "action-panel", Title = "Action Panel", ContentKey = "Action panel", PanelKind = DockPanelKind.ToolWindow },
            new DockPanelDescriptor { PanelId = "canvas", Title = "Canvas", ContentKey = "Canvas", PanelKind = DockPanelKind.DocumentWindow, ShowTabHeader = false, IsPinnable = false, IsClosable = false },
            new DockPanelDescriptor { PanelId = "logs", Title = "Logs", ContentKey = "Logs", PanelKind = DockPanelKind.ToolWindow },
        ];

        StatusBarItems = [];
        _runStateStatusItem = new StatusBarItemModel
        {
            Id = "run-state-text",
            Placement = StatusBarItemPlacement.Left,
            Order = 0,
            IconGlyph = "$(pulse)",
            Text = "Ready",
            ToolTip = "Current automation run status",
            IsEnabled = true,
        };
        _targetHostStatusItem = new StatusBarItemModel
        {
            Id = "target-host",
            Placement = StatusBarItemPlacement.Left,
            Order = 10,
            IconGlyph = "$(globe)",
            Text = BuildTargetHostText(Url),
            ToolTip = "Current navigation target host",
            IsEnabled = false,
        };
        _executionProfileStatusItem = new StatusBarItemModel
        {
            Id = "execution-profile",
            Placement = StatusBarItemPlacement.Right,
            Order = 80,
            IconGlyph = "$(browser)",
            Text = BuildExecutionProfileText(),
            ToolTip = BuildExecutionProfileTooltip(),
            IsEnabled = false,
        };
        _diagnosticsCountStatusItem = new StatusBarItemModel
        {
            Id = "diagnostics-count",
            Placement = StatusBarItemPlacement.Right,
            Order = 90,
            IconGlyph = "$(output)",
            Text = "Logs 0",
            ToolTip = "Number of diagnostics entries captured in this session",
            IsEnabled = false,
        };
        _runStateBadgeItem = new StatusBarItemModel
        {
            Id = "run-state-badge",
            Placement = StatusBarItemPlacement.Right,
            Order = 100,
            IconGlyph = "$(info)",
            Text = RunState.ToString(),
            ToolTip = "Current run state",
            IsEnabled = false,
        };
        _stopStatusActionItem = new StatusBarItemModel
        {
            Id = "stop-run-action",
            Placement = StatusBarItemPlacement.Right,
            Order = 110,
            IconGlyph = "$(debug-stop)",
            Text = "Stop",
            ToolTip = "Stop the active run",
            Command = StopCommand,
            IsEnabled = false,
        };
        StatusBarItems.Add(_runStateStatusItem);
        StatusBarItems.Add(_targetHostStatusItem);
        StatusBarItems.Add(_executionProfileStatusItem);
        StatusBarItems.Add(_diagnosticsCountStatusItem);
        StatusBarItems.Add(_runStateBadgeItem);
        StatusBarItems.Add(_stopStatusActionItem);

        LoadActionCatalog();
        RefreshSidebarProjection();

        SetStatus("Ready", "$(pulse)");
        UpdateTargetHostStatusItem();
        UpdateDiagnosticsCountStatusItem();
        _diagnosticsService.EntryAdded += OnLogEntryAdded;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Url
    {
        get => _url;
        set
        {
            if (SetProperty(ref _url, value))
            {
                UpdateTargetHostStatusItem();
                RaiseCommandStateChanged();
            }
        }
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                NotifyRunControlPropertiesChanged();
                RaiseCommandStateChanged();
            }
        }
    }

    public bool IsPaused => RunState == UiRunState.Paused;

    public bool CanPause => IsRunning && RunState == UiRunState.Running;

    public bool CanResume => IsRunning && RunState == UiRunState.Paused;

    public string RunButtonIconGlyph => IsPaused ? "\uE768" : CanPause ? "\uE769" : "\uE768";

    public string RunButtonToolTip => IsPaused ? "Continue playing" : CanPause ? "Pause" : "Run";

    public UiRunState RunState
    {
        get => _runState;
        private set
        {
            if (SetProperty(ref _runState, value))
            {
                _runStateBadgeItem.Text = value.ToString();
                NotifyRunControlPropertiesChanged();
                RaiseCommandStateChanged();
            }
        }
    }

    public ObservableCollection<UiLogItem> Logs { get; }

    public ObservableCollection<StatusBarItemModel> StatusBarItems { get; }

    public ObservableCollection<DockPanelDescriptor> Panels { get; }

    public DockLayoutSnapshot? ActiveLayout
    {
        get => _activeLayout;
        set => SetProperty(ref _activeLayout, value);
    }

    public ObservableCollection<UiActionCategory> ActionCatalog => _actionCatalog;

    public ObservableCollection<UiActionCategory> FilteredActionCategories => _actionsSidebarState.FilteredCategories;

    public FlowCanvasViewModel FlowCanvas => _flowCanvasViewModel;

    public HashSet<string> ExpandedCategoryIds => _actionsSidebarState.ExpandedCategoryIds;

    public string ActionSearchText
    {
        get => _actionsSidebarState.SearchText;
        set
        {
            if (_actionsSidebarState.SearchText == value)
            {
                return;
            }

            _actionsSidebarState.SearchText = value;
            RefreshSidebarProjection();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActionSearchText)));
        }
    }

    public UiActionItem? SelectedSidebarAction
    {
        get => _selectedSidebarAction;
        set => SetProperty(ref _selectedSidebarAction, value);
    }

    public UiActionInvokeRequest? LastInvokedActionRequest { get; private set; }

    public UiActionDragRequest? LastDragActionRequest { get; private set; }

    public ICommand StartCommand { get; }

    public ICommand DebugCommand { get; }

    public ICommand StopCommand { get; }

    public ICommand InvokeActionCommand { get; }

    public ICommand ToggleCategoryCommand { get; }

    public ICommand StartDragCommand { get; }

    public ICommand OpenDockLabCommand { get; }

    public ICommand LayoutChangedCommand { get; }

    public async Task InitializeDockingAsync()
    {
        if (_dockLayoutPersistenceService is null)
        {
            ActiveLayout = null;
            return;
        }

        ActiveLayout = await _dockLayoutPersistenceService.RestoreAsync();
    }

    public bool HasRestorableDockPanels()
    {
        if (ActiveLayout is null)
        {
            return false;
        }

        var knownPanelIds = Panels
            .Select(panel => panel.PanelId)
            .ToHashSet(StringComparer.Ordinal);

        var snapshotPanelIds = ActiveLayout.Groups
            .SelectMany(group => group.Panels)
            .Select(panel => panel.PanelId)
            .Concat(ActiveLayout.AutoHideItems.Select(item => item.PanelId))
            .ToHashSet(StringComparer.Ordinal);

        var hasUnknownPanelIds = snapshotPanelIds.Any(panelId => !knownPanelIds.Contains(panelId));
        if (hasUnknownPanelIds)
        {
            return false;
        }

        var mappedPanelCount = snapshotPanelIds.Count(knownPanelIds.Contains);

        return mappedPanelCount >= 3;
    }

    private async Task RunAsync(CancellationToken commandToken)
    {
        await ExecuteRunAsync(commandToken, debugMode: false);
    }

    private async Task DebugAsync(CancellationToken commandToken)
    {
        await ExecuteRunAsync(commandToken, debugMode: true);
    }

    private async Task ExecuteRunAsync(CancellationToken commandToken, bool debugMode)
    {
        var hasAuthoredFlow = _flowCanvasViewModel.Document.Nodes.Count > 0;

        _flowCanvasViewModel.ClearRuntimeNodeOutcomes();

        if (!hasAuthoredFlow && string.IsNullOrWhiteSpace(Url))
        {
            SetStatus("Enter a valid URL", "$(warning)");
            return;
        }

        _runCancellationTokenSource?.Dispose();
        _runCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(commandToken);
        _activeRunExecutionControl?.Dispose();
        _activeRunExecutionControl = new FlowRunExecutionControl();
        var originalHeadless = _browserOptions.Headless;

        if (debugMode && originalHeadless)
        {
            _browserOptions.Headless = false;
            UpdateExecutionProfileStatusItem();
        }

        try
        {
            IsRunning = true;
            RunState = UiRunState.Running;
            SetStatus(debugMode ? "Debugging" : "Running", "$(sync~spin)");
            _diagnosticsService.Info(debugMode ? "Debug run started." : "Run started.", new Dictionary<string, string>
            {
                ["usesFlowCanvas"] = hasAuthoredFlow.ToString(),
                ["targetUrl"] = Url,
            });

            if (debugMode)
            {
                _diagnosticsService.Info(hasAuthoredFlow
                    ? "Debug run requested; launching flow in headed mode."
                    : "Debug run requested; launching Chromium in headed mode.");
            }

            if (hasAuthoredFlow)
            {
                var executionGraph = _flowCanvasViewModel.CreateExecutionGraph();
                await _flowExecutionBridge.PrepareRunAsync(executionGraph, debugMode, _runCancellationTokenSource.Token, _activeRunExecutionControl);
            }
            else
            {
                await _orchestrator.RunNavigationAsync(Url, _runCancellationTokenSource.Token);
            }

            if (_activeRunExecutionControl is not null)
            {
                await _activeRunExecutionControl.WaitIfPausedAsync(_runCancellationTokenSource.Token);
            }

            _runCancellationTokenSource.Token.ThrowIfCancellationRequested();

            RunState = UiRunState.Completed;
            SetStatus(debugMode ? "Debug session ready" : "Completed", "$(check)");
            _diagnosticsService.Info(debugMode ? "Debug run completed." : "Run completed.");
        }
        catch (OperationCanceledException)
        {
            RunState = UiRunState.Cancelled;
            SetStatus(debugMode ? "Debug cancelled" : "Cancelled", "$(circle-slash)");
            _diagnosticsService.Warn(debugMode ? "Debug run cancelled." : "Run cancelled.");
        }
        catch (Exception exception)
        {
            RunState = UiRunState.Failed;
            SetStatus($"Failed: {exception.Message}", "$(error)");
            _diagnosticsService.Error(debugMode ? "Debug run failed." : "Run failed.", exception);
        }
        finally
        {
            if (_browserOptions.Headless != originalHeadless)
            {
                _browserOptions.Headless = originalHeadless;
                UpdateExecutionProfileStatusItem();
            }

            IsRunning = false;
            _runCancellationTokenSource?.Dispose();
            _runCancellationTokenSource = null;
            _activeRunExecutionControl?.Dispose();
            _activeRunExecutionControl = null;
        }
    }

    private bool CanUseStartCommand()
    {
        return !IsRunning || CanPause || CanResume;
    }

    private void ToggleRunPauseResume()
    {
        if (!IsRunning)
        {
            _ = RunAsync(CancellationToken.None);
            return;
        }

        if (CanPause)
        {
            _activeRunExecutionControl?.RequestPause();
            RunState = UiRunState.Paused;
            SetStatus("Paused", "$(debug-pause)");
            _diagnosticsService.Info("Pause requested.");
            _diagnosticsService.Info("Run paused.");
            return;
        }

        if (CanResume)
        {
            _activeRunExecutionControl?.Resume();
            RunState = UiRunState.Running;
            SetStatus("Running", "$(sync~spin)");
            _diagnosticsService.Info("Continue playing requested.");
            _diagnosticsService.Info("Run resumed.");
        }
    }

    private void Stop()
    {
        _activeRunExecutionControl?.Resume();
        _runCancellationTokenSource?.Cancel();
        _diagnosticsService.Warn("Stop requested");
        _ = CloseActiveRunsAsync();
    }

    private async void OnLogEntryAdded(LogEntry entry)
    {
        // Mirror in-memory diagnostics to the persisted app log for post-run troubleshooting.
        var persistedMessage = entry.Message;
        if (entry.ContextData is not null && entry.ContextData.TryGetValue("screenshotPath", out var screenshotPath) && !string.IsNullOrWhiteSpace(screenshotPath))
        {
            persistedMessage = $"{persistedMessage} (screenshotPath={screenshotPath})";
        }

        if (string.Equals(entry.Level, "ERROR", StringComparison.OrdinalIgnoreCase))
        {
            AppCrashLogger.Error(persistedMessage);
        }
        else if (string.Equals(entry.Level, "WARN", StringComparison.OrdinalIgnoreCase))
        {
            AppCrashLogger.Warn(persistedMessage);
        }
        else
        {
            AppCrashLogger.Info(persistedMessage);
        }

        await _uiDispatcherService.InvokeAsync(() =>
        {
            UpdateRuntimeNodeOutcomes(entry);

            Logs.Add(new UiLogItem
            {
                TimestampUtc = entry.TimestampUtc,
                Level = entry.Level,
                Message = entry.Message,
                ScreenshotPath = entry.ContextData is not null && entry.ContextData.TryGetValue("screenshotPath", out var path)
                    ? path
                    : null,
            });

            UpdateDiagnosticsCountStatusItem();
        });
    }

    private void UpdateRuntimeNodeOutcomes(LogEntry entry)
    {
        if (entry.ContextData is null)
        {
            return;
        }

        if (!entry.ContextData.TryGetValue("sourceNodeId", out var sourceNodeId) || string.IsNullOrWhiteSpace(sourceNodeId))
        {
            return;
        }

        if (entry.Message.Contains("[RUN][NODE][PASS]", StringComparison.Ordinal))
        {
            _flowCanvasViewModel.MarkNodeRunSucceeded(sourceNodeId);
            return;
        }

        if (entry.Message.Contains("[RUN][NODE][FAIL]", StringComparison.Ordinal))
        {
            _flowCanvasViewModel.MarkNodeRunFailed(sourceNodeId);
        }
    }

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private void RaiseCommandStateChanged()
    {
        _startCommand.RaiseCanExecuteChanged();
        _debugCommand.RaiseCanExecuteChanged();
        _stopCommand.RaiseCanExecuteChanged();
        _stopStatusActionItem.IsEnabled = IsRunning || IsPaused;
    }

    private void NotifyRunControlPropertiesChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPaused)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanPause)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanResume)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RunButtonIconGlyph)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RunButtonToolTip)));
    }

    private void InvokeAction(object? parameter)
    {
        if (parameter is not UiActionInvokeRequest request)
        {
            return;
        }

        LastInvokedActionRequest = request;
        _diagnosticsService.Info($"Sidebar action selected: {request.ActionName}");
    }

    private void ToggleCategory(object? parameter)
    {
        if (parameter is not UiActionCategoryToggleRequest request)
        {
            return;
        }

        if (request.IsExpanded)
        {
            ExpandedCategoryIds.Add(request.CategoryId);
        }
        else
        {
            ExpandedCategoryIds.Remove(request.CategoryId);
        }
    }

    private void StartDragAction(object? parameter)
    {
        if (parameter is not UiActionDragRequest request)
        {
            return;
        }

        LastDragActionRequest = request;
        _diagnosticsService.Info($"Sidebar drag started: {request.ActionName}");
    }

    private void OpenDockLab()
    {
        _testDockWindowService.Show();
        _diagnosticsService.Info("Opened dock panels playground");
    }

    private void OnDockLayoutChanged(DockLayoutSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return;
        }

        ActiveLayout = snapshot;
        _dockLayoutPersistenceService?.ScheduleSave(snapshot);
    }

    private void SetStatus(string value, string? glyph = null)
    {
        Status = value;
        _runStateStatusItem.Text = value;
        if (glyph is not null)
        {
            _runStateStatusItem.IconGlyph = glyph;
        }
    }

    private void UpdateTargetHostStatusItem()
    {
        _targetHostStatusItem.Text = BuildTargetHostText(Url);
        _targetHostStatusItem.ToolTip = string.IsNullOrWhiteSpace(Url)
            ? "No target URL is configured"
            : $"Full target URL: {Url}";
    }

    private void UpdateDiagnosticsCountStatusItem()
    {
        _diagnosticsCountStatusItem.Text = $"Logs {Logs.Count}";
    }

    private void UpdateExecutionProfileStatusItem()
    {
        _executionProfileStatusItem.Text = BuildExecutionProfileText();
        _executionProfileStatusItem.ToolTip = BuildExecutionProfileTooltip();
    }

    private string BuildExecutionProfileText()
    {
        var mode = _browserOptions.Headless ? "Headless" : "Headed";
        return $"Chromium • {mode}";
    }

    private string BuildExecutionProfileTooltip()
    {
        return $"Browser: Chromium | Timeout: {_browserOptions.TimeoutMs} ms | Retries: {_browserOptions.RetryCount}";
    }

    private static string BuildTargetHostText(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
        {
            return $"Target {uri.Host}";
        }

        return "Target not set";
    }

    private void RefreshSidebarProjection()
    {
        var search = ActionSearchText.Trim();

        FilteredActionCategories.Clear();
        foreach (var category in _actionCatalog)
        {
            var matches = category.Actions
                .Where(action => IsSearchMatch(action, search))
                .ToList();

            if (matches.Count == 0)
            {
                continue;
            }

            FilteredActionCategories.Add(new UiActionCategory
            {
                CategoryId = category.CategoryId,
                CategoryName = category.CategoryName,
                Actions = new ObservableCollection<UiActionItem>(matches),
            });
        }
    }

    private static bool IsSearchMatch(UiActionItem action, string search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        if (action.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return action.Keywords.Any(keyword => keyword.Contains(search, StringComparison.OrdinalIgnoreCase));
    }

    private void LoadActionCatalog()
    {
        _actionCatalog.Clear();

        var assemblies = new[] { typeof(OpenBrowserAction).Assembly };
        var categories = _actionCatalogBuilder.Build(assemblies);
        foreach (var category in categories)
        {
            _actionCatalog.Add(category);
        }
    }

    private async Task CloseActiveRunsAsync()
    {
        try
        {
            await Task.WhenAll(
                _orchestrator.CloseActiveSessionAsync(),
                _flowExecutionBridge.CloseActiveSessionAsync());
        }
        catch (Exception exception)
        {
            _diagnosticsService.Error("Failed to close active automation sessions.", exception);
        }
    }

    private sealed class NullTestDockWindowService : ITestDockWindowService
    {
        public void Show()
        {
        }
    }
}
