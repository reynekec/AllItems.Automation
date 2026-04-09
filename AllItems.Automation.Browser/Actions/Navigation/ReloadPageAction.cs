using WpfAutomation.Core.Abstractions.Actions;

namespace AllItems.Automation.Browser.Actions.Navigation;

public sealed class ReloadPageAction : IAutomationAction
{
    public ActionMetadata Metadata { get; } = new(
        ActionId: "reload-page",
        DisplayName: "Reload page",
        CategoryId: "navigation",
        CategoryName: "Navigation",
        IconKeyOrPath: "icon-navigation-reload",
        Keywords: ["refresh", "reload"],
        SortOrder: 50,
        IsContainer: false);
}