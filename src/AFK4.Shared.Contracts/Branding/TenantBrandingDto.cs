namespace AFK4.Shared.Contracts.Branding;

public sealed record TenantBrandingDto(
    Guid OrganizationId,
    string Name,
    string? LogoUrl,
    string? AccentColor);
