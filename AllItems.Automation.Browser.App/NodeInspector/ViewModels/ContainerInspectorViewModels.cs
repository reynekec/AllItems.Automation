using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using AllItems.Automation.Browser.App.Commands;
using AllItems.Automation.Browser.App.Models.Flow;
using AllItems.Automation.Browser.App.NodeInspector.Contracts;

namespace AllItems.Automation.Browser.App.NodeInspector.ViewModels;

public abstract class JsonContainerInspectorViewModelBase<TParameters> : IJsonNodeInspectorViewModel
    where TParameters : ContainerParameters
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly Action<ContainerParameters> _commit;
    private readonly TParameters _defaultParameters;
    private bool _suppressFieldCommit;
    private string _parametersJson;
    private string _lastCommittedJson;
    private bool _isDirty;
    private bool _hasValidationErrors;
    private IReadOnlyList<string> _validationErrors = [];

    protected JsonContainerInspectorViewModelBase(
        string title,
        string categoryName,
        string hintText,
        string? warningText,
        TParameters currentParameters,
        TParameters defaultParameters,
        Action<ContainerParameters> commit)
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
        return [];
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

        if (property.PropertyType == typeof(int) || property.PropertyType == typeof(int?))
        {
            return new InspectorFieldViewModel(
                property.Name,
                ToLabel(property.Name),
                InspectorFieldKind.Number,
                value?.ToString() ?? string.Empty,
                false,
                [],
                OnFieldChanged);
        }

        return new InspectorFieldViewModel(
            property.Name,
            ToLabel(property.Name),
            InspectorFieldKind.Text,
            value as string ?? string.Empty,
            false,
            [],
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

            if (property.PropertyType == typeof(int?))
            {
                if (string.IsNullOrWhiteSpace(field.StringValue))
                {
                    payload[field.Name] = null;
                    continue;
                }

                if (!int.TryParse(field.StringValue, out var nullableValue))
                {
                    parameters = null;
                    error = $"{field.Label} must be a whole number when provided.";
                    return false;
                }

                payload[field.Name] = nullableValue;
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

public sealed class ForContainerInspectorViewModel : JsonContainerInspectorViewModelBase<ForContainerParameters>
{
    public ForContainerInspectorViewModel(ForContainerParameters current, ForContainerParameters defaults, Action<ContainerParameters> commit)
        : base("For Loop", "Control Flow", "Configure start, end, and step values for loop execution.", null, current, defaults, commit)
    {
    }

    protected override IReadOnlyList<string> Validate(ForContainerParameters parameters)
    {
        List<string> errors = [];
        if (parameters.Step == 0)
        {
            errors.Add("Step must be non-zero.");
        }

        if (parameters.MaxIterationsOverride.HasValue && parameters.MaxIterationsOverride.Value <= 0)
        {
            errors.Add("Max iterations override must be greater than zero when provided.");
        }

        return errors;
    }
}

public sealed class ForEachContainerInspectorViewModel : JsonContainerInspectorViewModelBase<ForEachContainerParameters>
{
    public ForEachContainerInspectorViewModel(ForEachContainerParameters current, ForEachContainerParameters defaults, Action<ContainerParameters> commit)
        : base("ForEach Loop", "Control Flow", "Set the item source expression and loop variable name.", null, current, defaults, commit)
    {
    }

    protected override IReadOnlyList<string> Validate(ForEachContainerParameters parameters)
    {
        List<string> errors = [];
        if (string.IsNullOrWhiteSpace(parameters.ItemVariable))
        {
            errors.Add("Item variable is required.");
        }

        if (parameters.MaxIterationsOverride.HasValue && parameters.MaxIterationsOverride.Value <= 0)
        {
            errors.Add("Max iterations override must be greater than zero when provided.");
        }

        return errors;
    }
}

public sealed class WhileContainerInspectorViewModel : JsonContainerInspectorViewModelBase<WhileContainerParameters>
{
    public WhileContainerInspectorViewModel(WhileContainerParameters current, WhileContainerParameters defaults, Action<ContainerParameters> commit)
        : base("While Loop", "Control Flow", "Set the condition and max iteration guard.", "Use MaxIterations to prevent runaway loops.", current, defaults, commit)
    {
    }

    protected override IReadOnlyList<string> Validate(WhileContainerParameters parameters)
    {
        List<string> errors = [];
        if (string.IsNullOrWhiteSpace(parameters.ConditionExpression))
        {
            errors.Add("Condition expression is required.");
        }

        if (parameters.MaxIterations <= 0)
        {
            errors.Add("Max iterations must be greater than zero.");
        }

        return errors;
    }
}

public sealed class UnknownContainerInspectorViewModel : JsonContainerInspectorViewModelBase<UnknownContainerParameters>
{
    public UnknownContainerInspectorViewModel(string containerKind, UnknownContainerParameters current, UnknownContainerParameters defaults, Action<ContainerParameters> commit)
        : base(
            $"{containerKind} Container",
            "Control Flow",
            "No specialized inspector is registered for this container type.",
            null,
            current,
            defaults,
            commit)
    {
    }
}
