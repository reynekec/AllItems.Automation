using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfAutomation.App.Models;

namespace WpfAutomation.App.Views;

public partial class ActionBrowser : UserControl
{
    private Point _dragStartPoint;
    private UiActionItem? _dragCandidate;
    private bool _isDragging;

    public static readonly DependencyProperty CategoriesSourceProperty =
        DependencyProperty.Register(
            nameof(CategoriesSource),
            typeof(IEnumerable<UiActionCategory>),
            typeof(ActionBrowser),
            new PropertyMetadata(null, OnFilterSourceChanged));

    public static readonly DependencyProperty FilteredCategoriesSourceProperty =
        DependencyProperty.Register(
            nameof(FilteredCategoriesSource),
            typeof(IEnumerable<UiActionCategory>),
            typeof(ActionBrowser),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ExpandedCategoryIdsProperty =
        DependencyProperty.Register(
            nameof(ExpandedCategoryIds),
            typeof(HashSet<string>),
            typeof(ActionBrowser),
            new PropertyMetadata(null));

    public static readonly DependencyProperty SearchTextProperty =
        DependencyProperty.Register(
            nameof(SearchText),
            typeof(string),
            typeof(ActionBrowser),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnFilterSourceChanged));

    public static readonly DependencyProperty SelectedActionProperty =
        DependencyProperty.Register(
            nameof(SelectedAction),
            typeof(UiActionItem),
            typeof(ActionBrowser),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty InvokeActionCommandProperty =
        DependencyProperty.Register(
            nameof(InvokeActionCommand),
            typeof(ICommand),
            typeof(ActionBrowser),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ToggleCategoryCommandProperty =
        DependencyProperty.Register(
            nameof(ToggleCategoryCommand),
            typeof(ICommand),
            typeof(ActionBrowser),
            new PropertyMetadata(null));

    public static readonly DependencyProperty StartDragCommandProperty =
        DependencyProperty.Register(
            nameof(StartDragCommand),
            typeof(ICommand),
            typeof(ActionBrowser),
            new PropertyMetadata(null));

    public ActionBrowser()
    {
        InitializeComponent();
    }

    public IEnumerable<UiActionCategory>? CategoriesSource
    {
        get => (IEnumerable<UiActionCategory>?)GetValue(CategoriesSourceProperty);
        set => SetValue(CategoriesSourceProperty, value);
    }

    public IEnumerable<UiActionCategory>? FilteredCategoriesSource
    {
        get => (IEnumerable<UiActionCategory>?)GetValue(FilteredCategoriesSourceProperty);
        private set => SetValue(FilteredCategoriesSourceProperty, value);
    }

    public HashSet<string>? ExpandedCategoryIds
    {
        get => (HashSet<string>?)GetValue(ExpandedCategoryIdsProperty);
        set => SetValue(ExpandedCategoryIdsProperty, value);
    }

    public string SearchText
    {
        get => (string)GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public UiActionItem? SelectedAction
    {
        get => (UiActionItem?)GetValue(SelectedActionProperty);
        set => SetValue(SelectedActionProperty, value);
    }

    public ICommand? InvokeActionCommand
    {
        get => (ICommand?)GetValue(InvokeActionCommandProperty);
        set => SetValue(InvokeActionCommandProperty, value);
    }

    public ICommand? ToggleCategoryCommand
    {
        get => (ICommand?)GetValue(ToggleCategoryCommandProperty);
        set => SetValue(ToggleCategoryCommandProperty, value);
    }

    public ICommand? StartDragCommand
    {
        get => (ICommand?)GetValue(StartDragCommandProperty);
        set => SetValue(StartDragCommandProperty, value);
    }

    private void CategoryTreeItem_OnExpanded(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is not TreeViewItem { DataContext: UiActionCategory category })
        {
            return;
        }

        ExpandedCategoryIds ??= [];
        ExpandedCategoryIds.Add(category.CategoryId);

        ToggleCategoryCommand?.Execute(new UiActionCategoryToggleRequest
        {
            CategoryId = category.CategoryId,
            CategoryName = category.CategoryName,
            IsExpanded = true,
        });
    }

    private void CategoryTreeItem_OnCollapsed(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is not TreeViewItem { DataContext: UiActionCategory category })
        {
            return;
        }

        ExpandedCategoryIds?.Remove(category.CategoryId);

        ToggleCategoryCommand?.Execute(new UiActionCategoryToggleRequest
        {
            CategoryId = category.CategoryId,
            CategoryName = category.CategoryName,
            IsExpanded = false,
        });
    }

    private static void OnFilterSourceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        if (dependencyObject is not ActionBrowser control)
        {
            return;
        }

        control.RefreshFilteredCategories();
    }

