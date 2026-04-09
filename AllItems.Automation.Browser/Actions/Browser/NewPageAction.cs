using WpfAutomation.Core.Abstractions.Actions;

namespace AllItems.Automation.Browser.Actions.Browser;

public sealed class NewPageAction : IAutomationAction
{
    public ActionMetadata Metadata { get; } = new(
        ActionId: "new-page",
        DisplayName: "Create page",
        CategoryId: "browser",
        CategoryName: "Browser",
        IconKeyOrPath: "icon-page-new",
        Keywords: ["tab", "new", "page"],
        SortOrder: 30,
        IsContainer: false);
}