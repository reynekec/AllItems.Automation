using AllItems.Automation.Browser.Core.Abstractions.Actions;

namespace AllItems.Automation.Browser.Actions.Elements;

public sealed class FillInputAction : IAutomationAction
{
    public ActionMetadata Metadata { get; } = new(
        ActionId: "fill-input",
        DisplayName: "Fill input",
        CategoryId: "elements",
        CategoryName: "Elements",
        IconKeyOrPath: "icon-element-input",
        Keywords: ["type", "input", "text", "enter"],
        SortOrder: 20,
        IsContainer: false);
}