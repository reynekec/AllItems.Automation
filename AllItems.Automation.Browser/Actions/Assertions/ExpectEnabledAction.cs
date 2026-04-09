using WpfAutomation.Core.Abstractions.Actions;

namespace AllItems.Automation.Browser.Actions.Assertions;

public sealed class ExpectEnabledAction : IAutomationAction
{
    public ActionMetadata Metadata { get; } = new(
        ActionId: "expect-enabled",
        DisplayName: "Expect enabled",
        CategoryId: "assertions",
        CategoryName: "Assertions",
        IconKeyOrPath: "icon-assert-enabled",
        Keywords: ["enabled", "active", "interactive"],
        SortOrder: 40,
        IsContainer: false);
}