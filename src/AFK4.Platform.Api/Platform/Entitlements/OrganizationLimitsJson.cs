using System.Text.Json;
using AFK4.Shared.Contracts.Platform.Organizations;

namespace AFK4.Platform.Api.Platform.Entitlements;

/// <summary>
/// Единственное место, где лимиты организации превращаются из jsonb в объект и обратно.
/// Второй экземпляр этого правила разошёлся бы с первым молча и ни один тест бы этого не заметил.
/// </summary>
public static class OrganizationLimitsJson
{
    public static readonly OrganizationLimitsDto None = new(null, null, null, null);

    public static string Serialize(OrganizationLimitsDto? limits) =>
        JsonSerializer.Serialize(limits ?? None);

    public static OrganizationLimitsDto Deserialize(string? limitsJson)
    {
        if (string.IsNullOrWhiteSpace(limitsJson) || limitsJson == "{}")
        {
            return None;
        }

        try
        {
            return JsonSerializer.Deserialize<OrganizationLimitsDto>(limitsJson) ?? None;
        }
        catch (JsonException)
        {
            // Испорченный jsonb не должен ронять запрос: неизвестные лимиты = без ограничений,
            // потому что отказать по неизвестной причине хуже, чем пропустить.
            return None;
        }
    }
}
