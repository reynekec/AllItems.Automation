using System.Windows;
using System.Windows.Controls;
using AllItems.Automation.Browser.App.Docking.Models;

namespace AllItems.Automation.Browser.App.Docking.Controls;

public sealed class DockPanelContentTemplateSelector : DataTemplateSelector
{
    public DataTemplate? ActionPanelTemplate { get; init; }

    public DataTemplate? CanvasDocumentTemplate { get; init; }

    public DataTemplate? NodeInspectorTemplate { get; init; }

    public DataTemplate? EmptyToolTemplate { get; init; }

    public DataTemplate? LogsPanelTemplate { get; init; }

    public DataTemplate? DefaultTemplate { get; init; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is not DockPanelState panel)
        {
            return DefaultTemplate;
        }

        return panel.PanelId switch
        {
            "action-panel" => ActionPanelTemplate ?? DefaultTemplate,
            "canvas" => CanvasDocumentTemplate ?? DefaultTemplate,
            "runner-controls" => NodeInspectorTemplate ?? DefaultTemplate,
            "errors" => EmptyToolTemplate ?? DefaultTemplate,
            "logs" => LogsPanelTemplate ?? DefaultTemplate,
            _ => DefaultTemplate,
        };
    }
}
