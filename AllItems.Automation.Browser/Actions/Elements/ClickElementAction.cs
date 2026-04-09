using WpfAutomation.Core.Abstractions.Actions;

namespace AllItems.Automation.Browser.Actions.Elements;

public sealed class ClickElementAction : IAutomationAction
{
    public ActionMetadata Metadata { get; } = new(
        ActionId: "click-element",
        DisplayName: "Click element",
        CategoryId: "elements",
        CategoryName: "Elements",
        IconKeyOrPath: "icon-element-click",
        Keywords: ["click", "tap", "press"],
        SortOrder: 10,
        IsContainer: false);
}