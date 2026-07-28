namespace AFK4.Shared.Contracts.Branding;

public sealed record OrganizationBrandingDto(
    Guid OrganizationId,
    string Name,
    string? LogoUrl,
    string? AccentColor);
