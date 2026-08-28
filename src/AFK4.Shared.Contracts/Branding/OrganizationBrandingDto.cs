namespace AFK4.Shared.Contracts.Branding;

/// <summary>
/// Клуб, каким его видит своя веб-сборка: как он называется, как выглядит и из каких залов
/// состоит.
/// </summary>
/// <param name="Halls">
/// Залы сети — чтобы веб мог спросить «в какой вы придёте» до первого действия. Сеть из
/// нескольких залов иначе оказывается тупиком: счёт человеку открывает бронь или пополнение, а
/// зал для него сервер не гадает — и первый же запрос возвращает <c>branch_required</c>.
/// Мобильное приложение берёт залы из каталога клубов; у веба каталога нет — он и так знает
/// свой клуб, — поэтому залы едут сюда, вместе с остальным «что это за клуб».
/// </param>
public sealed record OrganizationBrandingDto(
    Guid OrganizationId,
    string Name,
    string? LogoUrl,
    string? AccentColor,
    IReadOnlyList<BrandingHallDto>? Halls = null);

/// <summary>Зал сети настолько, насколько его нужно узнать при выборе: как зовут и где стоит.</summary>
public sealed record BrandingHallDto(
    Guid BranchId,
    string Name,
    string City,
    string? Address);
