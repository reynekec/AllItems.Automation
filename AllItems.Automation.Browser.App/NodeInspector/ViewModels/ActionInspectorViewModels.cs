using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Input;
using AllItems.Automation.Browser.App.Commands;
using AllItems.Automation.Browser.App.Credentials;
using AllItems.Automation.Browser.App.Credentials.Models;
using AllItems.Automation.Browser.App.Models.Flow;
using AllItems.Automation.Browser.App.NodeInspector.Contracts;
using AllItems.Automation.Browser.App.Services.Credentials;
using AllItems.Automation.Browser.App.Services.Diagnostics;

namespace AllItems.Automation.Browser.App.NodeInspector.ViewModels;

public enum InspectorFieldKind
{
    Text = 0,
    Number = 1,
    Toggle = 2,
    Choice = 3,
}

public sealed class InspectorFieldViewModel : INotifyPropertyChanged
{
    private readonly Action _onValueChanged;
    private string _stringValue;
    private bool _boolValue;
    private string _selectedChoice;
    private bool _isVisible;

    public InspectorFieldViewModel(
        string name,
        string label,
        InspectorFieldKind kind,
        string stringValue,
        bool boolValue,
        IReadOnlyList<string> choices,
        Action onValueChanged)
    {
        Name = name;
        Label = label;
        Kind = kind;
        _stringValue = stringValue;
        _boolValue = boolValue;
        Choices = choices;
        _selectedChoice = stringValue;
        _isVisible = true;
        _onValueChanged = onValueChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name { get; }

    public string Label { get; }

    public InspectorFieldKind Kind { get; }

    public IReadOnlyList<string> Choices { get; }

    public string StringValue
    {
        get => _stringValue;
        set
        {
            if (string.Equals(_stringValue, value, StringComparison.Ordinal))
            {
                return;
            }

            _stringValue = value;
            if (Kind == InspectorFieldKind.Choice)
            {
                _selectedChoice = value;
                OnPropertyChanged(nameof(SelectedChoice));
            }

            OnPropertyChanged();
            _onValueChanged();
        }
    }

    public bool BoolValue
    {
        get => _boolValue;
        set
        {
            if (_boolValue == value)
            {
                return;
            }

            _boolValue = value;
            OnPropertyChanged();
            _onValueChanged();
        }
    }

    public string SelectedChoice
    {
        get => _selectedChoice;
        set
        {
            if (string.Equals(_selectedChoice, value, StringComparison.Ordinal))
            {
                return;
            }

            _selectedChoice = value;
            _stringValue = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StringValue));
            _onValueChanged();
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible == value)
            {
                return;
            }

            _isVisible = value;
            OnPropertyChanged();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public abstract class JsonActionInspectorViewModelBase<TParameters> : IJsonNodeInspectorViewModel
    where TParameters : ActionParameters
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly Action<ActionParameters> _commit;
    private readonly TParameters _defaultParameters;
    private bool _suppressFieldCommit;
    private string _parametersJson;
    private string _lastCommittedJson;
    private bool _isDirty;
    private bool _hasValidationErrors;
    private IReadOnlyList<string> _validationErrors = [];

