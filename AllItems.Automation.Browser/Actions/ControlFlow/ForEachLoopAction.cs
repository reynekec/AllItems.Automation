using AllItems.Automation.Browser.Core.Abstractions.Actions;

namespace AllItems.Automation.Browser.Actions.ControlFlow;

public sealed class ForEachLoopAction : IAutomationAction
{
    public ActionMetadata Metadata { get; } = new(
        ActionId: "for-each-loop",
        DisplayName: "ForEach Loop",
        CategoryId: "control-flow",
        CategoryName: "Control Flow",
        IconKeyOrPath: "icon-control-flow-foreach",
        Keywords: ["foreach", "loop", "items", "enumerate"],
        SortOrder: 20,
        IsContainer: true);
}
