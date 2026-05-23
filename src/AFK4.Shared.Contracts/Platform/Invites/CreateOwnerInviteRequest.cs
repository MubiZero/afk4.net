namespace AFK4.Shared.Contracts.Platform.Invites;

public sealed record CreateOwnerInviteRequest(
    Guid BranchId,
    string? OwnerUserName,
    string? OwnerDisplayName,
    TimeSpan? Lifetime);
