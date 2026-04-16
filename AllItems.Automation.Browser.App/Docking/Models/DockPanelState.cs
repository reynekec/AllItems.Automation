using System.ComponentModel;
using System.Runtime.CompilerServices;
using AllItems.Automation.Browser.App.Docking.Layout;

namespace AllItems.Automation.Browser.App.Docking.Models;

public sealed class DockPanelState : INotifyPropertyChanged
{
    private DockZone _zone;
    private bool _isPinned = true;
    private bool _isVisible = true;
    private bool _isActive;
    private int _tabOrder;
    private DockLayoutAutoHidePlacement _autoHidePlacement = DockLayoutAutoHidePlacement.Left;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string PanelId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string ContentKey { get; init; } = string.Empty;

    public bool IsClosable { get; init; } = true;

    public bool IsPinnable { get; init; } = true;

    public DockPanelKind PanelKind { get; init; } = DockPanelKind.ToolWindow;

    public bool ShowTabHeader { get; init; } = true;

    public bool IsToolWindow => PanelKind == DockPanelKind.ToolWindow;

    public bool IsDocumentWindow => PanelKind == DockPanelKind.DocumentWindow;

    public DockZone LastDockedZone { get; set; } = DockZone.Center;

    public DockZone Zone
    {
        get => _zone;
        set
        {
            if (_zone == value)
            {
                return;
            }

            _zone = value;
            OnPropertyChanged();
        }
    }

    public bool IsPinned
    {
        get => _isPinned;
        set
        {
            if (_isPinned == value)
            {
                return;
            }

            _isPinned = value;
            OnPropertyChanged();
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible == value)
            {
                return;
            }

            _isVisible = value;
            OnPropertyChanged();
        }
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value)
            {
                return;
            }

            _isActive = value;
            OnPropertyChanged();
        }
    }

    public int TabOrder
    {
        get => _tabOrder;
        set
        {
            if (_tabOrder == value)
            {
                return;
            }

            _tabOrder = value;
            OnPropertyChanged();
        }
    }

    public DockLayoutAutoHidePlacement AutoHidePlacement
    {
        get => _autoHidePlacement;
        set
        {
            if (_autoHidePlacement == value)
            {
                return;
            }

            _autoHidePlacement = value;
            OnPropertyChanged();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
