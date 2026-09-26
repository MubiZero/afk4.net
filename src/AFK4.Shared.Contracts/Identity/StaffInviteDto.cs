namespace AFK4.Shared.Contracts.Identity;

/// <summary>The created staff invite; <see cref="Code"/> is returned once so an admin can also share it out of band.</summary>
public sealed record StaffInviteDto(
    Guid StaffInviteId,
    string Code,
    DateTimeOffset ExpiresAtUtc);

/// <summary>
/// Сотрудник, которого добавили, но он ещё не входил: код первого входа живой, истёк или исчерпал
/// попытки. Сам код не отдаётся — он хранится хешем; нужен новый — руководитель выдаёт новый.
/// </summary>
public sealed record StaffInviteSummaryDto(
    Guid StaffInviteId,
    string UserName,
    string DisplayName,
    string PhoneNumber,
    string? Email,
    IReadOnlyList<string> RoleNames,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    int AttemptsLeft,
    /// Одно из StaffInviteStatusNames.
    string Status);

public static class StaffInviteStatusNames
{
    /// <summary>Код действует: сотрудник может войти.</summary>
    public const string Pending = "pending";

    /// <summary>Сутки прошли — нужен новый код.</summary>
    public const string Expired = "expired";

    /// <summary>Три неверных кода — этот больше не пустит.</summary>
    public const string Exhausted = "exhausted";
}
