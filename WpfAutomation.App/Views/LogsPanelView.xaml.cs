using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfAutomation.App.Models;

namespace WpfAutomation.App.Views;

public partial class LogsPanelView : UserControl
{
    public LogsPanelView()
    {
        InitializeComponent();
    }

    private void LogsListBox_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (sender is not ListBox listBox)
        {
            return;
        }

        var clickedElement = eventArgs.OriginalSource as DependencyObject;
        var clickedItem = FindAncestor<ListBoxItem>(clickedElement);

        if (clickedItem?.DataContext is UiLogItem logItem)
        {
            listBox.SelectedItem = logItem;
            return;
        }

        listBox.SelectedItem = null;
    }

    private void CopyMenuItem_OnClick(object sender, RoutedEventArgs eventArgs)
    {
        if (LogsListBox.SelectedItem is not UiLogItem selectedItem)
        {
            return;
        }

        Clipboard.SetText(UiLogClipboardFormatter.Format(selectedItem));
    }

    private void CopyAllMenuItem_OnClick(object sender, RoutedEventArgs eventArgs)
    {
        if (LogsListBox.ItemsSource is not IEnumerable items)
        {
            Clipboard.Clear();
            return;
        }

        var logs = items.OfType<UiLogItem>().ToList();
        if (logs.Count == 0)
        {
            Clipboard.Clear();
            return;
        }

        Clipboard.SetText(UiLogClipboardFormatter.FormatAll(logs));
    }

    private static T? FindAncestor<T>(DependencyObject? current)
        where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
