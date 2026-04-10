using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WpfAutomation.App.Services.Diagnostics;
using WpfAutomation.App.Commands;
using WpfAutomation.App.Services.Credentials;

namespace WpfAutomation.App.Credentials;

public sealed class CredentialManagerViewModel : INotifyPropertyChanged
{
    private readonly ICredentialStore _credentialStore;
    private readonly Guid? _preselectedCredentialId;
    private readonly bool _startWithNewCredential;
    private CredentialEntryViewModel? _selectedCredential;
    private string? _validationMessage;

    public CredentialManagerViewModel(ICredentialStore credentialStore, Guid? preselectedCredentialId = null, bool startWithNewCredential = false)
    {
        _credentialStore = credentialStore;
        _preselectedCredentialId = preselectedCredentialId;
        _startWithNewCredential = startWithNewCredential;

        Credentials = [];

        NewCommand = new RelayCommand(AddNew);
        DeleteCommand = new AsyncRelayCommand(_ => DeleteSelectedAsync(), () => SelectedCredential is not null);
        SelectCommand = new RelayCommand(SelectAndClose, () => SelectedCredential is not null);
        SaveCommand = new AsyncRelayCommand(_ => SaveAndCloseAsync());
        CancelCommand = new RelayCommand(Cancel);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event Action<bool>? CloseRequested;

    public ObservableCollection<CredentialEntryViewModel> Credentials { get; }

    public CredentialEntryViewModel? SelectedCredential
    {
        get => _selectedCredential;
        set
        {
            if (ReferenceEquals(_selectedCredential, value))
            {
                return;
            }

            _selectedCredential = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelection));
            RaiseCanExecuteChanged();
        }
    }

    public bool HasSelection => SelectedCredential is not null;

    public string? ValidationMessage
    {
        get => _validationMessage;
        private set
        {
            if (string.Equals(_validationMessage, value, StringComparison.Ordinal))
            {
                return;
            }

            _validationMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasValidationMessage));
        }
    }

    public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);

    public Guid? SelectedCredentialId { get; private set; }

    public string? SelectedCredentialName { get; private set; }

    public ICommand NewCommand { get; }

    public ICommand DeleteCommand { get; }

    public ICommand SelectCommand { get; }

    public ICommand SaveCommand { get; }

    public ICommand CancelCommand { get; }

    public async Task InitializeAsync()
    {
        var entries = await _credentialStore.LoadAllAsync().ConfigureAwait(false);
        var mapped = entries
            .Select(CredentialEntryViewModel.FromModel)
            .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Credentials.Clear();
        foreach (var entry in mapped)
        {
            Credentials.Add(entry);
        }

        if (_startWithNewCredential)
        {
            AddNew();
            return;
        }

        if (Credentials.Count == 0)
        {
            SelectedCredential = null;
            return;
        }

        SelectedCredential = _preselectedCredentialId.HasValue
            ? Credentials.FirstOrDefault(entry => entry.Id == _preselectedCredentialId.Value)
            : Credentials[0];

        SelectedCredential ??= Credentials[0];
    }

    private void AddNew()
    {
        ValidationMessage = null;
        var created = CredentialEntryViewModel.CreateNew();
        Credentials.Add(created);
        SelectedCredential = created;
    }

    private async Task DeleteSelectedAsync()
    {
        if (SelectedCredential is null)
        {
            return;
        }

        var selected = SelectedCredential;
        AppCrashLogger.Info($"Credential delete requested. Id={selected.Id}, Name={selected.Name}");
        await _credentialStore.DeleteAsync(selected.Id);
        AppCrashLogger.Info($"Credential delete persisted. Id={selected.Id}");
        Credentials.Remove(selected);

        if (Credentials.Count == 0)
        {
            SelectedCredential = null;
            AppCrashLogger.Info("Credential delete completed; collection is empty.");
            return;
        }

        SelectedCredential = Credentials[0];
        AppCrashLogger.Info($"Credential delete completed. NewSelectedId={SelectedCredential.Id}, RemainingCount={Credentials.Count}");
    }

    private async Task SaveAndCloseAsync()
    {
        ValidationMessage = null;

        if (SelectedCredential is null)
        {
            ValidationMessage = "Select a credential to save.";
            return;
        }

        var validationErrors = SelectedCredential.Validate();
        if (validationErrors.Count > 0)
        {
            ValidationMessage = string.Join(Environment.NewLine, validationErrors);
            return;
        }

        AppCrashLogger.Info($"Credential save requested. Id={SelectedCredential.Id}, Name={SelectedCredential.Name}");
        await _credentialStore.SaveAsync(SelectedCredential.ToModel());
        AppCrashLogger.Info($"Credential save completed. Id={SelectedCredential.Id}");

        AcceptCurrentSelection();
    }

    private void SelectAndClose()
    {
        if (SelectedCredential is null)
        {
            ValidationMessage = "Select a credential.";
            return;
        }

        ValidationMessage = null;
        AppCrashLogger.Info($"Credential select requested. Id={SelectedCredential.Id}, Name={SelectedCredential.Name}");
        AcceptCurrentSelection();
    }

    private void AcceptCurrentSelection()
    {
        if (SelectedCredential is null)
        {
            return;
        }

        SelectedCredentialId = SelectedCredential.Id;
        SelectedCredentialName = string.IsNullOrWhiteSpace(SelectedCredential.Name)
            ? SelectedCredential.DisplayName
            : SelectedCredential.Name.Trim();

        CloseRequested?.Invoke(true);
    }

    private void Cancel()
    {
        CloseRequested?.Invoke(false);
    }

    private void RaiseCanExecuteChanged()
    {
        (DeleteCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DeleteCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (SelectCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SelectCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
