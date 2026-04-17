using System.Windows;
using AllItems.Automation.Browser.App.ViewModels;
using AllItems.Automation.Browser.App.Views;

namespace AllItems.Automation.Browser.App;

public partial class TestDockWindow : Window
{
    private readonly TestDockViewModel _viewModel;

    public TestDockWindow(TestDockViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs eventArgs)
    {
        Loaded -= OnLoaded;
        await _viewModel.InitializeAsync();

        if (!_viewModel.HasRestorablePanels())
        {
            DockHost.ResetLayoutToDefault();
        }
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        var settings = new SettingsWindow { Owner = this };
        settings.ShowDialog();
    }
}
