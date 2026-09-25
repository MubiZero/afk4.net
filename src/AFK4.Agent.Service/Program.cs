using AFK4.Agent.Service;
using AFK4.Agent.Service.Commands;
using AFK4.Agent.Service.Enforcement;
using AFK4.Agent.Service.Logging;
using AFK4.Agent.Service.Network;
using AFK4.Agent.Service.Protection;
using AFK4.Agent.Service.Shell;
using AFK4.Agent.Service.Updates;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

// Persistent file log (Information+) for field diagnostics — the service has no visible
// console and Event Log captures Warning+ only, so shell-launch decisions stay hidden.
builder.Logging.AddProvider(new FileLoggerProvider(FileLoggerProvider.DefaultLogPath));

// Bootstrap config the Setup Wizard wrote after enrollment. Read from a FILE (authoritative,
// added last so it overrides env vars): a service launched by the SCM inherits a stale
// environment block, so machine env vars written by the wizard are not visible until the next
// reboot — but the file is read fresh on every start. See FileBootstrapWriter.
var agentConfigDirectory = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    "AFK4",
    "Agent");

// Load into memory via a stream rather than AddJsonFile so an unreadable file (ACL denial,
// partial write) is swallowed here instead of throwing during host build — a kiosk Agent must
// not fail to start over a config-read error; it falls back to environment configuration.
void AddAgentConfigFile(string fileName)
{
    var path = Path.Combine(agentConfigDirectory, fileName);
    try
    {
        if (File.Exists(path))
        {
            builder.Configuration.AddJsonStream(new MemoryStream(File.ReadAllBytes(path)));
        }
    }
    catch (Exception readException)
    {
        Console.Error.WriteLine($"Failed to read agent config '{path}': {readException.Message}");
    }
}

AddAgentConfigFile("bootstrap.json");
// Учётка игрока киоска (спека оболочки, §6.1): мастер пишет её SID отдельным файлом, а «Снять
// киоск» просто удаляет его — настройка с ключом устройства при этом не переписывается.
AddAgentConfigFile("kiosk.json");

builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection("Agent"));

// A kiosk Agent must stay alive: one background worker faulting (e.g. a transient auth error)
// must not tear down the whole host and with it the Player Shell supervision.
builder.Services.Configure<HostOptions>(hostOptions =>
    hostOptions.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "AFK4.Agent.Service";
});
// Часы агента подтягиваются к часам платформы: аренда приходит абсолютным временем, а на игровом
// ПК часы врут регулярно. Один экземпляр на всех — время должно быть одно и то же и у проверки
// аренды, и у монитора льготы, и у экрана игрока.
builder.Services.AddSingleton(new PlatformSyncedTimeProvider(TimeProvider.System));
builder.Services.AddSingleton<TimeProvider>(provider => provider.GetRequiredService<PlatformSyncedTimeProvider>());
builder.Services.AddSingleton<IPlatformClockSynchronizer>(provider => provider.GetRequiredService<PlatformSyncedTimeProvider>());
builder.Services.AddHttpClient("platform");
// Artifact downloads stream large MSIs; the per-request CancellationToken bounds the transfer,
// so disable the default 100s client timeout to avoid aborting slow but healthy downloads.
builder.Services.AddHttpClient("updates", client => client.Timeout = Timeout.InfiniteTimeSpan);
builder.Services.AddSingleton<ISessionLeaseStore, FileSessionLeaseStore>();
builder.Services.AddSingleton<ICommandResultOutbox, FileCommandResultOutbox>();
builder.Services.AddSingleton<IDeviceCredentialStore, FileDeviceCredentialStore>();
builder.Services.AddSingleton<IAgentRuntimeStateStore, AgentRuntimeStateStore>();
builder.Services.AddSingleton<SessionLeaseValidator>();
builder.Services.AddSingleton<IMachinePolicyStore, WindowsMachinePolicyStore>();
builder.Services.AddSingleton<IWorkstationLockController, WorkstationLockController>();
builder.Services.AddSingleton<ISessionEnforcementCoordinator, SessionEnforcementCoordinator>();
// Явная фабрика, а не сканирование конструкторов: у состояния есть и «в памяти» для тестов, и
// файловый — службе нужен файловый, иначе льгота не переживёт перезапуск.
builder.Services.AddSingleton<IOfflineGraceState>(provider =>
    new OfflineGraceState(provider.GetRequiredService<IOptions<AgentOptions>>()));
