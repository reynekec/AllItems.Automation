using System.ComponentModel;
using System.Windows.Input;

namespace WpfAutomation.App.NodeInspector.Contracts;

public interface INodeInspectorViewModel : INotifyPropertyChanged
{
    string Title { get; }

    bool IsDirty { get; }

    bool HasValidationErrors { get; }

    IReadOnlyList<string> ValidationErrors { get; }

    ICommand ResetToDefaultsCommand { get; }
}

public interface IJsonNodeInspectorViewModel : INodeInspectorViewModel
{
    string CategoryName { get; }

    string HintText { get; }

    string? WarningText { get; }

    string ParametersJson { get; set; }
}
