namespace AFK4.Shared.Contracts.Platform.Tenants;

public sealed record UpdateTenantStatusRequest(
    string Status,
    string Reason);
