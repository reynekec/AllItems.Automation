using System.Windows;
using AllItems.Automation.Browser.App.Models.Flow;

namespace AllItems.Automation.Browser.App.Services.Flow;

public interface IFlowEdgeRoutingService
{
    IReadOnlyList<Point> BuildRoute(FlowNodeBounds from, FlowNodeBounds to);
}

public interface IFlowHitTestService
{
    string? HitTestEdge(IReadOnlyList<FlowEdgeVisualModel> edges, Point position, double tolerance);
}

public interface IFlowLayoutService
{
    FlowDocumentModel Recalculate(FlowDocumentModel document);

    FlowDocumentModel AutoLayout(FlowDocumentModel document);
}

public sealed class FlowEdgeRoutingService : IFlowEdgeRoutingService
{
    public IReadOnlyList<Point> BuildRoute(FlowNodeBounds from, FlowNodeBounds to)
    {
        var start = new Point(from.X + (from.Width / 2), from.Y + from.Height);
        var end = new Point(to.X + (to.Width / 2), to.Y);

        // Keep a short vertical run from the source and target to mimic branch rails.
        var startLegY = start.Y + 18;
        var endLegY = Math.Max(to.Y - 18, startLegY + 8);
        var trunkY = (startLegY + endLegY) / 2;

        return
        [
            start,
            new Point(start.X, startLegY),
            new Point(start.X, trunkY),
            new Point(end.X, trunkY),
            new Point(end.X, endLegY),
            end,
        ];
    }
}

public sealed class FlowHitTestService : IFlowHitTestService
{
    public string? HitTestEdge(IReadOnlyList<FlowEdgeVisualModel> edges, Point position, double tolerance)
    {
        string? closestEdgeId = null;
        var closestDistance = double.MaxValue;

        foreach (var edge in edges)
        {
            var distance = edge.RoutePoints.Count >= 2
                ? ComputePolylineDistance(edge.RoutePoints, position)
                : Math.Sqrt(Math.Pow(edge.MidpointX - position.X, 2) + Math.Pow(edge.MidpointY - position.Y, 2));

            if (distance <= tolerance && distance < closestDistance)
            {
                closestDistance = distance;
                closestEdgeId = edge.EdgeId;
            }
        }

        return closestEdgeId;
    }

    private static double ComputePolylineDistance(IReadOnlyList<Point> points, Point position)
    {
        var minDistance = double.MaxValue;

        for (var index = 0; index < points.Count - 1; index++)
        {
            var distance = DistanceToSegment(position, points[index], points[index + 1]);
            if (distance < minDistance)
            {
                minDistance = distance;
            }
        }

        return minDistance;
    }

    private static double DistanceToSegment(Point position, Point start, Point end)
    {
        var deltaX = end.X - start.X;
        var deltaY = end.Y - start.Y;
        var segmentLengthSquared = (deltaX * deltaX) + (deltaY * deltaY);

        if (segmentLengthSquared <= double.Epsilon)
        {
            return Math.Sqrt(Math.Pow(position.X - start.X, 2) + Math.Pow(position.Y - start.Y, 2));
        }

        var projection = ((position.X - start.X) * deltaX + (position.Y - start.Y) * deltaY) / segmentLengthSquared;
        var clampedProjection = Math.Max(0d, Math.Min(1d, projection));
        var projectedX = start.X + (clampedProjection * deltaX);
        var projectedY = start.Y + (clampedProjection * deltaY);

        return Math.Sqrt(Math.Pow(position.X - projectedX, 2) + Math.Pow(position.Y - projectedY, 2));
    }
}

public sealed class FlowLayoutService : IFlowLayoutService
{
    private const double VerticalGap = 28;
    private const double ActionDefaultWidth = 380;
    private const double ActionDefaultHeight = 50;
    private const double ContainerDefaultWidth = 420;
    private const double ContainerHeaderHeight = 52;
    private const double LaneHeaderHeight = 26;
    private const double LaneGap = 10;
    private const double LaneIndent = 18;
    private const double LaneInsetTop = 10;

    // These constants mirror the XAML container DataTemplate metrics so that
    // Bounds.Height always matches the actual WPF auto-sized node height.
    private const double ContainerCollapsedHeight = 42.0;   // Grid margin(8+10) + header row(22) + border(2)
    private const double ContainerExpandedBase = 52.0;      // same without row-1 content
    private const double ContainerLaneHeight = 37.0;        // Border margin(5)+border(2)+padding(16)+text(14)
    private const double LaneContentBottomPadding = 18.0;
    private const double ChildHorizontalInset = 18.0;
    private const double MinNestedNodeWidth = 220.0;

