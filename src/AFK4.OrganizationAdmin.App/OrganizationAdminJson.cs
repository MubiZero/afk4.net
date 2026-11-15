using System.Text.Json;
using System.Text.Json.Serialization;
using AFK4.OrganizationAdmin.App.Connection;
using AFK4.OrganizationAdmin.App.Offline;
using AFK4.OrganizationAdmin.App.Updates;
using AFK4.OrganizationAdmin.Web;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Updates;

namespace AFK4.OrganizationAdmin.App;

// JSON Панели без отражения: в Native AOT его нет (docs/roadmap/windows7.md). Три контекста — по трём «диалектам»,
// которые уже лежат на дисках и ходят по каналам: менять их формат нельзя.

/// <summary>Файлы и каналы на camelCase: подключение к клубу, подтверждение остановки, обмен с агентом обновлений.</summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(OrganizationAdminConnectionSnapshot))]
[JsonSerializable(typeof(OrganizationAdminShutdownAcknowledgement))]
[JsonSerializable(typeof(LocalUpdateCoordinationRequest))]
[JsonSerializable(typeof(LocalUpdateCoordinationResponse))]
internal sealed partial class OrganizationAdminStoreJson : JsonSerializerContext;

/// <summary>Ключ офлайн-аренд под DPAPI: свойства как есть (PascalCase), как писала прежняя Панель, иначе у кассира сменился бы ключ.</summary>
[JsonSerializable(typeof(OfflineDeskKey.StoredKey))]
internal sealed partial class OrganizationAdminKeyJson : JsonSerializerContext;

/// <summary>Положение окна: свойства как есть (PascalCase), с отступами, как писала прежняя Панель.</summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(OrganizationAdminWindowPlacement))]
internal sealed partial class OrganizationAdminPlacementJson : JsonSerializerContext;

/// <summary>Мост Панели ↔ страница и обмен с ПК зала: camelCase, пустые поля не пишутся.</summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(OrganizationAdminWebHostBridge.BridgeRequest))]
[JsonSerializable(typeof(OrganizationAdminWebHostBridge.StoredConnectionPayload))]
[JsonSerializable(typeof(OrganizationAdminWebHostBridge.RestartAndInstallPayload))]
[JsonSerializable(typeof(OrganizationAdminWebHostBridge.SendLeasePayload))]
[JsonSerializable(typeof(OrganizationAdminWebHostBridge.JournalAppendPayload))]
[JsonSerializable(typeof(OrganizationAdminWebHostBridge.JournalAckPayload))]
[JsonSerializable(typeof(OrganizationAdminWebHostBridge.StoredConnection))]
[JsonSerializable(typeof(OrganizationAdminWebHostBridge.ClearedPayload))]
[JsonSerializable(typeof(OrganizationAdminWebHostBridge.AcceptedPayload))]
[JsonSerializable(typeof(OrganizationAdminWebHostBridge.OfflineKeyPayload))]
[JsonSerializable(typeof(OrganizationAdminWebHostBridge.AppendedPayload))]
[JsonSerializable(typeof(OrganizationAdminWebHostBridge.RecordsPayload))]
[JsonSerializable(typeof(OrganizationAdminWebHostBridge.RemainingPayload))]
[JsonSerializable(typeof(OrganizationAdminWindowMessage))]
[JsonSerializable(typeof(SessionLeaseDto))]
[JsonSerializable(typeof(OfflineSeatResponse))]
internal sealed partial class OrganizationAdminBridgeJson : JsonSerializerContext;
