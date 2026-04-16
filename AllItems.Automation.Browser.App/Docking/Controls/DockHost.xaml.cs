using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AllItems.Automation.Browser.App.Commands;
using AllItems.Automation.Browser.App.Docking.Contracts;
using AllItems.Automation.Browser.App.Docking.Layout;
using AllItems.Automation.Browser.App.Docking.Models;

namespace AllItems.Automation.Browser.App.Docking.Controls;

public partial class DockHost : UserControl, INotifyPropertyChanged
{
    private const string DockPanelDragFormat = "DockHost.PanelId";
    private const double ExpandedSideZoneWidth = 260;
    private const double CompactSideZoneWidth = 200;
    private const double ExpandedSideZoneMinWidth = 180;
    private const double CompactSideZoneMinWidth = 140;
    private const double AutoHideSideStripWidth = 24;
    private const double ExpandedVerticalZoneHeight = 170;
    private const double ExpandedVerticalZoneMinHeight = 100;
    private const double SplitterThickness = 6;

    public static readonly DependencyProperty PanelsSourceProperty = DependencyProperty.Register(
        nameof(PanelsSource),
        typeof(IEnumerable<DockPanelDescriptor>),
        typeof(DockHost),
        new PropertyMetadata(null, OnPanelsSourceChanged));

    public static readonly DependencyProperty ActiveLayoutProperty = DependencyProperty.Register(
        nameof(ActiveLayout),
        typeof(DockLayoutSnapshot),
        typeof(DockHost),
        new PropertyMetadata(null, OnActiveLayoutChanged));

    public static readonly DependencyProperty LifecycleCommandProperty = DependencyProperty.Register(
        nameof(LifecycleCommand),
        typeof(ICommand),
        typeof(DockHost),
        new PropertyMetadata(null));

    public static readonly DependencyProperty LayoutChangedCommandProperty = DependencyProperty.Register(
        nameof(LayoutChangedCommand),
        typeof(ICommand),
        typeof(DockHost),
        new PropertyMetadata(null));

    public static readonly DependencyProperty PanelContentTemplateSelectorProperty = DependencyProperty.Register(
        nameof(PanelContentTemplateSelector),
        typeof(DataTemplateSelector),
        typeof(DockHost),
        new PropertyMetadata(null));

    private readonly ObservableCollection<DockPanelState> _leftPanels = [];
    private readonly ObservableCollection<DockPanelState> _rightPanels = [];
    private readonly ObservableCollection<DockPanelState> _topPanels = [];
    private readonly ObservableCollection<DockPanelState> _bottomPanels = [];
    private readonly ObservableCollection<DockPanelState> _centerPanels = [];
    private readonly ObservableCollection<DockPanelState> _autoHideLeftPanels = [];
    private readonly ObservableCollection<DockPanelState> _autoHideRightPanels = [];
    private readonly ObservableCollection<DockPanelState> _autoHideTopPanels = [];
    private readonly ObservableCollection<DockPanelState> _autoHideBottomPanels = [];
    private readonly ObservableCollection<DockPanelState> _allPanels = [];
    private readonly Dictionary<string, DockFloatingWindow> _floatingWindows = new(StringComparer.Ordinal);
    private Point _dragStartPoint;
    private DockPanelState? _dragPanel;
    private DockZone _dropZone = DockZone.Center;
    private bool _isApplyingLayout;
    private DockPanelState? _selectedLeftPanel;
    private DockPanelState? _selectedRightPanel;
    private DockPanelState? _selectedTopPanel;
    private DockPanelState? _selectedBottomPanel;
    private DockPanelState? _selectedCenterPanel;

