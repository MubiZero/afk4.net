namespace AFK4.Shared.Contracts.Sessions;

public sealed record SessionReconciliationResponse(
    string Action,
    string Reason,
    Guid? SessionId,
    SessionLeaseDto? Lease);
