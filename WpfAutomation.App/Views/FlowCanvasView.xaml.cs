using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfAutomation.App.Models;
using WpfAutomation.App.Models.Flow;
using WpfAutomation.App.ViewModels;

namespace WpfAutomation.App.Views;

public partial class FlowCanvasView : UserControl
{
    private Point? _selectionStart;
    private Point? _dragStart;
    private bool _isTranslatingSelection;
    private bool _canTranslateSelection;

    public FlowCanvasView()
    {
        InitializeComponent();
    }

    private FlowCanvasViewModel? ViewModel => DataContext as FlowCanvasViewModel;

    private void OnSurfaceDrop(object sender, DragEventArgs eventArgs)
    {
        if (ViewModel is null)
        {
            return;
        }

        if (!eventArgs.Data.GetDataPresent(typeof(UiActionDragRequest)))
        {
            return;
        }

        if (eventArgs.Data.GetData(typeof(UiActionDragRequest)) is not UiActionDragRequest request)
        {
            return;
        }

        var point = eventArgs.GetPosition(FlowSurface);
        var target = ResolveDropTarget(eventArgs, point);
        if (!target.IsAccepted)
        {
            ViewModel.ClearDropPreview();
            eventArgs.Effects = DragDropEffects.None;
            eventArgs.Handled = true;
            return;
        }

        ViewModel.HandleDrop(request, point, target.ToDropContext(point));
        ViewModel.ClearDropPreview();
        eventArgs.Handled = true;
    }

    private void OnSurfaceDragOver(object sender, DragEventArgs eventArgs)
    {
        if (ViewModel is null)
        {
            return;
        }

        if (!eventArgs.Data.GetDataPresent(typeof(UiActionDragRequest)))
        {
            ViewModel.ClearDropPreview();
            eventArgs.Effects = DragDropEffects.None;
            eventArgs.Handled = true;
            return;
        }

        var point = eventArgs.GetPosition(FlowSurface);
        var target = ResolveDropTarget(eventArgs, point);
        if (!target.IsAccepted)
        {
            ViewModel.ClearDropPreview();
            eventArgs.Effects = DragDropEffects.None;
            eventArgs.Handled = true;
            return;
        }

        ViewModel.SetDropPreview(target.PreviewPoint, target.EdgeId, target.LaneId);
        eventArgs.Effects = DragDropEffects.Copy;
        eventArgs.Handled = true;
    }

    private void OnSurfaceDragLeave(object sender, DragEventArgs eventArgs)
    {
        ViewModel?.ClearDropPreview();
        eventArgs.Handled = true;
    }

    private void OnSurfaceMouseMove(object sender, MouseEventArgs eventArgs)
    {
        if (ViewModel is null)
        {
            return;
        }

        var point = eventArgs.GetPosition(FlowSurface);
        var hoveredLane = FindAncestorWithDataContext<FlowLaneModel>(ResolvePointerSource(point));
        ViewModel.SetLaneHover(hoveredLane?.LaneId);
        ViewModel.UpdateEdgeHover(point);

        if (_selectionStart.HasValue && eventArgs.LeftButton == MouseButtonState.Pressed)
        {
            UpdateSelectionRectangle(_selectionStart.Value, point);
            return;
        }

        if (_dragStart.HasValue && _canTranslateSelection && eventArgs.LeftButton == MouseButtonState.Pressed)
        {
            var delta = point - _dragStart.Value;

            if (Math.Abs(delta.X) < 0.5 && Math.Abs(delta.Y) < 0.5)
            {
                return;
            }

            _dragStart = point;
            _isTranslatingSelection = true;
            ViewModel.TranslateSelection(delta.X, delta.Y);
        }
    }

