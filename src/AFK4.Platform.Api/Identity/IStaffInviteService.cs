namespace AFK4.Platform.Api.Identity;

/// <summary>
/// Additive email-invite onboarding for staff: create an invite (emails an opaque token) and
/// accept it (creates the active staff user with their chosen password + branch roles). Runs
/// alongside inline-password staff creation; it does not replace it.
/// </summary>
public interface IStaffInviteService
{
    Task<StaffInviteCreateResult> CreateInviteAsync(
        Guid organizationId,
        Guid branchId,
        string userName,
        string displayName,
        string email,
        IReadOnlyList<string> roleNames,
        CancellationToken cancellationToken);

    Task<StaffInviteAcceptResult> AcceptInviteAsync(string token, string password, CancellationToken cancellationToken);
}

public sealed record StaffInviteCreateResult(
    bool Succeeded,
    string? Error,
    Guid StaffInviteId,
    string Code,
    DateTimeOffset ExpiresAtUtc)
{
    public static StaffInviteCreateResult Failed(string error) =>
        new(false, error, Guid.Empty, string.Empty, default);

    public static StaffInviteCreateResult Success(Guid staffInviteId, string code, DateTimeOffset expiresAtUtc) =>
        new(true, null, staffInviteId, code, expiresAtUtc);
}

public sealed record StaffInviteAcceptResult(
    bool Succeeded,
    string? Error,
    Guid OrganizationId,
    string UserName)
{
    public static StaffInviteAcceptResult Failed(string error) =>
        new(false, error, Guid.Empty, string.Empty);

    public static StaffInviteAcceptResult Success(Guid organizationId, string userName) =>
        new(true, null, organizationId, userName);
}
