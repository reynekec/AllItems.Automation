using FluentAssertions;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using AllItems.Automation.Browser.App.Models;
using AllItems.Automation.Browser.App.Models.Flow;
using AllItems.Automation.Browser.App.Services.Flow;
using AllItems.Automation.Browser.App.ViewModels;
using AllItems.Automation.Browser.Core.Diagnostics;

namespace WpfAutomation.Core.Tests;

public sealed class CanvasFlowTests
{
    [Fact]
    public void AddActionNode_InsertOnEdge_SplitsEdgeAndKeepsGraphValid()
    {
        var service = new FlowEditingService();
        var document = service.CreateEmptyDocument();

        document = service.AddActionNode(document, CreateRequest("open"), 20, 20);
        document = service.AddActionNode(document, CreateRequest("navigate"), 20, 140);
        document.Edges.Should().HaveCount(1);

        var edgeId = document.Edges[0].EdgeId;
        document = service.AddActionNode(document, CreateRequest("click"), 20, 240, edgeId);

        document.Edges.Should().HaveCount(2);
        document.RootLane.NodeIds.Should().HaveCount(3);

        var validation = FlowDocumentValidator.Validate(document);
        validation.IsValid.Should().BeTrue(string.Join(" | ", validation.Errors));
    }

    [Fact]
    public void AddActionNode_InsertContainerOnEdge_SplitsEdgeAndKeepsRootOrdering()
    {
        var service = new FlowEditingService();
        var document = service.CreateEmptyDocument();

        document = service.AddActionNode(document, CreateRequest("open"), 20, 20);
        document = service.AddActionNode(document, CreateRequest("navigate"), 20, 140);
        var edgeId = document.Edges.Single().EdgeId;

        document = service.AddActionNode(document, CreateRequest("group-like", isContainer: true), 20, 240, edgeId);

        document.RootLane.NodeIds.Should().HaveCount(3);
        var insertedNodeId = document.RootLane.NodeIds[1];
        document.Nodes.Should().ContainSingle(node => node.NodeId == insertedNodeId && node is FlowContainerNodeModel);
        document.Edges.Should().HaveCount(2);
    }

    [Fact]
    public void AddActionNode_InsertOnEdge_ShiftsDownstreamRootNodesToCreateSpace()
    {
        var service = new FlowEditingService();
        var document = service.CreateEmptyDocument();

        document = service.AddActionNode(document, CreateRequest("one"), 20, 20);
        document = service.AddActionNode(document, CreateRequest("two"), 20, 140);
        document = service.AddActionNode(document, CreateRequest("three"), 20, 260);

        var secondNodeId = document.RootLane.NodeIds[1];
        var thirdNodeId = document.RootLane.NodeIds[2];
        var secondBefore = document.Nodes.Single(node => node.NodeId == secondNodeId);
        var thirdBefore = document.Nodes.Single(node => node.NodeId == thirdNodeId);
        var edgeId = document.Edges.Single(edge => edge.FromNodeId == document.RootLane.NodeIds[0] && edge.ToNodeId == secondNodeId).EdgeId;

        document = service.AddActionNode(document, CreateRequest("inserted"), 20, 100, edgeId);

        var inserted = document.Nodes.Single(node => node.DisplayLabel == "inserted");
        var secondAfter = document.Nodes.Single(node => node.NodeId == secondNodeId);
        var thirdAfter = document.Nodes.Single(node => node.NodeId == thirdNodeId);

        secondAfter.Bounds.Y.Should().Be(inserted.Bounds.Y + inserted.Bounds.Height + 28);
        (thirdAfter.Bounds.Y - secondAfter.Bounds.Y).Should().Be(thirdBefore.Bounds.Y - secondBefore.Bounds.Y);
    }

    [Fact]
    public void AddActionNode_InsertContainerOnEdge_CentersInsertedNodeUnderSourceNode()
    {
        var service = new FlowEditingService();
        var document = service.CreateEmptyDocument();

        document = service.AddActionNode(document, CreateRequest("one"), 120, 20);
        document = service.AddActionNode(document, CreateRequest("two"), 520, 240);

        var sourceNodeId = document.RootLane.NodeIds[0];
        var targetNodeId = document.RootLane.NodeIds[1];
        var sourceBefore = document.Nodes.Single(node => node.NodeId == sourceNodeId);
        var edgeId = document.Edges.Single(edge => edge.FromNodeId == sourceNodeId && edge.ToNodeId == targetNodeId).EdgeId;

        document = service.AddActionNode(document, CreateRequest("inserted", isContainer: true), 900, 120, edgeId);

        var inserted = document.Nodes.Single(node => node.DisplayLabel == "inserted");
        var sourceCenterX = sourceBefore.Bounds.X + (sourceBefore.Bounds.Width / 2d);
        var insertedCenterX = inserted.Bounds.X + (inserted.Bounds.Width / 2d);
        insertedCenterX.Should().Be(sourceCenterX);
    }

    [Fact]
    public void AddActionNode_DropOnBlankArea_UsesDefaultGapForSameWidthNodes()
    {
        var viewModel = FlowCanvasViewModel.CreateDefault(new DiagnosticsService());

        viewModel.HandleDrop(CreateRequest("first"), new Point(150, 50));
        viewModel.HandleDrop(CreateRequest("second"), new Point(900, 900));

        var first = viewModel.Nodes.Single(node => node.DisplayLabel == "first");
        var second = viewModel.Nodes.Single(node => node.DisplayLabel == "second");

        second.Bounds.X.Should().Be(first.Bounds.X);
        second.Bounds.Y.Should().Be(first.Bounds.Y + first.Bounds.Height + 28);
    }

