using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AllItems.Automation.Browser.App.Commands;
using AllItems.Automation.Browser.App.NodeInspector.Contracts;

namespace AllItems.Automation.Browser.App.NodeInspector.ViewModels;

public sealed class DefaultNodeInspectorViewModel : INodeInspectorViewModel
{
    private bool _isDirty;
    private bool _hasValidationErrors;
    private IReadOnlyList<string> _validationErrors = [];

    public DefaultNodeInspectorViewModel(string title)
    {
        Title = title;
        ResetToDefaultsCommand = new RelayCommand(_ => { });
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title { get; }

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

    public void UpdateValidation(IReadOnlyList<string> errors)
    {
        ValidationErrors = errors;
        HasValidationErrors = errors.Count > 0;
    }

    public void SetDirty(bool isDirty)
    {
        IsDirty = isDirty;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
