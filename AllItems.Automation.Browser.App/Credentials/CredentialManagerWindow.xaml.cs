using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AllItems.Automation.Browser.App.Services.Diagnostics;

namespace AllItems.Automation.Browser.App.Credentials;

public partial class CredentialManagerWindow : Window
{
    private readonly CredentialManagerViewModel _viewModel;
    private bool _isSynchronizingPasswords;

    public CredentialManagerWindow(CredentialManagerViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        AppCrashLogger.Info("Credential manager window constructed.");

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.CloseRequested += OnCloseRequested;
        Loaded += OnLoaded;
        Closed += OnClosed;
        ContentRendered += OnContentRendered;
    }

    private async void OnLoaded(object sender, RoutedEventArgs eventArgs)
    {
        Loaded -= OnLoaded;
        AppCrashLogger.Info("Credential manager window loaded.");
        try
        {
            await _viewModel.InitializeAsync();
            AppCrashLogger.Info($"Credential manager initialized. CredentialCount={_viewModel.Credentials.Count}");
            SyncSecretBoxes();
        }
        catch (Exception exception)
        {
            AppCrashLogger.Error("Credential manager initialization failed.", exception);
            MessageBox.Show(
                this,
                "Unable to open credential manager because the credential store is not available. Unlock the store and try again.",
                "Credential Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            DialogResult = false;
            Close();
        }
    }

    private void OnClosed(object? sender, EventArgs eventArgs)
    {
        AppCrashLogger.Info("Credential manager window closed.");
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.CloseRequested -= OnCloseRequested;
        Closed -= OnClosed;
        ContentRendered -= OnContentRendered;
    }

    private void OnContentRendered(object? sender, EventArgs eventArgs)
    {
        EnsureWindowIsVisibleOnScreen();
        AppCrashLogger.Info($"Credential manager content rendered. Left={Left}, Top={Top}, Width={ActualWidth}, Height={ActualHeight}");

        WindowState = WindowState.Normal;
        Topmost = true;
        Activate();
        Focus();
        Topmost = false;
    }

    private void EnsureWindowIsVisibleOnScreen()
    {
        var windowWidth = ActualWidth > 0 ? ActualWidth : Width;
        var windowHeight = ActualHeight > 0 ? ActualHeight : Height;

        var virtualLeft = SystemParameters.VirtualScreenLeft;
        var virtualTop = SystemParameters.VirtualScreenTop;
        var virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
        var virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;

        var right = Left + windowWidth;
        var bottom = Top + windowHeight;
        var isOffScreen = right <= virtualLeft
            || Left >= virtualRight
            || bottom <= virtualTop
            || Top >= virtualBottom;

        if (!isOffScreen)
        {
            return;
        }

        Left = virtualLeft + Math.Max(0, (SystemParameters.VirtualScreenWidth - windowWidth) / 2);
        Top = virtualTop + Math.Max(0, (SystemParameters.VirtualScreenHeight - windowHeight) / 2);
        AppCrashLogger.Warn($"Credential manager window repositioned on-screen. Left={Left}, Top={Top}");
    }

    private void OnCloseRequested(bool accepted)
    {
        DialogResult = accepted;
        Close();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (!string.Equals(eventArgs.PropertyName, nameof(CredentialManagerViewModel.SelectedCredential), StringComparison.Ordinal))
        {
            return;
        }

        SyncSecretBoxes();
    }

    private void SyncSecretBoxes()
    {
        _isSynchronizingPasswords = true;
        try
        {
            var selected = _viewModel.SelectedCredential;
            var password = selected?.Password ?? string.Empty;
            UsernamePasswordValueBox.Password = password;
            EmailOtpPasswordBox.Password = password;
            SmsOtpPasswordBox.Password = password;
            TotpPasswordBox.Password = password;
            OAuthPasswordBox.Password = password;
            HttpBasicPasswordBox.Password = password;

            ImapPasswordBox.Password = selected?.ImapPassword ?? string.Empty;
            TotpSecretBox.Password = selected?.TotpSecret ?? string.Empty;
            ApiTokenBox.Password = selected?.Token ?? string.Empty;
            CertificatePasswordBox.Password = selected?.CertificatePassword ?? string.Empty;
        }
        finally
        {
            _isSynchronizingPasswords = false;
        }
    }

    private void SecretPasswordChanged(object sender, RoutedEventArgs eventArgs)
    {
        if (_isSynchronizingPasswords || sender is not PasswordBox passwordBox)
        {
            return;
        }

        var selected = _viewModel.SelectedCredential;
        if (selected is null)
        {
            return;
        }

        var key = passwordBox.Tag as string;
        var value = passwordBox.Password;

        switch (key)
        {
            case "Password":
                selected.Password = value;
                break;
            case "ImapPassword":
                selected.ImapPassword = value;
                break;
            case "TotpSecret":
                selected.TotpSecret = value;
                break;
            case "Token":
                selected.Token = value;
                break;
            case "CertificatePassword":
                selected.CertificatePassword = value;
                break;
        }
    }

    private void CredentialListBoxMouseDoubleClick(object sender, MouseButtonEventArgs eventArgs)
    {
        if (eventArgs.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (_viewModel.SelectCommand.CanExecute(null))
        {
            _viewModel.SelectCommand.Execute(null);
        }
    }
}