    public FlowDocumentModel Recalculate(FlowDocumentModel document)
    {
        var positionedNodes = document.Nodes
            .ToDictionary(node => node.NodeId, NormalizeInitialBounds, StringComparer.Ordinal);

        var visitedContainers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var container in positionedNodes.Values.OfType<FlowContainerNodeModel>())
        {
            if (!string.IsNullOrWhiteSpace(container.ParentContainerNodeId))
            {
                continue;
            }

            LayoutContainerNode(container.NodeId, positionedNodes, visitedContainers);
        }

        foreach (var container in positionedNodes.Values.OfType<FlowContainerNodeModel>())
        {
            if (visitedContainers.Contains(container.NodeId))
            {
                continue;
            }

            LayoutContainerNode(container.NodeId, positionedNodes, visitedContainers);
        }

        var updatedNodes = document.Nodes
            .Select(node => positionedNodes.TryGetValue(node.NodeId, out var positioned) ? positioned : node)
            .ToList();

        return document with { Nodes = updatedNodes };
    }

    private static FlowNodeModel NormalizeInitialBounds(FlowNodeModel node)
    {
        var width = node.Bounds.Width > 0
            ? node.Bounds.Width
            : (node is FlowContainerNodeModel ? ContainerDefaultWidth : ActionDefaultWidth);
        var height = node.Bounds.Height > 0
            ? node.Bounds.Height
            : (node is FlowContainerNodeModel ? ContainerExpandedBase + ContainerLaneHeight : ActionDefaultHeight);

        return node with
        {
            Bounds = node.Bounds with
            {
                Width = width,
                Height = height,
            },
        };
    }

    private static void LayoutContainerNode(
        string containerNodeId,
        IDictionary<string, FlowNodeModel> positionedNodes,
        ISet<string> visitedContainers)
    {
        if (visitedContainers.Contains(containerNodeId))
        {
            return;
        }

        if (!positionedNodes.TryGetValue(containerNodeId, out var unresolvedNode) || unresolvedNode is not FlowContainerNodeModel container)
        {
            return;
        }

        visitedContainers.Add(containerNodeId);

        if (container.IsCollapsed)
        {
            positionedNodes[container.NodeId] = container with
            {
                Bounds = container.Bounds with { Height = ContainerCollapsedHeight },
            };
            return;
        }

        var laneCursorY = container.Bounds.Y + ContainerHeaderHeight + LaneInsetTop;
        var laneVisualHeightTotal = 0d;
        var childNodeStartX = container.Bounds.X + LaneIndent;
        var availableChildWidth = Math.Max(MinNestedNodeWidth, container.Bounds.Width - LaneIndent - ChildHorizontalInset);
        var updatedLanes = new List<FlowLaneModel>(container.ChildLanes.Count);

        foreach (var lane in container.ChildLanes.OrderBy(lane => lane.SortOrder))
        {
            var laneContentTopY = laneCursorY + LaneHeaderHeight;
            var childCursorY = laneContentTopY;
            var contentHeight = 0d;

            foreach (var nodeId in lane.NodeIds)
            {
                if (!positionedNodes.TryGetValue(nodeId, out var laneNode))
                {
                    continue;
                }

                var laneNodeWidth = ResolveNodeWidth(laneNode);
                var normalizedLaneNode = laneNode with
                {
                    Bounds = laneNode.Bounds with
                    {
                        X = ResolveCenteredLaneX(childNodeStartX, availableChildWidth, laneNodeWidth),
                        Y = childCursorY,
                        Width = laneNodeWidth,
                        Height = laneNode.Bounds.Height > 0
                            ? laneNode.Bounds.Height
                            : (laneNode is FlowContainerNodeModel ? ContainerExpandedBase + ContainerLaneHeight : ActionDefaultHeight),
                    },
                };

                positionedNodes[nodeId] = normalizedLaneNode;

                if (normalizedLaneNode is FlowContainerNodeModel nestedContainer)
                {
                    LayoutContainerNode(nestedContainer.NodeId, positionedNodes, visitedContainers);
                    normalizedLaneNode = positionedNodes[nestedContainer.NodeId];
                }

                childCursorY += normalizedLaneNode.Bounds.Height + VerticalGap;
                contentHeight = childCursorY - laneContentTopY;
            }

            if (contentHeight > 0)
            {
                contentHeight = Math.Max(0, contentHeight - VerticalGap + LaneContentBottomPadding);
            }

            var laneVisualHeight = contentHeight > 0
                ? ContainerLaneHeight + contentHeight
                : ContainerLaneHeight;
            updatedLanes.Add(lane with { VisualHeight = laneVisualHeight });
            laneVisualHeightTotal += laneVisualHeight;
            laneCursorY += laneVisualHeight + LaneGap;
        }

        var containerHeight = ContainerExpandedBase + laneVisualHeightTotal;
        positionedNodes[container.NodeId] = container with
        {
            ChildLanes = updatedLanes,
            Bounds = container.Bounds with { Height = containerHeight },
        };
    }

    private static double ComputeVisualHeight(FlowNodeModel node)
    {
        if (node is FlowContainerNodeModel container)
        {
            if (container.IsCollapsed)
                return ContainerCollapsedHeight;

            return ContainerExpandedBase + (container.ChildLanes.Count * ContainerLaneHeight);
        }

        return node.Bounds.Height > 0 ? node.Bounds.Height : 50.0;
    }

    public FlowDocumentModel AutoLayout(FlowDocumentModel document)
    {
        if (document.Nodes.Count == 0)
        {
            return document;
        }

        var positionedNodes = document.Nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        _ = LayoutLane(document.RootLane.NodeIds, startX: 64, startY: 64, positionedNodes);

        return document with
        {
            Nodes = document.Nodes
                .Select(node => positionedNodes.TryGetValue(node.NodeId, out var positioned) ? positioned : node)
                .ToList(),
        };
    }

    private static LaneLayoutResult LayoutLane(IReadOnlyList<string> nodeIds, double startX, double startY, Dictionary<string, FlowNodeModel> positionedNodes)
    {
        var cursorY = startY;
        var maxWidth = 0d;
        var laneWidth = ResolveLaneWidth(nodeIds, positionedNodes);

        foreach (var nodeId in nodeIds)
        {
            if (!positionedNodes.TryGetValue(nodeId, out var node))
            {
                continue;
            }

            if (node is FlowContainerNodeModel container)
            {
                var containerWidth = ResolveNodeWidth(container);
                var containerX = ResolveCenteredLaneX(startX, laneWidth, containerWidth);
                var laneCursorY = cursorY + ContainerHeaderHeight + LaneInsetTop;

                foreach (var lane in container.ChildLanes.OrderBy(lane => lane.SortOrder))
                {
                    var laneStartY = laneCursorY + LaneHeaderHeight;
                    var laneLayout = LayoutLane(lane.NodeIds, containerX + LaneIndent, laneStartY, positionedNodes);
                    var laneContentHeight = Math.Max(38, laneLayout.NextY - laneStartY);
                    laneCursorY = laneStartY + laneContentHeight + LaneGap;
                }

                var containerHeight = container.Bounds.Height > 0
                    ? container.Bounds.Height
                    : ComputeVisualHeight(container);

                positionedNodes[nodeId] = container with
                {
                    Bounds = container.Bounds with
                    {
                        X = containerX,
                        Y = cursorY,
                        Width = containerWidth,
                        Height = containerHeight,
                    },
                };

                cursorY += containerHeight + VerticalGap;
                maxWidth = Math.Max(maxWidth, containerWidth);
                continue;
            }

            var width = ResolveNodeWidth(node);
            var height = node.Bounds.Height > 0 ? node.Bounds.Height : ActionDefaultHeight;
            positionedNodes[nodeId] = node with
            {
                Bounds = node.Bounds with
                {
                    X = ResolveCenteredLaneX(startX, laneWidth, width),
                    Y = cursorY,
                    Width = width,
                    Height = height,
                },
            };

            cursorY += height + VerticalGap;
            maxWidth = Math.Max(maxWidth, width);
        }

        return new LaneLayoutResult(cursorY, maxWidth);
    }

    private static double ResolveLaneWidth(IReadOnlyList<string> nodeIds, Dictionary<string, FlowNodeModel> positionedNodes)
    {
        var laneWidth = 0d;

        foreach (var nodeId in nodeIds)
        {
            if (!positionedNodes.TryGetValue(nodeId, out var node))
            {
                continue;
            }

            laneWidth = Math.Max(laneWidth, ResolveNodeWidth(node));
        }

        return laneWidth;
    }

    private static double ResolveNodeWidth(FlowNodeModel node)
    {
        var fallbackWidth = node is FlowContainerNodeModel ? ContainerDefaultWidth : ActionDefaultWidth;
        return node.Bounds.Width > 0 ? node.Bounds.Width : fallbackWidth;
    }

    private static double ResolveCenteredLaneX(double startX, double laneWidth, double nodeWidth)
    {
        return startX + Math.Max(0d, (laneWidth - nodeWidth) / 2d);
    }

    private readonly record struct LaneLayoutResult(double NextY, double MaxWidth);
}
