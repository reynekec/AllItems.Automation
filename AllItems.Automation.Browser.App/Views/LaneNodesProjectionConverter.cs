using System.Globalization;
using System.Windows.Data;
using AllItems.Automation.Browser.App.Models.Flow;

namespace AllItems.Automation.Browser.App.Views;

public sealed class LaneNodesProjectionConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not IReadOnlyList<string> laneNodeIds || values[1] is not IReadOnlyList<FlowNodeModel> allNodes)
        {
            return Array.Empty<FlowNodeModel>();
        }

        if (laneNodeIds.Count == 0 || allNodes.Count == 0)
        {
            return Array.Empty<FlowNodeModel>();
        }

        var nodeLookup = allNodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        var resolved = new List<FlowNodeModel>(laneNodeIds.Count);

        foreach (var nodeId in laneNodeIds)
        {
            if (nodeLookup.TryGetValue(nodeId, out var node))
            {
                resolved.Add(node);
            }
        }

        return resolved;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
