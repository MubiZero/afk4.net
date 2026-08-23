using AFK4.Shared.Contracts.Platform.Organizations;

namespace AFK4.Platform.Api.Identity;

/// <summary>
/// Единственный путь завести сотрудника клуба: владелец приглашает по номеру телефона, человек
/// принимает приглашение коротким кодом из SMS и придумывает себе пароль сам. Заведения с готовым
/// паролем нет намеренно — пароль должен знать только его владелец.
/// </summary>
public interface IStaffInviteService
{
    Task<StaffInviteCreateResult> CreateInviteAsync(
        Guid organizationId,
        Guid branchId,
        string userName,
        string displayName,
        string phoneNumber,
        string? email,
        IReadOnlyList<string> roleNames,
        CancellationToken cancellationToken);

    Task<StaffInviteAcceptResult> AcceptInviteAsync(
        string phoneNumber, string code, string password, CancellationToken cancellationToken);
}

public sealed record StaffInviteCreateResult(
    bool Succeeded,
    string? Error,
    Guid StaffInviteId,
    string Code,
    DateTimeOffset ExpiresAtUtc,
    PlanLimitExceededDto? PlanLimit = null)
{
    public static StaffInviteCreateResult Failed(string error) =>
        new(false, error, Guid.Empty, string.Empty, default);

    public static StaffInviteCreateResult Success(Guid staffInviteId, string code, DateTimeOffset expiresAtUtc) =>
        new(true, null, staffInviteId, code, expiresAtUtc);

    public static StaffInviteCreateResult PlanLimitReached(PlanLimitExceededDto planLimit) =>
        new(false, "Plan staff limit for this branch has been reached.", Guid.Empty, string.Empty, default, planLimit);
}

/// <summary>
/// Исходы приёма повторяют сброс пароля по телефону слово в слово: человек по ту сторону — тот
/// же самый, и два разных языка отказов на двух соседних экранах он читал бы как два разных сбоя.
/// </summary>
public enum StaffInviteAcceptStatus
{
    Success,
    NoActiveInvite,
    Expired,
    InvalidCode,
    TooManyAttempts,
    Refused,
}

public sealed record StaffInviteAcceptResult(
    StaffInviteAcceptStatus Status,
    string? Error,
    Guid OrganizationId,
    string UserName,
    int RemainingAttempts = 0,
    PlanLimitExceededDto? PlanLimit = null)
{
    public bool Succeeded => Status == StaffInviteAcceptStatus.Success;

    public static StaffInviteAcceptResult Failed(string error) =>
        new(StaffInviteAcceptStatus.Refused, error, Guid.Empty, string.Empty);

    public static StaffInviteAcceptResult Success(Guid organizationId, string userName) =>
        new(StaffInviteAcceptStatus.Success, null, organizationId, userName);

    public static StaffInviteAcceptResult NoActiveInvite() =>
        new(StaffInviteAcceptStatus.NoActiveInvite, "There is no active invite for this phone number.",
            Guid.Empty, string.Empty);

    public static StaffInviteAcceptResult Expired() =>
        new(StaffInviteAcceptStatus.Expired, "The invite has expired.", Guid.Empty, string.Empty);

    public static StaffInviteAcceptResult InvalidCode(int remainingAttempts) =>
        new(StaffInviteAcceptStatus.InvalidCode, "The code is not correct.", Guid.Empty, string.Empty,
            remainingAttempts);

    public static StaffInviteAcceptResult TooManyAttempts() =>
        new(StaffInviteAcceptStatus.TooManyAttempts, "Too many attempts.", Guid.Empty, string.Empty);

    public static StaffInviteAcceptResult PlanLimitReached(PlanLimitExceededDto planLimit) =>
        new(StaffInviteAcceptStatus.Refused, "Plan staff limit for this branch has been reached.",
            Guid.Empty, string.Empty, 0, planLimit);
}
