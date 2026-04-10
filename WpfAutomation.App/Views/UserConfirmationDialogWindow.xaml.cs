using System.Windows;

namespace WpfAutomation.App.Views;

public partial class UserConfirmationDialogWindow : Window
{
    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message),
        typeof(string),
        typeof(UserConfirmationDialogWindow),
        new PropertyMetadata("Click Continue to proceed with the automation flow."));

    public UserConfirmationDialogWindow()
    {
        InitializeComponent();
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    private void ContinueButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
