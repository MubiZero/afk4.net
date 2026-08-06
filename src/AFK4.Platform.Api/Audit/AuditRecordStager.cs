using System.Text.Json;
using System.Text.Json.Nodes;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Platform.Support;

namespace AFK4.Platform.Api.Audit;

public sealed class AuditRecordStager(
    PlatformDbContext dbContext,
    TimeProvider timeProvider,
    IStaffContextAccessor staffContextAccessor) : IAuditRecordStager
{
    public void Stage(AuditRecordWriteRequest request)
    {
        var support = staffContextAccessor.Current?.SupportAccess;
        if (support is not null)
        {
            // Под грантом действует платформенный сотрудник; записать сотрудника клуба означало бы
            // приписать клубу чужое действие.
            request = request with
            {
                ActorStaffUserId = null,
                ActorPlatformAdminUserId = support.PlatformAdminUserId,
                DetailsJson = WithSupportAccessDetails(request.DetailsJson, support)
            };
        }

        dbContext.AuditRecords.Add(AuditRecordFactory.Create(request, timeProvider.GetUtcNow()));
    }

    // Одной атрибуции актора мало: без гранта и причины прямо в записи сопоставлять действие с
    // конкретным обращением клиента приходится вручную по времени, а при нескольких грантах подряд
    // для одной организации это неоднозначно. Дописываем поля гранта в DetailsJson, не трогая то,
    // что там уже есть (операционные поля самого действия).
    private static string WithSupportAccessDetails(string detailsJson, PlatformSupportContext support)
    {
        var supportAccess = new JsonObject
        {
            ["grantId"] = support.GrantId,
            ["reason"] = support.Reason,
            ["permission"] = support.Permission
        };

        JsonNode? root;
        try
        {
            root = string.IsNullOrWhiteSpace(detailsJson) ? null : JsonNode.Parse(detailsJson);
        }
        catch (JsonException)
        {
            root = null;
        }

        if (root is JsonObject details)
        {
            details["supportAccess"] = supportAccess;
            return details.ToJsonString();
        }

        // DetailsJson оказался не объектом (пусто/примитив/массив) — не теряем исходное содержимое,
        // просто заворачиваем его рядом с данными гранта вместо того, чтобы затереть.
        var wrapper = new JsonObject { ["supportAccess"] = supportAccess };
        if (root is not null)
        {
            wrapper["details"] = root;
        }

        return wrapper.ToJsonString();
    }
}
