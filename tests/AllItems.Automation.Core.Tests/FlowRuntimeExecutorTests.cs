using FluentAssertions;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AllItems.Automation.Browser.App.Models.Flow;
using AllItems.Automation.Browser.App.Services.Flow;
using AllItems.Automation.Browser.Core.Diagnostics;
using Xunit;

namespace AllItems.Automation.Core.Tests;

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

    [Fact]
    public async Task ExecuteAsync_Pauses_At_Action_Boundary_And_Resumes()
    {
        var graph = CreateActionSequenceGraph();
        using var cts = new CancellationTokenSource();
        using var gate = new BoundaryPauseRunExecutionControl(pauseAtWaitCall: 2);

        var executionTask = Task.Run(() => _executor.ExecuteAsync(graph, cts.Token, gate));

        await gate.WaitUntilPausedAsync();
        executionTask.IsCompleted.Should().BeFalse();

        gate.Resume();
        var result = await executionTask;

        result.ExecutedNodeIds.Should().ContainInOrder("open-source", "click-source");
    }

    [Fact]
    public async Task ExecuteAsync_Cancelled_While_Paused_Throws()
    {
        var graph = CreateActionSequenceGraph();
        using var cts = new CancellationTokenSource();
        using var gate = new BoundaryPauseRunExecutionControl(pauseAtWaitCall: 1);

        var executionTask = Task.Run(() => _executor.ExecuteAsync(graph, cts.Token, gate));

        await gate.WaitUntilPausedAsync();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await executionTask);
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

    private static ExecutionFlowGraph CreateActionSequenceGraph()
    {
        var first = new ExecutionFlowNode
        {
            ExecutionNodeId = "exec-open",
            SourceNodeId = "open-source",
            DisplayLabel = "Open",
            NodeKind = FlowNodeKind.Action,
            ActionId = "open-browser",
        };

        var second = new ExecutionFlowNode
        {
            ExecutionNodeId = "exec-click",
            SourceNodeId = "click-source",
            DisplayLabel = "Click",
            NodeKind = FlowNodeKind.Action,
            ActionId = "click-element",
        };

        return new ExecutionFlowGraph
        {
            SchemaVersion = 1,
            Nodes = new List<IExecutionFlowNode> { first, second }.AsReadOnly(),
            Edges = new List<IExecutionFlowEdge>
            {
                new ExecutionFlowEdge
                {
                    FromExecutionNodeId = "exec-open",
                    ToExecutionNodeId = "exec-click",
                },
            }.AsReadOnly(),
        };
    }

    private sealed class BoundaryPauseRunExecutionControl : IFlowRunExecutionControl
    {
        private readonly int _pauseAtWaitCall;
        private readonly TaskCompletionSource<bool> _enteredPause = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly object _sync = new();
        private TaskCompletionSource<bool> _resumeSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _waitCount;
        private bool _isPaused;

        public BoundaryPauseRunExecutionControl(int pauseAtWaitCall)
        {
            _pauseAtWaitCall = pauseAtWaitCall;
        }

        public bool IsPauseRequested
        {
            get
            {
                lock (_sync)
                {
                    return _isPaused;
                }
            }
        }

        public void RequestPause()
        {
            lock (_sync)
            {
                _isPaused = true;
            }
        }

        public void Resume()
        {
            TaskCompletionSource<bool> signal;

            lock (_sync)
            {
                _isPaused = false;
                signal = _resumeSignal;
                _resumeSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            signal.TrySetResult(true);
        }

        public async Task WaitIfPausedAsync(CancellationToken cancellationToken = default)
        {
            Task waitTask;

            lock (_sync)
            {
                _waitCount++;
                if (_waitCount != _pauseAtWaitCall && !_isPaused)
                {
                    return;
                }

                _isPaused = true;
                waitTask = _resumeSignal.Task;
                _enteredPause.TrySetResult(true);
            }

            await waitTask.WaitAsync(cancellationToken);
        }

        public Task WaitUntilPausedAsync()
        {
            return _enteredPause.Task;
        }

        public void Dispose()
        {
            Resume();
        }
    }
}
