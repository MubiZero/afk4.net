namespace AFK4.Shared.Contracts.Updates;

public sealed record OrganizationAdminUpdatePreferenceDto(
    Guid OrganizationId,
    Guid BranchId,
    TimeOnly MaintenanceWindowStart,
    TimeOnly MaintenanceWindowEnd,
    string TimeZone);
