using System;
using System.Linq;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Threading;
using SelectorDemo.Wpf;
using AllItems.Automation.Browser.App.NodeInspector.ViewModels;

namespace AllItems.Automation.Browser.App.Views.NodeInspectors.Actions;

public partial class ClickElementInspectorView : UserControl
{
    private BrowserWindow? _browserWindow;

    public ClickElementInspectorView()
    {
        InitializeComponent();
    }

    private void SelectorModeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            SelectorValueTextBox.Focus();
            SelectorValueTextBox.SelectAll();
        }, DispatcherPriority.Input);
    }

    private void OpenBrowserWindow_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ClickElementInspectorViewModel viewModel)
        {
            return;
        }

        var defaultUrl = !string.IsNullOrWhiteSpace(viewModel.SelectedBrowserTargetUrl)
            ? viewModel.SelectedBrowserTargetUrl
            : viewModel.BrowserTargets.FirstOrDefault()?.Url ?? "https://example.com";

        var launch = ShowBrowserLaunchDialog(Window.GetWindow(this), defaultUrl);
        if (launch is null)
        {
            return;
        }

        var url = launch.Value.Url;
        var startWithDebug = launch.Value.StartWithDebug;

        if (_browserWindow is not null)
        {
            _browserWindow.Close();
        }

        var browserWindow = new BrowserWindow(url, startWithDebug, returnSelectionOnly: true);
        browserWindow.Owner = Window.GetWindow(this);
        browserWindow.ElementSelected += HandleElementSelected;
        browserWindow.Closed += BrowserWindow_OnClosed;

        _browserWindow = browserWindow;
        browserWindow.Show();
    }

    private void HandleElementSelected(BrowserElementSelection selection)
    {
        Dispatcher.Invoke(() =>
        {
            if (DataContext is not ClickElementInspectorViewModel viewModel)
            {
                return;
            }

            viewModel.ApplySelectedElement(selection);

            if (_browserWindow is not null)
            {
                _browserWindow.Close();
            }

            SelectorValueTextBox.Focus();
            SelectorValueTextBox.CaretIndex = SelectorValueTextBox.Text.Length;
        });
    }

    private static (string Url, bool StartWithDebug)? ShowBrowserLaunchDialog(Window? owner, string defaultUrl)
    {
        var urlTextBox = new TextBox
        {
            Width = 420,
            Text = defaultUrl,
            Margin = new Thickness(0, 6, 0, 10),
        };

        var debugCheckBox = new CheckBox
        {
            Content = "Start Browser Window in debug mode",
            IsChecked = false,
            Margin = new Thickness(0, 0, 0, 12),
        };

        var navigateButton = new Button
        {
            Content = "Navigate",
            Width = 90,
            IsDefault = true,
            Margin = new Thickness(0, 0, 8, 0),
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 90,
            IsCancel = true,
        };

        var dialog = new Window
        {
            Title = "Open Browser Window",
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            SizeToContent = SizeToContent.WidthAndHeight,
            MinWidth = 480,
            Content = new StackPanel
            {
                Margin = new Thickness(14),
                Children =
                {
                    new TextBlock { Text = "URL" },
                    urlTextBox,
                    debugCheckBox,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Children = { navigateButton, cancelButton },
                    },
                },
            },
        };

        if (owner is not null)
        {
            dialog.Owner = owner;
        }

        (string Url, bool StartWithDebug)? result = null;

        navigateButton.Click += (_, _) =>
        {
            var url = urlTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                MessageBox.Show(dialog, "Enter a valid absolute URL.", "Invalid URL", MessageBoxButton.OK, MessageBoxImage.Warning);
                urlTextBox.Focus();
                urlTextBox.SelectAll();
                return;
            }

            result = (url, debugCheckBox.IsChecked == true);
            dialog.DialogResult = true;
        };

        cancelButton.Click += (_, _) => dialog.DialogResult = false;

        var accepted = dialog.ShowDialog() == true;
        return accepted ? result : null;
    }

    private void BrowserWindow_OnClosed(object? sender, EventArgs e)
    {
        if (sender is not BrowserWindow browserWindow)
        {
            return;
        }

        browserWindow.ElementSelected -= HandleElementSelected;
        browserWindow.Closed -= BrowserWindow_OnClosed;

        if (ReferenceEquals(_browserWindow, browserWindow))
        {
            _browserWindow = null;
        }
    }
}
