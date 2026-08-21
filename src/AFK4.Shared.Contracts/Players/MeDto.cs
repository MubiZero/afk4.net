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
    bool NetworkBanned);

/// <summary>
/// Один клуб глазами игрока: сколько можно потратить, сколько придержано под брони, сколько
/// он должен и сколько раз приходил.
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