builder.Services.AddSingleton<IOfflineLeaseExtender, OfflineLeaseExtender>();
builder.Services.AddSingleton<IGraceModeMonitor, GraceModeMonitor>();
builder.Services.AddSingleton<IProcessLauncher, ProcessLauncher>();
builder.Services.AddSingleton<IRunningProcessTerminator, RunningProcessTerminator>();
builder.Services.AddSingleton<IProcessPolicyEnforcer, ProcessPolicyEnforcer>();
builder.Services.AddSingleton<IPlayerShellProcessQuery, PlayerShellProcessQuery>();
builder.Services.AddSingleton<IPlayerShellProcessStarter, PlayerShellProcessStarter>();
builder.Services.AddSingleton<IPlayerShellLaunchContext, PlayerShellLaunchContext>();
builder.Services.AddSingleton<IPlayerShellProcessSupervisor, PlayerShellProcessSupervisor>();
builder.Services.AddSingleton<IShellWarningStore, ShellWarningStore>();
builder.Services.AddSingleton<IShellHeartbeatSnapshot, ShellHeartbeatSnapshot>();
builder.Services.AddSingleton<IShellStateSignal, ShellStateSignal>();
builder.Services.AddSingleton<IPlayerShellStateBuilder, PlayerShellStateBuilder>();
builder.Services.AddSingleton<IAssistanceRequestReporter, HttpAssistanceRequestReporter>();
builder.Services.AddSingleton<IPlayerSignInClient, HttpPlayerSignInClient>();
builder.Services.AddSingleton<IPlayerSignIn, PlayerSignIn>();
builder.Services.AddSingleton<IPlayerShellRequestHandler, PlayerShellRequestHandler>();
builder.Services.AddSingleton<ISessionReconciliationReporter, SessionReconciliationReporter>();
builder.Services.AddSingleton<IMaintenanceMode, MaintenanceMode>();
builder.Services.AddSingleton<IMachineRegistry, WindowsMachineRegistry>();
builder.Services.AddSingleton<IProtectionProfileStore, FileProtectionProfileStore>();
builder.Services.AddSingleton<IProtectionPlatformClient, HttpProtectionPlatformClient>();
builder.Services.AddSingleton<IProtectionEnforcer, ProtectionEnforcer>();
builder.Services.AddSingleton<IMaintenanceDesktop, MaintenanceDesktop>();
builder.Services.AddSingleton<IMaintenanceReturnClient, HttpMaintenanceReturnClient>();
builder.Services.AddSingleton<MaintenanceReturn>();
builder.Services.AddSingleton<IMachinePowerController, WindowsMachinePowerController>();
builder.Services.AddSingleton<IWakeOnLanSender, UdpWakeOnLanSender>();
builder.Services.AddSingleton<INetworkIdentityProvider, SystemNetworkIdentityProvider>();
builder.Services.AddSingleton<ShellHostChannel>();
builder.Services.AddSingleton<IShellHostChannel>(provider => provider.GetRequiredService<ShellHostChannel>());
builder.Services.AddSingleton<IMachineCommandHandler, MachineCommandHandler>();
builder.Services.AddSingleton<IDeviceCommandHandler, DefaultDeviceCommandHandler>();
builder.Services.AddSingleton<IDeviceRealtimeClient, DeviceRealtimeClient>();
builder.Services.AddSingleton<IInstalledAppInventoryCollector, WindowsInstalledAppInventoryCollector>();
builder.Services.AddSingleton<IInstalledAppReporter, HttpInstalledAppReporter>();
builder.Services.AddSingleton<IAgentUpdateClient, HttpAgentUpdateClient>();
builder.Services.AddSingleton<IAgentComponentVersionProvider, AgentComponentVersionProvider>();
builder.Services.AddSingleton<IUpdateArtifactDownloader, HttpUpdateArtifactDownloader>();
builder.Services.AddSingleton<IUpdatePackageVerifier, Sha256UpdatePackageVerifier>();
builder.Services.AddSingleton<IUpdateInstallStateStore, FileUpdateInstallStateStore>();
builder.Services.AddSingleton<IUpdateAttemptLedger, FileUpdateAttemptLedger>();
builder.Services.AddSingleton<IGuestSeatUpdateGuard, GuestSeatUpdateGuard>();
builder.Services.AddSingleton<IUpdateInstallExecutor, ExternalProcessUpdateInstaller>();
builder.Services.AddSingleton<IUpdateRollbackExecutor, ExternalProcessUpdateRollbackExecutor>();
builder.Services.AddSingleton<IAgentRestartScheduler, ExternalProcessAgentRestartScheduler>();
builder.Services.AddSingleton<IUpdateInstaller, SafeUpdateInstaller>();
builder.Services.AddSingleton<IAgentUpdateCoordinator, AgentUpdateCoordinator>();
builder.Services.AddSingleton<IUpdateRecoveryService, UpdateRecoveryService>();
builder.Services.AddSingleton<IOrganizationAdminUpdateCoordinatorClient, NamedPipeOrganizationAdminUpdateCoordinatorClient>();
builder.Services.AddSingleton<IOrganizationAdminUpdateReadiness, OrganizationAdminUpdateReadiness>();
builder.Services.AddSingleton<IOrganizationAdminProcessLauncher, OrganizationAdminProcessLauncher>();
builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<AgentUpdateWorker>();
builder.Services.AddHostedService<ShellPipeServer>();

var host = builder.Build();
host.Run();
