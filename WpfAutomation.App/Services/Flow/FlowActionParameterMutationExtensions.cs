using WpfAutomation.App.Models.Flow;

namespace WpfAutomation.App.Services.Flow;

public static class FlowActionParameterMutationExtensions
{
    public static FlowDocumentModel UpdateActionParameters<TParameters>(
        this FlowDocumentModel document,
        string nodeId,
        Func<TParameters, TParameters> update)
        where TParameters : ActionParameters
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return document;
        }

        var nodes = document.Nodes.ToList();
        var nodeIndex = nodes.FindIndex(node => string.Equals(node.NodeId, nodeId, StringComparison.Ordinal));
        if (nodeIndex < 0 || nodes[nodeIndex] is not FlowActionNodeModel actionNode || actionNode.ActionParameters is not TParameters typed)
        {
            return document;
        }

        nodes[nodeIndex] = actionNode with
        {
            ActionParameters = update(typed),
        };

        return document with
        {
            Nodes = nodes,
        };
    }

    public static FlowDocumentModel ReplaceActionParameters(
        this FlowDocumentModel document,
        string nodeId,
        ActionParameters parameters)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (string.IsNullOrWhiteSpace(nodeId) || parameters is null)
        {
            return document;
        }

        var nodes = document.Nodes.Select(node =>
        {
            if (node is not FlowActionNodeModel actionNode || !string.Equals(actionNode.NodeId, nodeId, StringComparison.Ordinal))
            {
                return node;
            }

            return (FlowNodeModel)(actionNode with { ActionParameters = parameters });
        }).ToList();

        return document with
        {
            Nodes = nodes,
        };
    }

    public static FlowDocumentModel ReplaceContainerParameters(
        this FlowDocumentModel document,
        string nodeId,
        ContainerParameters parameters)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (string.IsNullOrWhiteSpace(nodeId) || parameters is null)
        {
            return document;
        }

        var nodes = document.Nodes.Select(node =>
        {
            if (node is not FlowContainerNodeModel containerNode || !string.Equals(containerNode.NodeId, nodeId, StringComparison.Ordinal))
            {
                return node;
            }

            return (FlowNodeModel)(containerNode with { ContainerParameters = parameters });
        }).ToList();

        return document with
        {
            Nodes = nodes,
        };
    }
}
