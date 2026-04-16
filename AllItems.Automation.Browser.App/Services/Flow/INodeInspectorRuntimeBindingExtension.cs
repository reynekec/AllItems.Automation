using AllItems.Automation.Browser.App.Models.Flow;

namespace AllItems.Automation.Browser.App.Services.Flow;

// Extension point for a follow-up phase to map editor-side action parameters into runtime execution commands.
public interface INodeInspectorRuntimeBindingExtension
{
    bool TryCreateRuntimeBinding(FlowActionNodeModel node, out object? runtimeBinding);
}

public sealed class NullNodeInspectorRuntimeBindingExtension : INodeInspectorRuntimeBindingExtension
{
    public bool TryCreateRuntimeBinding(FlowActionNodeModel node, out object? runtimeBinding)
    {
        runtimeBinding = null;
        return false;
    }
}
