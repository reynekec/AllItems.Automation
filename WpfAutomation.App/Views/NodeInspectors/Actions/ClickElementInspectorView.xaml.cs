using System.Windows.Controls;
using System.Windows.Threading;

namespace WpfAutomation.App.Views.NodeInspectors.Actions;

public partial class ClickElementInspectorView : UserControl
{
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
}
