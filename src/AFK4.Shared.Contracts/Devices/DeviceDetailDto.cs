using AFK4.Shared.Contracts.Install;

namespace AFK4.Shared.Contracts.Devices;

public sealed record DeviceDetailDto(
    Guid OrganizationId,
    Guid BranchId,
    Guid DeviceId,
    string MachineName,
    string AgentVersion,
    string ShellVersion,
    DateTimeOffset EnrolledAtUtc,
    DateTimeOffset? LastHeartbeatAtUtc,
    bool IsOnline,
    bool IsLocked,
    Guid? SeatId,
    string? SeatName,
    Guid? ZoneId,
    string? ZoneName,
    int ActiveCredentialCount,
    int InstalledAppCount,
    IReadOnlyList<DeviceCommandStatusDto> RecentCommands,
    string DisplayName = "",
    string Role = DeviceRoleNames.GamingPc,
    string EnrollmentState = DeviceEnrollmentStateNames.Approved,
    /// Последний отчёт ПК о защите; null — ПК ещё не докладывал.
    DeviceProtectionReportDto? ProtectionReport = null,
    /// Текущая версия профиля филиала: отчёт со старой версией значит «ПК ещё не применил».
    int BranchProtectionVersion = 0);
