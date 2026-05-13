namespace AFK4.Shared.Contracts.Billing;

public sealed record CreatePlayerAccountRequest(
    Guid OrganizationId,
    string DisplayName,
    string? PhoneNumber,
    string IdempotencyKey);
