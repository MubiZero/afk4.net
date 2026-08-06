namespace AFK4.Shared.Contracts.Platform.Organizations;

public sealed record UpdateOrganizationProfileRequest(
    string Name,
    string? ContactEmail,
    string? ContactPhone,
    string? LegalDetails);
