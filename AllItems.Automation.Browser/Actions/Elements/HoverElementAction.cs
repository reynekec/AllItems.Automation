using WpfAutomation.Core.Abstractions.Actions;

namespace AllItems.Automation.Browser.Actions.Elements;

public sealed class HoverElementAction : IAutomationAction
{
    public ActionMetadata Metadata { get; } = new(
        ActionId: "hover-element",
        DisplayName: "Hover element",
        CategoryId: "elements",
        CategoryName: "Elements",
        IconKeyOrPath: "icon-element-hover",
        Keywords: ["hover", "over", "mouse"],
        SortOrder: 40,
        IsContainer: false);
}