using System.Windows;

namespace WpfAutomation.App.Credentials;

public partial class CredentialUnlockWindow : Window
{
    private readonly CredentialUnlockViewModel _viewModel;

    public CredentialUnlockWindow(CredentialUnlockViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs eventArgs)
    {
        Loaded -= OnLoaded;
        MasterPasswordBox.Focus();
    }

    private void OkButton_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (_viewModel.TryUnlock(MasterPasswordBox.SecurePassword.Copy()))
        {
            DialogResult = true;
            Close();
            return;
        }

        MasterPasswordBox.Clear();
        MasterPasswordBox.Focus();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs eventArgs)
    {
        DialogResult = false;
        Close();
    }
}