    [Fact]
    public void AddActionNode_DropContainerOnBlankArea_CentersUnderPreviousRootNode()
    {
        var viewModel = FlowCanvasViewModel.CreateDefault(new DiagnosticsService());

        viewModel.HandleDrop(CreateRequest("first"), new Point(150, 50));
        viewModel.HandleDrop(CreateRequest("group", isContainer: true), new Point(900, 900));

        var first = viewModel.Nodes.Single(node => node.DisplayLabel == "first");
        var container = viewModel.Nodes.Single(node => node.DisplayLabel == "group");

        var firstCenterX = first.Bounds.X + (first.Bounds.Width / 2d);
        var containerCenterX = container.Bounds.X + (container.Bounds.Width / 2d);
        containerCenterX.Should().Be(firstCenterX);
        container.Bounds.Y.Should().Be(first.Bounds.Y + first.Bounds.Height + 28);
    }

    [Fact]
    public void AddContainerNode_AppendsCenteredUnderPreviousRootNode()
    {
        var service = new FlowEditingService();
        var document = service.CreateEmptyDocument();

        document = service.AddActionNode(document, CreateRequest("first"), 80, 20);
        document = service.AddContainerNode(document, FlowContainerKind.Group, 900, 240);

        var first = document.Nodes.Single(node => node.DisplayLabel == "first");
        var container = document.Nodes.OfType<FlowContainerNodeModel>().Single();

        var firstCenterX = first.Bounds.X + (first.Bounds.Width / 2d);
        var containerCenterX = container.Bounds.X + (container.Bounds.Width / 2d);
        containerCenterX.Should().Be(firstCenterX);
    }

    [Fact]
    public void AddActionNode_InsertOnEdge_DoesNotMoveDownstream_WhenThereIsAlreadyEnoughSpace()
    {
        var service = new FlowEditingService();
        var document = service.CreateEmptyDocument();

        document = service.AddActionNode(document, CreateRequest("one"), 20, 20);
        document = service.AddActionNode(document, CreateRequest("two"), 20, 320);

        var secondNodeId = document.RootLane.NodeIds[1];
        var secondBefore = document.Nodes.Single(node => node.NodeId == secondNodeId);
        var edgeId = document.Edges.Single(edge => edge.FromNodeId == document.RootLane.NodeIds[0] && edge.ToNodeId == secondNodeId).EdgeId;

        document = service.AddActionNode(document, CreateRequest("inserted"), 20, 120, edgeId);

        var secondAfter = document.Nodes.Single(node => node.NodeId == secondNodeId);
        secondAfter.Bounds.Y.Should().Be(secondBefore.Bounds.Y);
    }

    [Fact]
    public void DeleteSelection_RepairsGraphByConnectingPredecessorToSuccessor()
    {
        var service = new FlowEditingService();
        var document = service.CreateEmptyDocument();

        document = service.AddActionNode(document, CreateRequest("one"), 20, 20);
        document = service.AddActionNode(document, CreateRequest("two"), 20, 140);
        document = service.AddActionNode(document, CreateRequest("three"), 20, 260);

        var middleNodeId = document.RootLane.NodeIds[1];
        document = service.DeleteSelection(document, [middleNodeId], []);

        document.RootLane.NodeIds.Should().HaveCount(2);
        document.Edges.Should().ContainSingle(edge => edge.FromNodeId == document.RootLane.NodeIds[0] && edge.ToNodeId == document.RootLane.NodeIds[1]);
    }

    [Fact]
    public void MoveNodesToContainerLane_UpdatesOwnershipAndContainerBounds()
    {
        var service = new FlowEditingService();
        var document = service.CreateEmptyDocument();

        document = service.AddActionNode(document, CreateRequest("one"), 20, 20);
        document = service.AddActionNode(document, CreateRequest("two"), 20, 160);
        document = service.AddContainerNode(document, FlowContainerKind.Group, 280, 40);

        var container = document.Nodes.OfType<FlowContainerNodeModel>().Single();
        var originalHeight = container.Bounds.Height;
        var laneId = container.ChildLanes[0].LaneId;

        var firstNodeId = document.RootLane.NodeIds[0];
        document = service.MoveNodesToLane(document, [firstNodeId], laneId, 0);

        var movedNode = document.Nodes.Single(node => node.NodeId == firstNodeId);
        movedNode.ParentContainerNodeId.Should().Be(container.NodeId);

        var resizedContainer = document.Nodes.OfType<FlowContainerNodeModel>().Single();
        resizedContainer.Bounds.Height.Should().BeGreaterThanOrEqualTo(140);
    }

    [Fact]
    public async Task Persistence_RoundTripsDocumentAndSchemaValidationGuardsForwardVersions()
    {
        var persistence = new FlowPersistenceService();
        var editing = new FlowEditingService();
        var document = editing.CreateEmptyDocument("RoundTrip");
        document = editing.AddActionNode(document, CreateRequest("roundtrip-container", isContainer: true), 10, 20);
        var droppedContainer = document.Nodes.OfType<FlowContainerNodeModel>().Single();
        document = document with
        {
            Nodes = document.Nodes.Select(node =>
            {
                if (node is not FlowContainerNodeModel container || !string.Equals(container.NodeId, droppedContainer.NodeId, StringComparison.Ordinal))
                {
                    return node;
                }

                return (FlowNodeModel)(container with
                {
                    IsCollapsed = true,
                    IsExpanded = false,
                });
            }).ToList(),
        };

        var tempFile = Path.Combine(Path.GetTempPath(), $"flow-{Guid.NewGuid():N}.flow.json");

        await persistence.SaveAsync(document, tempFile);
        var loaded = await persistence.OpenAsync(tempFile);

        loaded.DocumentId.Should().Be(document.DocumentId);
        loaded.Nodes.Should().HaveCount(1);
        var loadedContainer = loaded.Nodes.OfType<FlowContainerNodeModel>().Single();
        loadedContainer.IsCollapsed.Should().BeTrue();
        loadedContainer.IsExpanded.Should().BeFalse();

        var snapshot = FlowSnapshotMapper.ToSnapshot(document) with { SchemaVersion = FlowDocumentSchema.CurrentVersion + 10 };
        var serializerOptions = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        };

