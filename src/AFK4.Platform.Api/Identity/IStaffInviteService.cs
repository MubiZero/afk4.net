using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Platform.Organizations;

namespace AFK4.Platform.Api.Identity;

/// <param name="Code">Код приглашения, который человек вводит при приёме (не путать с
/// <paramref name="ErrorCode"/>).</param>
/// <param name="ErrorCode">
/// Машинное имя отказа (<see cref="Shared.Contracts.Identity.StaffInviteErrorCodeNames"/>).
/// Мастер установки работает на трёх языках, а <paramref name="Error"/> всегда английский —
/// назвать причину человеку можно только по коду.
/// </param>
public sealed record StaffInviteCreateResult(
    bool Succeeded,
    string? Error,
    Guid StaffInviteId,
    string Code,
    DateTimeOffset ExpiresAtUtc,
    PlanLimitExceededDto? PlanLimit = null,
    string? ErrorCode = null)
{
    public static StaffInviteCreateResult Failed(string error, string? errorCode = null) =>
        new(false, error, Guid.Empty, string.Empty, default, null, errorCode);

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
    PlanLimitExceededDto? PlanLimit = null,
    Guid StaffUserId = default,
    // Какое приглашение ответило — и при отказе тоже: журнал должен видеть подбор кода.
    Guid? StaffInviteId = null,
    Guid BranchId = default)
{
    public bool Succeeded => Status == StaffInviteAcceptStatus.Success;

    public static StaffInviteAcceptResult Failed(string error) =>
        new(StaffInviteAcceptStatus.Refused, error, Guid.Empty, string.Empty);

    public static StaffInviteAcceptResult Success(Guid organizationId, string userName, Guid staffUserId) =>
        new(StaffInviteAcceptStatus.Success, null, organizationId, userName, StaffUserId: staffUserId);

    /// <summary>Код верный; приглашение по-прежнему ждёт ПИНа.</summary>
    public static StaffInviteAcceptResult CodeAccepted() =>
        new(StaffInviteAcceptStatus.Success, null, Guid.Empty, string.Empty);

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
