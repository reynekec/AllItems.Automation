using WpfAutomation.Core.Abstractions.Actions;

namespace AllItems.Automation.Browser.Actions.Automation;

public sealed class WaitForUserConfirmationAction : IAutomationAction
{
    public ActionMetadata Metadata { get; } = new(
        ActionId: "wait-for-user-confirmation",
        DisplayName: "Wait for user confirmation",
        CategoryId: "automation",
        CategoryName: "Automation",
        IconKeyOrPath: "icon-automation-confirmation",
        Keywords: ["wait", "confirm", "continue", "pause"],
        SortOrder: 5,
        IsContainer: false);
}