    protected JsonActionInspectorViewModelBase(
        string title,
        string categoryName,
        string hintText,
        string? warningText,
        TParameters currentParameters,
        TParameters defaultParameters,
        Action<ActionParameters> commit)
    {
        Title = title;
        CategoryName = categoryName;
        HintText = hintText;
        WarningText = warningText;
        _commit = commit;
        _defaultParameters = defaultParameters;
        Fields = [];

        _parametersJson = Serialize(currentParameters);
        _lastCommittedJson = _parametersJson;
        ResetToDefaultsCommand = new RelayCommand(_ => ApplyParameters(_defaultParameters, shouldCommit: true));

        ApplyParameters(currentParameters, shouldCommit: false);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title { get; }

    public string CategoryName { get; }

    public string HintText { get; }

    public string? WarningText { get; }

    public ObservableCollection<InspectorFieldViewModel> Fields { get; }

    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (_isDirty == value)
            {
                return;
            }

            _isDirty = value;
            OnPropertyChanged();
        }
    }

    public bool HasValidationErrors
    {
        get => _hasValidationErrors;
        private set
        {
            if (_hasValidationErrors == value)
            {
                return;
            }

            _hasValidationErrors = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<string> ValidationErrors
    {
        get => _validationErrors;
        private set
        {
            _validationErrors = value;
            OnPropertyChanged();
        }
    }

    public ICommand ResetToDefaultsCommand { get; }

    // Backward compatibility for existing tests and diagnostics usage.
    public string ParametersJson
    {
        get => _parametersJson;
        set
        {
            if (string.Equals(_parametersJson, value, StringComparison.Ordinal))
            {
                return;
            }

            if (!TryDeserialize(value, out var parsed, out var parseError))
            {
                SetValidation([parseError]);
                IsDirty = true;
                return;
            }

            ApplyParameters(parsed!, shouldCommit: true);
        }
    }

    protected virtual IReadOnlyList<string> Validate(TParameters parameters)
    {
        var errors = new List<string>();

        foreach (var property in typeof(TParameters).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.PropertyType == typeof(string) && IsRequiredStringField(property.Name))
            {
                var value = property.GetValue(parameters) as string;
                if (string.IsNullOrWhiteSpace(value))
                {
                    errors.Add($"{ToLabel(property.Name)} is required.");
                }
            }

            if (property.PropertyType == typeof(int) && property.Name.Contains("Timeout", StringComparison.OrdinalIgnoreCase))
            {
                var timeout = (int)(property.GetValue(parameters) ?? 0);
                if (timeout <= 0)
                {
                    errors.Add($"{ToLabel(property.Name)} must be greater than zero.");
                }
            }
        }

        return errors;
    }

    private void ApplyParameters(TParameters parameters, bool shouldCommit)
    {
        _suppressFieldCommit = true;
        Fields.Clear();

        foreach (var property in typeof(TParameters).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var field = CreateField(property, parameters);
            Fields.Add(field);
        }

        _suppressFieldCommit = false;

        _parametersJson = Serialize(parameters);
        OnPropertyChanged(nameof(ParametersJson));

        var errors = Validate(parameters);
        SetValidation(errors);

        if (shouldCommit && errors.Count == 0)
        {
            _commit(parameters);
            _lastCommittedJson = _parametersJson;
            IsDirty = false;
            return;
        }

        IsDirty = !string.Equals(_parametersJson, _lastCommittedJson, StringComparison.Ordinal);
    }

    private InspectorFieldViewModel CreateField(PropertyInfo property, TParameters parameters)
    {
        var value = property.GetValue(parameters);

        if (property.PropertyType == typeof(bool))
        {
            return new InspectorFieldViewModel(
                property.Name,
                ToLabel(property.Name),
                InspectorFieldKind.Toggle,
                string.Empty,
                (bool)(value ?? false),
                [],
                OnFieldChanged);
        }

        if (property.PropertyType == typeof(int))
        {
            return new InspectorFieldViewModel(
                property.Name,
                ToLabel(property.Name),
                InspectorFieldKind.Number,
                ((int)(value ?? 0)).ToString(),
                false,
                [],
                OnFieldChanged);
        }

        var choices = ResolveChoices(property.Name);
        return new InspectorFieldViewModel(
            property.Name,
            ToLabel(property.Name),
            choices.Count > 0 ? InspectorFieldKind.Choice : InspectorFieldKind.Text,
            value as string ?? string.Empty,
            false,
            choices,
            OnFieldChanged);
    }

    private void OnFieldChanged()
    {
        if (_suppressFieldCommit)
        {
            return;
        }

        if (!TryBuildParametersFromFields(out var parameters, out var conversionError))
        {
            SetValidation([conversionError]);
            IsDirty = true;
            return;
        }

        var errors = Validate(parameters!);
        SetValidation(errors);

        _parametersJson = Serialize(parameters!);
        OnPropertyChanged(nameof(ParametersJson));

        if (errors.Count > 0)
        {
            IsDirty = true;
            return;
        }

        _commit(parameters!);
        _lastCommittedJson = _parametersJson;
        IsDirty = false;
    }

    private bool TryBuildParametersFromFields(out TParameters? parameters, out string error)
    {
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var field in Fields)
        {
            var property = typeof(TParameters).GetProperty(field.Name, BindingFlags.Public | BindingFlags.Instance);
            if (property is null)
            {
                continue;
            }

            if (property.PropertyType == typeof(bool))
            {
                payload[field.Name] = field.BoolValue;
                continue;
            }

            if (property.PropertyType == typeof(int))
            {
                if (!int.TryParse(field.StringValue, out var numberValue))
                {
                    parameters = null;
                    error = $"{field.Label} must be a whole number.";
                    return false;
                }

                payload[field.Name] = numberValue;
                continue;
            }

            payload[field.Name] = field.StringValue;
        }

        try
        {
            var json = JsonSerializer.Serialize(payload, SerializerOptions);
            parameters = JsonSerializer.Deserialize<TParameters>(json, SerializerOptions);
            if (parameters is null)
            {
                error = "Unable to create parameter object from editor values.";
                return false;
            }

            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            parameters = null;
            error = $"Invalid parameter values: {exception.Message}";
            return false;
        }
    }

    private static IReadOnlyList<string> ResolveChoices(string propertyName)
    {
        if (string.Equals(propertyName, "BrowserEngine", StringComparison.Ordinal))
        {
            return ["chromium", "firefox", "webkit"];
        }

        return [];
    }

    private static bool IsRequiredStringField(string propertyName)
    {
        return propertyName switch
        {
            "Selector" => true,
            "Url" => true,
            "ExpectedText" => true,
            "OptionValue" => true,
            "Key" => true,
            _ => false,
        };
    }

    private static bool TryDeserialize(string json, out TParameters? parameters, out string error)
    {
        try
        {
            parameters = JsonSerializer.Deserialize<TParameters>(json, SerializerOptions);
            if (parameters is null)
            {
                error = "Parameters JSON did not produce a valid value.";
                return false;
            }

            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            parameters = null;
            error = $"Invalid JSON: {exception.Message}";
            return false;
        }
    }

    private static string Serialize(TParameters parameters)
    {
        return JsonSerializer.Serialize(parameters, SerializerOptions);
    }

    private static string ToLabel(string propertyName)
    {
        return string.Concat(propertyName.Select((character, index) =>
            index > 0 && char.IsUpper(character) ? $" {character}" : character.ToString()));
    }

    private void SetValidation(IReadOnlyList<string> errors)
    {
        ValidationErrors = errors;
        HasValidationErrors = errors.Count > 0;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class OpenBrowserInspectorViewModel : JsonActionInspectorViewModelBase<OpenBrowserActionParameters>
{
    public OpenBrowserInspectorViewModel(OpenBrowserActionParameters current, OpenBrowserActionParameters defaults, Action<ActionParameters> commit)
        : base("Open browser", "Browser", "Configure browser startup behavior.", null, current, defaults, commit) { }
}

public sealed class NewPageInspectorViewModel : JsonActionInspectorViewModelBase<NewPageActionParameters>
{
    public NewPageInspectorViewModel(NewPageActionParameters current, NewPageActionParameters defaults, Action<ActionParameters> commit)
        : base("New page", "Browser", "Configure page bootstrap options.", null, current, defaults, commit) { }
}

public sealed class CloseBrowserInspectorViewModel : JsonActionInspectorViewModelBase<CloseBrowserActionParameters>
{
    public CloseBrowserInspectorViewModel(CloseBrowserActionParameters current, CloseBrowserActionParameters defaults, Action<ActionParameters> commit)
        : base("Close browser", "Browser", "Control browser shutdown behavior.", "Closing all pages may interrupt pending actions.", current, defaults, commit) { }
}

public sealed class NavigateToUrlInspectorViewModel : JsonActionInspectorViewModelBase<NavigateToUrlActionParameters>
{
    private static readonly IReadOnlyList<string> HiddenFieldNames =
    [
        "CredentialId",
        "CredentialName",
    ];

    private readonly ICredentialManagerDialogService _credentialManagerDialogService;
    private readonly ICredentialStore? _credentialStore;
    private readonly ObservableCollection<CredentialPickerItem> _credentialOptions = [];
    private readonly ObservableCollection<CredentialPickerItem> _filteredCredentialOptions = [];
    private CredentialPickerItem? _selectedCredentialOption;
    private string _credentialSearchText = string.Empty;
    private bool _isSynchronizingCredentialSelection;
    private bool _isCredentialPickerEnabled;
    private string? _credentialPickerStateMessage;
    private string? _credentialWarningMessage;
    private int _credentialWarningRefreshVersion;
    private InspectorFieldViewModel? _enableAuthenticationField;

    public NavigateToUrlInspectorViewModel(
        NavigateToUrlActionParameters current,
        NavigateToUrlActionParameters defaults,
        Action<ActionParameters> commit,
        ICredentialManagerDialogService? credentialManagerDialogService = null,
        ICredentialStore? credentialStore = null)
        : base("Navigate to URL", "Target", "Set the destination and timeout settings.", null, current, defaults, commit)
    {
        _credentialManagerDialogService = credentialManagerDialogService ?? new NullCredentialManagerDialogService();
        _credentialStore = credentialStore;
        OpenCredentialManagerCommand = new RelayCommand(OpenCredentialManager);
        ClearCredentialCommand = new RelayCommand(ClearCredential);
        _isCredentialPickerEnabled = TryIsCredentialStoreUnlocked(_credentialStore);

        foreach (var hiddenFieldName in HiddenFieldNames)
        {
            var field = FindOptionalField(hiddenFieldName);
            if (field is not null)
            {
                field.IsVisible = false;
            }
        }

        _enableAuthenticationField = FindOptionalField(nameof(NavigateToUrlActionParameters.EnableAuthentication));
        if (_enableAuthenticationField is not null)
        {
            _enableAuthenticationField.PropertyChanged += EnableAuthenticationFieldPropertyChanged;
        }

        AppCrashLogger.Info("NavigateToUrl inspector created.");
        RefreshCredentialWarning();
        _ = LoadCredentialOptionsAsync();
    }

    public bool IsAuthenticationSectionVisible => _enableAuthenticationField?.BoolValue == true;

    public ICommand OpenCredentialManagerCommand { get; }

    public ICommand ClearCredentialCommand { get; }

    public IReadOnlyList<CredentialPickerItem> FilteredCredentialOptions => _filteredCredentialOptions;

    public CredentialPickerItem? SelectedCredentialOption
    {
        get => _selectedCredentialOption;
        set
        {
            if (ReferenceEquals(_selectedCredentialOption, value))
            {
                return;
            }

            _selectedCredentialOption = value;
            OnPropertyChanged();

            if (_isSynchronizingCredentialSelection || value is null)
            {
                return;
            }

            AssignCredentialSelection(value.Id, value.Name);
            AppCrashLogger.Info($"NavigateToUrl inspector: credential selected from picker. Id={value.Id}");
            RefreshCredentialWarning();
        }
    }

    public string CredentialSearchText
    {
        get => _credentialSearchText;
        set
        {
            var next = value;
            if (string.Equals(_credentialSearchText, next, StringComparison.Ordinal))
            {
                return;
            }

            _credentialSearchText = next;
            OnPropertyChanged();
            RebuildFilteredCredentialOptions();
        }
    }

    public bool IsCredentialPickerEnabled
    {
        get => _isCredentialPickerEnabled;
        private set
        {
            if (_isCredentialPickerEnabled == value)
            {
                return;
            }

            _isCredentialPickerEnabled = value;
            OnPropertyChanged();
        }
    }

    public string? CredentialPickerStateMessage
    {
        get => _credentialPickerStateMessage;
        private set
        {
            if (string.Equals(_credentialPickerStateMessage, value, StringComparison.Ordinal))
            {
                return;
            }

            _credentialPickerStateMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasCredentialPickerStateMessage));
        }
    }

    public bool HasCredentialPickerStateMessage => !string.IsNullOrWhiteSpace(CredentialPickerStateMessage);

    public bool HasNoCredentialMatches => _credentialOptions.Count > 0 && _filteredCredentialOptions.Count == 0;

    public string? CredentialWarningMessage
    {
        get => _credentialWarningMessage;
        private set
        {
            if (string.Equals(_credentialWarningMessage, value, StringComparison.Ordinal))
            {
                return;
            }

            _credentialWarningMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasCredentialWarning));
        }
    }

    public bool HasCredentialWarning => !string.IsNullOrWhiteSpace(CredentialWarningMessage);

    public string CredentialName
    {
        get
        {
            var field = FindOptionalField("CredentialName");
            if (field is null || string.IsNullOrWhiteSpace(field.StringValue))
            {
                return "None";
            }

            return field.StringValue;
        }
    }

    private void OpenCredentialManager()
    {
        AppCrashLogger.Info("NavigateToUrl inspector: open credential manager requested.");
        var existingSelectionId = GetSelectedCredentialId();
        var result = _credentialManagerDialogService.ShowDialog(selectedCredentialId: existingSelectionId, startWithNewCredential: true);

        if (result.Accepted && result.SelectedCredentialId.HasValue && !string.IsNullOrWhiteSpace(result.SelectedCredentialName))
        {
            AssignCredentialSelection(result.SelectedCredentialId.Value, result.SelectedCredentialName);
            AppCrashLogger.Info("NavigateToUrl inspector: credential assigned.");
            RefreshCredentialWarning();
            _ = LoadCredentialOptionsAsync(result.SelectedCredentialId.Value);
            return;
        }

        AppCrashLogger.Info("NavigateToUrl inspector: credential selection canceled.");
        _ = LoadCredentialOptionsAsync(existingSelectionId);
    }

    private void ClearCredential()
    {
        AppCrashLogger.Info("NavigateToUrl inspector: clear credential requested.");
        var credentialIdField = FindOptionalField("CredentialId");
        var credentialNameField = FindOptionalField("CredentialName");
        if (credentialIdField is null || credentialNameField is null)
        {
            return;
        }

        credentialIdField.StringValue = string.Empty;
        credentialNameField.StringValue = string.Empty;
        OnPropertyChanged(nameof(CredentialName));
        SetSelectedCredentialOption(null);
        RefreshCredentialWarning();
    }

    private async Task LoadCredentialOptionsAsync(Guid? preferredCredentialId = null)
    {
        if (_credentialStore is null)
        {
            IsCredentialPickerEnabled = false;
            CredentialPickerStateMessage = "Credential store is unavailable.";
            ReplaceCredentialOptions([]);
            return;
        }

        try
        {
            if (!_credentialStore.IsUnlocked)
            {
                IsCredentialPickerEnabled = false;
                CredentialPickerStateMessage = "Unlock credentials to search and select saved credentials.";
                ReplaceCredentialOptions([]);
                return;
            }

            var entries = await _credentialStore.LoadAllAsync();
            var mapped = entries
                .Select(MapCredentialPickerItem)
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            IsCredentialPickerEnabled = true;
            CredentialPickerStateMessage = null;
            ReplaceCredentialOptions(mapped);

            var selectedId = preferredCredentialId ?? GetSelectedCredentialId();
            SetSelectedCredentialOptionById(selectedId);
        }
        catch (Exception exception)
        {
            AppCrashLogger.Error("NavigateToUrl inspector: failed to load credential picker options.", exception);
            IsCredentialPickerEnabled = false;
            CredentialPickerStateMessage = "Unable to load credentials right now.";
            ReplaceCredentialOptions([]);
        }
    }

    private static bool TryIsCredentialStoreUnlocked(ICredentialStore? credentialStore)
    {
        if (credentialStore is null)
        {
            return false;
        }

        try
        {
            return credentialStore.IsUnlocked;
        }
        catch (Exception exception)
        {
            AppCrashLogger.Error("NavigateToUrl inspector: failed to read credential store state.", exception);
            return false;
        }
    }

    private void ReplaceCredentialOptions(IEnumerable<CredentialPickerItem> items)
    {
        _credentialOptions.Clear();
        foreach (var item in items)
        {
            _credentialOptions.Add(item);
        }

        RebuildFilteredCredentialOptions();
    }

    private void RebuildFilteredCredentialOptions()
    {
        var search = _credentialSearchText.Trim();

        _filteredCredentialOptions.Clear();
        foreach (var option in _credentialOptions)
        {
            if (search.Length == 0 || option.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                _filteredCredentialOptions.Add(option);
            }
        }

        OnPropertyChanged(nameof(FilteredCredentialOptions));
        OnPropertyChanged(nameof(HasNoCredentialMatches));
    }

    private void SetSelectedCredentialOptionById(Guid? credentialId)
    {
        CredentialPickerItem? selected = null;
        if (credentialId.HasValue)
        {
            selected = _credentialOptions.FirstOrDefault(option => option.Id == credentialId.Value);
        }

        SetSelectedCredentialOption(selected);
    }

    private void SetSelectedCredentialOption(CredentialPickerItem? option)
    {
        _isSynchronizingCredentialSelection = true;
        try
        {
            SelectedCredentialOption = option;
        }
        finally
        {
            _isSynchronizingCredentialSelection = false;
        }
    }

    private Guid? GetSelectedCredentialId()
    {
        var credentialIdField = FindOptionalField("CredentialId");
        if (credentialIdField is null || string.IsNullOrWhiteSpace(credentialIdField.StringValue))
        {
            return null;
        }

        return Guid.TryParse(credentialIdField.StringValue, out var parsed) ? parsed : null;
    }

    private void AssignCredentialSelection(Guid credentialId, string credentialName)
    {
        var credentialIdField = FindOptionalField("CredentialId");
        var credentialNameField = FindOptionalField("CredentialName");
        if (credentialIdField is null || credentialNameField is null)
        {
            return;
        }

        credentialIdField.StringValue = credentialId.ToString();
        credentialNameField.StringValue = credentialName;
        OnPropertyChanged(nameof(CredentialName));
    }

    private static CredentialPickerItem MapCredentialPickerItem(CredentialEntry entry)
    {
        var name = string.IsNullOrWhiteSpace(entry.Name) ? "(Unnamed credential)" : entry.Name.Trim();
        var authType = entry switch
        {
            WebCredentialEntry web => WebAuthKindDisplayConverter.GetDisplayName(web.WebAuthKind),
            _ => entry.AuthType.ToString(),
        };

        return new CredentialPickerItem(entry.Id, name, authType, $"{name} • {authType}");
    }

    private void RefreshCredentialWarning()
    {
        var refreshVersion = Interlocked.Increment(ref _credentialWarningRefreshVersion);
        _ = RefreshCredentialWarningAsync(refreshVersion);
    }

    private async Task RefreshCredentialWarningAsync(int refreshVersion)
    {
        if (!IsAuthenticationSectionVisible)
        {
            ApplyCredentialWarningMessage(refreshVersion, null);
            return;
        }

        var credentialIdField = FindOptionalField("CredentialId");
        if (credentialIdField is null || string.IsNullOrWhiteSpace(credentialIdField.StringValue))
        {
            ApplyCredentialWarningMessage(refreshVersion, null);
            return;
        }

        if (!Guid.TryParse(credentialIdField.StringValue, out var credentialId))
        {
            ApplyCredentialWarningMessage(refreshVersion, "Credential reference is invalid. Please re-select.");
            return;
        }

        try
        {
            if (_credentialStore is null || !_credentialStore.IsUnlocked)
            {
                ApplyCredentialWarningMessage(refreshVersion, null);
                return;
            }

            var existing = await _credentialStore.GetByIdAsync(credentialId);
            ApplyCredentialWarningMessage(
                refreshVersion,
                existing is null
                ? "Credential not found. Please re-select."
                : null);
        }
        catch (Exception exception)
        {
            AppCrashLogger.Error("NavigateToUrl inspector: credential warning refresh failed.", exception);
            ApplyCredentialWarningMessage(refreshVersion, null);
        }
    }

    private void ApplyCredentialWarningMessage(int refreshVersion, string? message)
    {
        if (refreshVersion != Volatile.Read(ref _credentialWarningRefreshVersion))
        {
            return;
        }

        CredentialWarningMessage = message;
    }

    private void EnableAuthenticationFieldPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (!string.Equals(eventArgs.PropertyName, nameof(InspectorFieldViewModel.BoolValue), StringComparison.Ordinal))
        {
            return;
        }

        OnPropertyChanged(nameof(IsAuthenticationSectionVisible));
        RefreshCredentialWarning();
    }

    private InspectorFieldViewModel? FindOptionalField(string name)
    {
        return Fields.FirstOrDefault(field => string.Equals(field.Name, name, StringComparison.Ordinal));
    }

    public sealed record CredentialPickerItem(Guid Id, string Name, string AuthType, string DisplayText);
}

