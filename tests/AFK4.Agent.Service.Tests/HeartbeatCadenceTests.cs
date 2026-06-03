using AFK4.Agent.Service;
using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Agent.Service.Tests;

public sealed class HeartbeatCadenceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-14T10:00:00Z");

    [Fact]
    public void NextDelay_SuccessfulHeartbeat_UsesNormalInterval()
    {
        var delay = HeartbeatCadence.NextDelay(
            lastHeartbeatSucceeded: true,
            lease: Lease(Now.AddMinutes(1)),
            normalIntervalSeconds: 10,
            Now,
            jitterFraction: 0);

        Assert.Equal(TimeSpan.FromSeconds(10), delay);
    }

    [Fact]
    public void NextDelay_FailedButLeaseFarFromExpiry_UsesNormalInterval()
    {
        var delay = HeartbeatCadence.NextDelay(
            lastHeartbeatSucceeded: false,
            lease: Lease(Now.AddMinutes(6)), // > 5 min RefreshThreshold
            normalIntervalSeconds: 10,
            Now,
            jitterFraction: 0);

        Assert.Equal(TimeSpan.FromSeconds(10), delay);
    }

    [Fact]
    public void NextDelay_FailedAndLeaseNearExpiry_Escalates()
    {
        var delay = HeartbeatCadence.NextDelay(
            lastHeartbeatSucceeded: false,
            lease: Lease(Now.AddMinutes(4)),
            normalIntervalSeconds: 10,
            Now,
            jitterFraction: 0);

        Assert.Equal(HeartbeatCadence.EscalatedBase, delay);
    }

    [Fact]
    public void NextDelay_FailedAndLeaseAlreadyExpiredWithinGrace_Escalates()
    {
        var delay = HeartbeatCadence.NextDelay(
            lastHeartbeatSucceeded: false,
            lease: Lease(Now.AddMinutes(-2)), // expired, still cached during grace
            normalIntervalSeconds: 10,
            Now,
            jitterFraction: 0);

        Assert.Equal(HeartbeatCadence.EscalatedBase, delay);
    }

    [Fact]
    public void NextDelay_Escalated_AddsProportionalJitter()
    {
        var delay = HeartbeatCadence.NextDelay(
            lastHeartbeatSucceeded: false,
            lease: Lease(Now.AddMinutes(1)),
            normalIntervalSeconds: 10,
            Now,
            jitterFraction: 1.0);

        Assert.Equal(HeartbeatCadence.EscalatedBase + HeartbeatCadence.EscalatedJitterMax, delay);
    }

    [Fact]
    public void NextDelay_FailedButNoLease_UsesNormalInterval()
    {
        var delay = HeartbeatCadence.NextDelay(
            lastHeartbeatSucceeded: false,
            lease: null,
            normalIntervalSeconds: 10,
            Now,
            jitterFraction: 0);

        Assert.Equal(TimeSpan.FromSeconds(10), delay);
    }

    private static SessionLeaseDto Lease(DateTimeOffset expiresAtUtc) =>
        new(
            SessionId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            SeatId: Guid.Parse("11111111-1111-4111-8111-111111111111"),
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            State: SessionStateNames.Active,
            Sequence: 1,
            IssuedAtUtc: Now.AddMinutes(-10),
            ExpiresAtUtc: expiresAtUtc,
            SignatureAlgorithm: "ECDSA-P256-SHA256",
            Signature: "signed-payload");
}
