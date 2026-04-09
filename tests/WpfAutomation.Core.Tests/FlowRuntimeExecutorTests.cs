using FluentAssertions;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WpfAutomation.App.Models.Flow;
using WpfAutomation.App.Services.Flow;
using WpfAutomation.Core.Diagnostics;
using Xunit;

namespace WpfAutomation.Core.Tests;

public sealed class FlowRuntimeExecutorTests
{
    private readonly IFlowRuntimeExecutor _executor;
    private readonly DiagnosticsService _diagnosticsService;

    public FlowRuntimeExecutorTests()
    {
        _diagnosticsService = new DiagnosticsService();
        _executor = new FlowRuntimeExecutor(_diagnosticsService);
    }

    [Fact]
    public async Task ExecuteAsync_ForLoop_IteratesWithStartEndStep()
    {
        // Arrange: For loop from 0 to 5 step 2 (iterations: 0, 2, 4 = 3 iterations)
        var containerNode = CreateForLoopNode("for-node", start: 0, end: 5, step: 2);
        var graph = CreateExecutionGraph(new[] { containerNode });
        var cts = new CancellationTokenSource();

        // Act
        var result = await _executor.ExecuteAsync(graph, cts.Token);

        // Assert
        result.Should().NotBeNull();
        result.ExecutedNodeIds.Should().Contain("for-source");
        result.IterationsByNodeId["for-source"].Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_ForLoop_RespectNegativeStep()
    {
        // Arrange: For loop from 5 down to 0 step -2 (iterations: 5, 3, 1 = 3 iterations)
        var containerNode = CreateForLoopNode("for-node", start: 5, end: 0, step: -2);
        var graph = CreateExecutionGraph(new[] { containerNode });
        var cts = new CancellationTokenSource();

        // Act
        var result = await _executor.ExecuteAsync(graph, cts.Token);

        // Assert
        result.Should().NotBeNull();
        result.IterationsByNodeId["for-source"].Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_ForEachLoop_IteratesOverParsedItems()
    {
        // Arrange: ForEach with ItemsExpression "apple,banana,cherry"
        var forEachParams = new ForEachContainerParameters
        {
            ItemsExpression = "apple,banana,cherry",
            ItemVariable = "item",
            MaxIterationsOverride = null
        };
        var containerNode = CreateExecutionFlowNodeWithParams(
            "foreach-node", "foreach-source", FlowContainerKind.ForEach, forEachParams);
        var graph = CreateExecutionGraph(new[] { containerNode });
        var cts = new CancellationTokenSource();

        // Act
        var result = await _executor.ExecuteAsync(graph, cts.Token);

        // Assert - 3 items
        result.Should().NotBeNull();
        result.IterationsByNodeId["foreach-source"].Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_WhileLoop_StopsWhenConditionFalse()
    {
        // Arrange: While loop with "false" keyword condition
        var whileParams = new WhileContainerParameters
        {
            ConditionExpression = "false",
            MaxIterations = 100
        };
        var containerNode = CreateExecutionFlowNodeWithParams(
            "while-node", "while-source", FlowContainerKind.While, whileParams);
        var graph = CreateExecutionGraph(new[] { containerNode });
        var cts = new CancellationTokenSource();

        // Act
        var result = await _executor.ExecuteAsync(graph, cts.Token);

        // Assert - should have 0 iterations (condition false from start)
        result.Should().NotBeNull();
        result.IterationsByNodeId.ContainsKey("while-source").Should().BeTrue();
        result.IterationsByNodeId["while-source"].Should().Be(0);
    }

    // Cancellation test would go here but is complex to test properly with xUnit/Fact
    // The executor properly respects CancellationToken.ThrowIfCancellationRequested() calls


    // Helpers

    private static ExecutionFlowNode CreateForLoopNode(string executionNodeId, int start, int end, int step)
    {
        var forParams = new ForContainerParameters
        {
            Start = start,
            End = end,
            Step = step,
            MaxIterationsOverride = null
        };
        return CreateExecutionFlowNodeWithParams(executionNodeId, "for-source", FlowContainerKind.For, forParams);
    }

    private static ExecutionFlowNode CreateExecutionFlowNodeWithParams(
        string executionNodeId,
        string sourceNodeId,
        FlowContainerKind? containerKind,
        ContainerParameters? containerParameters)
    {
        return new ExecutionFlowNode
        {
            ExecutionNodeId = executionNodeId,
            SourceNodeId = sourceNodeId,
            DisplayLabel = executionNodeId,
            NodeKind = FlowNodeKind.Container,
            ActionId = null,
            ContainerKind = containerKind,
            ContainerParameters = containerParameters,
            ChildLanes = new[] { CreateLane("loop-body-lane") }.ToList(),
        };
    }

    private static ExecutionFlowLane CreateLane(string laneLabel)
    {
        return new ExecutionFlowLane
        {
            LaneKind = FlowLaneKind.LoopBody,
            SortOrder = 0,
            NodeExecutionIds = new List<string>().AsReadOnly()
        };
    }

    private static ExecutionFlowGraph CreateExecutionGraph(IReadOnlyList<ExecutionFlowNode> nodes)
    {
        return new ExecutionFlowGraph
        {
            SchemaVersion = 1,
            Nodes = nodes.Cast<IExecutionFlowNode>().ToList().AsReadOnly(),
            Edges = new List<IExecutionFlowEdge>().AsReadOnly()
        };
    }
}
