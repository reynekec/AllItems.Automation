using System.Windows;
using WpfAutomation.App.ViewModels;

namespace WpfAutomation.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs eventArgs)
    {
        Loaded -= OnLoaded;
        await _viewModel.InitializeDockingAsync();

        if (!_viewModel.HasRestorableDockPanels())
        {
            MainDockHost.ResetLayoutToDefault();
        }
    }
}