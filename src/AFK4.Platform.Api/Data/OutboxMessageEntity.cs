namespace AFK4.Platform.Api.Data;

/// <summary>
/// One queued downstream effect of a committed money-moving command (session start / extend /
/// checkout). The row is written in the <em>same transaction</em> as the ledger rows it follows, so
/// the effect (lock notify, settlement confirmation, notification fan-out) can never run without a
/// committed charge and a committed charge can never be left without its effect. A hosted
/// <c>OutboxDispatcher</c> drains <see cref="OutboxMessageStatus.Pending"/> rows with backoff;
/// dispatch is idempotent on <see cref="IdempotencyKey"/> so redelivery never double-charges.
/// </summary>
public sealed class OutboxMessageEntity
{
    public Guid OutboxMessageId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    /// <summary>Handler discriminator (e.g. <c>session.checkout</c>); routes the row to its <c>IOutboxMessageHandler</c>.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>The handler's input, serialized. Money amounts stay <see cref="long"/> minor units inside the JSON.</summary>
    public string PayloadJson { get; set; } = string.Empty;

    public string Status { get; set; } = OutboxMessageStatus.Pending;

    public int AttemptCount { get; set; }

    /// <summary>Earliest time a <see cref="OutboxMessageStatus.Pending"/> row may be dispatched; advanced by backoff on retry.</summary>
    public DateTimeOffset AvailableAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? DispatchedAtUtc { get; set; }

    /// <summary>Caller-supplied key (reuses the command's idempotency key), unique — makes redelivery safe.</summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    public string? LastError { get; set; }
}

/// <summary>The closed set of <see cref="OutboxMessageEntity.Status"/> values (§6.3).</summary>
public static class OutboxMessageStatus
{
    public const string Pending = "Pending";
    public const string Dispatched = "Dispatched";
    public const string Failed = "Failed";
}
