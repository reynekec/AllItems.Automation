using System.Windows.Controls;
using System.Windows.Threading;

namespace AllItems.Automation.Browser.App.Views.NodeInspectors.Actions;

public partial class FillInputInspectorView
{
    public FillInputInspectorView()
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
