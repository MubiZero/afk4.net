namespace AFK4.Shared.Contracts.Updates;

public sealed record UpdateRolloutStateChangeRequest(
    Guid OrganizationId,
    string State,
    string Reason);
