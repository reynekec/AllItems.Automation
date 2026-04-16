using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AllItems.Automation.Browser.App.Models;

/// <summary>
/// Represents a single actionable item surfaced in the sidebar catalog.
/// </summary>
public sealed class UiActionItem : INotifyPropertyChanged
{
    private bool _isSelected;

    public string ActionId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string IconKeyOrPath { get; init; } = string.Empty;

    public string CategoryId { get; init; } = string.Empty;

    public string CategoryName { get; init; } = string.Empty;

    public IReadOnlyList<string> Keywords { get; init; } = [];

    public bool IsContainer { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}