using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace AllItems.Automation.Browser.App.Credentials;

public partial class OtpInputWindow : Window, INotifyPropertyChanged
{
    private string _otpCode = string.Empty;
    private string _errorMessage = string.Empty;

    public OtpInputWindow(string promptTarget)
    {
        InitializeComponent();
        PromptTarget = string.IsNullOrWhiteSpace(promptTarget) ? "your account" : promptTarget;
        DataContext = this;
        Loaded += OnLoaded;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string PromptTarget { get; }

    public string OtpCode => _otpCode;

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (string.Equals(_errorMessage, value, StringComparison.Ordinal))
            {
                return;
            }

            _errorMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    private void OnLoaded(object sender, RoutedEventArgs eventArgs)
    {
        Loaded -= OnLoaded;
        OtpCodeTextBox.Focus();
    }

    private void OkButton_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (string.IsNullOrWhiteSpace(OtpCodeTextBox.Text))
        {
            ErrorMessage = "Enter a verification code.";
            OtpCodeTextBox.Focus();
            return;
        }

        _otpCode = OtpCodeTextBox.Text.Trim();
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs eventArgs)
    {
        DialogResult = false;
        Close();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