public sealed class GoBackInspectorViewModel : JsonActionInspectorViewModelBase<GoBackActionParameters>
{
    public GoBackInspectorViewModel(GoBackActionParameters current, GoBackActionParameters defaults, Action<ActionParameters> commit)
        : base("Go back", "Navigation", "Set timeout behavior for back navigation.", null, current, defaults, commit) { }
}

public sealed class GoForwardInspectorViewModel : JsonActionInspectorViewModelBase<GoForwardActionParameters>
{
    public GoForwardInspectorViewModel(GoForwardActionParameters current, GoForwardActionParameters defaults, Action<ActionParameters> commit)
        : base("Go forward", "Navigation", "Set timeout behavior for forward navigation.", null, current, defaults, commit) { }
}

public sealed class ReloadPageInspectorViewModel : JsonActionInspectorViewModelBase<ReloadPageActionParameters>
{
    public ReloadPageInspectorViewModel(ReloadPageActionParameters current, ReloadPageActionParameters defaults, Action<ActionParameters> commit)
        : base("Reload page", "Navigation", "Configure refresh strategy and timeout.", null, current, defaults, commit) { }
}

public sealed class WaitForUrlInspectorViewModel : JsonActionInspectorViewModelBase<WaitForUrlActionParameters>
{
    public WaitForUrlInspectorViewModel(WaitForUrlActionParameters current, WaitForUrlActionParameters defaults, Action<ActionParameters> commit)
        : base("Wait for URL", "Navigation", "Set URL matching behavior and timeout.", null, current, defaults, commit) { }
}

