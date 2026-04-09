using WpfAutomation.App.Models.Flow;

namespace WpfAutomation.App.Services.Flow;

public static class FlowDragDropPolicy
{
    public static bool ShouldCommitNodeMove(string? laneId, string? edgeId)
    {
        if (string.IsNullOrWhiteSpace(laneId))
        {
            return false;
        }

        if (string.Equals(laneId, FlowLaneIdentifiers.RootLaneId, StringComparison.Ordinal))
        {
            return !string.IsNullOrWhiteSpace(edgeId);
        }

        return true;
    }
}
