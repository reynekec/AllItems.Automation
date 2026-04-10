using System.Windows;
using System.Windows.Controls;
using WpfAutomation.App.NodeInspector.ViewModels;

namespace WpfAutomation.App.Views.NodeInspectors.Actions;

public partial class NavigateToUrlInspectorView : UserControl
{
    public NavigateToUrlInspectorView()
    {
        InitializeComponent();
    }

    private void CredentialPickerComboBoxGotKeyboardFocus(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is not ComboBox comboBox || !comboBox.IsEnabled)
        {
            return;
        }

        if (DataContext is NavigateToUrlInspectorViewModel viewModel)
        {
            viewModel.CredentialSearchText = string.Empty;
        }

        // Wait until focus routing completes, then open the full list like F4.
        Dispatcher.BeginInvoke(() => comboBox.IsDropDownOpen = true, System.Windows.Threading.DispatcherPriority.Input);
    }
}