public sealed class WaitForUserConfirmationInspectorViewModel : JsonActionInspectorViewModelBase<WaitForUserConfirmationActionParameters>
{
    public WaitForUserConfirmationInspectorViewModel(WaitForUserConfirmationActionParameters current, WaitForUserConfirmationActionParameters defaults, Action<ActionParameters> commit)
        : base("Wait for user confirmation", "Automation", "Pause the flow and wait for the user to click Continue.", null, current, defaults, commit) { }
}

public sealed class ClickElementInspectorViewModel : JsonActionInspectorViewModelBase<ClickElementActionParameters>
{
    private const string SelectorById = "Id";
    private const string SelectorByClass = "Class";
    private const string SelectorByName = "Name";
    private const string SelectorByTag = "Tag";
    private const string SelectorByCss = "CSS";

    private static readonly IReadOnlyList<string> TargetingModes =
    [
        SelectorById,
        SelectorByClass,
        SelectorByName,
        SelectorByTag,
        SelectorByCss,
    ];

    private static readonly Regex AttributeSelectorTokenPattern = new(
        "^\\[(?<name>[A-Za-z_][A-Za-z0-9_-]*)\\s*(?<operator>~=|=)\\s*(?:\"(?<double>(?:\\\\.|[^\"])*)\"|'(?<single>(?:\\\\.|[^'])*)'|(?<bare>[^\\]]+))\\]",
        RegexOptions.Compiled);

