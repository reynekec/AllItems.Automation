namespace WpfAutomation.App.Services.Flow;

public interface IFlowExecutionBridge
{
    Task PrepareRunAsync(ExecutionFlowGraph executionGraph, bool forceHeaded = false, CancellationToken cancellationToken = default);

    Task CloseActiveSessionAsync();
}

public sealed class NullFlowExecutionBridge : IFlowExecutionBridge
{
    public Task PrepareRunAsync(ExecutionFlowGraph executionGraph, bool forceHeaded = false, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task CloseActiveSessionAsync()
    {
        return Task.CompletedTask;
    }
}
