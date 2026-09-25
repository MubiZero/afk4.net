using System.Text.Json;
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Platform.Api.Devices;

/// <summary>Отчёт ПК о защите — JSON на устройстве.</summary>
public static class ProtectionReports
{
    /// <summary>Пунктов в профиле семь; потолок с запасом, но не безграничный.</summary>
    public const int MaxItems = 32;

    private const int MaxDetailLength = 500;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string Write(DeviceProtectionReportDto report) =>
        JsonSerializer.Serialize(report with
        {
            // Подробность — текст исключения Windows: для разбора хватит начала, а строка в сотни
            // килобайт от сломанного агента в базе не нужна.
            Items = report.Items
                .Select(item => item with { Detail = item.Detail is { Length: > MaxDetailLength } ? item.Detail[..MaxDetailLength] : item.Detail })
                .ToList()
        }, Json);

    public static DeviceProtectionReportDto? Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<DeviceProtectionReportDto>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
