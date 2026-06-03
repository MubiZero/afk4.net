namespace AFK4.Shared.Contracts.Identity;

/// <summary>The created staff invite; <see cref="Code"/> is returned once so an admin can also share it out of band.</summary>
public sealed record StaffInviteDto(
    Guid StaffInviteId,
    string Code,
    DateTimeOffset ExpiresAtUtc);
