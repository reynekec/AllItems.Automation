using WpfAutomation.Core.Abstractions.Actions;

namespace AllItems.Automation.Browser.Actions.Navigation;

public sealed class WaitForUrlAction : IAutomationAction
{
    public ActionMetadata Metadata { get; } = new(
        ActionId: "wait-for-url",
        DisplayName: "Wait for URL",
        CategoryId: "navigation",
        CategoryName: "Navigation",
        IconKeyOrPath: "icon-navigation-wait",
        Keywords: ["wait", "route", "match"],
        SortOrder: 20,
        IsContainer: false);
}