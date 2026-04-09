using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WpfAutomation.App.Commands;
using WpfAutomation.App.Docking.Layout;
using WpfAutomation.App.Docking.Models;
using WpfAutomation.App.Docking.Services;

namespace WpfAutomation.App.ViewModels;

public sealed class TestDockViewModel : INotifyPropertyChanged
{
    private readonly IDockLayoutPersistenceService _layoutPersistenceService;
    private DockLayoutSnapshot? _activeLayout;

    public TestDockViewModel(IDockLayoutPersistenceService layoutPersistenceService)
    {
        _layoutPersistenceService = layoutPersistenceService;

        Panels =
        [
            new DockPanelDescriptor { PanelId = "action-panel", Title = "Action Panel", ContentKey = "Automation action toolbox", PanelKind = DockPanelKind.ToolWindow },
            new DockPanelDescriptor { PanelId = "canvas", Title = "Canvas", ContentKey = "Primary composition surface", PanelKind = DockPanelKind.DocumentWindow, IsPinnable = false, IsClosable = false },
            new DockPanelDescriptor { PanelId = "properties", Title = "Properties Panel", ContentKey = "Selected node and panel properties", PanelKind = DockPanelKind.ToolWindow },
            new DockPanelDescriptor { PanelId = "errors", Title = "Errors", ContentKey = "Validation and execution errors", PanelKind = DockPanelKind.ToolWindow },
            new DockPanelDescriptor { PanelId = "logs", Title = "Logs", ContentKey = "Run and diagnostics log output", PanelKind = DockPanelKind.ToolWindow },
        ];

        LayoutChangedCommand = new RelayCommand(parameter => OnLayoutChanged(parameter as DockLayoutSnapshot));
        ResetLayoutCommand = new AsyncRelayCommand(ResetLayoutAsync);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<DockPanelDescriptor> Panels { get; }

    public DockLayoutSnapshot? ActiveLayout
    {
        get => _activeLayout;
        set => SetProperty(ref _activeLayout, value);
    }

    public ICommand LayoutChangedCommand { get; }

    public ICommand ResetLayoutCommand { get; }

    public async Task InitializeAsync()
    {
        ActiveLayout = await _layoutPersistenceService.RestoreAsync();
    }

    public bool HasRestorablePanels()
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

    private void OnLayoutChanged(DockLayoutSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return;
        }

        ActiveLayout = snapshot;
        _layoutPersistenceService.ScheduleSave(snapshot);
    }

    private async Task ResetLayoutAsync(CancellationToken cancellationToken)
    {
        await _layoutPersistenceService.ResetAsync(cancellationToken);
        ActiveLayout = new DockLayoutSnapshot();
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
}
