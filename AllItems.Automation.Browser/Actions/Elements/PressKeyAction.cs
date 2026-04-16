using AllItems.Automation.Browser.Core.Abstractions.Actions;

namespace AllItems.Automation.Browser.Actions.Elements;

public sealed class PressKeyAction : IAutomationAction
{
    public ActionMetadata Metadata { get; } = new(
        ActionId: "press-key",
        DisplayName: "Press key",
        CategoryId: "elements",
        CategoryName: "Elements",
        IconKeyOrPath: "icon-element-keyboard",
        Keywords: ["keyboard", "key", "shortcut"],
        SortOrder: 50,
        IsContainer: false);
}