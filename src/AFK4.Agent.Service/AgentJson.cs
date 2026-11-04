using System.Text.Json;
using System.Text.Json.Serialization;
using AFK4.Agent.Service.Showcase;
using AFK4.Agent.Service.Updates.Delta;
using AFK4.Agent.Service.Enforcement;
using AFK4.Agent.Service.Updates;
using AFK4.Shared.Contracts.Ads;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Games;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shell;
using AFK4.Shared.Contracts.Showcase;
using AFK4.Shared.Contracts.Updates;

namespace AFK4.Agent.Service;

/// <summary>
/// Весь JSON агента: HTTP-запросы, файлы состояния, именованные каналы и полезная нагрузка SignalR.
/// В Native AOT нет JSON на отражении, поэтому каждый тип перечислен здесь и разбирается
/// сгенерированным кодом (docs/roadmap/windows7.md). Новый сериализуемый тип добавляют сюда же.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(SessionLeaseDto))]
[JsonSerializable(typeof(OfflineSeatResponse))]
[JsonSerializable(typeof(DeviceHeartbeatRequest))]
[JsonSerializable(typeof(DeviceHeartbeatResponse))]
[JsonSerializable(typeof(SelfRotateDeviceCredentialRequest))]
[JsonSerializable(typeof(RotateDeviceCredentialResponse))]
[JsonSerializable(typeof(DeviceCommandDto))]
[JsonSerializable(typeof(DeviceCommandResultDto))]
[JsonSerializable(typeof(List<DeviceCommandResultDto>))]
[JsonSerializable(typeof(PlayerSignInClaimedDto))]
[JsonSerializable(typeof(DeviceConnectionRequest))]
[JsonSerializable(typeof(DeviceSessionSnapshotRequest))]
[JsonSerializable(typeof(SessionReconciliationResponse))]
[JsonSerializable(typeof(InstalledAppReportRequest))]
[JsonSerializable(typeof(DeviceHardwareReportRequest))]
[JsonSerializable(typeof(DeviceHealthReportRequest))]
[JsonSerializable(typeof(DeviceAssistanceRequest))]
[JsonSerializable(typeof(DeviceMaintenanceReturnRequest))]
[JsonSerializable(typeof(DevicePlayerSignInRequest))]
[JsonSerializable(typeof(DeviceRedeemSignInClaimRequest))]
[JsonSerializable(typeof(PlatformPersonSessionResponse))]
[JsonSerializable(typeof(PlayerShellStateDto))]
[JsonSerializable(typeof(ProtectionProfileDto))]
[JsonSerializable(typeof(DeviceProtectionReportRequest))]
[JsonSerializable(typeof(List<OfflineLeaseKeyDto>))]
[JsonSerializable(typeof(AgentRuntimeState))]
[JsonSerializable(typeof(StoredShowcase))]
[JsonSerializable(typeof(DeviceShowcaseDto))]
[JsonSerializable(typeof(ShowcaseImpressionLog))]
[JsonSerializable(typeof(DeviceShowcaseImpressionsRequest))]
[JsonSerializable(typeof(DeviceGameLibraryDto))]
[JsonSerializable(typeof(DeviceUpdateCheckRequest))]
[JsonSerializable(typeof(DeviceUpdateCheckResponse))]
[JsonSerializable(typeof(DeviceUpdateStatusReportRequest))]
[JsonSerializable(typeof(DeviceUpdateStatusResultDto))]
[JsonSerializable(typeof(UpdateDeltaJournal))]
[JsonSerializable(typeof(UpdateManifestDocument))]
[JsonSerializable(typeof(LocalUpdateCoordinationRequest))]
[JsonSerializable(typeof(LocalUpdateCoordinationResponse))]
[JsonSerializable(typeof(Dictionary<string, int>))]
[JsonSerializable(typeof(Power.PowerLossTracker.PendingLoss))]
[JsonSerializable(typeof(Power.PowerLossTracker.Alive))]
[JsonSerializable(typeof(Power.PowerLossTracker.CleanStop))]
[JsonSerializable(typeof(OfflineGraceState.PersistedGrace))]
[JsonSerializable(typeof(FileDeviceCredentialStore.StoredCredential))]
[JsonSerializable(typeof(Shell.ShellHeartbeatSnapshot.RememberedPlace))]
internal sealed partial class AgentJson : JsonSerializerContext;

/// <summary>
/// Файлы состояния, которые исторически пишутся с настройками System.Text.Json по умолчанию
/// (PascalCase). Формат на диске не меняем, чтобы уже записанные файлы читались и дальше.
/// </summary>
[JsonSerializable(typeof(PlatformSyncedTimeProvider.PlatformClockMemory))]
[JsonSerializable(typeof(OrganizationAdminProcessLauncher.PendingLaunch))]
internal sealed partial class AgentJsonDefaults : JsonSerializerContext;

/// <summary>Состояние установки пишется с отступами: его читают глазами при разборе сбоев.</summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, WriteIndented = true)]
[JsonSerializable(typeof(UpdateInstallState))]
[JsonSerializable(typeof(LastKnownGoodUpdate))]
internal sealed partial class AgentJsonIndented : JsonSerializerContext;
