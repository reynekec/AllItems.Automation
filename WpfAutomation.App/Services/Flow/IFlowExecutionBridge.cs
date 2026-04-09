namespace WpfAutomation.App.Services.Flow;

public interface IFlowExecutionBridge
{
    Task PrepareRunAsync(ExecutionFlowGraph executionGraph, CancellationToken cancellationToken = default);
}

public sealed class NullFlowExecutionBridge : IFlowExecutionBridge
{
    public Task PrepareRunAsync(ExecutionFlowGraph executionGraph, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
