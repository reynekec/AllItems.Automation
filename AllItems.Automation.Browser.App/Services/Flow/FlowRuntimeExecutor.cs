using AllItems.Automation.Browser.App.Models.Flow;
using AllItems.Automation.Browser.Core.Diagnostics;

namespace AllItems.Automation.Browser.App.Services.Flow;

public sealed record FlowRuntimeOptions(int GlobalMaxIterations = 1000);

public interface IFlowRuntimeExecutor
{
    Task<FlowRuntimeExecutionResult> ExecuteAsync(
        ExecutionFlowGraph executionGraph,
        CancellationToken cancellationToken = default,
        IFlowRunExecutionControl? runExecutionControl = null);
}

public sealed record FlowRuntimeExecutionResult(
    IReadOnlyList<string> ExecutedNodeIds,
    IReadOnlyDictionary<string, int> IterationsByNodeId);

public sealed class FlowRuntimeExecutor : IFlowRuntimeExecutor
{
    private readonly DiagnosticsService _diagnosticsService;
    private readonly FlowRuntimeOptions _options;

    public FlowRuntimeExecutor(DiagnosticsService diagnosticsService)
        : this(diagnosticsService, new FlowRuntimeOptions())
    {
    }

    public FlowRuntimeExecutor(DiagnosticsService diagnosticsService, FlowRuntimeOptions options)
    {
        _diagnosticsService = diagnosticsService;
        _options = options;
    }

    public Task<FlowRuntimeExecutionResult> ExecuteAsync(
        ExecutionFlowGraph executionGraph,
        CancellationToken cancellationToken = default,
        IFlowRunExecutionControl? runExecutionControl = null)
    {
        ArgumentNullException.ThrowIfNull(executionGraph);

        var nodeLookup = executionGraph.Nodes.ToDictionary(node => node.ExecutionNodeId, StringComparer.Ordinal);
        var outgoing = executionGraph.Edges
            .GroupBy(edge => edge.FromExecutionNodeId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(edge => edge.ToExecutionNodeId).ToList(), StringComparer.Ordinal);
        var incomingCounts = executionGraph.Nodes.ToDictionary(node => node.ExecutionNodeId, _ => 0, StringComparer.Ordinal);

        foreach (var edge in executionGraph.Edges)
        {
            incomingCounts[edge.ToExecutionNodeId] += 1;
        }

        var laneChildNodeIds = new HashSet<string>(
            executionGraph.Nodes.SelectMany(node => node.ChildLanes).SelectMany(lane => lane.NodeExecutionIds),
            StringComparer.Ordinal);

        var rootStarts = executionGraph.Nodes
            .Where(node => !laneChildNodeIds.Contains(node.ExecutionNodeId) && incomingCounts[node.ExecutionNodeId] == 0)
            .Select(node => node.ExecutionNodeId)
            .ToList();

        var executedNodeIds = new List<string>();
        var iterations = new Dictionary<string, int>(StringComparer.Ordinal);
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rootStart in rootStarts)
        {
            ExecuteSequence(rootStart, nodeLookup, outgoing, executedNodeIds, iterations, variables, cancellationToken, runExecutionControl);
        }

