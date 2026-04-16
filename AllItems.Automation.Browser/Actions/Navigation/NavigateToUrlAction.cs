using AllItems.Automation.Browser.Core.Abstractions.Actions;

namespace AllItems.Automation.Browser.Actions.Navigation;

public sealed class NavigateToUrlAction : IAutomationAction
{
    public ActionMetadata Metadata { get; } = new(
        ActionId: "navigate-to-url",
        DisplayName: "Navigate to URL",
        CategoryId: "navigation",
        CategoryName: "Navigation",
        IconKeyOrPath: "icon-navigation-open",
        Keywords: ["goto", "url", "visit", "open"],
        SortOrder: 10,
        IsContainer: false);
}