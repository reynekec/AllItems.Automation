using AllItems.Automation.Browser.Core.Abstractions.Actions;

namespace AllItems.Automation.Browser.Actions.Browser;

public sealed class CloseBrowserAction : IAutomationAction
{
    public ActionMetadata Metadata { get; } = new(
        ActionId: "close-browser",
        DisplayName: "Close browser",
        CategoryId: "browser",
        CategoryName: "Browser",
        IconKeyOrPath: "icon-browser-close",
        Keywords: ["quit", "exit", "dispose"],
        SortOrder: 20,
        IsContainer: false);
}