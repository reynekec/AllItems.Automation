using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace WpfAutomation.App.Views;

public partial class FlowActionNodeControl : UserControl
{
    public static readonly DependencyProperty SelectedNodeIdsProperty =
        DependencyProperty.Register(
            nameof(SelectedNodeIds),
            typeof(IReadOnlyList<string>),
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
}
