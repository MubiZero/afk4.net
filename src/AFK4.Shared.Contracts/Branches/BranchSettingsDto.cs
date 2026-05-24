namespace AFK4.Shared.Contracts.Branches;

public sealed record BranchSettingsDto(
    Guid OrganizationId,
    Guid BranchId,
    bool RequireManualDeviceApproval);
