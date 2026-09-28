using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// На каком языке писать игроку. Язык принадлежит человеку, а не клубу: приложение меняет его в
/// личности (<c>PATCH /api/me</c>), и уведомление любого клуба обязано прийти на нём. Язык клубной
/// карточки остаётся запасным — для карточек старого образца без личности и для личности, которая
/// язык ещё не выбирала.
/// </summary>
public static class PlayerNotificationLocale
{
    public static async Task<string> ResolveAsync(
        PlatformDbContext dbContext,
        Guid playerAccountId,
        CancellationToken cancellationToken)
    {
        var row = await dbContext.PlayerAccounts
            .AsNoTracking()
            .Where(account => account.PlayerAccountId == playerAccountId)
            .Select(account => new
            {
                PersonLocale = dbContext.PlatformPersons
                    .Where(person => person.PlatformPersonId == account.PlatformPersonId)
                    .Select(person => person.PreferredLocale)
                    .FirstOrDefault(),
                AccountLocale = account.PreferredLocale,
            })
            .FirstOrDefaultAsync(cancellationToken);

        return FirstChosen(row?.PersonLocale, row?.AccountLocale) ?? string.Empty;
    }

    private static string? FirstChosen(params string?[] locales) =>
        locales.FirstOrDefault(locale => !string.IsNullOrWhiteSpace(locale));
}
