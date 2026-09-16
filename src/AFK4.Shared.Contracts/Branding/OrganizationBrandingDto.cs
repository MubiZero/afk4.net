namespace AFK4.Shared.Contracts.Branding;

/// <summary>Клуб, каким его видит мастер установки: как называется и как выглядит.</summary>
public sealed record OrganizationBrandingDto(
    Guid OrganizationId,
    string Name,
    string? LogoUrl,
    string? AccentColor);