        File.WriteAllText(tempFile, System.Text.Json.JsonSerializer.Serialize(snapshot, serializerOptions));

        var open = async () => await persistence.OpenAsync(tempFile);
        await open.Should().ThrowAsync<InvalidOperationException>();

        if (File.Exists(tempFile))
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ViewModel_HandlesHoverAndUndoRedoAndClipboard()
    {
        var viewModel = FlowCanvasViewModel.CreateDefault(new DiagnosticsService());

        viewModel.HandleDrop(CreateRequest("first"), new Point(20, 20));
        viewModel.HandleDrop(CreateRequest("second"), new Point(20, 140));
        viewModel.UpdateEdgeHover(new Point(viewModel.EdgeVisuals[0].MidpointX, viewModel.EdgeVisuals[0].MidpointY));

        viewModel.InteractionState.IsDropInsertPreviewVisible.Should().BeTrue();

        viewModel.SetSelection([viewModel.Document.RootLane.NodeIds[0]], []);
        viewModel.CopySelectionCommand.Execute(null);
        viewModel.PasteSelectionCommand.Execute(null);
        var afterPasteCount = viewModel.Nodes.Count;

        viewModel.UndoCommand.Execute(null);
        viewModel.Nodes.Count.Should().BeLessThan(afterPasteCount);

        viewModel.RedoCommand.Execute(null);
        viewModel.Nodes.Count.Should().Be(afterPasteCount);
    }

    [Fact]
    public void ViewModel_EdgeHover_ActivatesOnNonMidpointSegment()
    {
        var viewModel = FlowCanvasViewModel.CreateDefault(new DiagnosticsService());

        viewModel.HandleDrop(CreateRequest("first"), new Point(20, 20));
        viewModel.HandleDrop(CreateRequest("second"), new Point(360, 420));

        var route = viewModel.EdgeVisuals[0].RoutePoints;
        var start = route[0];
        var next = route[1];
        var testPoint = new Point(start.X, (start.Y + next.Y) / 2d);

        viewModel.UpdateEdgeHover(testPoint);

        viewModel.InteractionState.IsDropInsertPreviewVisible.Should().BeTrue();
        viewModel.InteractionState.HoveredEdgeId.Should().Be(viewModel.EdgeVisuals[0].EdgeId);
    }

