namespace AFK4.Shared.Contracts.Branches;

public sealed record UpdateBranchSettingsRequest(
    Guid OrganizationId,
    bool RequireManualDeviceApproval);
