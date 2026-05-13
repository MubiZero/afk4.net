namespace AFK4.Shared.Contracts.Billing;

public sealed record PlayerAccountDto(
    Guid PlayerAccountId,
    Guid OrganizationId,
    Guid HomeBranchId,
    string DisplayName,
    string? PhoneNumber,
    bool IsActive,
    DateTimeOffset CreatedAtUtc);
