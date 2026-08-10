using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.Sessions;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Platform.Api.Tests.Sessions;

// Тестовые двойники старта сессии, общие для Postgres-тестов этого namespace: раньше каждый файл
// держал собственные приватные копии, и любой новый тест начинался с их переписывания.

internal sealed class SavingCommandDispatchService(PlatformDbContext db) : IDeviceCommandDispatchService
{
    private readonly EfDeviceCommandStore store = new(db);

    public List<(Guid DeviceId, DeviceCommandDto Command)> Notifications { get; } = [];

    public async Task<DeviceCommandDto> EnqueueAsync(
        Guid deviceId,
        CreateDeviceCommandRequest request,
        CancellationToken cancellationToken)
    {
        var command = new DeviceCommandDto(Guid.NewGuid(), request.Type, DateTimeOffset.UtcNow, request.Payload);
        await store.AddPendingAsync(deviceId, command, cancellationToken);
        return command;
    }

    public Task NotifyAsync(Guid deviceId, DeviceCommandDto command, CancellationToken cancellationToken)
    {
        Notifications.Add((deviceId, command));
        return Task.CompletedTask;
    }

    public async Task<DeviceCommandDto> DispatchAsync(
        Guid deviceId,
        CreateDeviceCommandRequest request,
        CancellationToken cancellationToken)
    {
        var command = await EnqueueAsync(deviceId, request, cancellationToken);
        await NotifyAsync(deviceId, command, cancellationToken);
        return command;
    }
}

internal sealed class TrackingSessionBillingService(
    PlatformDbContext db,
    Guid organizationId,
    Guid branchId) : ISessionBillingService
{
    public Task<SessionBillingValidationResult> ValidateStartAsync(Guid organizationId, Guid branchId, Guid? playerAccountId, string billingMode, Guid? tariffVersionId, Guid? playerPackageId, int durationMinutes, CancellationToken cancellationToken) => Task.FromResult(Valid(durationMinutes));
    public Task<SessionBillingValidationResult> ComputeCompValueAsync(Guid organizationId, Guid branchId, Guid tariffVersionId, int durationMinutes, CancellationToken cancellationToken) => Task.FromResult(Valid(durationMinutes));
    public Task<SessionBillingValidationResult> ValidateExtendAsync(Guid organizationId, Guid branchId, Guid? playerAccountId, string billingMode, Guid? tariffVersionId, Guid? playerPackageId, int additionalMinutes, CancellationToken cancellationToken) => Task.FromResult(Valid(additionalMinutes));
    public Task AppendExtendLedgerEntriesAsync(Guid sessionId, Guid actorStaffUserId, SessionBillingValidationResult validation, Guid playerAccountId, Guid? playerPackageId, string billingMode, DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task<SessionBillingValidationResult> ComputeCheckoutChargeAsync(Guid sessionId, DateTimeOffset now, CancellationToken cancellationToken) => Task.FromResult(Valid(0));
    public Task AppendCheckoutLedgerEntriesAsync(Guid sessionId, Guid actorStaffUserId, SessionBillingValidationResult validation, Guid playerAccountId, DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task AppendStartLedgerEntriesAsync(
        Guid sessionId,
        Guid actorStaffUserId,
        SessionBillingValidationResult validation,
        Guid playerAccountId,
        Guid? playerPackageId,
        string billingMode,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        db.LedgerEntries.Add(new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            PlayerAccountId = playerAccountId,
            SessionId = sessionId,
            EntryType = LedgerEntryTypeNames.GameplayCharge,
            AccountType = LedgerAccountTypeNames.Wallet,
            AmountMinorUnits = -100,
            CurrencyCode = "TJS",
            Description = "rollback proof",
            Reason = "session-start",
            CreatedByStaffUserId = actorStaffUserId,
            CreatedAtUtc = now
        });
        return Task.CompletedTask;
    }

    private static SessionBillingValidationResult Valid(int minutes) =>
        new(true, null, "postgres-rollback", null, minutes * 60, 100, "TJS");
}

internal sealed class RecordingLifecycleNotifier : ISessionLifecycleNotifier
{
    public List<SessionLifecycleChangedDto> Events { get; } = [];

    public Task NotifyAsync(SessionLifecycleChangedDto change, CancellationToken cancellationToken)
    {
        Events.Add(change);
        return Task.CompletedTask;
    }
}

internal sealed class FakeSessionLeaseSigner : ISessionLeaseSigner
{
    public SessionLeaseDto Sign(Guid SessionId, Guid OrganizationId, Guid BranchId, Guid SeatId, Guid DeviceId, string State, int Sequence, DateTimeOffset IssuedAtUtc, DateTimeOffset ExpiresAtUtc) =>
        new(SessionId, OrganizationId, BranchId, SeatId, DeviceId, State, Sequence, IssuedAtUtc, ExpiresAtUtc, EcdsaSessionLeaseSigner.SignatureAlgorithm, $"postgres-signature-{Sequence}");
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
