using System.Collections.ObjectModel;

namespace AllItems.Automation.Browser.App.Models;

/// <summary>
/// Represents a logical sidebar action group and its available actions.
/// </summary>
public sealed class UiActionCategory
{
    public string CategoryId { get; init; } = string.Empty;

    public string CategoryName { get; init; } = string.Empty;

    public ObservableCollection<UiActionCategory> ChildCategories { get; init; } = [];

    public ObservableCollection<UiActionItem> Actions { get; init; } = [];

    public IEnumerable<object> Items => ChildCategories.Count > 0
        ? ChildCategories
        : Actions;
}