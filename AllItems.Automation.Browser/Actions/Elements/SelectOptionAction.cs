using WpfAutomation.Core.Abstractions.Actions;

namespace AllItems.Automation.Browser.Actions.Elements;

public sealed class SelectOptionAction : IAutomationAction
{
    public ActionMetadata Metadata { get; } = new(
        ActionId: "select-option",
        DisplayName: "Select option",
        CategoryId: "elements",
        CategoryName: "Elements",
        IconKeyOrPath: "icon-element-select",
        Keywords: ["dropdown", "select", "option", "choose"],
        SortOrder: 30,
        IsContainer: false);
}