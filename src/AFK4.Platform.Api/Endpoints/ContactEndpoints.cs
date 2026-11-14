using System.Net;
using System.Text;
using AFK4.Platform.Api.Notifications.Telegram;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Endpoints;

/// <summary>Куда приходят сообщения с формы связи на публичном сайте afk4.net: чат владельца в Telegram.</summary>
public sealed class ContactOptions
{
    public const string SectionName = "Contact";

    /// <summary>Чат владельца, куда бот AFK4.NET присылает заявки (<c>Contact__TelegramChatId</c>).</summary>
    public long TelegramChatId { get; set; }

    public bool IsConfigured => TelegramChatId != 0;
}

/// <summary>Тело формы связи. Поле <c>Website</c> — ловушка для ботов: человек его не видит и не заполняет.</summary>
public sealed record ContactRequestDto(string? Name, string? Club, string? Contact, string? Message, string? Lang, string? Website);

/// <summary>
/// Форма связи публичного сайта: владелец клуба оставляет имя, клуб и телефон или Telegram, сообщение уходит
/// владельцу проекта в Telegram. Ручка без входа, поэтому с пределом на адрес, ловушкой для ботов и
/// своей политикой CORS: только адреса сайта и без cookie.
/// </summary>
internal static class ContactEndpoints
{
    public const string CorsPolicyName = "public-site";

    private const int NameMax = 80;
    private const int ContactMax = 80;
    private const int ClubMax = 120;
    private const int MessageMax = 1000;

    public static void MapContactEndpoints(this WebApplication app)
    {
        app.MapPost("/api/public/contact", async (
            ContactRequestDto request,
            IOptions<ContactOptions> contact,
            IOptions<TelegramOptions> telegram,
            ITelegramBotApi bot,
            ILogger<ContactRequestDto> logger,
            CancellationToken cancellationToken) =>
        {
            // Бот заполняет все поля подряд, человек скрытое не видит: молча говорим «принято» и ничего не шлём.
            if (!string.IsNullOrWhiteSpace(request.Website))
            {
                return Results.Ok(new { ok = true });
            }

            var name = Clean(request.Name, NameMax);
            var contactValue = Clean(request.Contact, ContactMax);
            if (name.Length == 0 || contactValue.Length == 0)
            {
                return Results.BadRequest(new { error = "name and contact are required", code = "required" });
            }

            if (!contact.Value.IsConfigured || !telegram.Value.IsConfigured)
            {
                return Results.Json(
                    new { error = "contact form is not configured", code = "not-configured" },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var club = Clean(request.Club, ClubMax);
            var message = Clean(request.Message, MessageMax);
            var lang = request.Lang is "ru" or "tg" or "en" ? request.Lang : "?";

            var html = new StringBuilder("<b>Сообщение с сайта afk4.net</b>\n\n");
            html.Append("Имя: ").Append(WebUtility.HtmlEncode(name)).Append('\n');
            html.Append("Контакт: ").Append(WebUtility.HtmlEncode(contactValue)).Append('\n');
            if (club.Length > 0) html.Append("Клуб: ").Append(WebUtility.HtmlEncode(club)).Append('\n');
            if (message.Length > 0) html.Append("\n").Append(WebUtility.HtmlEncode(message)).Append('\n');
            html.Append("\nЯзык страницы: ").Append(lang);

            try
            {
                await bot.SendHtmlAsync(contact.Value.TelegramChatId, html.ToString(), cancellationToken);
            }
            catch (TelegramApiException exception)
            {
                logger.LogWarning(exception, "Contact form message was not delivered to Telegram.");
                return Results.Json(
                    new { error = "telegram is unreachable", code = "unreachable" },
                    statusCode: StatusCodes.Status502BadGateway);
            }

            return Results.Ok(new { ok = true });
        })
        .RequireRateLimiting("contact-public")
        .RequireCors(CorsPolicyName);
    }

    // Ничего, кроме обрезанного текста: переводы строк в имени и контакте убираем, чтобы нельзя было «дорисовать» чужие строки.
    private static string Clean(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var text = value.Trim();
        return text.Length > max ? text[..max] : text;
    }
}
