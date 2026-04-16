using AllItems.Automation.Browser.Core.Abstractions.Actions;

namespace AllItems.Automation.Browser.Actions.Navigation;

public sealed class GoForwardAction : IAutomationAction
{
    public ActionMetadata Metadata { get; } = new(
        ActionId: "go-forward",
        DisplayName: "Go forward",
        CategoryId: "navigation",
        CategoryName: "Navigation",
        IconKeyOrPath: "icon-navigation-forward",
        Keywords: ["forward", "history", "next"],
        SortOrder: 40,
        IsContainer: false);
}