    [Fact]
    public void ViewModel_EdgePathData_UsesInvariantFormattingForFractionalCoordinates()
    {
        var previousCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

            var viewModel = FlowCanvasViewModel.CreateDefault(new DiagnosticsService());
            viewModel.HandleDrop(CreateRequest("first"), new Point(20, 20));
            viewModel.HandleDrop(CreateRequest("second"), new Point(360, 420));

            var firstNodeId = viewModel.Document.RootLane.NodeIds[0];
            viewModel.SetSelection([firstNodeId], []);
            viewModel.TranslateSelection(10.5, 5.25);

            var pathData = viewModel.EdgeVisuals[0].PathData;

            pathData.Should().Contain("220.5");
            var parse = () => Geometry.Parse(pathData);
            parse.Should().NotThrow();
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public void DragDropPolicy_RootLaneRequiresExplicitEdgeToCommitMove()
    {
        FlowDragDropPolicy.ShouldCommitNodeMove(FlowLaneIdentifiers.RootLaneId, edgeId: null).Should().BeFalse();
        FlowDragDropPolicy.ShouldCommitNodeMove(FlowLaneIdentifiers.RootLaneId, "edge-1").Should().BeTrue();
        FlowDragDropPolicy.ShouldCommitNodeMove("lane-loop-body", edgeId: null).Should().BeTrue();
        FlowDragDropPolicy.ShouldCommitNodeMove(laneId: null, edgeId: "edge-1").Should().BeFalse();
    }

    [Fact]
    public void ViewModel_IgnoresImmediateDuplicateDropForSamePayloadAndPoint()
    {
        var viewModel = FlowCanvasViewModel.CreateDefault(new DiagnosticsService());
        var request = CreateRequest("expect-text");
        var point = new Point(120, 180);

        viewModel.HandleDrop(request, point);
        viewModel.HandleDrop(request, point);

        viewModel.Nodes.Should().HaveCount(1);
        viewModel.Document.RootLane.NodeIds.Should().HaveCount(1);
    }

    [Fact]
    public void ViewModel_NewCommand_ResetsCanvasStateAndAllowsFreshDrop()
    {
        var viewModel = FlowCanvasViewModel.CreateDefault(new DiagnosticsService());
        var point = new Point(120, 180);
        var request = CreateRequest("expect-text");

        viewModel.HandleDrop(request, point);
        viewModel.HandleDrop(CreateRequest("second"), new Point(120, 260));
        viewModel.SetSelection([viewModel.Document.RootLane.NodeIds[0]], []);
        viewModel.CopySelectionCommand.Execute(null);

        viewModel.NewCommand.Execute(null);

        viewModel.Nodes.Should().BeEmpty();
        viewModel.Document.RootLane.NodeIds.Should().BeEmpty();
        viewModel.EdgeVisuals.Should().BeEmpty();
        viewModel.HasSelection.Should().BeFalse();
        viewModel.InteractionState.SelectedNodeIds.Should().BeEmpty();
        viewModel.InteractionState.SelectedEdgeIds.Should().BeEmpty();
        viewModel.PasteSelectionCommand.CanExecute(null).Should().BeFalse();
        viewModel.UndoCommand.CanExecute(null).Should().BeFalse();
        viewModel.RedoCommand.CanExecute(null).Should().BeFalse();

        viewModel.HandleDrop(request, point);

        viewModel.Nodes.Should().HaveCount(1);
    }

    [Fact]
    public void ViewModel_DoesNotIgnoreDrop_WhenOnlyContainerFlagDiffers()
    {
        var viewModel = FlowCanvasViewModel.CreateDefault(new DiagnosticsService());
        var point = new Point(120, 180);

        viewModel.HandleDrop(CreateRequest("action", isContainer: false), point);
        viewModel.HandleDrop(CreateRequest("action", isContainer: true), point);

        viewModel.Nodes.Should().HaveCount(2);
        viewModel.Nodes.Should().Contain(node => node is FlowActionNodeModel);
        viewModel.Nodes.Should().Contain(node => node is FlowContainerNodeModel);
    }

    [Fact]
    public void Drop_NonContainer_CreatesFlowActionNodeModel()
    {
        var viewModel = FlowCanvasViewModel.CreateDefault(new DiagnosticsService());

        viewModel.HandleDrop(CreateRequest("click", isContainer: false), new Point(20, 20));

        viewModel.Nodes.Should().ContainSingle(node => node is FlowActionNodeModel && node.DisplayLabel == "click");
        viewModel.Nodes.Should().NotContain(node => node is FlowContainerNodeModel && node.DisplayLabel == "click");
    }

    [Fact]
    public void Drop_Container_CreatesFlowContainerNodeModel_WithExpandedDefaults()
    {
        var viewModel = FlowCanvasViewModel.CreateDefault(new DiagnosticsService());

        viewModel.HandleDrop(CreateRequest("group-like", isContainer: true), new Point(40, 40));

        var container = viewModel.Nodes.OfType<FlowContainerNodeModel>().Single(node => node.DisplayLabel == "group-like");
        container.IsExpanded.Should().BeTrue();
        container.IsCollapsed.Should().BeFalse();
    }

    [Fact]
    public void Drop_Container_CanToggleCollapseImmediately()
    {
        var viewModel = FlowCanvasViewModel.CreateDefault(new DiagnosticsService());

        viewModel.HandleDrop(CreateRequest("container", isContainer: true), new Point(40, 40));
        var container = viewModel.Nodes.OfType<FlowContainerNodeModel>().Single(node => node.DisplayLabel == "container");

        viewModel.ToggleCollapseCommand.Execute(container.NodeId);

        var updated = viewModel.Nodes.OfType<FlowContainerNodeModel>().Single(node => node.NodeId == container.NodeId);
        updated.IsCollapsed.Should().BeTrue();
        updated.IsExpanded.Should().BeFalse();
    }

    [Fact]
    public void ViewModel_DropAutoPlacesBelowMostRecentlyCreatedNode()
    {
        var viewModel = FlowCanvasViewModel.CreateDefault(new DiagnosticsService());

        viewModel.HandleDrop(CreateRequest("first"), new Point(20, 20));
        viewModel.HandleDrop(CreateRequest("second"), new Point(800, 600));

        var first = viewModel.Nodes.Single(node => node.DisplayLabel == "first");
        var second = viewModel.Nodes.Single(node => node.DisplayLabel == "second");

        second.Bounds.X.Should().Be(first.Bounds.X);
        second.Bounds.Y.Should().Be(first.Bounds.Y + first.Bounds.Height + 28);
    }

    [Fact]
    public void ViewModel_DropCollisionMovesFurtherDownUntilSpotIsFree()
    {
        var viewModel = FlowCanvasViewModel.CreateDefault(new DiagnosticsService());

        viewModel.HandleDrop(CreateRequest("first"), new Point(20, 20));
        viewModel.HandleDrop(CreateRequest("second"), new Point(500, 500));

        var firstNodeId = viewModel.Document.RootLane.NodeIds[0];
        viewModel.SetSelection([firstNodeId], []);
        viewModel.TranslateSelection(0, 156);

        viewModel.HandleDrop(CreateRequest("third"), new Point(900, 900));

        var second = viewModel.Nodes.Single(node => node.DisplayLabel == "second");
        var third = viewModel.Nodes.Single(node => node.DisplayLabel == "third");

        third.Bounds.X.Should().Be(second.Bounds.X);
        third.Bounds.Y.Should().Be(second.Bounds.Y + second.Bounds.Height + 28 + second.Bounds.Height + 28);
    }

    [Fact]
    public void AutoLayout_PreservesRootLaneWidthsAndCentersNodes()
    {
        var editing = new FlowEditingService();
        var layout = new FlowLayoutService();

        var document = editing.CreateEmptyDocument();
        document = editing.AddActionNode(document, CreateRequest("first"), 20, 20);
        document = editing.AddContainerNode(document, FlowContainerKind.Group, 320, 120);
        document = editing.AddActionNode(document, CreateRequest("third"), 20, 260);

        var laidOut = layout.Recalculate(layout.AutoLayout(layout.Recalculate(document)));
        var rootNodes = laidOut.RootLane.NodeIds
            .Select(nodeId => laidOut.Nodes.Single(node => node.NodeId == nodeId))
            .ToList();

        rootNodes.Select(node => node.Bounds.Width).Should().Equal(380, 420, 380);

        var centers = rootNodes
            .Select(node => node.Bounds.X + (node.Bounds.Width / 2d))
            .Distinct()
            .ToList();

        centers.Should().ContainSingle();
    }

    [Fact]
    public void AutoLayout_Recalculate_PreservesNestedNodeWidthsAndCentersNodes()
    {
        var editing = new FlowEditingService();
        var layout = new FlowLayoutService();

        var document = editing.CreateEmptyDocument();
        document = editing.AddContainerNode(document, FlowContainerKind.Group, 20, 20);

        var container = document.Nodes.OfType<FlowContainerNodeModel>().Single();
        var lane = container.ChildLanes.Single();

        document = editing.AddActionNode(document, CreateRequest("child-action"), 80, 100, dropContext: new FlowDropContextModel { DropPoint = new Point(80, 100), TargetLaneId = lane.LaneId, TargetContainerNodeId = container.NodeId });
        document = editing.AddActionNode(document, CreateRequest("child-container", isContainer: true), 80, 180, dropContext: new FlowDropContextModel { DropPoint = new Point(80, 180), TargetLaneId = lane.LaneId, TargetContainerNodeId = container.NodeId });

        var preparedNodes = document.Nodes.Select(node =>
        {
            if (node is FlowContainerNodeModel group && string.Equals(group.NodeId, container.NodeId, StringComparison.Ordinal))
            {
                return (FlowNodeModel)(group with
                {
                    Bounds = group.Bounds with { Width = 640 },
                });
            }

            return node.DisplayLabel switch
            {
                "child-action" => node with { Bounds = node.Bounds with { Width = 260 } },
                "child-container" => node with { Bounds = node.Bounds with { Width = 340 } },
                _ => node,
            };
        }).ToList();

        document = document with { Nodes = preparedNodes };

        var laidOut = layout.Recalculate(layout.AutoLayout(document));
        var updatedContainer = laidOut.Nodes.OfType<FlowContainerNodeModel>().Single(node => string.Equals(node.NodeId, container.NodeId, StringComparison.Ordinal));
        var childNodes = updatedContainer.ChildLanes.Single().NodeIds
            .Select(nodeId => laidOut.Nodes.Single(node => string.Equals(node.NodeId, nodeId, StringComparison.Ordinal)))
            .ToList();

        childNodes.Select(node => node.Bounds.Width).Should().Equal(260, 340);

        var centers = childNodes
            .Select(node => node.Bounds.X + (node.Bounds.Width / 2d))
            .Distinct()
            .ToList();

        centers.Should().ContainSingle();
    }

    [Fact]
    public void ViewModel_AutoLayoutCommand_PreservesRootWidthsAndCentersNodes()
    {
        var viewModel = FlowCanvasViewModel.CreateDefault(new DiagnosticsService());

        viewModel.HandleDrop(CreateRequest("open browser"), new Point(40, 40));
        viewModel.HandleDrop(CreateRequest("for-loop", isContainer: true), new Point(40, 220));

        viewModel.AutoLayoutCommand.Execute(null);

        var rootNodes = viewModel.Document.RootLane.NodeIds
            .Select(nodeId => viewModel.Document.Nodes.Single(node => string.Equals(node.NodeId, nodeId, StringComparison.Ordinal)))
            .ToList();

        rootNodes.Select(node => node.Bounds.Width).Should().Equal(380, 420);

        var centers = rootNodes
            .Select(node => node.Bounds.X + (node.Bounds.Width / 2d))
            .Distinct()
            .ToList();

        centers.Should().ContainSingle();
    }

    [Fact]
    public void AutoLayout_UsesEqualVerticalGapsBetweenConsecutiveRootNodes()
    {
        var editing = new FlowEditingService();
        var layout = new FlowLayoutService();

        var document = editing.CreateEmptyDocument();
        document = editing.AddActionNode(document, CreateRequest("first"), 20, 20);
        document = editing.AddContainerNode(document, FlowContainerKind.Group, 320, 120);
        document = editing.AddActionNode(document, CreateRequest("third"), 20, 260);

        var laidOut = layout.Recalculate(layout.AutoLayout(layout.Recalculate(document)));
        var rootNodes = laidOut.RootLane.NodeIds
            .Select(nodeId => laidOut.Nodes.Single(node => node.NodeId == nodeId))
            .ToList();

        for (var index = 0; index < rootNodes.Count - 1; index++)
        {
            var current = rootNodes[index];
            var next = rootNodes[index + 1];
            next.Bounds.Y.Should().Be(current.Bounds.Y + current.Bounds.Height + 28);
        }
    }

    [Fact]
    public void RuntimeMapper_ProducesExecutionGraphAndValidatesContainerSemantics()
    {
        var editing = new FlowEditingService();
        var mapper = new FlowExecutionMapper();

        var doc = editing.CreateEmptyDocument();
        doc = editing.AddContainerNode(doc, FlowContainerKind.Condition, 40, 40);

        var graph = mapper.Map(doc);
        graph.Nodes.Should().NotBeEmpty();

        var brokenCondition = doc with
        {
            Nodes = doc.Nodes.Select(node =>
            {
                if (node is not FlowContainerNodeModel container)
                {
                    return node;
                }

                return (FlowNodeModel)(container with { ChildLanes = [container.ChildLanes[0]] });
            }).ToList(),
        };

        var map = () => mapper.Map(brokenCondition);
        map.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Drop_ControlFlowContainerActions_MapToSpecificContainerKinds()
    {
        var editing = new FlowEditingService();
        var document = editing.CreateEmptyDocument();

        document = editing.AddActionNode(document, CreateRequest("for-loop", isContainer: true), 20, 20);
        document = editing.AddActionNode(document, CreateRequest("for-each-loop", isContainer: true), 20, 160);
        document = editing.AddActionNode(document, CreateRequest("while-loop", isContainer: true), 20, 300);

        var containers = document.Nodes.OfType<FlowContainerNodeModel>().ToList();
        containers.Should().HaveCount(3);
        containers[0].ContainerKind.Should().Be(FlowContainerKind.For);
        containers[1].ContainerKind.Should().Be(FlowContainerKind.ForEach);
        containers[2].ContainerKind.Should().Be(FlowContainerKind.While);
        containers[0].ContainerParameters.Should().BeOfType<ForContainerParameters>();
        containers[1].ContainerParameters.Should().BeOfType<ForEachContainerParameters>();
        containers[2].ContainerParameters.Should().BeOfType<WhileContainerParameters>();
    }

    [Fact]
    public void Performance_MediumGraphEditing_StaysWithinReasonableBudget()
    {
        var editing = new FlowEditingService();
        var document = editing.CreateEmptyDocument();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        for (var index = 0; index < 200; index++)
        {
            document = editing.AddActionNode(document, CreateRequest($"node-{index}"), 20, 20 + (index * 48));
        }

        stopwatch.Stop();

        document.Nodes.Should().HaveCount(200);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(2500);
    }

    [Fact]
    public void Drop_IntoEmptyContainerLane_AppendsAsFirstChild()
    {
        var viewModel = FlowCanvasViewModel.CreateDefault(new DiagnosticsService());

        viewModel.HandleDrop(CreateRequest("group", isContainer: true), new Point(40, 40));
        var container = viewModel.Nodes.OfType<FlowContainerNodeModel>().Single();
        var lane = container.ChildLanes[0];

        viewModel.HandleDrop(
            CreateRequest("child-one"),
            new Point(120, 120),
            new FlowDropContextModel
            {
                DropPoint = new Point(120, 120),
                TargetLaneId = lane.LaneId,
                TargetContainerNodeId = container.NodeId,
            });

        var updatedContainer = viewModel.Nodes.OfType<FlowContainerNodeModel>().Single();
        updatedContainer.ChildLanes[0].NodeIds.Should().ContainSingle();

        var childNodeId = updatedContainer.ChildLanes[0].NodeIds[0];
        var childNode = viewModel.Nodes.Single(node => string.Equals(node.NodeId, childNodeId, StringComparison.Ordinal));
        childNode.Bounds.X.Should().BeGreaterThan(updatedContainer.Bounds.X);
        childNode.Bounds.Y.Should().BeGreaterThan(updatedContainer.Bounds.Y);
        childNode.Bounds.Y.Should().BeLessThan(updatedContainer.Bounds.Y + updatedContainer.Bounds.Height);

        viewModel.Document.RootLane.NodeIds.Should().ContainSingle();
    }

    [Fact]
    public void Drop_SecondNodeIntoSameContainerLane_CreatesSiblingConnectorMetadata()
    {
        var viewModel = FlowCanvasViewModel.CreateDefault(new DiagnosticsService());

        viewModel.HandleDrop(CreateRequest("group", isContainer: true), new Point(40, 40));
        var container = viewModel.Nodes.OfType<FlowContainerNodeModel>().Single();
        var lane = container.ChildLanes[0];

        viewModel.HandleDrop(CreateRequest("child-one"), new Point(120, 120), new FlowDropContextModel { DropPoint = new Point(120, 120), TargetLaneId = lane.LaneId, TargetContainerNodeId = container.NodeId });
        viewModel.HandleDrop(CreateRequest("child-two"), new Point(120, 180), new FlowDropContextModel { DropPoint = new Point(120, 180), TargetLaneId = lane.LaneId, TargetContainerNodeId = container.NodeId });

        var updatedContainer = viewModel.Nodes.OfType<FlowContainerNodeModel>().Single();
        var laneNodeIds = updatedContainer.ChildLanes[0].NodeIds;
        laneNodeIds.Should().HaveCount(2);

        var laneEdge = viewModel.Document.Edges.Single(edge =>
            string.Equals(edge.FromNodeId, laneNodeIds[0], StringComparison.Ordinal) &&
            string.Equals(edge.ToNodeId, laneNodeIds[1], StringComparison.Ordinal));

        laneEdge.LaneMetadata.Should().NotBeNull();
        laneEdge.LaneMetadata!.OwningContainerNodeId.Should().Be(container.NodeId);
        laneEdge.LaneMetadata.SourceLaneId.Should().Be(lane.LaneId);
        laneEdge.LaneMetadata.TargetLaneId.Should().Be(lane.LaneId);
    }

    [Fact]
    public void Drop_OnContainerLaneEdge_InsertsBetweenSiblingsAndPreservesLaneOrder()
    {
        var service = new FlowEditingService();
        var document = service.CreateEmptyDocument();

        document = service.AddContainerNode(document, FlowContainerKind.Group, 40, 40);
        var container = document.Nodes.OfType<FlowContainerNodeModel>().Single();
        var lane = container.ChildLanes[0];

        document = service.AddActionNode(document, CreateRequest("first"), 120, 120, dropContext: new FlowDropContextModel { DropPoint = new Point(120, 120), TargetLaneId = lane.LaneId, TargetContainerNodeId = container.NodeId });
        document = service.AddActionNode(document, CreateRequest("third"), 120, 200, dropContext: new FlowDropContextModel { DropPoint = new Point(120, 200), TargetLaneId = lane.LaneId, TargetContainerNodeId = container.NodeId });

        var containerAfterInitialDrops = document.Nodes.OfType<FlowContainerNodeModel>().Single(node => node.NodeId == container.NodeId);
        var laneNodeIdsAfterInitialDrops = containerAfterInitialDrops.ChildLanes[0].NodeIds;

        var laneEdge = document.Edges.Single(edge =>
            string.Equals(edge.FromNodeId, laneNodeIdsAfterInitialDrops[0], StringComparison.Ordinal) &&
            string.Equals(edge.ToNodeId, laneNodeIdsAfterInitialDrops[1], StringComparison.Ordinal));

        document = service.AddActionNode(
            document,
            CreateRequest("second"),
            120,
            160,
            dropContext: new FlowDropContextModel
            {
                DropPoint = new Point(120, 160),
                TargetLaneId = lane.LaneId,
                TargetEdgeId = laneEdge.EdgeId,
                TargetContainerNodeId = container.NodeId,
            });

        var updatedContainer = document.Nodes.OfType<FlowContainerNodeModel>().Single();
        updatedContainer.ChildLanes[0].NodeIds.Should().HaveCount(3);
        document.Edges.Count(edge => edge.LaneMetadata is not null && string.Equals(edge.LaneMetadata.SourceLaneId, lane.LaneId, StringComparison.Ordinal)).Should().Be(2);
    }

    [Fact]
    public void Drop_OutsideLane_StillUsesRootBehavior()
    {
        var viewModel = FlowCanvasViewModel.CreateDefault(new DiagnosticsService());
        viewModel.HandleDrop(CreateRequest("one"), new Point(20, 20));
        viewModel.HandleDrop(CreateRequest("two"), new Point(600, 600));

        viewModel.Document.RootLane.NodeIds.Should().HaveCount(2);
        viewModel.Nodes.Should().OnlyContain(node => node.ParentContainerNodeId == null);
    }

    [Fact]
    public void NestedContainerDrop_KeepsLinksContainerLocal()
    {
        var service = new FlowEditingService();
        var document = service.CreateEmptyDocument();

        document = service.AddContainerNode(document, FlowContainerKind.Group, 20, 20);
        var outer = document.Nodes.OfType<FlowContainerNodeModel>().Single();
        var outerLane = outer.ChildLanes[0];

        document = service.AddActionNode(document, CreateRequest("inner", isContainer: true), 80, 100, dropContext: new FlowDropContextModel { DropPoint = new Point(80, 100), TargetLaneId = outerLane.LaneId, TargetContainerNodeId = outer.NodeId });

        var inner = document.Nodes.OfType<FlowContainerNodeModel>().Single(node => node.NodeId != outer.NodeId);
        var innerLane = inner.ChildLanes[0];
        document = service.AddActionNode(document, CreateRequest("inner-child-a"), 120, 160, dropContext: new FlowDropContextModel { DropPoint = new Point(120, 160), TargetLaneId = innerLane.LaneId, TargetContainerNodeId = inner.NodeId });
        document = service.AddActionNode(document, CreateRequest("inner-child-b"), 120, 220, dropContext: new FlowDropContextModel { DropPoint = new Point(120, 220), TargetLaneId = innerLane.LaneId, TargetContainerNodeId = inner.NodeId });

        document.Edges
            .Where(edge => edge.LaneMetadata is not null)
            .Select(edge => edge.LaneMetadata!.OwningContainerNodeId)
            .Distinct(StringComparer.Ordinal)
            .Should()
            .Contain(inner.NodeId)
            .And.NotContain(outer.NodeId);
    }

    [Fact]
    public void Nodes_WhenOuterContainerCollapsed_HidesNestedDescendants()
    {
        var viewModel = FlowCanvasViewModel.CreateDefault(new DiagnosticsService());

        viewModel.HandleDrop(CreateRequest("outer", isContainer: true), new Point(40, 40));

        var outer = viewModel.Nodes
            .OfType<FlowContainerNodeModel>()
            .Single(node => string.Equals(node.DisplayLabel, "outer", StringComparison.Ordinal));
        var outerLane = outer.ChildLanes[0];

        viewModel.HandleDrop(
            CreateRequest("inner", isContainer: true),
            new Point(120, 120),
            new FlowDropContextModel
            {
                DropPoint = new Point(120, 120),
                TargetLaneId = outerLane.LaneId,
                TargetContainerNodeId = outer.NodeId,
            });

        var inner = viewModel.Document.Nodes
            .OfType<FlowContainerNodeModel>()
            .Single(node => !string.Equals(node.NodeId, outer.NodeId, StringComparison.Ordinal));
        var innerLane = inner.ChildLanes[0];

        viewModel.HandleDrop(
            CreateRequest("inner-child"),
            new Point(140, 200),
            new FlowDropContextModel
            {
                DropPoint = new Point(140, 200),
                TargetLaneId = innerLane.LaneId,
                TargetContainerNodeId = inner.NodeId,
            });

        viewModel.Nodes.Should().Contain(node => string.Equals(node.NodeId, inner.NodeId, StringComparison.Ordinal));
        viewModel.Nodes.Should().Contain(node => string.Equals(node.ParentContainerNodeId, inner.NodeId, StringComparison.Ordinal));

        viewModel.ToggleCollapseCommand.Execute(outer.NodeId);

        viewModel.Nodes.Should().ContainSingle(node => string.Equals(node.NodeId, outer.NodeId, StringComparison.Ordinal));
        viewModel.Nodes.Should().NotContain(node => string.Equals(node.ParentContainerNodeId, outer.NodeId, StringComparison.Ordinal));
        viewModel.Nodes.Should().NotContain(node => string.Equals(node.ParentContainerNodeId, inner.NodeId, StringComparison.Ordinal));
    }

    [Fact]
    public void MoveNodesToLane_RebuildsLaneScopedSequentialEdges()
    {
        var service = new FlowEditingService();
        var document = service.CreateEmptyDocument();

        document = service.AddActionNode(document, CreateRequest("one"), 20, 20);
        document = service.AddActionNode(document, CreateRequest("two"), 20, 140);
        document = service.AddActionNode(document, CreateRequest("three"), 20, 260);
        document = service.AddContainerNode(document, FlowContainerKind.Group, 320, 40);

        var container = document.Nodes.OfType<FlowContainerNodeModel>().Single();
        var laneId = container.ChildLanes[0].LaneId;
        var movedIds = document.RootLane.NodeIds.Take(2).ToList();

        document = service.MoveNodesToLane(document, movedIds, laneId, 0);

        var updatedContainer = document.Nodes.OfType<FlowContainerNodeModel>().Single();
        updatedContainer.ChildLanes[0].NodeIds.Should().Equal(movedIds);
        AssertDocumentValid(document);

        var laneEdges = document.Edges.Where(edge => edge.LaneMetadata is not null && string.Equals(edge.LaneMetadata.SourceLaneId, laneId, StringComparison.Ordinal)).ToList();
        laneEdges.Should().ContainSingle();
        laneEdges[0].FromNodeId.Should().Be(movedIds[0]);
        laneEdges[0].ToNodeId.Should().Be(movedIds[1]);
    }

    [Fact]
    public void MoveNodesToLane_DoesNotAllowMovingContainerIntoItsOwnLane()
    {
        var service = new FlowEditingService();
        var document = service.CreateEmptyDocument();

        document = service.AddContainerNode(document, FlowContainerKind.For, 120, 120);
        var containerBefore = document.Nodes.OfType<FlowContainerNodeModel>().Single();
        var laneId = containerBefore.ChildLanes.Single().LaneId;
        var originalRootIds = document.RootLane.NodeIds.ToList();
        var originalHeight = containerBefore.Bounds.Height;

        document = service.MoveNodesToLane(document, [containerBefore.NodeId], laneId, 0);

        var containerAfter = document.Nodes.OfType<FlowContainerNodeModel>().Single();
        document.RootLane.NodeIds.Should().Equal(originalRootIds);
        containerAfter.ParentContainerNodeId.Should().BeNull();
        containerAfter.ChildLanes.Single().NodeIds.Should().BeEmpty();
        containerAfter.Bounds.Height.Should().Be(originalHeight);
        AssertDocumentValid(document);
    }

    [Fact]
    public void Validator_FailsWhenLaneMetadataOwnershipDoesNotMatchLaneParent()
    {
        var service = new FlowEditingService();
        var document = service.CreateEmptyDocument();

        document = service.AddContainerNode(document, FlowContainerKind.Group, 20, 20);
        var container = document.Nodes.OfType<FlowContainerNodeModel>().Single();
        var lane = container.ChildLanes[0];

        document = service.AddActionNode(document, CreateRequest("a"), 80, 80, dropContext: new FlowDropContextModel { DropPoint = new Point(80, 80), TargetLaneId = lane.LaneId, TargetContainerNodeId = container.NodeId });
        document = service.AddActionNode(document, CreateRequest("b"), 80, 140, dropContext: new FlowDropContextModel { DropPoint = new Point(80, 140), TargetLaneId = lane.LaneId, TargetContainerNodeId = container.NodeId });

        var corrupted = document with
        {
            Edges = document.Edges.Select(edge => edge.LaneMetadata is null
                ? edge
                : edge with
                {
                    LaneMetadata = edge.LaneMetadata with
                    {
                        OwningContainerNodeId = "container-bad-owner",
                    },
                }).ToList(),
        };

        var validation = FlowDocumentValidator.Validate(corrupted);
        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain(error => error.Contains("ownership", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Persistence_RoundTripsContainerLaneMetadataEdges()
    {
        var persistence = new FlowPersistenceService();
        var service = new FlowEditingService();
        var document = service.CreateEmptyDocument();

        document = service.AddContainerNode(document, FlowContainerKind.Group, 20, 20);
        var container = document.Nodes.OfType<FlowContainerNodeModel>().Single();
        var lane = container.ChildLanes[0];
        document = service.AddActionNode(document, CreateRequest("left"), 80, 80, dropContext: new FlowDropContextModel { DropPoint = new Point(80, 80), TargetLaneId = lane.LaneId, TargetContainerNodeId = container.NodeId });
        document = service.AddActionNode(document, CreateRequest("right"), 80, 140, dropContext: new FlowDropContextModel { DropPoint = new Point(80, 140), TargetLaneId = lane.LaneId, TargetContainerNodeId = container.NodeId });

        var filePath = Path.Combine(Path.GetTempPath(), $"lane-meta-{Guid.NewGuid():N}.flow.json");
        await persistence.SaveAsync(document, filePath);
        var loaded = await persistence.OpenAsync(filePath);

        var loadedLaneEdge = loaded.Edges.Single(edge => edge.LaneMetadata is not null);
        loadedLaneEdge.LaneMetadata!.SourceLaneId.Should().Be(lane.LaneId);
        loadedLaneEdge.LaneMetadata.TargetLaneId.Should().Be(lane.LaneId);
        loadedLaneEdge.LaneMetadata.OwningContainerNodeId.Should().Be(container.NodeId);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task RuntimeExecution_HonorsContainerLaneNodeOrder()
    {
        var service = new FlowEditingService();
        var mapper = new FlowExecutionMapper();
        var diagnostics = new DiagnosticsService();
        var runtime = new FlowRuntimeExecutor(diagnostics);

        var document = service.CreateEmptyDocument();
        document = service.AddActionNode(document, CreateRequest("root-start"), 20, 20);
        document = service.AddContainerNode(document, FlowContainerKind.ForEach, 320, 20);

        var container = document.Nodes.OfType<FlowContainerNodeModel>().Single();
        var lane = container.ChildLanes.Single();
        document = document.ReplaceContainerParameters(container.NodeId, new ForEachContainerParameters("item", "[1]", 10));
        document = service.AddActionNode(document, CreateRequest("lane-first"), 360, 120, dropContext: new FlowDropContextModel { DropPoint = new Point(360, 120), TargetLaneId = lane.LaneId, TargetContainerNodeId = container.NodeId });
        document = service.AddActionNode(document, CreateRequest("lane-second"), 360, 180, dropContext: new FlowDropContextModel { DropPoint = new Point(360, 180), TargetLaneId = lane.LaneId, TargetContainerNodeId = container.NodeId });

        var laneFirstNodeId = document.Nodes.Single(node => string.Equals(node.DisplayLabel, "lane-first", StringComparison.Ordinal)).NodeId;
        var laneSecondNodeId = document.Nodes.Single(node => string.Equals(node.DisplayLabel, "lane-second", StringComparison.Ordinal)).NodeId;

        var executionGraph = mapper.Map(document);
        var result = await runtime.ExecuteAsync(executionGraph);

        var executionOrder = result.ExecutedNodeIds.ToList();

        var firstIndex = executionOrder.IndexOf(laneFirstNodeId);
        var secondIndex = executionOrder.IndexOf(laneSecondNodeId);
        firstIndex.Should().BeGreaterThanOrEqualTo(0);
        secondIndex.Should().BeGreaterThan(firstIndex);
    }

    private static void AssertDocumentValid(FlowDocumentModel document)
    {
        var validation = FlowDocumentValidator.Validate(document);
        validation.IsValid.Should().BeTrue(string.Join(" | ", validation.Errors));
    }

    private static UiActionDragRequest CreateRequest(string actionName, bool isContainer = false)
    {
        return new UiActionDragRequest
        {
            ActionId = actionName,
            ActionName = actionName,
            CategoryId = "test",
            CategoryName = "Test",
            IsContainer = isContainer,
        };
    }
}
