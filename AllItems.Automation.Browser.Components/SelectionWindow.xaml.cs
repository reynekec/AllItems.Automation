using System.Windows;

namespace SelectorDemo.Wpf;

/// <summary>
/// Interaction logic for SelectionWindow.xaml
/// </summary>
public partial class SelectionWindow : Window
{
    public SelectionWindow()
    {
        InitializeComponent();
    }

    public void SetStatus(string status)
    {
        StatusTextBox.Text = status;
    }

    public void SetSelectors(string cssSelector, string xpathSelector)
    {
        CssSelectorTextBox.Text = cssSelector;
        XPathSelectorTextBox.Text = xpathSelector;
    }

    public void ClearSelectors()
    {
        CssSelectorTextBox.Text = string.Empty;
        XPathSelectorTextBox.Text = string.Empty;
    }
}
