using AFK4.Agent.Service;
using AFK4.Agent.Service.Updates;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Tests;

public sealed class AgentUpdateWorkerTests
{
    private static readonly TimeSpan WorkerStopTimeout = TimeSpan.FromSeconds(75);
    private static readonly TimeSpan WorkerObservationTimeout = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task ExecuteAsync_ChecksForUpdatesOnStartupWithoutBlockingHeartbeatWorker()
    {
        using var stopping = new CancellationTokenSource(WorkerStopTimeout);
        var updateChecked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new RecordingUpdateCoordinator(updateChecked);
        var recovery = new RecordingRecoveryService();
        var worker = new AgentUpdateWorker(
            NullLogger<AgentUpdateWorker>.Instance,
            coordinator,
            recovery,
            Options.Create(new AgentOptions { UpdateCheckIntervalSeconds = 60 }));

        await worker.StartAsync(stopping.Token);
        await updateChecked.Task.WaitAsync(WorkerObservationTimeout);
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(1, recovery.CallCount);
        Assert.Equal(1, coordinator.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCoordinatorFailsContinuesUpdateLoop()
    {
        using var stopping = new CancellationTokenSource(WorkerStopTimeout);
        var secondAttempt = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new FailingThenRecordingUpdateCoordinator(secondAttempt);
        var worker = new AgentUpdateWorker(
            NullLogger<AgentUpdateWorker>.Instance,
            coordinator,
            new RecordingRecoveryService(),
            Options.Create(new AgentOptions { UpdateCheckIntervalSeconds = 1 }));

        await worker.StartAsync(stopping.Token);
        await secondAttempt.Task.WaitAsync(WorkerObservationTimeout);
        await worker.StopAsync(CancellationToken.None);

        Assert.True(coordinator.CallCount >= 2);
    }

    private sealed class RecordingUpdateCoordinator(TaskCompletionSource updateChecked) : IAgentUpdateCoordinator
    {
        public int CallCount { get; private set; }

        public Task<AgentUpdateExecutionResult> CheckAndApplyUpdatesAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            updateChecked.TrySetResult();

            return Task.FromResult(new AgentUpdateExecutionResult(0, 0, 0));
        }
    }

    private sealed class RecordingRecoveryService : IUpdateRecoveryService
    {
        public int CallCount { get; private set; }

        public Task RecoverAsync(CancellationToken cancellationToken)
        {
            CallCount++;

            return Task.CompletedTask;
        }
    }

    private sealed class FailingThenRecordingUpdateCoordinator(TaskCompletionSource secondAttempt) : IAgentUpdateCoordinator
    {
        public int CallCount { get; private set; }

        public Task<AgentUpdateExecutionResult> CheckAndApplyUpdatesAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            if (CallCount == 1)
            {
                throw new InvalidOperationException("temporary update failure");
            }

            secondAttempt.TrySetResult();

            return Task.FromResult(new AgentUpdateExecutionResult(0, 0, 0));
        }
    }
}