    private void OnSurfaceMouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs)
    {
        Focus();

        if (ViewModel is null)
        {
            return;
        }

        // Let button clicks (for example container collapse) execute without starting node drag/select.
        if (eventArgs.OriginalSource is DependencyObject clickSource &&
            FindAncestor<System.Windows.Controls.Primitives.ButtonBase>(clickSource) is not null)
        {
            _dragStart = null;
            _canTranslateSelection = false;
            _isTranslatingSelection = false;
            return;
        }

        if (eventArgs.OriginalSource == FlowSurface)
        {
            _selectionStart = eventArgs.GetPosition(FlowSurface);
            SelectionRectangle.Visibility = Visibility.Visible;
            ViewModel.SetSelection([], []);
            return;
        }

        if (eventArgs.OriginalSource is DependencyObject source)
        {
            var presenter = FindAncestor<ContentPresenter>(source);
            if (presenter?.DataContext is WpfAutomation.App.Models.Flow.FlowNodeModel node)
            {
                if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    var selected = ViewModel.Document.Selection.SelectedNodeIds.ToList();
                    if (!selected.Contains(node.NodeId, StringComparer.Ordinal))
                    {
                        selected.Add(node.NodeId);
                    }

                    ViewModel.SetSelection(selected, []);
                }
                else
                {
                    ViewModel.SetSelection([node.NodeId], []);
                }

                _dragStart = eventArgs.GetPosition(FlowSurface);
                _canTranslateSelection = true;
            }
        }
    }

    private void OnNodeMouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (ViewModel is null || sender is not ContentPresenter { DataContext: WpfAutomation.App.Models.Flow.FlowNodeModel node })
        {
            return;
        }

        // If the click originated inside a button, let the button handle it without interference.
        if (eventArgs.OriginalSource is DependencyObject clickSource &&
            FindAncestor<System.Windows.Controls.Primitives.ButtonBase>(clickSource) is not null)
        {
            _dragStart = null;
            _canTranslateSelection = false;
            _isTranslatingSelection = false;
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            var ids = ViewModel.Document.Selection.SelectedNodeIds.ToList();
            if (!ids.Contains(node.NodeId, StringComparer.Ordinal))
            {
                ids.Add(node.NodeId);
            }

            ViewModel.SetSelection(ids, []);
        }
        else
        {
            ViewModel.SetSelection([node.NodeId], []);
        }

        _dragStart = eventArgs.GetPosition(FlowSurface);
        _canTranslateSelection = true;
        eventArgs.Handled = true;
    }

    private void OnSurfaceMouseLeftButtonUp(object sender, MouseButtonEventArgs eventArgs)
    {
        if (ViewModel is null)
        {
            return;
        }

        if (_selectionStart.HasValue)
        {
            var start = _selectionStart.Value;
            var end = eventArgs.GetPosition(FlowSurface);
            var selectionRect = new Rect(start, end);
            var selected = ViewModel.Nodes
                .Where(node => selectionRect.IntersectsWith(new Rect(node.Bounds.X, node.Bounds.Y, node.Bounds.Width, node.Bounds.Height)))
                .Select(node => node.NodeId)
                .ToList();

            ViewModel.SetSelection(selected, []);
        }

        if (_dragStart.HasValue && _isTranslatingSelection && ViewModel.Document.Selection.SelectedNodeIds.Count > 0)
        {
            var point = eventArgs.GetPosition(FlowSurface);
            var moveTarget = ResolveDropTarget(ResolvePointerSource(point) ?? eventArgs.OriginalSource as DependencyObject, point, rejectNodeInternals: false);
            if (moveTarget.IsAccepted && !string.IsNullOrWhiteSpace(moveTarget.LaneId))
            {
                var insertIndex = ResolveInsertIndex(ViewModel, moveTarget);
                ViewModel.MoveSelectionToLane(moveTarget.LaneId!, insertIndex);
            }
        }

        _selectionStart = null;
        _dragStart = null;
        _canTranslateSelection = false;
        _isTranslatingSelection = false;
        SelectionRectangle.Visibility = Visibility.Collapsed;

        var hoveredLane = FindAncestorWithDataContext<FlowLaneModel>(eventArgs.OriginalSource as DependencyObject);
        ViewModel.SetLaneHover(hoveredLane?.LaneId);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs eventArgs)
    {
        if (ViewModel is null)
        {
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && eventArgs.Key == Key.C)
        {
            ViewModel.CopySelectionCommand.Execute(null);
            eventArgs.Handled = true;
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && eventArgs.Key == Key.V)
        {
            ViewModel.PasteSelectionCommand.Execute(null);
            eventArgs.Handled = true;
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && eventArgs.Key == Key.Z)
        {
            ViewModel.UndoCommand.Execute(null);
            eventArgs.Handled = true;
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && eventArgs.Key == Key.Y)
        {
            ViewModel.RedoCommand.Execute(null);
            eventArgs.Handled = true;
            return;
        }

        if (eventArgs.Key == Key.Delete)
        {
            ViewModel.DeleteSelectionCommand.Execute(null);
            eventArgs.Handled = true;
        }
    }

    private void UpdateSelectionRectangle(Point start, Point end)
    {
        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        var width = Math.Abs(start.X - end.X);
        var height = Math.Abs(start.Y - end.Y);

        SelectionRectangle.Width = width;
        SelectionRectangle.Height = height;
        Canvas.SetLeft(SelectionRectangle, left);
        Canvas.SetTop(SelectionRectangle, top);
    }

    private static T? FindAncestor<T>(DependencyObject? dependencyObject)
        where T : DependencyObject
    {
        while (dependencyObject is not null)
        {
            if (dependencyObject is T typed)
            {
                return typed;
            }

            dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
        }

        return null;
    }

    private DropTargetResolution ResolveDropTarget(DragEventArgs eventArgs, Point point)
    {
        var hitSource = ResolvePointerSource(point) ?? eventArgs.OriginalSource as DependencyObject;
        return ResolveDropTarget(hitSource, point, rejectNodeInternals: true);
    }

    private DropTargetResolution ResolveDropTarget(DependencyObject? source, Point point, bool rejectNodeInternals)
    {
        if (ViewModel is null)
        {
            return DropTargetResolution.Reject(point);
        }

        var lane = FindAncestorWithDataContext<FlowLaneModel>(source);
        if (lane is not null && !string.IsNullOrWhiteSpace(lane.LaneId))
        {
            // Nested lane drops append to the lane in order to keep placement predictable.
            var laneEdgeId = string.Equals(lane.LaneId, FlowLaneIdentifiers.RootLaneId, StringComparison.Ordinal)
                ? ViewModel.ResolveDropEdge(point, lane.LaneId)
                : null;
            var previewPoint = ResolvePreviewPoint(point, laneEdgeId);
            return DropTargetResolution.Accept(previewPoint, laneEdgeId, lane.LaneId, lane.ParentContainerNodeId);
        }

        var hitNode = FindAncestorWithDataContext<FlowNodeModel>(source);
        if (hitNode is FlowContainerNodeModel containerNode)
        {
            var targetLane = ResolveContainerDropLane(containerNode);
            if (targetLane is not null)
            {
                return DropTargetResolution.Accept(point, edgeId: null, targetLane.LaneId, containerNode.NodeId);
            }
        }

        var containerByBounds = ResolveContainerAtPoint(point);
        if (containerByBounds is not null)
        {
            var targetLane = ResolveContainerDropLane(containerByBounds);
            if (targetLane is not null)
            {
                return DropTargetResolution.Accept(point, edgeId: null, targetLane.LaneId, containerByBounds.NodeId);
            }
        }

        if (rejectNodeInternals && hitNode is not null)
        {
            // Only lane bodies inside container nodes are valid insertion targets for child drops.
            return DropTargetResolution.Reject(point);
        }

        var rootEdgeId = ViewModel.ResolveDropEdge(point);
        var rootPreviewPoint = ResolvePreviewPoint(point, rootEdgeId);
        return DropTargetResolution.Accept(rootPreviewPoint, rootEdgeId, FlowLaneIdentifiers.RootLaneId, containerNodeId: null);
    }

    private static FlowLaneModel? ResolveContainerDropLane(FlowContainerNodeModel containerNode)
    {
        if (containerNode.ChildLanes.Count == 0)
        {
            return null;
        }

        return containerNode.ChildLanes
            .OrderBy(lane => lane.SortOrder)
            .FirstOrDefault(lane => lane.LaneKind == FlowLaneKind.LoopBody)
            ?? containerNode.ChildLanes.OrderBy(lane => lane.SortOrder).First();
    }

    private FlowContainerNodeModel? ResolveContainerAtPoint(Point point)
    {
        if (ViewModel is null)
        {
            return null;
        }

        return ViewModel.Nodes
            .OfType<FlowContainerNodeModel>()
            .Where(container =>
            {
                var bounds = container.Bounds;
                var rect = new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
                return rect.Contains(point);
            })
            // Prefer deepest visual container for nested scenarios.
            .OrderBy(container => container.Bounds.Width * container.Bounds.Height)
            .FirstOrDefault();
    }

    private static int ResolveInsertIndex(FlowCanvasViewModel viewModel, DropTargetResolution target)
    {
        if (string.IsNullOrWhiteSpace(target.LaneId))
        {
            return 0;
        }

        var laneNodeIds = ResolveLaneNodeIds(viewModel.Document, target.LaneId!);
        if (laneNodeIds.Count == 0)
        {
            return 0;
        }

        if (string.IsNullOrWhiteSpace(target.EdgeId))
        {
            return laneNodeIds.Count;
        }

        var hitEdge = viewModel.Document.Edges.FirstOrDefault(edge => string.Equals(edge.EdgeId, target.EdgeId, StringComparison.Ordinal));
        if (hitEdge is null)
        {
            return laneNodeIds.Count;
        }

        var fromIndex = laneNodeIds
            .Select((nodeId, index) => new { nodeId, index })
            .Where(entry => string.Equals(entry.nodeId, hitEdge.FromNodeId, StringComparison.Ordinal))
            .Select(entry => entry.index)
            .DefaultIfEmpty(-1)
            .First();
        return fromIndex >= 0 ? fromIndex + 1 : laneNodeIds.Count;
    }

    private static IReadOnlyList<string> ResolveLaneNodeIds(FlowDocumentModel document, string laneId)
    {
        if (string.Equals(laneId, FlowLaneIdentifiers.RootLaneId, StringComparison.Ordinal))
        {
            return document.RootLane.NodeIds;
        }

        return document.Nodes
            .OfType<FlowContainerNodeModel>()
            .SelectMany(container => container.ChildLanes)
            .FirstOrDefault(lane => string.Equals(lane.LaneId, laneId, StringComparison.Ordinal))
            ?.NodeIds ?? [];
    }

    private Point ResolvePreviewPoint(Point fallbackPoint, string? edgeId)
    {
        if (ViewModel is null || string.IsNullOrWhiteSpace(edgeId))
        {
            return fallbackPoint;
        }

        var edgeMidpoint = ViewModel.ResolveEdgeMidpoint(edgeId);
        return edgeMidpoint ?? fallbackPoint;
    }

    private DependencyObject? ResolvePointerSource(Point point)
    {
        return FlowSurface.InputHitTest(point) as DependencyObject;
    }

    private static TData? FindAncestorWithDataContext<TData>(DependencyObject? dependencyObject)
        where TData : class
    {
        while (dependencyObject is not null)
        {
            if (dependencyObject is FrameworkElement element && element.DataContext is TData data)
            {
                return data;
            }

            dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
        }

        return null;
    }

    private sealed record DropTargetResolution(bool IsAccepted, Point PreviewPoint, string? EdgeId, string? LaneId, string? ContainerNodeId)
    {
        public FlowDropContextModel ToDropContext(Point dropPoint)
        {
            return new FlowDropContextModel
            {
                DropPoint = dropPoint,
                TargetEdgeId = EdgeId,
                TargetLaneId = LaneId,
                TargetContainerNodeId = ContainerNodeId,
            };
        }

        public static DropTargetResolution Accept(Point previewPoint, string? edgeId, string? laneId, string? containerNodeId)
        {
            return new DropTargetResolution(true, previewPoint, edgeId, laneId, containerNodeId);
        }

        public static DropTargetResolution Reject(Point previewPoint)
        {
            return new DropTargetResolution(false, previewPoint, EdgeId: null, LaneId: null, ContainerNodeId: null);
        }
    }
}
