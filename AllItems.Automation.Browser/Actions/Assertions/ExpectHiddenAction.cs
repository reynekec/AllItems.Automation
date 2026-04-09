using WpfAutomation.Core.Abstractions.Actions;

namespace AllItems.Automation.Browser.Actions.Assertions;

public sealed class ExpectHiddenAction : IAutomationAction
{
    public ActionMetadata Metadata { get; } = new(
        ActionId: "expect-hidden",
        DisplayName: "Expect hidden",
        CategoryId: "assertions",
        CategoryName: "Assertions",
        IconKeyOrPath: "icon-assert-hidden",
        Keywords: ["hidden", "absent", "not visible"],
        SortOrder: 30,
        IsContainer: false);
}