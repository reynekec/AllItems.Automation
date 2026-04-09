namespace WpfAutomation.App.NodeInspector.Contracts;

public interface INodeInspectorDescriptor
{
    string NodeId { get; }

    string ActionId { get; }

    string DisplayName { get; }

    string CategoryName { get; }
}
