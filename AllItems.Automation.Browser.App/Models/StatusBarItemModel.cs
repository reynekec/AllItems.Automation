using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace AllItems.Automation.Browser.App.Models;

public sealed class StatusBarItemModel : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _text = string.Empty;
    private string? _iconGlyph;
    private string? _toolTip;
    private ICommand? _command;
    private object? _commandParameter;
    private StatusBarItemPlacement _placement;
    private bool _isVisible = true;
    private bool _isEnabled = true;
    private int _order;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value);
    }

    public string? IconGlyph
    {
        get => _iconGlyph;
        set => SetProperty(ref _iconGlyph, value);
    }

    public string? ToolTip
    {
        get => _toolTip;
        set => SetProperty(ref _toolTip, value);
    }

    public ICommand? Command
    {
        get => _command;
        set => SetProperty(ref _command, value);
    }

    public object? CommandParameter
    {
        get => _commandParameter;
        set => SetProperty(ref _commandParameter, value);
    }

    public StatusBarItemPlacement Placement
    {
        get => _placement;
        set => SetProperty(ref _placement, value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public int Order
    {
        get => _order;
        set => SetProperty(ref _order, value);
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