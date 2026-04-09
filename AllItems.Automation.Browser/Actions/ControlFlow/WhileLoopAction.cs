using WpfAutomation.Core.Abstractions.Actions;

namespace AllItems.Automation.Browser.Actions.ControlFlow;

public sealed class WhileLoopAction : IAutomationAction
{
    public ActionMetadata Metadata { get; } = new(
        ActionId: "while-loop",
        DisplayName: "While Loop",
        CategoryId: "control-flow",
        CategoryName: "Control Flow",
        IconKeyOrPath: "icon-control-flow-while",
        Keywords: ["while", "loop", "condition", "repeat"],
        SortOrder: 30,
        IsContainer: true);
}
