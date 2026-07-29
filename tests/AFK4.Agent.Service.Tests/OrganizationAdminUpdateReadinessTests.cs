using AFK4.Agent.Service;
using AFK4.Agent.Service.Updates;
using AFK4.Shared.Contracts.Updates;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Tests;

public sealed class OrganizationAdminUpdateReadinessTests
{
    [Fact]
    public async Task EvaluateAsync_WhenAppIsClosed_InstallsImmediately()
    {
        var readiness = CreateReadinessFromStates(new OrganizationAdminCoordinationResult(LocalUpdateCoordinationStatuses.NotRunning, "closed"));

        var result = await readiness.EvaluateAsync(Instruction(), Preference(new(4, 0), new(5, 0)),
            DateTimeOffset.Parse("2026-07-29T00:00:00Z"), CancellationToken.None);

        Assert.Equal(OrganizationAdminUpdateReadinessNames.InstallNow, result.Status);
    }

    [Fact]
    public async Task EvaluateAsync_WhenIdleInsideWindow_WaitsForAcknowledgedExit()
    {
        var client = new SequenceCoordinatorClient(
            new(LocalUpdateCoordinationStatuses.Idle, "idle"),
            new(LocalUpdateCoordinationStatuses.NotRunning, "closed"))
        { ShutdownResult = new(LocalUpdateCoordinationStatuses.ShutdownAcknowledged, "accepted") };
        var readiness = CreateReadiness(client);

        var result = await readiness.EvaluateAsync(Instruction(), Preference(new(4, 0), new(5, 0)),
            DateTimeOffset.Parse("2026-07-29T04:30:00Z"), CancellationToken.None);

        Assert.Equal(OrganizationAdminUpdateReadinessNames.ReadyAfterShutdown, result.Status);
        Assert.Equal(Instruction().UpdateRolloutId, client.RolloutId);
        Assert.Equal(Instruction().UpdatePackageId, client.PackageId);
    }

    [Fact]
    public async Task EvaluateAsync_WhenCriticalCommandIsActive_DefersWithoutShutdown()
    {
        var client = new SequenceCoordinatorClient(new OrganizationAdminCoordinationResult(LocalUpdateCoordinationStatuses.CriticalCommandActive, "busy"));
        var readiness = CreateReadiness(client);

        var result = await readiness.EvaluateAsync(Instruction(), Preference(new(4, 0), new(5, 0)),
            DateTimeOffset.Parse("2026-07-29T04:30:00Z"), CancellationToken.None);

        Assert.Equal(OrganizationAdminUpdateReadinessNames.DeferredCriticalCommand, result.Status);
        Assert.Null(client.RolloutId);
    }

    [Theory]
    [InlineData("2026-07-29T23:30:00Z", true)]
    [InlineData("2026-07-30T00:30:00Z", true)]
    [InlineData("2026-07-30T02:00:00Z", false)]
    public void IsInsideWindow_HandlesCrossMidnight(string utc, bool expected)
    {
        Assert.Equal(expected, OrganizationAdminUpdateReadiness.IsInsideWindow(
            Preference(new(23, 0), new(1, 0)), DateTimeOffset.Parse(utc)));
    }

    private static OrganizationAdminUpdateReadiness CreateReadinessFromStates(params OrganizationAdminCoordinationResult[] states) =>
        CreateReadiness(new SequenceCoordinatorClient(states));

    private static OrganizationAdminUpdateReadiness CreateReadiness(SequenceCoordinatorClient client) =>
        new(client, Options.Create(new AgentOptions { OrganizationAdminUpdateExitPollMilliseconds = 10 }));

    private static OrganizationAdminUpdatePreferenceDto Preference(TimeOnly start, TimeOnly end) =>
        new(Guid.NewGuid(), Guid.NewGuid(), start, end, "UTC");

    private static ComponentUpdateInstructionDto Instruction() => new(
        Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb"), Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
        UpdateComponentNames.OrganizationAdmin, "1.2.3", "internal", "https://updates.test/admin.msi", "sha", "sig", "ed25519", 1, "notes");

    private sealed class SequenceCoordinatorClient(params OrganizationAdminCoordinationResult[] states)
        : IOrganizationAdminUpdateCoordinatorClient
    {
        private readonly Queue<OrganizationAdminCoordinationResult> queue = new(states);
        public OrganizationAdminCoordinationResult ShutdownResult { get; init; } = new(LocalUpdateCoordinationStatuses.Rejected, "rejected");
        public Guid? RolloutId { get; private set; }
        public Guid? PackageId { get; private set; }

        public Task<OrganizationAdminCoordinationResult> QueryStateAsync(CancellationToken cancellationToken) =>
            Task.FromResult(queue.Dequeue());

        public Task<OrganizationAdminCoordinationResult> RequestShutdownAsync(Guid updateRolloutId, Guid updatePackageId, CancellationToken cancellationToken)
        {
            RolloutId = updateRolloutId;
            PackageId = updatePackageId;
            return Task.FromResult(ShutdownResult);
        }
    }
}
