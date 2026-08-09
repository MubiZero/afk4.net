using System.Text.Json;
using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.Platform.Announcements;

/// <summary>
/// Единственное место, где живёт правило «подходит ли клуб под аудиторию анонса». Его зовут оба
/// пути — выдача полосы клубу и разворачивание получателей письма. Разведи их по двум копиям
/// правила, и однажды полоса покажется тому, кому письмо не ушло, а заметит это клиент.
/// </summary>
public static class AnnouncementAudience
{
    public static IReadOnlySet<string> ParsePlanCodes(string json) =>
        new HashSet<string>(Parse<string>(json), StringComparer.OrdinalIgnoreCase);

    public static IReadOnlySet<Guid> ParseOrganizationIds(string json) =>
        new HashSet<Guid>(Parse<Guid>(json));

    public static bool Matches(
        string audienceKind,
        IReadOnlySet<string> planCodes,
        IReadOnlySet<Guid> organizationIds,
        Guid organizationId,
        string? organizationPlanCode) =>
        audienceKind switch
        {
            AnnouncementAudienceKinds.All => true,
            // Клуб без подписки не подходит ни под какой тариф. «Нет тарифа» — это не «подходит
            // всем»: сообщение «вы на старом тарифе» такому клубу адресовано быть не может.
            AnnouncementAudienceKinds.Plans =>
                organizationPlanCode is not null && planCodes.Contains(organizationPlanCode),
            AnnouncementAudienceKinds.Organizations => organizationIds.Contains(organizationId),
            _ => false
        };

    public static bool Matches(PlatformAnnouncementEntity announcement, Guid organizationId, string? organizationPlanCode) =>
        Matches(
            announcement.AudienceKind,
            ParsePlanCodes(announcement.AudiencePlanCodesJson),
            ParseOrganizationIds(announcement.AudienceOrganizationIdsJson),
            organizationId,
            organizationPlanCode);

    private static IReadOnlyList<T> Parse<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<T>>(json) ?? [];
        }
        catch (JsonException)
        {
            // Испорченная строка аудитории значит «никому», а не «всем»: разослать сообщение
            // всему парку из-за битого JSON хуже, чем не разослать никому.
            return [];
        }
    }
}
