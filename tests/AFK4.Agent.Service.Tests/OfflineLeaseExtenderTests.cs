using AFK4.Agent.Service.Enforcement;
using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Agent.Service.Tests;

public sealed class OfflineLeaseExtenderTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-14T10:00:00Z");

    [Fact]
    public void ShouldExtend_ActiveLease_WithinGraceFromLastContact_IsTrue()
    {
        var grace = new OfflineGraceState();
        grace.RecordSuccessfulContact(Now.AddMinutes(-10), effectiveGraceMinutes: 15);
        var extender = new OfflineLeaseExtender(grace);

        Assert.True(extender.ShouldExtend(Lease(SessionStateNames.Active), Now));
    }

    [Fact]
    public void ShouldExtend_PastGraceFromLastContact_IsFalse()
    {
        var grace = new OfflineGraceState();
        grace.RecordSuccessfulContact(Now.AddMinutes(-20), effectiveGraceMinutes: 15);
        var extender = new OfflineLeaseExtender(grace);

        Assert.False(extender.ShouldExtend(Lease(SessionStateNames.Active), Now));
    }

    [Fact]
    public void ShouldExtend_ExactlyAtGraceBoundary_IsFalse()
    {
        var grace = new OfflineGraceState();
        grace.RecordSuccessfulContact(Now.AddMinutes(-15), effectiveGraceMinutes: 15);
        var extender = new OfflineLeaseExtender(grace);

        Assert.False(extender.ShouldExtend(Lease(SessionStateNames.Active), Now));
    }

    [Fact]
    public void ShouldExtend_NeverContacted_IsFalse()
    {
        var extender = new OfflineLeaseExtender(new OfflineGraceState());

        Assert.False(extender.ShouldExtend(Lease(SessionStateNames.Active), Now));
    }

    [Theory]
    [InlineData(SessionStateNames.Ending)]
    [InlineData(SessionStateNames.Paused)]
    public void ShouldExtend_NonActiveLease_IsFalse(string state)
    {
        var grace = new OfflineGraceState();
        grace.RecordSuccessfulContact(Now.AddMinutes(-1), effectiveGraceMinutes: 15);
        var extender = new OfflineLeaseExtender(grace);

        Assert.False(extender.ShouldExtend(Lease(state), Now));
    }

    private static SessionLeaseDto Lease(string state) =>
        new(
            SessionId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            SeatId: Guid.Parse("11111111-1111-4111-8111-111111111111"),
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            State: state,
            Sequence: 1,
            IssuedAtUtc: Now.AddMinutes(-15),
            ExpiresAtUtc: Now.AddSeconds(-1),
            SignatureAlgorithm: "ECDSA-P256-SHA256",
            Signature: "signed-payload");
}
