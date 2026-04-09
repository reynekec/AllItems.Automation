using System.Windows.Input;

namespace WpfAutomation.App.Models;

/// <summary>
/// Defines parent-owned commands consumed by the reusable actions sidebar control.
/// </summary>
public interface IUiActionsSidebarCommandContract
{
    ICommand InvokeActionCommand { get; }

    ICommand ToggleCategoryCommand { get; }

    ICommand StartDragCommand { get; }
}