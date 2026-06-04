namespace AFK4.Shared.Contracts.Payments;

// One row in the owner cabinet list.
public sealed record OwnerPaymentGatewayDto(
    Guid BranchPaymentGatewayId,
    Guid? BranchId,
    string DcgateProjectId,
    string CardLast4,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record OwnerPaymentGatewayListResponse(
    IReadOnlyList<OwnerPaymentGatewayDto> Gateways);

// Phase 1 — provision. Null BranchId => org-level (network-wide) gateway.
public sealed record ProvisionPaymentGatewayRequest(
    Guid? BranchId,
    string CardNumber);

// Phase 2 — telegram attach.
public sealed record TelegramStartRequest(string Phone);
public sealed record TelegramStartResponse(string LoginAttemptId, string State);

public sealed record TelegramVerifyCodeRequest(string LoginAttemptId, string Code);
public sealed record TelegramVerifyPasswordRequest(string LoginAttemptId, string Password);
public sealed record TelegramVerifyResponse(string State, string GatewayStatus);

// Live dcgate status proxied to the cabinet.
public sealed record OwnerGatewayStatusResponse(
    string GatewayStatus,
    string SessionHealth,
    DateTimeOffset? LastConnectedAt,
    DateTimeOffset? LastMessageAt,
    int TelegramMessagesCount);
