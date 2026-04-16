using AllItems.Automation.Browser.Core.Abstractions.Actions;

namespace AllItems.Automation.Browser.Actions.Browser;

public sealed class OpenBrowserAction : IAutomationAction
{
    public ActionMetadata Metadata { get; } = new(
        ActionId: "open-browser",
        DisplayName: "Open browser",
        CategoryId: "browser",
        CategoryName: "Browser",
        IconKeyOrPath: "icon-browser-open",
        Keywords: ["launch", "start", "chromium", "firefox", "webkit"],
        SortOrder: 10,
        IsContainer: false);
}