    public DockHost()
    {
        InitializeComponent();

        TogglePinCommand = new RelayCommand(parameter => TogglePin(parameter as DockPanelState));
        FloatCommand = new RelayCommand(parameter => FloatPanel(parameter as DockPanelState));
        CloseCommand = new RelayCommand(parameter => ClosePanel(parameter as DockPanelState));
        RestoreAutoHidePanelCommand = new RelayCommand(parameter => RestoreAutoHidePanel(parameter as DockPanelState));

        Loaded += (_, _) => QueueEnsureTabSelection();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IEnumerable<DockPanelDescriptor>? PanelsSource
    {
        get => (IEnumerable<DockPanelDescriptor>?)GetValue(PanelsSourceProperty);
        set => SetValue(PanelsSourceProperty, value);
    }

    public DockLayoutSnapshot? ActiveLayout
    {
        get => (DockLayoutSnapshot?)GetValue(ActiveLayoutProperty);
        set => SetValue(ActiveLayoutProperty, value);
    }

    public ICommand? LifecycleCommand
    {
        get => (ICommand?)GetValue(LifecycleCommandProperty);
        set => SetValue(LifecycleCommandProperty, value);
    }

    public ICommand? LayoutChangedCommand
    {
        get => (ICommand?)GetValue(LayoutChangedCommandProperty);
        set => SetValue(LayoutChangedCommandProperty, value);
    }

    public DataTemplateSelector? PanelContentTemplateSelector
    {
        get => (DataTemplateSelector?)GetValue(PanelContentTemplateSelectorProperty);
        set => SetValue(PanelContentTemplateSelectorProperty, value);
    }

    public ICommand TogglePinCommand { get; }

    public ICommand FloatCommand { get; }

    public ICommand CloseCommand { get; }

    public ICommand RestoreAutoHidePanelCommand { get; }

    public ObservableCollection<DockPanelState> LeftPanels => _leftPanels;

    public ObservableCollection<DockPanelState> RightPanels => _rightPanels;

    public ObservableCollection<DockPanelState> TopPanels => _topPanels;

    public ObservableCollection<DockPanelState> BottomPanels => _bottomPanels;

    public ObservableCollection<DockPanelState> CenterPanels => _centerPanels;

    public ObservableCollection<DockPanelState> AutoHideLeftPanels => _autoHideLeftPanels;

    public ObservableCollection<DockPanelState> AutoHideRightPanels => _autoHideRightPanels;

    public ObservableCollection<DockPanelState> AutoHideTopPanels => _autoHideTopPanels;

    public ObservableCollection<DockPanelState> AutoHideBottomPanels => _autoHideBottomPanels;

    public IReadOnlyList<DockPanelState> LeftAutoHideTabs => GetAutoHidePanels(DockLayoutAutoHidePlacement.Left);

    public IReadOnlyList<DockPanelState> RightAutoHideTabs => GetAutoHidePanels(DockLayoutAutoHidePlacement.Right);

    public IReadOnlyList<DockPanelState> TopAutoHideTabs => GetAutoHidePanels(DockLayoutAutoHidePlacement.Top);

    public IReadOnlyList<DockPanelState> BottomAutoHideTabs => GetAutoHidePanels(DockLayoutAutoHidePlacement.Bottom);

    public bool HasLeftPanels => _leftPanels.Count > 0;

    public bool HasAutoHideLeftPanels => LeftAutoHideTabs.Count > 0;

    public bool HasAutoHideRightPanels => RightAutoHideTabs.Count > 0;

    public bool HasAutoHideTopPanels => TopAutoHideTabs.Count > 0;

    public bool HasAutoHideBottomPanels => BottomAutoHideTabs.Count > 0;

    public bool HasRightPanels => _rightPanels.Count > 0;

    public bool HasTopPanels => _topPanels.Count > 0;

    public bool HasBottomPanels => _bottomPanels.Count > 0;

    public bool HasCenterPanels => _centerPanels.Count > 0;

    public DockPanelState? SelectedLeftPanel
    {
        get => _selectedLeftPanel;
        set
        {
            if (ReferenceEquals(_selectedLeftPanel, value))
            {
                return;
            }

            _selectedLeftPanel = value;
            OnPropertyChanged(nameof(SelectedLeftPanel));
        }
    }

    public DockPanelState? SelectedRightPanel
    {
        get => _selectedRightPanel;
        set
        {
            if (ReferenceEquals(_selectedRightPanel, value))
            {
                return;
            }

            _selectedRightPanel = value;
            OnPropertyChanged(nameof(SelectedRightPanel));
        }
    }

    public DockPanelState? SelectedTopPanel
    {
        get => _selectedTopPanel;
        set
        {
            if (ReferenceEquals(_selectedTopPanel, value))
            {
                return;
            }

            _selectedTopPanel = value;
            OnPropertyChanged(nameof(SelectedTopPanel));
        }
    }

    public DockPanelState? SelectedBottomPanel
    {
        get => _selectedBottomPanel;
        set
        {
            if (ReferenceEquals(_selectedBottomPanel, value))
            {
                return;
            }

            _selectedBottomPanel = value;
            OnPropertyChanged(nameof(SelectedBottomPanel));
        }
    }

    public DockPanelState? SelectedCenterPanel
    {
        get => _selectedCenterPanel;
        set
        {
            if (ReferenceEquals(_selectedCenterPanel, value))
            {
                return;
            }

            _selectedCenterPanel = value;
            OnPropertyChanged(nameof(SelectedCenterPanel));
        }
    }

    private static void OnPanelsSourceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        if (dependencyObject is not DockHost dockHost)
        {
            return;
        }

        dockHost.LoadPanels(eventArgs.NewValue as IEnumerable<DockPanelDescriptor>);
    }

    private static void OnActiveLayoutChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        if (dependencyObject is not DockHost dockHost || eventArgs.NewValue is not DockLayoutSnapshot snapshot || dockHost._isApplyingLayout)
        {
            return;
        }

