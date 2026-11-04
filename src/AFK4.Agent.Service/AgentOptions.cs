using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Agent.Service;

/// <summary>
/// Настройки агента из bootstrap.json. Свойства — с <c>set</c>, не <c>init</c>: в Native AOT их заполняет генератор
/// привязки, а он <c>init</c> молча пропускает — агент на стенде 04.11.2026 считал себя незарегистрированным.
/// </summary>
public sealed class AgentOptions
{
    public Uri PlatformBaseUrl { get; set; } = new("http://localhost:5000");

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public Guid DeviceId { get; set; }

    public string MachineName { get; set; } = Environment.MachineName;

    public string AgentVersion { get; set; } = "0.1.0";

    public string ShellVersion { get; set; } = string.Empty;

    public string OrganizationAdminVersion { get; set; } = string.Empty;

    public string OrganizationAdminExecutablePath { get; set; } = string.Empty;

    public string OrganizationAdminStartArguments { get; set; } = string.Empty;

    public string OrganizationAdminUpdateCoordinationPipeName { get; set; } = "afk4-organization-admin-updates";

    public string OrganizationAdminUpdateCoordinationSecret { get; set; } = string.Empty;

    public int OrganizationAdminUpdateCoordinationTimeoutMilliseconds { get; set; } = 2000;

    public int OrganizationAdminUpdateExitTimeoutSeconds { get; set; } = 30;

    public int OrganizationAdminUpdateExitPollMilliseconds { get; set; } = 250;

    public string DeviceRole { get; set; } = DeviceRoleNames.GamingPc;

    /// <summary>
    /// The branch's preferred customer-shell locale (mirrors <c>BranchEntity.PreferredLocale</c>),
    /// provisioned into the agent config and stamped onto the player-shell state so the kiosk
    /// renders in the branch language. Defaults to <c>ru</c>.
    /// </summary>
    public string PreferredLocale { get; set; } = "ru";

    /// <summary>
    /// How many seconds of remaining session time trigger a low-time warning in the player shell.
    /// Defaults to 300 (5 minutes).
    /// </summary>
    public int ShellWarningThresholdSeconds { get; set; } = 300;

    public string? ClubName { get; set; }

    public string? LogoUrl { get; set; }

    public string? AccentColor { get; set; }

    public string DeviceCredentialSecret { get; set; } = string.Empty;

    public string LeaseSigningPublicKeyPem { get; set; } = string.Empty;

    public string StateDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "AFK4",
        "Agent");

    public string PlayerShellExecutablePath { get; set; } = string.Empty;

    public string PlayerShellStartArguments { get; set; } = string.Empty;

    public bool PlayerShellAutoStartEnabled { get; set; } = true;

    /// <summary>Канал агент ↔ оболочка: один постоянный, состояние и запросы идут по нему.</summary>
    public string ShellPipeName { get; set; } = ShellPipeProtocol.DefaultPipeName;

    /// <summary>
    /// SID учётной записи игрока, которой разрешено подключаться к каналу. С киоском (P5) сюда ставят
    /// игровую учётку, и чужой процесс в сессии канал не откроет. Пусто — киоска нет: ПК не игровое
    /// место, агент не запускает оболочку и не держит запретов, а канал (для запущенной вручную
    /// оболочки при разработке) пускает любого интерактивного пользователя.
    /// </summary>
    public string? ShellPipeClientSid { get; set; }

    public List<AgentLauncherAppOptions> LauncherApps { get; set; } = [];

    public List<string> DeniedProcessNames { get; set; } = [];

    /// <summary>
    /// Как часто пересобирать список установленного софта. Шесть часов — компромисс: реестр на
    /// игровой машине читается небыстро, а клуб хочет видеть новую игру в тот же день, а не после
    /// следующей перезагрузки.
    /// </summary>
    public int InstalledAppReportIntervalMinutes { get; set; } = 360;

    public string UpdateChannel { get; set; } = "stable";

    /// <summary>Сколько загрузка пакета может молчать, прежде чем её бросить и повторить на следующей проверке.</summary>
    public TimeSpan UpdateDownloadStallTimeout { get; set; } = TimeSpan.FromSeconds(60);

    public string UpdateStagingDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "AFK4",
        "Agent",
        "Updates");

    /// <summary>
    /// Дельта-обновление по манифесту файлов: качать только изменившиеся файлы вместо целого MSI.
    /// Включено с приёмки на Windows 11 (VM, 27.10.2026): агент, Панель и работающая оболочка
    /// обновились за 0,8–1,2 МБ вместо 55–62 МБ. Любой отказ дельты ставит целый MSI.
    /// </summary>
    public bool UpdateDeltaEnabled { get; set; } = true;

    /// <summary>Дельта применяется, только если качать надо не больше этой доли целого пакета.</summary>
    public double UpdateDeltaMaxDownloadRatio { get; set; } = 0.6;

    public string UpdateInstallerExecutablePath { get; set; } = string.Empty;

    public string UpdateInstallerArgumentsTemplate { get; set; } = "\"{PackagePath}\" --component \"{Component}\" --version \"{Version}\"";

    public int UpdateInstallerTimeoutSeconds { get; set; } = 1800;

    public int UpdateCheckIntervalSeconds { get; set; } = 300;

    /// <summary>
    /// Порт, на котором агент принимает офлайн-аренды от Панели по сети клуба (docs/roadmap/offline-front-desk.md).
    /// 0 — не слушать.
    /// </summary>
    public int OfflineLanPort { get; set; } = AFK4.Shared.Contracts.Sessions.OfflineLeaseSigning.AgentLanPort;

    /// <summary>Хост префикса HttpListener: «http://+» — все адреса машины; тесты слушают только петлю.</summary>
    public string OfflineLanPrefixHost { get; set; } = "http://+";

    public string UpdateStateDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "AFK4",
        "Agent",
        "UpdateState");

    public string UpdateRollbackExecutablePath { get; set; } = string.Empty;

    public string UpdateRollbackArgumentsTemplate { get; set; } = "--component \"{Component}\" --version \"{TargetVersion}\" --artifact \"{ArtifactPath}\"";

    public string UpdateRestartExecutablePath { get; set; } = string.Empty;

    public string UpdateRestartArgumentsTemplate { get; set; } = "--component \"{Component}\" --version \"{TargetVersion}\"";

    public string UpdatePackageSigningPublicKeyPem { get; set; } = string.Empty;

    /// <summary>
    /// True when the agent has a usable enrollment: real org/branch/device identifiers and an
    /// https platform URL that is not the localhost default. When false the workers must stay idle
    /// rather than beacon to localhost — bootstrap.json was missing or unreadable.
    /// </summary>
    public bool IsConfigured =>
        OrganizationId != Guid.Empty
        && BranchId != Guid.Empty
        && DeviceId != Guid.Empty
        && PlatformBaseUrl.Scheme == Uri.UriSchemeHttps
        && !PlatformBaseUrl.IsLoopback;
}

public sealed class AgentLauncherAppOptions
{
    public string AppId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string ExecutablePath { get; set; } = string.Empty;

    public string Arguments { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Можно запускать и без сессии — например, браузер для входа в клубный аккаунт. По умолчанию
    /// нельзя: игра на запертом ПК — это бесплатное время.
    /// </summary>
    public bool AllowWithoutSession { get; set; }
}
