using System.Windows;
using WpfAutomation.App.ViewModels;

namespace WpfAutomation.App;

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
}
