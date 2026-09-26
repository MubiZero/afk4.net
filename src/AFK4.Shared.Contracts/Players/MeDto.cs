namespace AFK4.Shared.Contracts.Players;

/// <summary>
/// Человек и его клубы одним ответом. Приложение открывается на этом: сначала «кто я», потом
/// «где у меня что». Общей суммы денег здесь нет и не будет — у каждого клуба своя касса, и
/// складывать остатки разных клубов значит показать число, которое ниоткуда нельзя потратить.
/// </summary>
public sealed record MeDto(MePersonDto Person, IReadOnlyList<MyClubDto> Clubs);

/// <summary>
/// Личность: то, что принадлежит человеку, а не клубу. PIN сюда не попадает никогда — только
/// признак, задан он или ещё нет.
/// </summary>
public sealed record MePersonDto(
    Guid PlatformPersonId,
    string PhoneNumber,
    string DisplayName,
    string? PreferredLocale,
    bool PhoneVerified,
    bool PinSet,
    bool NetworkBanned,
    // За что закрыт вход. Запрет, о котором человек не может узнать причину, читается как поломка
    // приложения — и он идёт спорить к стойке, которая его не ставила.
    string? NetworkBanReason,
    // День рождения, если человек его ввёл: по желанию, для подарка клуба и игр с возрастом.
    DateOnly? BirthDate = null);

/// <summary>День рождения в профиле; null стирает его.</summary>
public sealed record SetBirthDateRequest(DateOnly? BirthDate);

/// <summary>
/// Один клуб глазами игрока: сколько можно потратить, сколько придержано под брони, сколько
/// он должен и сколько раз приходил.
///
/// Клуба нет в списке — значит человек в нём ещё ничего не делал, и счёта там пока нет. Это
/// нормальное состояние, а не сбой: показывать его ошибкой значит пугать на ровном месте.
/// </summary>
public sealed record MyClubDto(
    Guid OrganizationId,
    string OrganizationName,
    Guid PlayerAccountId,
    Guid HomeBranchId,
    string CurrencyCode,
    long WalletBalanceMinorUnits,
    long HeldMinorUnits,
    long DebtMinorUnits,
    int VisitCount);
