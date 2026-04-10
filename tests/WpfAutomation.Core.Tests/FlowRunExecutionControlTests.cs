using FluentAssertions;
using WpfAutomation.App.Services.Flow;

namespace WpfAutomation.Core.Tests;

public sealed class FlowRunExecutionControlTests
{
    [Fact]
    public async Task WaitIfPausedAsync_Completes_When_Resume_Is_Called()
    {
        using var control = new FlowRunExecutionControl();
        control.RequestPause();

        var waitTask = control.WaitIfPausedAsync();
        waitTask.IsCompleted.Should().BeFalse();

        control.Resume();

        await waitTask;
    }

    [Fact]
    public async Task WaitIfPausedAsync_Is_Cancellation_Aware()
    {
        using var control = new FlowRunExecutionControl();
        control.RequestPause();

        using var cts = new CancellationTokenSource();
        var waitTask = control.WaitIfPausedAsync(cts.Token);

        cts.Cancel();

        Func<Task> act = async () => await waitTask;
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Dispose_Unblocks_Waiters_And_Prevents_New_Requests()
    {
        var control = new FlowRunExecutionControl();
        control.RequestPause();

        var waitTask = control.WaitIfPausedAsync();
        waitTask.IsCompleted.Should().BeFalse();

        control.Dispose();

        await waitTask;
        var action = () => control.RequestPause();
        action.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task Repeated_Pause_Resume_Cycles_Do_Not_Deadlock()
    {
        using var control = new FlowRunExecutionControl();

        for (var i = 0; i < 10; i++)
        {
            control.RequestPause();
            var waitTask = control.WaitIfPausedAsync();
            waitTask.IsCompleted.Should().BeFalse();

            control.Resume();
            await waitTask;
        }
    }
}
