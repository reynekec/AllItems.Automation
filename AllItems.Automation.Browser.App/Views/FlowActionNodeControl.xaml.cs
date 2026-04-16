using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace AllItems.Automation.Browser.App.Views;

public partial class FlowActionNodeControl : UserControl
{
    public static readonly DependencyProperty SelectedNodeIdsProperty =
        DependencyProperty.Register(
            nameof(SelectedNodeIds),
            typeof(IReadOnlyList<string>),
            typeof(FlowActionNodeControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty SuccessfulNodeIdsProperty =
        DependencyProperty.Register(
            nameof(SuccessfulNodeIds),
            typeof(IEnumerable<string>),
            typeof(FlowActionNodeControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty FailedNodeIdsProperty =
        DependencyProperty.Register(
            nameof(FailedNodeIds),
            typeof(IEnumerable<string>),
            typeof(FlowActionNodeControl),
            new PropertyMetadata(null));

    public FlowActionNodeControl()
    {
        InitializeComponent();
    }

    public IReadOnlyList<string>? SelectedNodeIds
    {
        get => (IReadOnlyList<string>?)GetValue(SelectedNodeIdsProperty);
        set => SetValue(SelectedNodeIdsProperty, value);
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
