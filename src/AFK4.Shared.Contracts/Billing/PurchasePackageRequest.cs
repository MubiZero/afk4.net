namespace AFK4.Shared.Contracts.Billing;

public sealed record PurchasePackageRequest(
    Guid OrganizationId,
    Guid PackageDefinitionId,
    string IdempotencyKey);
