namespace AFK4.Shared.Contracts.Identity;

/// <summary>Первый шаг входа сотрудника: только номер.</summary>
public sealed record StaffSignInNextStepRequest(string PhoneNumber);

/// <summary>Что спросить у человека вторым шагом (<see cref="StaffSignInStepNames"/>).</summary>
public sealed record StaffSignInNextStepResponse(string Step);

/// <summary>
/// Второй шаг входа по номеру. Код первого входа нужен, потому что номер — не секрет: без него
/// ПИН новому сотруднику успел бы назначить любой, кто знает его номер.
/// </summary>
public static class StaffSignInStepNames
{
    /// <summary>У номера есть ПИН — спросить его.</summary>
    public const string Pin = "pin";

    /// <summary>Руководитель добавил сотрудника, тот ещё не входил: спросить код первого входа, потом новый ПИН.</summary>
    public const string InviteCode = "invite-code";

    /// <summary>Код первого входа истёк или исчерпал попытки — нужен новый от руководителя.</summary>
    public const string InviteExpired = "invite-expired";

    /// <summary>Номер не заведён ни в одном клубе.</summary>
    public const string Unknown = "unknown";
}

/// <summary>Проверка кода первого входа до того, как человек придумывает ПИН.</summary>
public sealed record CheckStaffInviteRequest(string PhoneNumber, string Code);
