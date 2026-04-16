namespace AllItems.Automation.Browser.App.Services.Flow;

public interface IFlowExecutionBridge
{
    Task PrepareRunAsync(
        ExecutionFlowGraph executionGraph,
        bool forceHeaded = false,
        CancellationToken cancellationToken = default,
        IFlowRunExecutionControl? runExecutionControl = null);

    Task CloseActiveSessionAsync();
}

public sealed class NullFlowExecutionBridge : IFlowExecutionBridge
{
    public Task PrepareRunAsync(
        ExecutionFlowGraph executionGraph,
        bool forceHeaded = false,
        CancellationToken cancellationToken = default,
        IFlowRunExecutionControl? runExecutionControl = null)
    {
        return Task.CompletedTask;
    }

    public Task CloseActiveSessionAsync()
    {
        return Task.CompletedTask;
    }
}
