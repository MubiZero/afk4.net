using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Health;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformPeriodicJobTests
{
    private sealed class RecordingRecorder : IJobRunRecorder
    {
        public List<(string JobName, string Outcome, int Items, string? Error)> Records { get; } = [];

        public Task RecordAsync(string jobName, DateTimeOffset startedAtUtc, DateTimeOffset finishedAtUtc,
            string outcome, int itemsProcessed, string? error, CancellationToken cancellationToken)
        {
            Records.Add((jobName, outcome, itemsProcessed, error));
            return Task.CompletedTask;
        }
    }

    private sealed class TestJob(IServiceProvider services, TimeProvider time, Func<Task<int>> body)
        : PlatformPeriodicJob(services, time, NullLogger.Instance)
    {
        protected override string JobName => "test_job";
        protected override TimeSpan Interval => TimeSpan.FromMinutes(5);
        protected override Task<int> TickAsync(IServiceProvider scopedServices, CancellationToken cancellationToken) => body();
    }

    private static (ServiceProvider Services, RecordingRecorder Recorder) BuildServices()
    {
        var recorder = new RecordingRecorder();
        var services = new ServiceCollection();
        services.AddScoped<IJobRunRecorder>(_ => recorder);
        return (services.BuildServiceProvider(), recorder);
    }

    [Fact]
    public async Task SuccessfulTick_RecordsSucceededWithItemCount()
    {
        var (services, recorder) = BuildServices();
        var job = new TestJob(services, TimeProvider.System, () => Task.FromResult(7));

        await job.RunOnceAsync(CancellationToken.None);

        var record = Assert.Single(recorder.Records);
        Assert.Equal("test_job", record.JobName);
        Assert.Equal(PlatformJobOutcomeNames.Succeeded, record.Outcome);
        Assert.Equal(7, record.Items);
        Assert.Null(record.Error);
    }

    [Fact]
    public async Task ThrowingTick_RecordsFailedAndSwallowsException()
    {
        var (services, recorder) = BuildServices();
        var job = new TestJob(services, TimeProvider.System, () => throw new InvalidOperationException("boom"));

        await job.RunOnceAsync(CancellationToken.None);

        var record = Assert.Single(recorder.Records);
        Assert.Equal(PlatformJobOutcomeNames.Failed, record.Outcome);
        Assert.Equal("boom", record.Error);
    }
}
