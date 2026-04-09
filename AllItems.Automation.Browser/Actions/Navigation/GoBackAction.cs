using WpfAutomation.Core.Abstractions.Actions;

namespace AllItems.Automation.Browser.Actions.Navigation;

public sealed class GoBackAction : IAutomationAction
{
    public ActionMetadata Metadata { get; } = new(
        ActionId: "go-back",
        DisplayName: "Go back",
        CategoryId: "navigation",
        CategoryName: "Navigation",
        IconKeyOrPath: "icon-navigation-back",
        Keywords: ["back", "history", "previous"],
        SortOrder: 30,
        IsContainer: false);
}