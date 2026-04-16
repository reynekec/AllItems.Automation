using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AllItems.Automation.Browser.App.Views;

public partial class FlowContainerNodeControl : UserControl
{
    public static readonly DependencyProperty SelectedNodeIdsProperty =
        DependencyProperty.Register(
            nameof(SelectedNodeIds),
            typeof(IReadOnlyList<string>),
            typeof(FlowContainerNodeControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty HoveredLaneIdProperty =
        DependencyProperty.Register(
            nameof(HoveredLaneId),
            typeof(string),
            typeof(FlowContainerNodeControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ToggleCollapseCommandProperty =
        DependencyProperty.Register(
            nameof(ToggleCollapseCommand),
            typeof(ICommand),
            typeof(FlowContainerNodeControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty SuccessfulNodeIdsProperty =
        DependencyProperty.Register(
            nameof(SuccessfulNodeIds),
            typeof(IEnumerable<string>),
            typeof(FlowContainerNodeControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty FailedNodeIdsProperty =
        DependencyProperty.Register(
            nameof(FailedNodeIds),
            typeof(IEnumerable<string>),
            typeof(FlowContainerNodeControl),
            new PropertyMetadata(null));

    public FlowContainerNodeControl()
    {
        InitializeComponent();
    }

    public IReadOnlyList<string>? SelectedNodeIds
    {
        get => (IReadOnlyList<string>?)GetValue(SelectedNodeIdsProperty);
        set => SetValue(SelectedNodeIdsProperty, value);
    }

    public string? HoveredLaneId
    {
        get => (string?)GetValue(HoveredLaneIdProperty);
        set => SetValue(HoveredLaneIdProperty, value);
    }

    public ICommand? ToggleCollapseCommand
    {
        get => (ICommand?)GetValue(ToggleCollapseCommandProperty);
        set => SetValue(ToggleCollapseCommandProperty, value);
    }

    public IEnumerable<string>? SuccessfulNodeIds
    {
        get => (IEnumerable<string>?)GetValue(SuccessfulNodeIdsProperty);
        set => SetValue(SuccessfulNodeIdsProperty, value);
    }

    public IEnumerable<string>? FailedNodeIds
    {
        get => (IEnumerable<string>?)GetValue(FailedNodeIdsProperty);
        set => SetValue(FailedNodeIdsProperty, value);
    }
}
