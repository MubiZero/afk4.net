namespace AFK4.Shared.Contracts.Players;

/// <summary>
/// Войти на ПК с телефона (спека оболочки, §5.4): приложение сканирует QR с монитора — в нём код
/// посадки — и просит сервер впустить своего человека на эту машину. Номер и ПИН-код у ПК при
/// этом не набираются вовсе.
/// </summary>
public sealed record CreatePlayerSignInClaimRequest(
    string SeatingCode,
    string IdempotencyKey);

/// <summary>Заявка на вход и что с ней стало: приложение показывает «Вы вошли на ПК 07».</summary>
public sealed record PlayerSignInClaimDto(
    Guid ClaimId,
    // Одно из PlayerSignInClaimStatusNames.
    string Status,
    DateTimeOffset ExpiresAtUtc,
    // Имя места: «ПК 07». Пусто, если ПК не привязан к месту.
    string? SeatLabel);

public static class PlayerSignInClaimStatusNames
{
    /// <summary>ПК ещё не забрал заявку.</summary>
    public const string Pending = "pending";

    /// <summary>ПК забрал заявку — человек вошёл.</summary>
    public const string Redeemed = "redeemed";

    /// <summary>ПК не забрал заявку за отведённое время.</summary>
    public const string Expired = "expired";
}

/// <summary>Почему код посадки не приняли. Одни и те же у старта с телефона и у заявки на вход.</summary>
public static class SeatingCodeErrorCodeNames
{
    /// <summary>
    /// Код не подошёл. Чужой клуб, истёкший код и опечатка снаружи неразличимы: иначе перебор
    /// шестизначных цифр становится осмысленным.
    /// </summary>
    public const string Invalid = "seating_code_invalid";

    /// <summary>Слишком много неверных кодов — у человека или у всего клуба; ответ несёт, когда можно снова.</summary>
    public const string AttemptsExceeded = "seating_code_attempts_exceeded";

    /// <summary>Заявке нужен аккаунт AFK4, а у входа — только клубная карточка старого образца.</summary>
    public const string PlatformAccountRequired = "platform_account_required";
}
