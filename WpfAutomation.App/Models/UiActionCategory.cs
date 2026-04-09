using System.Collections.ObjectModel;

namespace WpfAutomation.App.Models;

/// <summary>
/// Represents a logical sidebar action group and its available actions.
/// </summary>
public sealed class UiActionCategory
{
    public string CategoryId { get; init; } = string.Empty;

    public string CategoryName { get; init; } = string.Empty;

    public ObservableCollection<UiActionItem> Actions { get; init; } = [];
}