    private void RefreshFilteredCategories()
    {
        var search = SearchText?.Trim() ?? string.Empty;
        var categories = CategoriesSource ?? [];

        var filtered = new List<UiActionCategory>();
        foreach (var category in categories)
        {
            var matchingActions = category.Actions.Where(action => MatchesSearch(action, search)).ToList();
            if (matchingActions.Count == 0)
            {
                continue;
            }

            filtered.Add(new UiActionCategory
            {
                CategoryId = category.CategoryId,
                CategoryName = category.CategoryName,
                Actions = new ObservableCollection<UiActionItem>(matchingActions),
            });
        }

        FilteredCategoriesSource = filtered;
    }

    private static bool MatchesSearch(UiActionItem action, string search)
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

    private void CategoryTreeItem_OnLoaded(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is not TreeViewItem { DataContext: UiActionCategory category } treeItem)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            treeItem.IsExpanded = true;
            return;
        }

        if (ExpandedCategoryIds?.Contains(category.CategoryId) == true)
        {
            treeItem.IsExpanded = true;
        }
    }

    private void CategoryHeader_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs eventArgs)
    {
        if (eventArgs.ClickCount > 1)
        {
            eventArgs.Handled = true;
            return;
        }

        if (sender is not DependencyObject source)
        {
            return;
        }

        var treeItem = ItemsControl.ContainerFromElement(CategoryTree, source) as TreeViewItem;
        if (treeItem?.DataContext is not UiActionCategory || !treeItem.HasItems)
        {
            return;
        }

        treeItem.IsExpanded = !treeItem.IsExpanded;
        eventArgs.Handled = true;
    }

    private void ActionInvokeButton_OnClick(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Button { DataContext: UiActionItem action })
        {
            return;
        }

        var request = new UiActionInvokeRequest
        {
            ActionId = action.ActionId,
            ActionName = action.DisplayName,
            CategoryId = action.CategoryId,
            CategoryName = action.CategoryName,
        };

        if (InvokeActionCommand?.CanExecute(request) == true)
        {
            InvokeActionCommand.Execute(request);
        }
    }

    private void ActionInvokeButton_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs)
    {
        _isDragging = false;

        if (sender is not Button { DataContext: UiActionItem action })
        {
            return;
        }

        _dragCandidate = action;
        _dragStartPoint = eventArgs.GetPosition(this);
    }

    private void ActionInvokeButton_OnPreviewMouseMove(object sender, MouseEventArgs eventArgs)
    {
        if (_isDragging || _dragCandidate is null || eventArgs.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = eventArgs.GetPosition(this);
        if (Math.Abs(current.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var candidate = _dragCandidate;
        _dragCandidate = null;
        _isDragging = true;

        var request = new UiActionDragRequest
        {
            ActionId = candidate.ActionId,
            ActionName = candidate.DisplayName,
            CategoryId = candidate.CategoryId,
            CategoryName = candidate.CategoryName,
            IsContainer = candidate.IsContainer,
        };

        try
        {
            if (StartDragCommand?.CanExecute(request) == true)
            {
                StartDragCommand.Execute(request);
            }

            if (sender is DependencyObject dragSource)
            {
                DragDrop.DoDragDrop(dragSource, request, DragDropEffects.Copy);
            }
        }
        finally
        {
            _isDragging = false;
        }
    }

    private void ActionInvokeButton_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs eventArgs)
    {
        _dragCandidate = null;
        _isDragging = false;
    }

    private void CategoryTree_OnPreviewMouseWheel(object sender, MouseWheelEventArgs eventArgs)
    {
        var scrollLines = Math.Max(1, Math.Abs(eventArgs.Delta) / Mouse.MouseWheelDeltaForOneLine);
        for (var line = 0; line < scrollLines; line++)
        {
            if (eventArgs.Delta > 0)
            {
                CategoryScrollViewer.LineUp();
            }
            else
            {
                CategoryScrollViewer.LineDown();
            }
        }

        eventArgs.Handled = true;
    }
}