        return Task.FromResult(new FlowRuntimeExecutionResult(executedNodeIds, iterations));
    }

    private void ExecuteSequence(
        string executionNodeId,
        IReadOnlyDictionary<string, IExecutionFlowNode> nodes,
        IReadOnlyDictionary<string, List<string>> outgoing,
        ICollection<string> executedNodeIds,
        IDictionary<string, int> iterations,
        IDictionary<string, string> variables,
        CancellationToken cancellationToken,
        IFlowRunExecutionControl? runExecutionControl)
    {
        var current = executionNodeId;
        var pathVisited = new HashSet<string>(StringComparer.Ordinal);

        while (true)
        {
            WaitForResumeIfPaused(runExecutionControl, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (!nodes.TryGetValue(current, out var node))
            {
                throw new InvalidOperationException($"Runtime mapping references missing execution node '{current}'.");
            }

            if (!pathVisited.Add(current))
            {
                throw new InvalidOperationException($"Runtime graph cycle detected at execution node '{current}'.");
            }

            ExecuteNode(node, nodes, outgoing, executedNodeIds, iterations, variables, cancellationToken, runExecutionControl);

            if (!outgoing.TryGetValue(current, out var nextNodes) || nextNodes.Count == 0)
            {
                return;
            }

            current = nextNodes[0];
        }
    }

    private void ExecuteNode(
        IExecutionFlowNode node,
        IReadOnlyDictionary<string, IExecutionFlowNode> nodes,
        IReadOnlyDictionary<string, List<string>> outgoing,
        ICollection<string> executedNodeIds,
        IDictionary<string, int> iterations,
        IDictionary<string, string> variables,
        CancellationToken cancellationToken,
        IFlowRunExecutionControl? runExecutionControl)
    {
        executedNodeIds.Add(node.SourceNodeId);

        if (node.NodeKind == FlowNodeKind.Action)
        {
            return;
        }

        try
        {
            switch (node.ContainerKind)
            {
                case FlowContainerKind.For:
                    ExecuteForLoop(node, nodes, outgoing, iterations, variables, executedNodeIds, cancellationToken, runExecutionControl);
                    break;
                case FlowContainerKind.ForEach:
                    ExecuteForEachLoop(node, nodes, outgoing, iterations, variables, executedNodeIds, cancellationToken, runExecutionControl);
                    break;
                case FlowContainerKind.While:
                    ExecuteWhileLoop(node, nodes, outgoing, iterations, variables, executedNodeIds, cancellationToken, runExecutionControl);
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            _diagnosticsService.Warn("Flow runtime canceled while executing loop.", new Dictionary<string, string>
            {
                ["sourceNodeId"] = node.SourceNodeId,
                ["kind"] = node.ContainerKind?.ToString() ?? "unknown",
            });
            throw;
        }
        catch (Exception exception)
        {
            _diagnosticsService.Error("Flow runtime loop execution failed.", exception, new Dictionary<string, string>
            {
                ["sourceNodeId"] = node.SourceNodeId,
                ["kind"] = node.ContainerKind?.ToString() ?? "unknown",
            });
            throw;
        }
    }

    private void ExecuteForLoop(
        IExecutionFlowNode node,
        IReadOnlyDictionary<string, IExecutionFlowNode> nodes,
        IReadOnlyDictionary<string, List<string>> outgoing,
        IDictionary<string, int> iterations,
        IDictionary<string, string> variables,
        ICollection<string> executedNodeIds,
        CancellationToken cancellationToken,
        IFlowRunExecutionControl? runExecutionControl)
    {
        if (node.ContainerParameters is not ForContainerParameters parameters)
        {
            throw new InvalidOperationException($"For container '{node.SourceNodeId}' is missing typed for-loop parameters.");
        }

        if (parameters.Step == 0)
        {
            throw new InvalidOperationException($"For container '{node.SourceNodeId}' has step value 0.");
        }

        var effectiveCap = ResolveEffectiveCap(parameters.MaxIterationsOverride);
        var lane = RequireLoopBodyLane(node);
        var iterationCount = 0;

        _diagnosticsService.Info("Flow runtime for-loop start.", new Dictionary<string, string>
        {
            ["sourceNodeId"] = node.SourceNodeId,
            ["start"] = parameters.Start.ToString(),
            ["end"] = parameters.End.ToString(),
            ["step"] = parameters.Step.ToString(),
        });

        for (var i = parameters.Start; parameters.Step > 0 ? i <= parameters.End : i >= parameters.End; i += parameters.Step)
        {
            WaitForResumeIfPaused(runExecutionControl, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            iterationCount++;
            EnsureWithinCap(node.SourceNodeId, iterationCount, effectiveCap);

            variables["index"] = i.ToString();
            _diagnosticsService.Info("Flow runtime for-loop iteration.", new Dictionary<string, string>
            {
                ["sourceNodeId"] = node.SourceNodeId,
                ["iteration"] = iterationCount.ToString(),
                ["index"] = i.ToString(),
            });
            ExecuteLane(lane, nodes, outgoing, executedNodeIds, iterations, variables, cancellationToken, runExecutionControl);
        }

        iterations[node.SourceNodeId] = iterationCount;
        _diagnosticsService.Info("Flow runtime for-loop end.", new Dictionary<string, string>
        {
            ["sourceNodeId"] = node.SourceNodeId,
            ["iterations"] = iterationCount.ToString(),
        });
    }

    private void ExecuteForEachLoop(
        IExecutionFlowNode node,
        IReadOnlyDictionary<string, IExecutionFlowNode> nodes,
        IReadOnlyDictionary<string, List<string>> outgoing,
        IDictionary<string, int> iterations,
        IDictionary<string, string> variables,
        ICollection<string> executedNodeIds,
        CancellationToken cancellationToken,
        IFlowRunExecutionControl? runExecutionControl)
    {
        if (node.ContainerParameters is not ForEachContainerParameters parameters)
        {
            throw new InvalidOperationException($"ForEach container '{node.SourceNodeId}' is missing typed foreach parameters.");
        }

        if (string.IsNullOrWhiteSpace(parameters.ItemVariable))
        {
            throw new InvalidOperationException($"ForEach container '{node.SourceNodeId}' has an empty item variable.");
        }

        var items = ParseItems(parameters.ItemsExpression);
        var effectiveCap = ResolveEffectiveCap(parameters.MaxIterationsOverride);
        var lane = RequireLoopBodyLane(node);
        var iterationCount = 0;

        _diagnosticsService.Info("Flow runtime foreach-loop start.", new Dictionary<string, string>
        {
            ["sourceNodeId"] = node.SourceNodeId,
            ["itemCount"] = items.Count.ToString(),
            ["itemVariable"] = parameters.ItemVariable,
        });

        foreach (var item in items)
        {
            WaitForResumeIfPaused(runExecutionControl, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            iterationCount++;
            EnsureWithinCap(node.SourceNodeId, iterationCount, effectiveCap);

            variables[parameters.ItemVariable] = item;
            _diagnosticsService.Info("Flow runtime foreach-loop iteration.", new Dictionary<string, string>
            {
                ["sourceNodeId"] = node.SourceNodeId,
                ["iteration"] = iterationCount.ToString(),
                ["item"] = item,
                ["itemVariable"] = parameters.ItemVariable,
            });
            ExecuteLane(lane, nodes, outgoing, executedNodeIds, iterations, variables, cancellationToken, runExecutionControl);
        }

        iterations[node.SourceNodeId] = iterationCount;
        _diagnosticsService.Info("Flow runtime foreach-loop end.", new Dictionary<string, string>
        {
            ["sourceNodeId"] = node.SourceNodeId,
            ["iterations"] = iterationCount.ToString(),
        });
    }

    private void ExecuteWhileLoop(
        IExecutionFlowNode node,
        IReadOnlyDictionary<string, IExecutionFlowNode> nodes,
        IReadOnlyDictionary<string, List<string>> outgoing,
        IDictionary<string, int> iterations,
        IDictionary<string, string> variables,
        ICollection<string> executedNodeIds,
        CancellationToken cancellationToken,
        IFlowRunExecutionControl? runExecutionControl)
    {
        if (node.ContainerParameters is not WhileContainerParameters parameters)
        {
            throw new InvalidOperationException($"While container '{node.SourceNodeId}' is missing typed while-loop parameters.");
        }

        if (parameters.MaxIterations <= 0)
        {
            throw new InvalidOperationException($"While container '{node.SourceNodeId}' has max iterations <= 0.");
        }

        var effectiveCap = ResolveEffectiveCap(parameters.MaxIterations);
        var lane = RequireLoopBodyLane(node);
        var iterationCount = 0;

        _diagnosticsService.Info("Flow runtime while-loop start.", new Dictionary<string, string>
        {
            ["sourceNodeId"] = node.SourceNodeId,
            ["condition"] = parameters.ConditionExpression,
            ["maxIterations"] = parameters.MaxIterations.ToString(),
        });

        while (EvaluateCondition(parameters.ConditionExpression, (IReadOnlyDictionary<string, string>)variables))
        {
            WaitForResumeIfPaused(runExecutionControl, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            iterationCount++;
            EnsureWithinCap(node.SourceNodeId, iterationCount, effectiveCap);

            _diagnosticsService.Info("Flow runtime while-loop iteration.", new Dictionary<string, string>
            {
                ["sourceNodeId"] = node.SourceNodeId,
                ["iteration"] = iterationCount.ToString(),
            });
            ExecuteLane(lane, nodes, outgoing, executedNodeIds, iterations, variables, cancellationToken, runExecutionControl);
        }

        iterations[node.SourceNodeId] = iterationCount;
        _diagnosticsService.Info("Flow runtime while-loop end.", new Dictionary<string, string>
        {
            ["sourceNodeId"] = node.SourceNodeId,
            ["iterations"] = iterationCount.ToString(),
        });
    }

    private static IExecutionFlowLane RequireLoopBodyLane(IExecutionFlowNode node)
    {
        var lane = node.ChildLanes
            .OrderBy(candidate => candidate.SortOrder)
            .FirstOrDefault(candidate => candidate.LaneKind == FlowLaneKind.LoopBody);

        return lane ?? throw new InvalidOperationException($"Loop container '{node.SourceNodeId}' does not contain a loop body lane.");
    }

    private static IReadOnlyList<string> ParseItems(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return [];
        }

        var normalized = expression.Trim();
        if (normalized.StartsWith("[", StringComparison.Ordinal) && normalized.EndsWith("]", StringComparison.Ordinal))
        {
            normalized = normalized[1..^1];
        }

        return normalized
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim().Trim('"', '\''))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
    }

    private static bool EvaluateCondition(string expression, IReadOnlyDictionary<string, string> variables)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new InvalidOperationException("Condition expression is empty.");
        }

        var normalized = expression.Trim();
        if (string.Equals(normalized, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(normalized, "false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var operators = new[] { "<=", ">=", "==", "!=", "<", ">" };
        foreach (var op in operators)
        {
            var opIndex = normalized.IndexOf(op, StringComparison.Ordinal);
            if (opIndex < 0)
            {
                continue;
            }

            var leftToken = normalized[..opIndex].Trim();
            var rightToken = normalized[(opIndex + op.Length)..].Trim();

            var left = ResolveNumber(leftToken, variables);
            var right = ResolveNumber(rightToken, variables);

            return op switch
            {
                "<" => left < right,
                ">" => left > right,
                "<=" => left <= right,
                ">=" => left >= right,
                "==" => left == right,
                "!=" => left != right,
                _ => throw new InvalidOperationException($"Unsupported operator '{op}'."),
            };
        }

        throw new InvalidOperationException($"Unsupported condition expression '{expression}'.");
    }

    private static int ResolveNumber(string token, IReadOnlyDictionary<string, string> variables)
    {
        if (int.TryParse(token, out var literal))
        {
            return literal;
        }

        if (variables.TryGetValue(token, out var variableValue) && int.TryParse(variableValue, out var variableNumber))
        {
            return variableNumber;
        }

        throw new InvalidOperationException($"Unsupported numeric token '{token}'.");
    }

    private int ResolveEffectiveCap(int? nodeCap)
    {
        if (!nodeCap.HasValue)
        {
            return _options.GlobalMaxIterations;
        }

        return Math.Min(_options.GlobalMaxIterations, nodeCap.Value);
    }

    private void EnsureWithinCap(string sourceNodeId, int iterationCount, int maxAllowed)
    {
        if (iterationCount <= maxAllowed)
        {
            return;
        }

        throw new InvalidOperationException($"Loop container '{sourceNodeId}' exceeded the max iteration cap ({maxAllowed}).");
    }

    private void ExecuteLane(
        IExecutionFlowLane lane,
        IReadOnlyDictionary<string, IExecutionFlowNode> nodes,
        IReadOnlyDictionary<string, List<string>> outgoing,
        ICollection<string> executedNodeIds,
        IDictionary<string, int> iterations,
        IDictionary<string, string> variables,
        CancellationToken cancellationToken,
        IFlowRunExecutionControl? runExecutionControl)
    {
        foreach (var laneNodeExecutionId in lane.NodeExecutionIds)
        {
            WaitForResumeIfPaused(runExecutionControl, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            ExecuteSequence(laneNodeExecutionId, nodes, outgoing, executedNodeIds, iterations, variables, cancellationToken, runExecutionControl);
        }
    }

    private static void WaitForResumeIfPaused(IFlowRunExecutionControl? runExecutionControl, CancellationToken cancellationToken)
    {
        if (runExecutionControl is null)
        {
            return;
        }

        runExecutionControl.WaitIfPausedAsync(cancellationToken).GetAwaiter().GetResult();
    }
}
