using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WpfAutomation.App.Models;

namespace WpfAutomation.App.Views;

public partial class StatusBarControl : UserControl
{
    private INotifyCollectionChanged? _collectionChangedSource;

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(StatusBarControl),
            new PropertyMetadata(null, OnItemsSourceChanged));

    public StatusBarControl()
    {
        LeftItems = [];
        RightItems = [];
        InitializeComponent();
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public ObservableCollection<StatusBarItemModel> LeftItems { get; }

    public ObservableCollection<StatusBarItemModel> RightItems { get; }

    private static void OnItemsSourceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        if (dependencyObject is not StatusBarControl control)
        {
            return;
        }

        control.DetachFromItems(eventArgs.OldValue as IEnumerable);
        control.AttachToItems(eventArgs.NewValue as IEnumerable);
        control.RefreshProjection();
    }

    private void AttachToItems(IEnumerable? items)
    {
        if (items is null)
        {
            return;
        }

        if (items is INotifyCollectionChanged collectionChanged)
        {
            _collectionChangedSource = collectionChanged;
            _collectionChangedSource.CollectionChanged += OnItemsCollectionChanged;
        }

        foreach (var item in EnumerateItems(items))
        {
            item.PropertyChanged += OnItemPropertyChanged;
        }
    }

    private void DetachFromItems(IEnumerable? items)
    {
        if (_collectionChangedSource is not null)
        {
            _collectionChangedSource.CollectionChanged -= OnItemsCollectionChanged;
            _collectionChangedSource = null;
        }

        if (items is null)
        {
            return;
        }

        foreach (var item in EnumerateItems(items))
        {
            item.PropertyChanged -= OnItemPropertyChanged;
        }
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs)
    {
        if (eventArgs.OldItems is not null)
        {
            foreach (var item in eventArgs.OldItems.OfType<StatusBarItemModel>())
            {
                item.PropertyChanged -= OnItemPropertyChanged;
            }
        }

        if (eventArgs.NewItems is not null)
        {
            foreach (var item in eventArgs.NewItems.OfType<StatusBarItemModel>())
            {
                item.PropertyChanged += OnItemPropertyChanged;
            }
        }

        RefreshProjection();
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        RefreshProjection();
    }

    private void RefreshProjection()
    {
        var items = EnumerateItems(ItemsSource)
            .Where(item => item.IsVisible)
            .OrderBy(item => item.Order)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToList();

        LeftItems.Clear();
        foreach (var leftItem in items.Where(item => item.Placement == StatusBarItemPlacement.Left))
        {
            LeftItems.Add(leftItem);
        }

        RightItems.Clear();
        foreach (var rightItem in items.Where(item => item.Placement == StatusBarItemPlacement.Right))
        {
            RightItems.Add(rightItem);
        }
    }

    private static IEnumerable<StatusBarItemModel> EnumerateItems(IEnumerable? items)
    {
        if (items is null)
        {
            return [];
        }

        return items.OfType<StatusBarItemModel>();
    }
}