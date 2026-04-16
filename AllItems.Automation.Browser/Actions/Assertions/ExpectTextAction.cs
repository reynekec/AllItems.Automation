using AllItems.Automation.Browser.Core.Abstractions.Actions;

namespace AllItems.Automation.Browser.Actions.Assertions;

public sealed class ExpectTextAction : IAutomationAction
{
    public ActionMetadata Metadata { get; } = new(
        ActionId: "expect-text",
        DisplayName: "Expect text",
        CategoryId: "assertions",
        CategoryName: "Assertions",
        IconKeyOrPath: "icon-assert-text",
        Keywords: ["text", "contains", "assert", "verify"],
        SortOrder: 10,
        IsContainer: false);
}