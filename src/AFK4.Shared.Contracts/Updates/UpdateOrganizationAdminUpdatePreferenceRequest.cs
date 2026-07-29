namespace AFK4.Shared.Contracts.Updates;

public sealed record UpdateOrganizationAdminUpdatePreferenceRequest(
    Guid OrganizationId,
    TimeOnly MaintenanceWindowStart,
    TimeOnly MaintenanceWindowEnd);
