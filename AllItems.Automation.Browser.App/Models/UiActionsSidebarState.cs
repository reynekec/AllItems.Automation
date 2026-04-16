using System.Collections.ObjectModel;

namespace AllItems.Automation.Browser.App.Models;

/// <summary>
/// Parent-owned mutable state for the actions sidebar session.
/// </summary>
public sealed class UiActionsSidebarState
{
    public string SearchText { get; set; } = string.Empty;

    public HashSet<string> ExpandedCategoryIds { get; } = [];

    public ObservableCollection<UiActionCategory> FilteredCategories { get; } = [];
}