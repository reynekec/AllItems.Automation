using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;

namespace SelectorDemo.Wpf;

public partial class BrowserDebugControl : UserControl
{
    private readonly ObservableCollection<DebugEntryViewModel> _consoleEntries = [];
    private readonly ObservableCollection<DebugEntryViewModel> _networkEntries = [];
    private readonly ICollectionView _consoleEntriesView;
    private readonly ICollectionView _networkEntriesView;
    private string _selectedNetworkType = "All";
    private bool _viewsReady;

    public BrowserDebugControl()
    {
        _consoleEntriesView = CollectionViewSource.GetDefaultView(_consoleEntries);
        _networkEntriesView = CollectionViewSource.GetDefaultView(_networkEntries);

        InitializeComponent();

        _consoleEntriesView.Filter = MatchConsoleFilter;
        _networkEntriesView.Filter = MatchNetworkFilter;

        ConsoleEntriesDataGrid.ItemsSource = _consoleEntriesView;
        NetworkEntriesDataGrid.ItemsSource = _networkEntriesView;
        _viewsReady = true;
    }

    public void SetSessionStatus(string message)
    {
        SessionStatusTextBlock.Text = message;
    }

    public void AddEntry(
        string category,
        string message,
        string? detail = null,
        string? statusText = null,
        Brush? statusBrush = null,
        string? networkType = null,
        string? headersText = null)
    {
        var entry = new DebugEntryViewModel(
            DateTime.Now.ToString("HH:mm:ss"),
            category,
            message,
            detail ?? string.Empty,
            statusText ?? string.Empty,
            statusBrush ?? Brushes.Gray,
            networkType ?? string.Empty,
            headersText ?? string.Empty);

        if (string.Equals(category, "Console", StringComparison.OrdinalIgnoreCase))
        {
            _consoleEntries.Insert(0, entry);
        }

        if (string.Equals(category, "Network", StringComparison.OrdinalIgnoreCase))
        {
            _networkEntries.Insert(0, entry);
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        ClearEntries();
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedHeader = (DebugTabs.SelectedItem as TabItem)?.Header?.ToString();
        string content;

        if (string.Equals(selectedHeader, "Network", StringComparison.OrdinalIgnoreCase))
        {
            content = BuildNetworkClipboardText();
        }
        else
        {
            content = BuildConsoleClipboardText();
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            MessageBox.Show("There are no visible entries to copy on this tab.", "Copy", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Clipboard.SetText(content);
    }

    public void ClearEntries()
    {
        _consoleEntries.Clear();
        _networkEntries.Clear();
    }

    private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshViews();
    }

    private void NetworkTypeFilter_Checked(object sender, RoutedEventArgs e)
    {
        _selectedNetworkType = (sender as RadioButton)?.Tag?.ToString() ?? "All";
        RefreshViews();
    }

    private bool MatchConsoleFilter(object item)
    {
        if (item is not DebugEntryViewModel entry)
        {
            return false;
        }

        var term = FilterTextBox?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(term))
        {
            return true;
        }

        return entry.Message.Contains(term, StringComparison.OrdinalIgnoreCase) ||
             entry.Detail.Contains(term, StringComparison.OrdinalIgnoreCase) ||
             entry.StatusText.Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    private bool MatchNetworkFilter(object item)
    {
        if (item is not DebugEntryViewModel entry)
        {
            return false;
        }

        if (!string.Equals(_selectedNetworkType, "All", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(entry.NetworkType, _selectedNetworkType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var term = FilterTextBox?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(term))
        {
            return true;
        }

        return entry.Message.Contains(term, StringComparison.OrdinalIgnoreCase) ||
               entry.Detail.Contains(term, StringComparison.OrdinalIgnoreCase) ||
               entry.StatusText.Contains(term, StringComparison.OrdinalIgnoreCase) ||
               entry.NetworkType.Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshViews()
    {
        if (!_viewsReady)
        {
            return;
        }

        _consoleEntriesView.Refresh();
        _networkEntriesView.Refresh();
    }

    private void NetworkEntriesDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (NetworkEntriesDataGrid.SelectedItem is not DebugEntryViewModel entry)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(entry.HeadersText))
        {
            MessageBox.Show("No headers were captured for this network row.", "Headers", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ShowMultilinePopup("Network Headers", entry.HeadersText);
    }

    private void ConsoleEntriesDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ConsoleEntriesDataGrid.SelectedItem is not DebugEntryViewModel entry)
        {
            return;
        }

        ShowConsolePopup(entry);
    }

    private void ShowConsolePopup(DebugEntryViewModel entry)
    {
        var popup = new Window
        {
            Title = "Console Entry",
            Owner = Window.GetWindow(this),
            Width = 900,
            Height = 620,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var root = new Grid { Margin = new Thickness(12) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var meta = new TextBlock
        {
            Text = $"Time: {entry.Timestamp}    Category: {entry.Category}    Severity: {entry.StatusText}",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(meta, 0);
        root.Children.Add(meta);

        var messageLabel = new TextBlock
        {
            Text = "Message",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4)
        };
        Grid.SetRow(messageLabel, 1);
        root.Children.Add(messageLabel);

        var messageBox = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Text = entry.Message
        };
        Grid.SetRow(messageBox, 2);
        root.Children.Add(messageBox);

        var detailLabel = new TextBlock
        {
            Text = "Detail / Source",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 8, 0, 4)
        };
        Grid.SetRow(detailLabel, 3);
        root.Children.Add(detailLabel);

        var detailBox = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Consolas"),
            Text = string.IsNullOrWhiteSpace(entry.Detail) ? "(no detail)" : entry.Detail
        };
        Grid.SetRow(detailBox, 5);
        root.Children.Add(detailBox);

        popup.Content = root;
        _ = popup.ShowDialog();
    }

    private void ShowMultilinePopup(string title, string content)
    {
        var popup = new Window
        {
            Title = title,
            Owner = Window.GetWindow(this),
            Width = 860,
            Height = 560,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var headerTextBox = new TextBox
        {
            Margin = new Thickness(12),
            IsReadOnly = true,
            AcceptsReturn = true,
            AcceptsTab = true,
            TextWrapping = TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Consolas"),
            Text = content
        };

        popup.Content = headerTextBox;
        _ = popup.ShowDialog();
    }

    private string BuildConsoleClipboardText()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Time\tSeverity\tMessage\tDetail");

        foreach (var entry in _consoleEntriesView.Cast<DebugEntryViewModel>())
        {
            builder.Append(entry.Timestamp);
            builder.Append('\t');
            builder.Append(SanitizeForClipboard(entry.StatusText));
            builder.Append('\t');
            builder.Append(SanitizeForClipboard(entry.Message));
            builder.Append('\t');
            builder.Append(SanitizeForClipboard(entry.Detail));
            builder.AppendLine();
        }

        return builder.Length == "Time\tSeverity\tMessage\tDetail".Length + Environment.NewLine.Length
            ? string.Empty
            : builder.ToString();
    }

    private string BuildNetworkClipboardText()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Time\tType\tStatus\tMessage\tDetail");

        foreach (var entry in _networkEntriesView.Cast<DebugEntryViewModel>())
        {
            builder.Append(entry.Timestamp);
            builder.Append('\t');
            builder.Append(SanitizeForClipboard(entry.NetworkType));
            builder.Append('\t');
            builder.Append(SanitizeForClipboard(entry.StatusText));
            builder.Append('\t');
            builder.Append(SanitizeForClipboard(entry.Message));
            builder.Append('\t');
            builder.Append(SanitizeForClipboard(entry.Detail));
            builder.AppendLine();
        }

        return builder.Length == "Time\tType\tStatus\tMessage\tDetail".Length + Environment.NewLine.Length
            ? string.Empty
            : builder.ToString();
    }

    private static string SanitizeForClipboard(string value)
        => value.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");

    private sealed record DebugEntryViewModel(
        string Timestamp,
        string Category,
        string Message,
        string Detail,
        string StatusText,
        Brush StatusBrush,
        string NetworkType,
        string HeadersText);
}