        dockHost.ApplyLayoutSnapshot(snapshot);
    }

    private void LoadPanels(IEnumerable<DockPanelDescriptor>? descriptors)
    {
        _leftPanels.Clear();
        _rightPanels.Clear();
        _topPanels.Clear();
        _bottomPanels.Clear();
        _centerPanels.Clear();
        _autoHideLeftPanels.Clear();
        _autoHideRightPanels.Clear();
        _autoHideTopPanels.Clear();
        _autoHideBottomPanels.Clear();
        _allPanels.Clear();

        if (descriptors is null)
        {
            RaiseZoneChangeNotifications();
            return;
        }

        var order = 0;
        foreach (var descriptor in descriptors.Where(descriptor => descriptor.IsInitiallyVisible))
        {
            var panel = new DockPanelState
            {
                PanelId = descriptor.PanelId,
                Title = descriptor.Title,
                ContentKey = descriptor.ContentKey,
                IsClosable = descriptor.IsClosable,
                IsPinnable = descriptor.PanelKind == DockPanelKind.ToolWindow && descriptor.IsPinnable,
                PanelKind = descriptor.PanelKind,
                ShowTabHeader = descriptor.ShowTabHeader,
                Zone = DockZone.Center,
                LastDockedZone = DockZone.Center,
                AutoHidePlacement = DockLayoutAutoHidePlacement.Left,
                IsPinned = false,
                TabOrder = order++,
            };

            _centerPanels.Add(panel);
            _allPanels.Add(panel);
        }

        RaiseZoneChangeNotifications();
        PublishLayoutChanged();
    }

    public void ResetLayoutToDefault()
    {
        ResetAllCollectionsAndPanelState();
        PopulateDefaultDockDistribution();

        RaiseZoneChangeNotifications();
        PublishLayoutChanged();
    }

    private void TogglePin(DockPanelState? panel)
    {
        if (panel is null || !panel.IsPinnable)
        {
            return;
        }

        if (panel.IsPinned)
        {
            panel.IsPinned = false;
            RemoveFromAutoHideCollections(panel);
            DockPanel(panel, panel.LastDockedZone);
            PublishLifecycle(panel, DockPanelLifecycleAction.Unpin, panel.LastDockedZone);
        }
        else
        {
            panel.IsPinned = true;
            panel.LastDockedZone = panel.Zone;
            panel.AutoHidePlacement = ResolveAutoHidePlacement(panel.Zone);
            RemoveFromCurrentCollection(panel);
            AddToAutoHideCollection(panel, panel.LastDockedZone);
            RaiseZoneChangeNotifications();
            PublishLifecycle(panel, DockPanelLifecycleAction.Pin, panel.LastDockedZone);
        }

        PublishLayoutChanged();
    }

    private void FloatPanel(DockPanelState? panel)
    {
        if (panel is null)
        {
            return;
        }

        panel.Zone = DockZone.Floating;
        panel.IsVisible = false;
        RemoveFromCurrentCollection(panel);
        RaiseZoneChangeNotifications();

        OpenFloatingWindow(panel);

        PublishLifecycle(panel, DockPanelLifecycleAction.Float, panel.LastDockedZone);
        PublishLayoutChanged();
    }

    private void ClosePanel(DockPanelState? panel)
    {
        if (panel is null)
        {
            return;
        }

        if (panel.IsToolWindow && panel.IsPinnable)
        {
            var dockZone = panel.Zone == DockZone.Floating ? panel.LastDockedZone : panel.Zone;

            if (_floatingWindows.TryGetValue(panel.PanelId, out var floatingWindow))
            {
                _floatingWindows.Remove(panel.PanelId);
                floatingWindow.Close();
            }

            panel.IsVisible = true;
            panel.IsPinned = true;
            panel.Zone = dockZone;
            panel.LastDockedZone = dockZone;
            panel.AutoHidePlacement = ResolveAutoHidePlacement(dockZone);

            RemoveFromCurrentCollection(panel);
            AddToAutoHideCollection(panel, dockZone);
            RaiseZoneChangeNotifications();

            PublishLifecycle(panel, DockPanelLifecycleAction.Close, dockZone);
            PublishLayoutChanged();
            return;
        }

        panel.IsVisible = false;
        RemoveFromCurrentCollection(panel);
        RaiseZoneChangeNotifications();

        PublishLifecycle(panel, DockPanelLifecycleAction.Close, panel.Zone);
        PublishLayoutChanged();
    }

    private void RemoveFromCurrentCollection(DockPanelState panel)
    {
        _leftPanels.Remove(panel);
        _rightPanels.Remove(panel);
        _topPanels.Remove(panel);
        _bottomPanels.Remove(panel);
        _centerPanels.Remove(panel);
        RemoveFromAutoHideCollections(panel);
    }

    private void RemoveFromAutoHideCollections(DockPanelState panel)
    {
        _autoHideLeftPanels.Remove(panel);
        _autoHideRightPanels.Remove(panel);
        _autoHideTopPanels.Remove(panel);
        _autoHideBottomPanels.Remove(panel);
    }

    private void PublishLifecycle(DockPanelState panel, DockPanelLifecycleAction action, DockZone zone)
    {
        if (LifecycleCommand is null)
        {
            return;
        }

        var placement = zone switch
        {
            DockZone.Left => DockPlacement.Left,
            DockZone.Right => DockPlacement.Right,
            DockZone.Top => DockPlacement.Top,
            DockZone.Bottom => DockPlacement.Bottom,
            _ => DockPlacement.Center,
        };

        var command = new DockPanelLifecycleCommand
        {
            PanelId = panel.PanelId,
            Action = action,
            Placement = placement,
        };

        if (LifecycleCommand.CanExecute(command))
        {
            LifecycleCommand.Execute(command);
        }
    }

    private void PublishLayoutChanged()
    {
        var snapshot = new DockLayoutSnapshot
        {
            Groups =
            [
                BuildGroupSnapshot("left", _leftPanels),
                BuildGroupSnapshot("top", _topPanels),
                BuildGroupSnapshot("center", _centerPanels),
                BuildGroupSnapshot("right", _rightPanels),
                BuildGroupSnapshot("bottom", _bottomPanels),
                .. BuildFloatingGroupSnapshots(),
            ],
            AutoHideItems = BuildAutoHideSnapshots(),
            FloatingHosts = BuildFloatingHostSnapshots(),
        };

        _isApplyingLayout = true;
        try
        {
            ActiveLayout = snapshot;
        }
        finally
        {
            _isApplyingLayout = false;
        }

        if (LayoutChangedCommand is null)
        {
            return;
        }

        if (LayoutChangedCommand.CanExecute(snapshot))
        {
            LayoutChangedCommand.Execute(snapshot);
        }
    }

    private static DockLayoutGroupSnapshot BuildGroupSnapshot(string groupId, IEnumerable<DockPanelState> panels)
    {
        var snapshotPanels = panels
            .OrderBy(panel => panel.TabOrder)
            .Select((panel, index) => new DockLayoutPanelSnapshot
            {
                PanelId = panel.PanelId,
                Title = panel.Title,
                IsPinned = panel.IsPinned,
                TabOrder = index,
            })
            .ToList();

        var activePanel = snapshotPanels.FirstOrDefault()?.PanelId ?? string.Empty;

        return new DockLayoutGroupSnapshot
        {
            GroupId = groupId,
            ActivePanelId = activePanel,
            Panels = snapshotPanels,
        };
    }

    private void RaiseZoneChangeNotifications()
    {
        OnPropertyChanged(nameof(HasLeftPanels));
        OnPropertyChanged(nameof(HasRightPanels));
        OnPropertyChanged(nameof(HasTopPanels));
        OnPropertyChanged(nameof(HasBottomPanels));
        OnPropertyChanged(nameof(HasCenterPanels));
        OnPropertyChanged(nameof(LeftAutoHideTabs));
        OnPropertyChanged(nameof(RightAutoHideTabs));
        OnPropertyChanged(nameof(TopAutoHideTabs));
        OnPropertyChanged(nameof(BottomAutoHideTabs));
        OnPropertyChanged(nameof(HasAutoHideLeftPanels));
        OnPropertyChanged(nameof(HasAutoHideRightPanels));
        OnPropertyChanged(nameof(HasAutoHideTopPanels));
        OnPropertyChanged(nameof(HasAutoHideBottomPanels));

        SelectedLeftPanel = EnsureSelectedPanel(_leftPanels, SelectedLeftPanel);
        SelectedRightPanel = EnsureSelectedPanel(_rightPanels, SelectedRightPanel);
        SelectedTopPanel = EnsureSelectedPanel(_topPanels, SelectedTopPanel);
        SelectedBottomPanel = EnsureSelectedPanel(_bottomPanels, SelectedBottomPanel);
        SelectedCenterPanel = EnsureSelectedPanel(_centerPanels, SelectedCenterPanel);

        ApplyZoneLayoutMetrics();
        QueueEnsureTabSelection();
    }

    private static DockPanelState? EnsureSelectedPanel(IList<DockPanelState> panels, DockPanelState? selected)
    {
        if (panels.Count == 0)
        {
            return null;
        }

        if (selected is null)
        {
            return panels[0];
        }

        return panels.Contains(selected) ? selected : panels[0];
    }

    private void QueueEnsureTabSelection()
    {
        Dispatcher.InvokeAsync(() =>
        {
            EnsureZoneSelection(TopTabControl);
            EnsureZoneSelection(LeftTabControl);
            EnsureZoneSelection(CenterTabControl);
            EnsureZoneSelection(RightTabControl);
            EnsureZoneSelection(BottomTabControl);
        }, DispatcherPriority.Loaded);
    }

    private static void EnsureZoneSelection(TabControl tabControl)
    {
        if (tabControl.Items.Count == 0)
        {
            return;
        }

        if (tabControl.SelectedIndex < 0 || tabControl.SelectedIndex >= tabControl.Items.Count)
        {
            tabControl.SelectedIndex = 0;
        }
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void TabItem_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs)
    {
        _dragStartPoint = eventArgs.GetPosition(this);
        _dragPanel = (sender as TabItem)?.DataContext as DockPanelState;
    }

    private void TabItem_OnPreviewMouseMove(object sender, MouseEventArgs eventArgs)
    {
        if (_dragPanel is null || eventArgs.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var currentPosition = eventArgs.GetPosition(this);
        var delta = currentPosition - _dragStartPoint;

        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var dataObject = new DataObject();
        dataObject.SetData(DockPanelDragFormat, _dragPanel.PanelId);
        DragDrop.DoDragDrop((DependencyObject)sender, dataObject, DragDropEffects.Move);
    }

    private void RootGrid_OnDragEnter(object sender, DragEventArgs eventArgs)
    {
        if (!eventArgs.Data.GetDataPresent(DockPanelDragFormat))
        {
            return;
        }

        DropOverlay.Visibility = Visibility.Visible;
        _dropZone = DockZone.Center;
        HighlightDropTarget(_dropZone);
    }

    private void RootGrid_OnDragOver(object sender, DragEventArgs eventArgs)
    {
        if (!eventArgs.Data.GetDataPresent(DockPanelDragFormat))
        {
            eventArgs.Effects = DragDropEffects.None;
            eventArgs.Handled = true;
            return;
        }

        var zone = ResolveDropZone(eventArgs.GetPosition(DropOverlay));
        _dropZone = zone;
        HighlightDropTarget(zone);
        eventArgs.Effects = DragDropEffects.Move;
        eventArgs.Handled = true;
    }

    private void RootGrid_OnDragLeave(object sender, DragEventArgs eventArgs)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        HighlightDropTarget(null);
    }

    private void RootGrid_OnDrop(object sender, DragEventArgs eventArgs)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        HighlightDropTarget(null);

        if (!TryGetDraggedPanel(eventArgs, out var panel))
        {
            return;
        }

        DockPanel(panel, _dropZone);
        eventArgs.Handled = true;
    }

    private void LeftTabControl_OnDrop(object sender, DragEventArgs eventArgs)
    {
        DropOnTabControl(sender as TabControl, DockZone.Left, eventArgs);
    }

    private void TopTabControl_OnDrop(object sender, DragEventArgs eventArgs)
    {
        DropOnTabControl(sender as TabControl, DockZone.Top, eventArgs);
    }

    private void CenterTabControl_OnDrop(object sender, DragEventArgs eventArgs)
    {
        DropOnTabControl(sender as TabControl, DockZone.Center, eventArgs);
    }

    private void RightTabControl_OnDrop(object sender, DragEventArgs eventArgs)
    {
        DropOnTabControl(sender as TabControl, DockZone.Right, eventArgs);
    }

    private void BottomTabControl_OnDrop(object sender, DragEventArgs eventArgs)
    {
        DropOnTabControl(sender as TabControl, DockZone.Bottom, eventArgs);
    }

    private void DropOnTabControl(TabControl? tabControl, DockZone zone, DragEventArgs eventArgs)
    {
        if (tabControl is null || !TryGetDraggedPanel(eventArgs, out var panel))
        {
            return;
        }

        var targetCollection = GetCollectionForZone(zone);
        var targetIndex = ResolveDropTabIndex(tabControl, eventArgs.GetPosition(tabControl));

        if (panel.Zone == zone)
        {
            ReorderWithinCollection(targetCollection, panel, targetIndex);
            PublishLayoutChanged();
        }
        else
        {
            DockPanel(panel, zone, targetIndex);
        }

        eventArgs.Handled = true;
    }

    private bool TryGetDraggedPanel(DragEventArgs eventArgs, out DockPanelState panel)
    {
        panel = null!;

        var panelId = eventArgs.Data.GetData(DockPanelDragFormat) as string;
        if (string.IsNullOrWhiteSpace(panelId))
        {
            return false;
        }

        var match = _allPanels.FirstOrDefault(existing => string.Equals(existing.PanelId, panelId, StringComparison.Ordinal));
        if (match is null)
        {
            return false;
        }

        panel = match;
        return true;
    }

    private DockZone ResolveDropZone(Point position)
    {
        var element = DropOverlay.InputHitTest(position) as DependencyObject;

        while (element is not null)
        {
            if (element is Border border && border.Tag is string tag)
            {
                return tag switch
                {
                    "Left" => DockZone.Left,
                    "Right" => DockZone.Right,
                    "Top" => DockZone.Top,
                    "Bottom" => DockZone.Bottom,
                    _ => DockZone.Center,
                };
            }

            element = VisualTreeHelper.GetParent(element);
        }

        return DockZone.Center;
    }

    private void HighlightDropTarget(DockZone? zone)
    {
        ApplyDropHighlight(DropTargetLeft, zone == DockZone.Left);
        ApplyDropHighlight(DropTargetRight, zone == DockZone.Right);
        ApplyDropHighlight(DropTargetTop, zone == DockZone.Top);
        ApplyDropHighlight(DropTargetBottom, zone == DockZone.Bottom);
        ApplyDropHighlight(DropTargetCenter, zone == DockZone.Center);
    }

    private static void ApplyDropHighlight(Border border, bool isHighlighted)
    {
        border.BorderThickness = isHighlighted ? new Thickness(2) : new Thickness(1);
        border.Opacity = isHighlighted ? 1 : 0.86;
    }

    private void DockPanel(DockPanelState panel, DockZone zone, int targetIndex = -1)
    {
        if (_floatingWindows.TryGetValue(panel.PanelId, out var floatingWindow))
        {
            _floatingWindows.Remove(panel.PanelId);
            panel.Zone = zone;
            floatingWindow.Close();
        }

        RemoveFromCurrentCollection(panel);
        var targetCollection = GetCollectionForZone(zone);

        if (targetIndex < 0 || targetIndex >= targetCollection.Count)
        {
            targetCollection.Add(panel);
        }
        else
        {
            targetCollection.Insert(targetIndex, panel);
        }

        panel.Zone = zone;
        panel.LastDockedZone = zone;
        panel.IsVisible = true;

        ResetTabOrder(targetCollection);

        PublishLifecycle(panel, DockPanelLifecycleAction.Dock, zone);
        RaiseZoneChangeNotifications();
        PublishLayoutChanged();
    }

    private static void ReorderWithinCollection(ObservableCollection<DockPanelState> collection, DockPanelState panel, int targetIndex)
    {
        var currentIndex = collection.IndexOf(panel);
        if (currentIndex < 0)
        {
            return;
        }

        if (targetIndex < 0 || targetIndex >= collection.Count)
        {
            targetIndex = collection.Count - 1;
        }

        if (currentIndex == targetIndex)
        {
            return;
        }

        collection.Move(currentIndex, targetIndex);
        ResetTabOrder(collection);
    }

    private static void ResetTabOrder(IEnumerable<DockPanelState> panels)
    {
        var order = 0;
        foreach (var panel in panels)
        {
            panel.TabOrder = order++;
        }
    }

    private static void AddPanelToZone(DockPanelState panel, ICollection<DockPanelState> collection, DockZone zone)
    {
        panel.Zone = zone;
        panel.LastDockedZone = zone;
        panel.AutoHidePlacement = ResolveAutoHidePlacement(zone);
        panel.IsPinned = false;
        panel.IsVisible = true;
        collection.Add(panel);
    }

    private void PopulateDefaultDockDistribution()
    {
        var orderedPanels = _allPanels.OrderBy(panel => panel.TabOrder).ToList();

        if (orderedPanels.Count > 0)
        {
            AddPanelToZone(orderedPanels[0], _leftPanels, DockZone.Left);
        }

        if (orderedPanels.Count > 1)
        {
            AddPanelToZone(orderedPanels[1], _centerPanels, DockZone.Center);
        }

        if (orderedPanels.Count > 2)
        {
            AddPanelToZone(orderedPanels[2], _rightPanels, DockZone.Right);
        }

        if (orderedPanels.Count > 3)
        {
            AddPanelToZone(orderedPanels[3], _rightPanels, DockZone.Right);
        }

        if (orderedPanels.Count > 4)
        {
            AddPanelToZone(orderedPanels[4], _bottomPanels, DockZone.Bottom);
        }

        if (orderedPanels.Count > 5)
        {
            AddPanelToZone(orderedPanels[5], _bottomPanels, DockZone.Bottom);
        }

        for (var index = 6; index < orderedPanels.Count; index++)
        {
            AddPanelToZone(orderedPanels[index], _centerPanels, DockZone.Center);
        }

        ResetTabOrder(_leftPanels);
        ResetTabOrder(_centerPanels);
        ResetTabOrder(_rightPanels);
        ResetTabOrder(_bottomPanels);
    }

    private void PlaceOrphanedPanels()
    {
        var placed = new HashSet<string>(
            _leftPanels.Select(p => p.PanelId)
                .Concat(_centerPanels.Select(p => p.PanelId))
                .Concat(_rightPanels.Select(p => p.PanelId))
                .Concat(_topPanels.Select(p => p.PanelId))
                .Concat(_bottomPanels.Select(p => p.PanelId))
                .Concat(_allPanels.Where(p => p.IsPinned).Select(p => p.PanelId))
                .Concat(_floatingWindows.Keys),
            StringComparer.Ordinal);

        foreach (var panel in _allPanels.Where(p => !placed.Contains(p.PanelId)).OrderBy(p => p.TabOrder))
        {
            if (panel.IsToolWindow)
            {
                AddPanelToZone(panel, _rightPanels, DockZone.Right);
            }
            else
            {
                AddPanelToZone(panel, _centerPanels, DockZone.Center);
            }
        }
    }

    private bool HasVisibleLayoutContent()
    {
        return _leftPanels.Count > 0 ||
               _rightPanels.Count > 0 ||
               _topPanels.Count > 0 ||
               _bottomPanels.Count > 0 ||
               _centerPanels.Count > 0 ||
               _autoHideLeftPanels.Count > 0 ||
               _autoHideRightPanels.Count > 0 ||
               _autoHideTopPanels.Count > 0 ||
               _autoHideBottomPanels.Count > 0 ||
               _floatingWindows.Count > 0;
    }

    private void ResetAllCollectionsAndPanelState()
    {
        foreach (var floatingWindow in _floatingWindows.Values.ToList())
        {
            floatingWindow.Close();
        }

        _floatingWindows.Clear();
        _leftPanels.Clear();
        _rightPanels.Clear();
        _topPanels.Clear();
        _bottomPanels.Clear();
        _centerPanels.Clear();
        _autoHideLeftPanels.Clear();
        _autoHideRightPanels.Clear();
        _autoHideTopPanels.Clear();
        _autoHideBottomPanels.Clear();

        foreach (var panel in _allPanels)
        {
            panel.IsPinned = false;
            panel.IsVisible = true;
            panel.Zone = DockZone.Center;
            panel.LastDockedZone = DockZone.Center;
            panel.AutoHidePlacement = DockLayoutAutoHidePlacement.Left;
        }
    }

    private IReadOnlyList<DockPanelState> GetAutoHidePanels(DockLayoutAutoHidePlacement placement)
    {
        return _allPanels
            .Where(panel => panel.IsPinned && panel.AutoHidePlacement == placement)
            .OrderBy(panel => panel.TabOrder)
            .ToList();
    }

    private static DockLayoutAutoHidePlacement ResolveAutoHidePlacement(DockZone zone)
    {
        return zone switch
        {
            DockZone.Right => DockLayoutAutoHidePlacement.Right,
            DockZone.Top => DockLayoutAutoHidePlacement.Top,
            DockZone.Bottom => DockLayoutAutoHidePlacement.Bottom,
            _ => DockLayoutAutoHidePlacement.Left,
        };
    }

    private ObservableCollection<DockPanelState> GetCollectionForZone(DockZone zone)
    {
        return zone switch
        {
            DockZone.Left => _leftPanels,
            DockZone.Right => _rightPanels,
            DockZone.Top => _topPanels,
            DockZone.Bottom => _bottomPanels,
            _ => _centerPanels,
        };
    }

    private static int ResolveDropTabIndex(TabControl tabControl, Point point)
    {
        var element = tabControl.InputHitTest(point) as DependencyObject;
        while (element is not null && element is not TabItem)
        {
            element = VisualTreeHelper.GetParent(element);
        }

        if (element is not TabItem tabItem)
        {
            return -1;
        }

        return tabControl.ItemContainerGenerator.IndexFromContainer(tabItem);
    }

    private void AddToAutoHideCollection(DockPanelState panel, DockZone zone)
    {
        var collection = zone switch
        {
            DockZone.Left => _autoHideLeftPanels,
            DockZone.Right => _autoHideRightPanels,
            DockZone.Top => _autoHideTopPanels,
            DockZone.Bottom => _autoHideBottomPanels,
            _ => _autoHideLeftPanels,
        };

        if (!collection.Contains(panel))
        {
            collection.Add(panel);
        }
    }

    private void RestoreAutoHidePanel(DockPanelState? panel)
    {
        if (panel is null)
        {
            return;
        }

        panel.IsPinned = false;
        RemoveFromAutoHideCollections(panel);
        DockPanel(panel, panel.LastDockedZone);
        PublishLifecycle(panel, DockPanelLifecycleAction.Unpin, panel.LastDockedZone);
    }

    private void OpenFloatingWindow(DockPanelState panel)
    {
        OpenFloatingWindow(panel, null);
    }

    private void OpenFloatingWindow(DockPanelState panel, DockLayoutFloatingHostSnapshot? hostSnapshot)
    {
        if (_floatingWindows.TryGetValue(panel.PanelId, out var existing))
        {
            existing.Activate();
            return;
        }

        var floatingWindow = new DockFloatingWindow(panel, DockPanelDragFormat)
        {
            Owner = Window.GetWindow(this),
        };

        if (hostSnapshot is not null)
        {
            floatingWindow.Left = hostSnapshot.Left;
            floatingWindow.Top = hostSnapshot.Top;
            floatingWindow.Width = hostSnapshot.Width;
            floatingWindow.Height = hostSnapshot.Height;
        }

        floatingWindow.Closed += (_, _) =>
        {
            if (_floatingWindows.Remove(panel.PanelId))
            {
                if (panel.Zone == DockZone.Floating)
                {
                    panel.Zone = panel.LastDockedZone;
                    panel.IsVisible = true;
                    DockPanel(panel, panel.LastDockedZone);
                }
            }
        };

        _floatingWindows[panel.PanelId] = floatingWindow;
        floatingWindow.Show();
        floatingWindow.Focus();
    }

    private void ApplyLayoutSnapshot(DockLayoutSnapshot snapshot)
    {
        if (_allPanels.Count == 0)
        {
            return;
        }

        _isApplyingLayout = true;
        try
        {
            foreach (var floatingWindow in _floatingWindows.Values.ToList())
            {
                floatingWindow.Close();
            }

            _floatingWindows.Clear();
            _leftPanels.Clear();
            _rightPanels.Clear();
            _topPanels.Clear();
            _bottomPanels.Clear();
            _centerPanels.Clear();
            _autoHideLeftPanels.Clear();
            _autoHideRightPanels.Clear();
            _autoHideTopPanels.Clear();
            _autoHideBottomPanels.Clear();

            foreach (var panel in _allPanels)
            {
                panel.IsVisible = true;
                panel.IsPinned = true;
                panel.Zone = DockZone.Center;
                panel.LastDockedZone = DockZone.Center;
                panel.AutoHidePlacement = DockLayoutAutoHidePlacement.Left;
            }

            foreach (var group in snapshot.Groups)
            {
                if (TryGetFloatingHost(snapshot, group.GroupId, out var floatingHost))
                {
                    foreach (var panelSnapshot in group.Panels.OrderBy(panel => panel.TabOrder))
                    {
                        var floatingPanel = _allPanels.FirstOrDefault(panel => string.Equals(panel.PanelId, panelSnapshot.PanelId, StringComparison.Ordinal));
                        if (floatingPanel is null)
                        {
                            continue;
                        }

                        floatingPanel.IsPinned = panelSnapshot.IsPinned;
                        floatingPanel.Zone = DockZone.Floating;
                        floatingPanel.IsVisible = false;
                        OpenFloatingWindow(floatingPanel, floatingHost);
                    }

                    continue;
                }

                var zone = group.GroupId switch
                {
                    "left" => DockZone.Left,
                    "right" => DockZone.Right,
                    "top" => DockZone.Top,
                    "bottom" => DockZone.Bottom,
                    _ => DockZone.Center,
                };

                var target = GetCollectionForZone(zone);
                foreach (var panelSnapshot in group.Panels.OrderBy(panel => panel.TabOrder))
                {
                    var match = _allPanels.FirstOrDefault(panel => string.Equals(panel.PanelId, panelSnapshot.PanelId, StringComparison.Ordinal));
                    if (match is null)
                    {
                        continue;
                    }

                    match.IsPinned = false;
                    match.Zone = zone;
                    match.LastDockedZone = zone;
                    match.AutoHidePlacement = ResolveAutoHidePlacement(zone);
                    target.Add(match);
                }
            }

            foreach (var autoHide in snapshot.AutoHideItems.OrderBy(item => item.Order))
            {
                var match = _allPanels.FirstOrDefault(panel => string.Equals(panel.PanelId, autoHide.PanelId, StringComparison.Ordinal));
                if (match is null)
                {
                    continue;
                }

                var zone = autoHide.Placement switch
                {
                    DockLayoutAutoHidePlacement.Left => DockZone.Left,
                    DockLayoutAutoHidePlacement.Right => DockZone.Right,
                    DockLayoutAutoHidePlacement.Top => DockZone.Top,
                    DockLayoutAutoHidePlacement.Bottom => DockZone.Bottom,
                    _ => DockZone.Left,
                };

                RemoveFromCurrentCollection(match);
                match.IsPinned = true;
                match.Zone = zone;
                match.LastDockedZone = zone;
                match.AutoHidePlacement = autoHide.Placement;
                AddToAutoHideCollection(match, zone);
            }

            if (!HasVisibleLayoutContent())
            {
                PopulateDefaultDockDistribution();
            }
            else
            {
                PlaceOrphanedPanels();
            }

            ResetTabOrder(_leftPanels);
            ResetTabOrder(_rightPanels);
            ResetTabOrder(_topPanels);
            ResetTabOrder(_bottomPanels);
            ResetTabOrder(_centerPanels);
            RaiseZoneChangeNotifications();
        }
        finally
        {
            _isApplyingLayout = false;
        }
    }

    private IReadOnlyList<DockLayoutAutoHideSnapshot> BuildAutoHideSnapshots()
    {
        return BuildAutoHideSnapshots(LeftAutoHideTabs, DockLayoutAutoHidePlacement.Left)
            .Concat(BuildAutoHideSnapshots(RightAutoHideTabs, DockLayoutAutoHidePlacement.Right))
            .Concat(BuildAutoHideSnapshots(TopAutoHideTabs, DockLayoutAutoHidePlacement.Top))
            .Concat(BuildAutoHideSnapshots(BottomAutoHideTabs, DockLayoutAutoHidePlacement.Bottom))
            .ToList();
    }

    private static IEnumerable<DockLayoutAutoHideSnapshot> BuildAutoHideSnapshots(IEnumerable<DockPanelState> panels, DockLayoutAutoHidePlacement placement)
    {
        return panels.Select((panel, index) => new DockLayoutAutoHideSnapshot
        {
            PanelId = panel.PanelId,
            Placement = placement,
            Order = index,
        });
    }

    private IReadOnlyList<DockLayoutGroupSnapshot> BuildFloatingGroupSnapshots()
    {
        return _floatingWindows.Keys
            .Select(panelId => _allPanels.FirstOrDefault(panel => string.Equals(panel.PanelId, panelId, StringComparison.Ordinal)))
            .Where(panel => panel is not null)
            .Select(panel => new DockLayoutGroupSnapshot
            {
                GroupId = $"float:{panel!.PanelId}",
                ActivePanelId = panel.PanelId,
                Panels =
                [
                    new DockLayoutPanelSnapshot
                    {
                        PanelId = panel.PanelId,
                        Title = panel.Title,
                        IsPinned = panel.IsPinned,
                        TabOrder = 0,
                    },
                ],
            })
            .ToList();
    }

    private IReadOnlyList<DockLayoutFloatingHostSnapshot> BuildFloatingHostSnapshots()
    {
        return _floatingWindows.Select(pair => new DockLayoutFloatingHostSnapshot
        {
            HostId = pair.Key,
            GroupId = $"float:{pair.Key}",
            Left = pair.Value.Left,
            Top = pair.Value.Top,
            Width = pair.Value.Width,
            Height = pair.Value.Height,
        }).ToList();
    }

    private static bool TryGetFloatingHost(DockLayoutSnapshot snapshot, string groupId, out DockLayoutFloatingHostSnapshot host)
    {
        host = snapshot.FloatingHosts.FirstOrDefault(candidate => string.Equals(candidate.GroupId, groupId, StringComparison.Ordinal))!;
        return host is not null;
    }

    private void AutoHideButton_OnMouseEnter(object sender, MouseEventArgs eventArgs)
    {
        if (sender is not Button { DataContext: DockPanelState panel })
        {
            return;
        }

        RestoreAutoHidePanel(panel);
    }

    private void RootGrid_OnSizeChanged(object sender, SizeChangedEventArgs eventArgs)
    {
        ApplyZoneLayoutMetrics();
    }

    private void ApplyZoneLayoutMetrics()
    {
        var compact = RootGrid.ActualWidth > 0 && RootGrid.ActualWidth < 980;
        var sideWidth = compact ? CompactSideZoneWidth : ExpandedSideZoneWidth;
        var sideMinWidth = compact ? CompactSideZoneMinWidth : ExpandedSideZoneMinWidth;

        if (HasLeftPanels)
        {
            LeftZoneColumn.Width = new GridLength(sideWidth);
            LeftZoneColumn.MinWidth = sideMinWidth;
            LeftSplitterColumn.Width = new GridLength(SplitterThickness);
        }
        else if (HasAutoHideLeftPanels)
        {
            LeftZoneColumn.Width = new GridLength(AutoHideSideStripWidth);
            LeftZoneColumn.MinWidth = AutoHideSideStripWidth;
            LeftSplitterColumn.Width = new GridLength(0);
        }
        else
        {
            LeftZoneColumn.Width = new GridLength(0);
            LeftZoneColumn.MinWidth = 0;
            LeftSplitterColumn.Width = new GridLength(0);
        }

        if (HasRightPanels)
        {
            RightZoneColumn.Width = new GridLength(sideWidth);
            RightZoneColumn.MinWidth = sideMinWidth;
            RightSplitterColumn.Width = new GridLength(SplitterThickness);
        }
        else if (HasAutoHideRightPanels)
        {
            RightZoneColumn.Width = new GridLength(AutoHideSideStripWidth);
            RightZoneColumn.MinWidth = AutoHideSideStripWidth;
            RightSplitterColumn.Width = new GridLength(0);
        }
        else
        {
            RightZoneColumn.Width = new GridLength(0);
            RightZoneColumn.MinWidth = 0;
            RightSplitterColumn.Width = new GridLength(0);
        }

        if (HasTopPanels)
        {
            TopZoneRow.Height = new GridLength(ExpandedVerticalZoneHeight);
            TopZoneRow.MinHeight = ExpandedVerticalZoneMinHeight;
            TopSplitterRow.Height = new GridLength(SplitterThickness);
        }
        else
        {
            TopZoneRow.Height = new GridLength(0);
            TopZoneRow.MinHeight = 0;
            TopSplitterRow.Height = new GridLength(0);
        }

        if (HasBottomPanels)
        {
            BottomZoneRow.Height = new GridLength(ExpandedVerticalZoneHeight);
            BottomZoneRow.MinHeight = ExpandedVerticalZoneMinHeight;
            BottomSplitterRow.Height = new GridLength(SplitterThickness);
        }
        else
        {
            BottomZoneRow.Height = new GridLength(0);
            BottomZoneRow.MinHeight = 0;
            BottomSplitterRow.Height = new GridLength(0);
        }
    }

    private void RootGrid_OnPreviewKeyDown(object sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key != Key.Tab || Keyboard.Modifiers != ModifierKeys.Control)
        {
            return;
        }

        var tabControl = CenterTabControl;
        if (tabControl.Items.Count == 0)
        {
            return;
        }

        var index = tabControl.SelectedIndex;
        if (index < 0)
        {
            index = 0;
        }

        var nextIndex = (index + 1) % tabControl.Items.Count;
        tabControl.SelectedIndex = nextIndex;
        eventArgs.Handled = true;
    }
}
