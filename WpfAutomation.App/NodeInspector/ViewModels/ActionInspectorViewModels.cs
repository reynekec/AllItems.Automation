using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using WpfAutomation.App.Commands;
using WpfAutomation.App.Models.Flow;
using WpfAutomation.App.NodeInspector.Contracts;

namespace WpfAutomation.App.NodeInspector.ViewModels;

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
    public NavigateToUrlInspectorViewModel(NavigateToUrlActionParameters current, NavigateToUrlActionParameters defaults, Action<ActionParameters> commit)
        : base("Navigate to URL", "Target", "Set the destination and timeout settings.", null, current, defaults, commit) { }
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

public sealed class ClickElementInspectorViewModel : JsonActionInspectorViewModelBase<ClickElementActionParameters>
{
    public ClickElementInspectorViewModel(ClickElementActionParameters current, ClickElementActionParameters defaults, Action<ActionParameters> commit)
        : base("Click element", "Target", "Set selector targeting and click options.", null, current, defaults, commit) { }
}

public sealed class FillInputInspectorViewModel : JsonActionInspectorViewModelBase<FillInputActionParameters>
{
    public FillInputInspectorViewModel(FillInputActionParameters current, FillInputActionParameters defaults, Action<ActionParameters> commit)
        : base("Fill input", "Target", "Configure input target and value.", null, current, defaults, commit) { }
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
