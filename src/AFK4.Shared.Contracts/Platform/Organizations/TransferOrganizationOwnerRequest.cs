namespace AFK4.Shared.Contracts.Platform.Organizations;

public sealed record TransferOrganizationOwnerRequest(
    string NewOwnerEmail,
    string Reason);