    private static readonly Regex SimpleIdSelectorPattern = new(
        "^#(?<value>[^\\s>+~]+)$",
        RegexOptions.Compiled);

    private static readonly Regex SimpleClassSelectorPattern = new(
        "^(?:\\.[^\\s>+~.#\\[:]+)+$",
        RegexOptions.Compiled);

    private static readonly Regex SimpleTagSelectorPattern = new(
        "^[A-Za-z][A-Za-z0-9-]*$",
        RegexOptions.Compiled);

    private string _selectedSelectorMode = SelectorById;
    private string _selectorInputValue = string.Empty;
    private bool _isSynchronizingFromFields;

    public ClickElementInspectorViewModel(ClickElementActionParameters current, ClickElementActionParameters defaults, Action<ActionParameters> commit)
        : base("Click element", "Target", "Choose how to target the element and set click timeout.", null, current, defaults, commit)
    {
        ClearUnsupportedOptionsCommand = new RelayCommand(ClearUnsupportedOptions);
        PropertyChanged += HandlePropertyChanged;
        SynchronizeFromFields();
    }

    public IReadOnlyList<string> SelectorModes => TargetingModes;

    public string SelectedSelectorMode
    {
        get => _selectedSelectorMode;
        set
        {
            if (string.Equals(_selectedSelectorMode, value, StringComparison.Ordinal))
            {
                return;
            }

            _selectedSelectorMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectorInputLabel));
            OnPropertyChanged(nameof(SelectorInputHelpText));
            OnPropertyChanged(nameof(SelectorPreview));
            UpdateSelectorField();
        }
    }

    public string SelectorInputValue
    {
        get => _selectorInputValue;
        set
        {
            if (string.Equals(_selectorInputValue, value, StringComparison.Ordinal))
            {
                return;
            }

            _selectorInputValue = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectorPreview));
            UpdateSelectorField();
        }
    }

    public string SelectorInputLabel => SelectedSelectorMode switch
    {
        SelectorById => "Id",
        SelectorByClass => "Class",
        SelectorByName => "Name",
        SelectorByTag => "Tag",
        _ => "Selector",
    };

    public string SelectorInputHelpText => SelectedSelectorMode switch
    {
        SelectorById => "Enter the element id without the # prefix.",
        SelectorByClass => "Enter one or more class names separated by spaces.",
        SelectorByName => "Enter the name attribute value.",
        SelectorByTag => "Enter the tag name, for example button or input.",
        _ => "Enter a full CSS selector for advanced matching.",
    };

    public string SelectorPreview => string.IsNullOrWhiteSpace(BuildSelector(SelectedSelectorMode, SelectorInputValue))
        ? "Select a target mode and enter a value."
        : BuildSelector(SelectedSelectorMode, SelectorInputValue);

    public string TimeoutMs
    {
        get => FindField("TimeoutMs").StringValue;
        set
        {
            var field = FindField("TimeoutMs");
            if (string.Equals(field.StringValue, value, StringComparison.Ordinal))
            {
                return;
            }

            field.StringValue = value;
        }
    }

    public bool HasUnsupportedOptions =>
        !string.IsNullOrWhiteSpace(FindOptionalField("FrameSelector")?.StringValue) ||
        (FindOptionalField("Force")?.BoolValue ?? false);

    public string UnsupportedOptionsMessage => HasUnsupportedOptions
        ? "Legacy Frame Selector or Force values are still present. The current runtime does not use them."
        : string.Empty;

    public ICommand ClearUnsupportedOptionsCommand { get; }

    private void HandlePropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (!string.Equals(eventArgs.PropertyName, nameof(ParametersJson), StringComparison.Ordinal))
        {
            return;
        }

        SynchronizeFromFields();
    }

    private void SynchronizeFromFields()
    {
        _isSynchronizingFromFields = true;

        try
        {
            var selectorField = FindField("Selector");
            var parsedSelector = ParseSelector(selectorField.StringValue);
            _selectedSelectorMode = parsedSelector.Mode;
            _selectorInputValue = parsedSelector.Value;

            var normalizedSelector = BuildSelector(_selectedSelectorMode, _selectorInputValue);
            if (!string.Equals(selectorField.StringValue, normalizedSelector, StringComparison.Ordinal))
            {
                selectorField.StringValue = normalizedSelector;
            }

            OnPropertyChanged(nameof(SelectedSelectorMode));
            OnPropertyChanged(nameof(SelectorInputValue));
            OnPropertyChanged(nameof(SelectorInputLabel));
            OnPropertyChanged(nameof(SelectorInputHelpText));
            OnPropertyChanged(nameof(SelectorPreview));
            OnPropertyChanged(nameof(TimeoutMs));
            OnPropertyChanged(nameof(HasUnsupportedOptions));
            OnPropertyChanged(nameof(UnsupportedOptionsMessage));
        }
        finally
        {
            _isSynchronizingFromFields = false;
        }
    }

    private void UpdateSelectorField()
    {
        if (_isSynchronizingFromFields)
        {
            return;
        }

        var field = FindField("Selector");
        var selector = BuildSelector(SelectedSelectorMode, SelectorInputValue);
        if (string.Equals(field.StringValue, selector, StringComparison.Ordinal))
        {
            return;
        }

        field.StringValue = selector;
    }

    private void ClearUnsupportedOptions()
    {
        var frameSelectorField = FindOptionalField("FrameSelector");
        if (frameSelectorField is not null && !string.IsNullOrWhiteSpace(frameSelectorField.StringValue))
        {
            frameSelectorField.StringValue = string.Empty;
        }

        var forceField = FindOptionalField("Force");
        if (forceField is not null && forceField.BoolValue)
        {
            forceField.BoolValue = false;
        }

        OnPropertyChanged(nameof(HasUnsupportedOptions));
        OnPropertyChanged(nameof(UnsupportedOptionsMessage));
    }

    private InspectorFieldViewModel FindField(string name)
    {
        return Fields.Single(field => string.Equals(field.Name, name, StringComparison.Ordinal));
    }

    private InspectorFieldViewModel? FindOptionalField(string name)
    {
        return Fields.FirstOrDefault(field => string.Equals(field.Name, name, StringComparison.Ordinal));
    }

    private static (string Mode, string Value) ParseSelector(string selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
        {
            return (SelectorById, string.Empty);
        }

        var trimmed = selector.Trim();
        if (TryParseAttributeSelector(trimmed, "id", "=", out var idValue))
        {
            return (SelectorById, idValue);
        }

        var idMatch = SimpleIdSelectorPattern.Match(trimmed);
        if (idMatch.Success)
        {
            return (SelectorById, idMatch.Groups["value"].Value);
        }

        if (TryParseClassSelector(trimmed, out var classValue))
        {
            return (SelectorByClass, classValue);
        }

        if (TryParseAttributeSelector(trimmed, "name", "=", out var nameValue))
        {
            return (SelectorByName, nameValue);
        }

        if (SimpleTagSelectorPattern.IsMatch(trimmed))
        {
            return (SelectorByTag, trimmed);
        }

        return (SelectorByCss, trimmed);
    }

    private static bool TryParseClassSelector(string selector, out string classValue)
    {
        if (TryParseRepeatedAttributeSelectors(selector, "class", "~=", out var classTokens))
        {
            classValue = string.Join(" ", classTokens);
            return true;
        }

        if (SimpleClassSelectorPattern.IsMatch(selector))
        {
            classValue = string.Join(" ", selector
                .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            return true;
        }

        classValue = string.Empty;
        return false;
    }

    private static bool TryParseRepeatedAttributeSelectors(string selector, string attributeName, string attributeOperator, out IReadOnlyList<string> values)
    {
        values = [];
        var remaining = selector.Trim();
        var parsedValues = new List<string>();

        while (remaining.Length > 0)
        {
            var match = AttributeSelectorTokenPattern.Match(remaining);
            if (!match.Success)
            {
                values = [];
                return false;
            }

            var name = match.Groups["name"].Value;
            var selectorOperator = match.Groups["operator"].Value;
            if (!string.Equals(name, attributeName, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(selectorOperator, attributeOperator, StringComparison.Ordinal))
            {
                values = [];
                return false;
            }

            parsedValues.Add(UnescapeCssAttributeValue(ReadAttributeValue(match)));
            remaining = remaining[match.Length..].TrimStart();
        }

        if (parsedValues.Count == 0)
        {
            values = [];
            return false;
        }

        values = parsedValues;
        return true;
    }

    private static bool TryParseAttributeSelector(string selector, string attributeName, string attributeOperator, out string value)
    {
        var trimmedSelector = selector.Trim();
        var match = AttributeSelectorTokenPattern.Match(trimmedSelector);
        if (!match.Success || match.Length != trimmedSelector.Length)
        {
            value = string.Empty;
            return false;
        }

        var name = match.Groups["name"].Value;
        var selectorOperator = match.Groups["operator"].Value;
        if (!string.Equals(name, attributeName, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(selectorOperator, attributeOperator, StringComparison.Ordinal))
        {
            value = string.Empty;
            return false;
        }

        value = UnescapeCssAttributeValue(ReadAttributeValue(match));
        return true;
    }

    private static string ReadAttributeValue(Match match)
    {
        if (match.Groups["double"].Success)
        {
            return match.Groups["double"].Value;
        }

        if (match.Groups["single"].Success)
        {
            return match.Groups["single"].Value;
        }

        return match.Groups["bare"].Value.Trim();
    }

    private static string BuildSelector(string mode, string inputValue)
    {
        var trimmed = inputValue.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        return mode switch
        {
            SelectorById => BuildAttributeSelector("id", trimmed.TrimStart('#')),
            SelectorByClass => BuildClassSelector(trimmed),
            SelectorByName => BuildAttributeSelector("name", trimmed),
            _ => trimmed,
        };
    }

    private static string BuildClassSelector(string inputValue)
    {
        if (LooksLikeCssSelector(inputValue))
        {
            return inputValue.Trim();
        }

        var classTokens = inputValue
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => token.TrimStart('.'))
            .Where(token => token.Length > 0)
            .ToArray();

        if (classTokens.Length == 0)
        {
            return string.Empty;
        }

        return string.Concat(classTokens.Select(token => $"[class~=\"{EscapeCssAttributeValue(token)}\"]"));
    }

    private static bool LooksLikeCssSelector(string inputValue)
    {
        return inputValue.IndexOfAny(['[', ']', '#', '=', '>', '+', '~', ':']) >= 0;
    }

    private static string BuildAttributeSelector(string attributeName, string attributeValue)
    {
        return $"[{attributeName}=\"{EscapeCssAttributeValue(attributeValue)}\"]";
    }

    private static string EscapeCssAttributeValue(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static string UnescapeCssAttributeValue(string value)
    {
        return value
            .Replace("\\\"", "\"", StringComparison.Ordinal)
            .Replace("\\\\", "\\", StringComparison.Ordinal);
    }
}

public sealed class FillInputInspectorViewModel : JsonActionInspectorViewModelBase<FillInputActionParameters>
{
    private const string SelectorById = "Id";
    private const string SelectorByClass = "Class";
    private const string SelectorByName = "Name";
    private const string SelectorByTag = "Tag";
    private const string SelectorByCss = "CSS";

    private static readonly IReadOnlyList<string> TargetingModes =
    [
        SelectorById,
        SelectorByClass,
        SelectorByName,
        SelectorByTag,
        SelectorByCss,
    ];

    private static readonly Regex AttributeSelectorTokenPattern = new(
        "^\\[(?<name>[A-Za-z_][A-Za-z0-9_-]*)\\s*(?<operator>~=|=)\\s*(?:\"(?<double>(?:\\\\.|[^\"])*)\"|'(?<single>(?:\\\\.|[^'])*)'|(?<bare>[^\\]]+))\\]",
        RegexOptions.Compiled);

    private static readonly Regex SimpleIdSelectorPattern = new(
        "^#(?<value>[^\\s>+~]+)$",
        RegexOptions.Compiled);

    private static readonly Regex SimpleClassSelectorPattern = new(
        "^(?:\\.[^\\s>+~.#\\[:]+)+$",
        RegexOptions.Compiled);

    private static readonly Regex SimpleTagSelectorPattern = new(
        "^[A-Za-z][A-Za-z0-9-]*$",
        RegexOptions.Compiled);

    private string _selectedSelectorMode = SelectorById;
    private string _selectorInputValue = string.Empty;
    private bool _isSynchronizingFromFields;

    public FillInputInspectorViewModel(FillInputActionParameters current, FillInputActionParameters defaults, Action<ActionParameters> commit)
        : base("Fill input", "Target", "Configure input target and value.", null, current, defaults, commit)
    {
        PropertyChanged += HandlePropertyChanged;
        SynchronizeFromFields();
    }

    public IReadOnlyList<string> SelectorModes => TargetingModes;

    public string SelectedSelectorMode
    {
        get => _selectedSelectorMode;
        set
        {
            if (string.Equals(_selectedSelectorMode, value, StringComparison.Ordinal))
            {
                return;
            }

            _selectedSelectorMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectorInputLabel));
            OnPropertyChanged(nameof(SelectorInputHelpText));
            OnPropertyChanged(nameof(SelectorPreview));
            UpdateSelectorField();
        }
    }

    public string SelectorInputValue
    {
        get => _selectorInputValue;
        set
        {
            if (string.Equals(_selectorInputValue, value, StringComparison.Ordinal))
            {
                return;
            }

            _selectorInputValue = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectorPreview));
            UpdateSelectorField();
        }
    }

    public string SelectorInputLabel => SelectedSelectorMode switch
    {
        SelectorById => "Id",
        SelectorByClass => "Class",
        SelectorByName => "Name",
        SelectorByTag => "Tag",
        _ => "Selector",
    };

    public string SelectorInputHelpText => SelectedSelectorMode switch
    {
        SelectorById => "Enter the element id without the # prefix.",
        SelectorByClass => "Enter one or more class names separated by spaces.",
        SelectorByName => "Enter the name attribute value.",
        SelectorByTag => "Enter the tag name, for example button or input.",
        _ => "Enter a full CSS selector for advanced matching.",
    };

    public string SelectorPreview => string.IsNullOrWhiteSpace(BuildSelector(SelectedSelectorMode, SelectorInputValue))
        ? "Select a target mode and enter a value."
        : BuildSelector(SelectedSelectorMode, SelectorInputValue);

    public string Value
    {
        get => FindField("Value").StringValue;
        set
        {
            var field = FindField("Value");
            if (string.Equals(field.StringValue, value, StringComparison.Ordinal))
            {
                return;
            }

            field.StringValue = value;
        }
    }

    public bool ClearFirst
    {
        get => FindField("ClearFirst").BoolValue;
        set
        {
            var field = FindField("ClearFirst");
            if (field.BoolValue == value)
            {
                return;
            }

            field.BoolValue = value;
        }
    }

    public string TimeoutMs
    {
        get => FindField("TimeoutMs").StringValue;
        set
        {
            var field = FindField("TimeoutMs");
            if (string.Equals(field.StringValue, value, StringComparison.Ordinal))
            {
                return;
            }

            field.StringValue = value;
        }
    }

    private void HandlePropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (!string.Equals(eventArgs.PropertyName, nameof(ParametersJson), StringComparison.Ordinal))
        {
            return;
        }

        SynchronizeFromFields();
    }

    private void SynchronizeFromFields()
    {
        _isSynchronizingFromFields = true;

        try
        {
            var selectorField = FindField("Selector");
            var parsedSelector = ParseSelector(selectorField.StringValue);
            _selectedSelectorMode = parsedSelector.Mode;
            _selectorInputValue = parsedSelector.Value;

            var normalizedSelector = BuildSelector(_selectedSelectorMode, _selectorInputValue);
            if (!string.Equals(selectorField.StringValue, normalizedSelector, StringComparison.Ordinal))
            {
                selectorField.StringValue = normalizedSelector;
            }

            OnPropertyChanged(nameof(SelectedSelectorMode));
            OnPropertyChanged(nameof(SelectorInputValue));
            OnPropertyChanged(nameof(SelectorInputLabel));
            OnPropertyChanged(nameof(SelectorInputHelpText));
            OnPropertyChanged(nameof(SelectorPreview));
            OnPropertyChanged(nameof(Value));
            OnPropertyChanged(nameof(ClearFirst));
            OnPropertyChanged(nameof(TimeoutMs));
        }
        finally
        {
            _isSynchronizingFromFields = false;
        }
    }

    private void UpdateSelectorField()
    {
        if (_isSynchronizingFromFields)
        {
            return;
        }

        var field = FindField("Selector");
        var selector = BuildSelector(SelectedSelectorMode, SelectorInputValue);
        if (string.Equals(field.StringValue, selector, StringComparison.Ordinal))
        {
            return;
        }

        field.StringValue = selector;
    }

    private InspectorFieldViewModel FindField(string name)
    {
        return Fields.Single(field => string.Equals(field.Name, name, StringComparison.Ordinal));
    }

    private static (string Mode, string Value) ParseSelector(string selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
        {
            return (SelectorById, string.Empty);
        }

        var trimmed = selector.Trim();
        if (TryParseAttributeSelector(trimmed, "id", "=", out var idValue))
        {
            return (SelectorById, idValue);
        }

        var idMatch = SimpleIdSelectorPattern.Match(trimmed);
        if (idMatch.Success)
        {
            return (SelectorById, idMatch.Groups["value"].Value);
        }

        if (TryParseClassSelector(trimmed, out var classValue))
        {
            return (SelectorByClass, classValue);
        }

        if (TryParseAttributeSelector(trimmed, "name", "=", out var nameValue))
        {
            return (SelectorByName, nameValue);
        }

        if (SimpleTagSelectorPattern.IsMatch(trimmed))
        {
            return (SelectorByTag, trimmed);
        }

        return (SelectorByCss, trimmed);
    }

    private static bool TryParseClassSelector(string selector, out string classValue)
    {
        if (TryParseRepeatedAttributeSelectors(selector, "class", "~=", out var classTokens))
        {
            classValue = string.Join(" ", classTokens);
            return true;
        }

        if (SimpleClassSelectorPattern.IsMatch(selector))
        {
            classValue = string.Join(" ", selector
                .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            return true;
        }

        classValue = string.Empty;
        return false;
    }

    private static bool TryParseRepeatedAttributeSelectors(string selector, string attributeName, string attributeOperator, out IReadOnlyList<string> values)
    {
        values = [];
        var remaining = selector.Trim();
        var parsedValues = new List<string>();

        while (remaining.Length > 0)
        {
            var match = AttributeSelectorTokenPattern.Match(remaining);
            if (!match.Success)
            {
                values = [];
                return false;
            }

            var name = match.Groups["name"].Value;
            var selectorOperator = match.Groups["operator"].Value;
            if (!string.Equals(name, attributeName, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(selectorOperator, attributeOperator, StringComparison.Ordinal))
            {
                values = [];
                return false;
            }

            parsedValues.Add(UnescapeCssAttributeValue(ReadAttributeValue(match)));
            remaining = remaining[match.Length..].TrimStart();
        }

        if (parsedValues.Count == 0)
        {
            values = [];
            return false;
        }

        values = parsedValues;
        return true;
    }

    private static bool TryParseAttributeSelector(string selector, string attributeName, string attributeOperator, out string value)
    {
        var trimmedSelector = selector.Trim();
        var match = AttributeSelectorTokenPattern.Match(trimmedSelector);
        if (!match.Success || match.Length != trimmedSelector.Length)
        {
            value = string.Empty;
            return false;
        }

        var name = match.Groups["name"].Value;
        var selectorOperator = match.Groups["operator"].Value;
        if (!string.Equals(name, attributeName, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(selectorOperator, attributeOperator, StringComparison.Ordinal))
        {
            value = string.Empty;
            return false;
        }

        value = UnescapeCssAttributeValue(ReadAttributeValue(match));
        return true;
    }

    private static string ReadAttributeValue(Match match)
    {
        if (match.Groups["double"].Success)
        {
            return match.Groups["double"].Value;
        }

        if (match.Groups["single"].Success)
        {
            return match.Groups["single"].Value;
        }

        return match.Groups["bare"].Value.Trim();
    }

    private static string BuildSelector(string mode, string inputValue)
    {
        var trimmed = inputValue.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        return mode switch
        {
            SelectorById => BuildAttributeSelector("id", trimmed.TrimStart('#')),
            SelectorByClass => BuildClassSelector(trimmed),
            SelectorByName => BuildAttributeSelector("name", trimmed),
            _ => trimmed,
        };
    }

    private static string BuildClassSelector(string inputValue)
    {
        if (LooksLikeCssSelector(inputValue))
        {
            return inputValue.Trim();
        }

        var classTokens = inputValue
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => token.TrimStart('.'))
            .Where(token => token.Length > 0)
            .ToArray();

        if (classTokens.Length == 0)
        {
            return string.Empty;
        }

        return string.Concat(classTokens.Select(token => $"[class~=\"{EscapeCssAttributeValue(token)}\"]"));
    }

    private static bool LooksLikeCssSelector(string inputValue)
    {
        return inputValue.IndexOfAny(['[', ']', '#', '=', '>', '+', '~', ':']) >= 0;
    }

    private static string BuildAttributeSelector(string attributeName, string attributeValue)
    {
        return $"[{attributeName}=\"{EscapeCssAttributeValue(attributeValue)}\"]";
    }

    private static string EscapeCssAttributeValue(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static string UnescapeCssAttributeValue(string value)
    {
        return value
            .Replace("\\\"", "\"", StringComparison.Ordinal)
            .Replace("\\\\", "\\", StringComparison.Ordinal);
    }
}

public sealed class HoverElementInspectorViewModel : JsonActionInspectorViewModelBase<HoverElementActionParameters>
{
    public HoverElementInspectorViewModel(HoverElementActionParameters current, HoverElementActionParameters defaults, Action<ActionParameters> commit)
        : base("Hover element", "Target", "Set selector targeting for hover actions.", null, current, defaults, commit) { }
}

public sealed class PressKeyInspectorViewModel : JsonActionInspectorViewModelBase<PressKeyActionParameters>
{
    public PressKeyInspectorViewModel(PressKeyActionParameters current, PressKeyActionParameters defaults, Action<ActionParameters> commit)
        : base("Press key", "Input", "Set key chords and optional element target.", null, current, defaults, commit) { }
}

public sealed class SelectOptionInspectorViewModel : JsonActionInspectorViewModelBase<SelectOptionActionParameters>
{
    public SelectOptionInspectorViewModel(SelectOptionActionParameters current, SelectOptionActionParameters defaults, Action<ActionParameters> commit)
        : base("Select option", "Target", "Configure select element and option value.", null, current, defaults, commit) { }
}

public sealed class ExpectEnabledInspectorViewModel : JsonActionInspectorViewModelBase<ExpectEnabledActionParameters>
{
    public ExpectEnabledInspectorViewModel(ExpectEnabledActionParameters current, ExpectEnabledActionParameters defaults, Action<ActionParameters> commit)
        : base("Expect enabled", "Assertions", "Define selector and assertion timeout.", null, current, defaults, commit) { }
}

public sealed class ExpectHiddenInspectorViewModel : JsonActionInspectorViewModelBase<ExpectHiddenActionParameters>
{
    public ExpectHiddenInspectorViewModel(ExpectHiddenActionParameters current, ExpectHiddenActionParameters defaults, Action<ActionParameters> commit)
        : base("Expect hidden", "Assertions", "Define selector and assertion timeout.", null, current, defaults, commit) { }
}

public sealed class ExpectTextInspectorViewModel : JsonActionInspectorViewModelBase<ExpectTextActionParameters>
{
    public ExpectTextInspectorViewModel(ExpectTextActionParameters current, ExpectTextActionParameters defaults, Action<ActionParameters> commit)
        : base("Expect text", "Assertions", "Define selector, expected text, and options.", null, current, defaults, commit) { }
}

public sealed class ExpectVisibleInspectorViewModel : JsonActionInspectorViewModelBase<ExpectVisibleActionParameters>
{
    public ExpectVisibleInspectorViewModel(ExpectVisibleActionParameters current, ExpectVisibleActionParameters defaults, Action<ActionParameters> commit)
        : base("Expect visible", "Assertions", "Define selector and assertion timeout.", null, current, defaults, commit) { }
}

public sealed class UnknownActionInspectorViewModel : JsonActionInspectorViewModelBase<UnknownActionParameters>
{
    public UnknownActionInspectorViewModel(string actionId, UnknownActionParameters current, UnknownActionParameters defaults, Action<ActionParameters> commit)
        : base($"Unknown action ({actionId})", "Fallback", "This action id is not mapped yet. Field-based fallback editor is being used.", "Using fallback inspector until a dedicated one is registered.", current, defaults, commit) { }
}
