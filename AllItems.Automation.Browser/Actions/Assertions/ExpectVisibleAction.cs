using WpfAutomation.Core.Abstractions.Actions;

namespace AllItems.Automation.Browser.Actions.Assertions;

public sealed class ExpectVisibleAction : IAutomationAction
{
    public ActionMetadata Metadata { get; } = new(
        ActionId: "expect-visible",
        DisplayName: "Expect visible",
        CategoryId: "assertions",
        CategoryName: "Assertions",
        IconKeyOrPath: "icon-assert-visible",
        Keywords: ["visible", "present", "assert", "verify"],
        SortOrder: 20,
        IsContainer: false);
}