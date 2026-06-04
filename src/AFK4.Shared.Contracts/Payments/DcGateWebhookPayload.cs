using System;

namespace AFK4.Shared.Contracts.Payments;

public sealed record DcGateWebhookPayload(
    string EventId,
    string EventType,
    string ProjectId,
    DcGateWebhookPaymentDto Payment);

public sealed record DcGateWebhookPaymentDto(
    string Id,
    string Amount,
    string Comment,
    string Currency,
    string ExternalOrderId,
    DateTimeOffset? PaidAt,
    string Status);
