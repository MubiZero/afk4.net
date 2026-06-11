using AFK4.Agent.Service;
using AFK4.Agent.Service.Enforcement;
using AFK4.Agent.Service.Logging;
using AFK4.Agent.Service.Shell;
using AFK4.Agent.Service.Updates;
using Microsoft.Extensions.Hosting.WindowsServices;

var builder = Host.CreateApplicationBuilder(args);

// Persistent file log (Information+) for field diagnostics — the service has no visible
// console and Event Log captures Warning+ only, so shell-launch decisions stay hidden.
builder.Logging.AddProvider(new FileLoggerProvider(FileLoggerProvider.DefaultLogPath));

// Bootstrap config the Setup Wizard wrote after enrollment. Read from a FILE (authoritative,
// added last so it overrides env vars): a service launched by the SCM inherits a stale
// environment block, so machine env vars written by the wizard are not visible until the next
// reboot — but the file is read fresh on every start. See FileBootstrapWriter.
var bootstrapConfigPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    "AFK4",
    "Agent",
    "bootstrap.json");

// Load into memory via a stream rather than AddJsonFile so an unreadable file (ACL denial,
// partial write) is swallowed here instead of throwing during host build — a kiosk Agent must
// not fail to start over a config-read error; it falls back to environment configuration.
try
{
    if (File.Exists(bootstrapConfigPath))
    {
        builder.Configuration.AddJsonStream(new MemoryStream(File.ReadAllBytes(bootstrapConfigPath)));
    }
}
catch (Exception bootstrapReadException)
{
    Console.Error.WriteLine($"Failed to read bootstrap config '{bootstrapConfigPath}': {bootstrapReadException.Message}");
}

builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection("Agent"));

// A kiosk Agent must stay alive: one background worker faulting (e.g. a transient auth error)
// must not tear down the whole host and with it the Player Shell supervision.
builder.Services.Configure<HostOptions>(hostOptions =>
    hostOptions.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "AFK4.Agent.Service";
});
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpClient("platform");
// Artifact downloads stream large MSIs; the per-request CancellationToken bounds the transfer,
// so disable the default 100s client timeout to avoid aborting slow but healthy downloads.
builder.Services.AddHttpClient("updates", client => client.Timeout = Timeout.InfiniteTimeSpan);
builder.Services.AddSingleton<ISessionLeaseStore, FileSessionLeaseStore>();
builder.Services.AddSingleton<ICommandResultOutbox, FileCommandResultOutbox>();
builder.Services.AddSingleton<IAgentRuntimeStateStore, AgentRuntimeStateStore>();
builder.Services.AddSingleton<SessionLeaseValidator>();
builder.Services.AddSingleton<IWorkstationLockController, WorkstationLockController>();
builder.Services.AddSingleton<ISessionEnforcementCoordinator, SessionEnforcementCoordinator>();
builder.Services.AddSingleton<IOfflineGraceState, OfflineGraceState>();
builder.Services.AddSingleton<IOfflineLeaseExtender, OfflineLeaseExtender>();
builder.Services.AddSingleton<IGraceModeMonitor, GraceModeMonitor>();
builder.Services.AddSingleton<IProcessLauncher, ProcessLauncher>();
builder.Services.AddSingleton<IRunningProcessTerminator, RunningProcessTerminator>();
builder.Services.AddSingleton<IProcessPolicyEnforcer, ProcessPolicyEnforcer>();
builder.Services.AddSingleton<IPlayerShellProcessQuery, PlayerShellProcessQuery>();
builder.Services.AddSingleton<IPlayerShellProcessStarter, PlayerShellProcessStarter>();
builder.Services.AddSingleton<IPlayerShellLaunchContext, PlayerShellLaunchContext>();
builder.Services.AddSingleton<IPlayerShellProcessSupervisor, PlayerShellProcessSupervisor>();
builder.Services.AddSingleton<IPlayerShellStatePublisher, NamedPipePlayerShellStateServer>();
builder.Services.AddSingleton<IPlayerShellCommandHandler, PlayerShellCommandHandler>();
builder.Services.AddSingleton<ISessionReconciliationReporter, SessionReconciliationReporter>();
builder.Services.AddSingleton<IDeviceCommandHandler, DefaultDeviceCommandHandler>();
builder.Services.AddSingleton<IDeviceRealtimeClient, DeviceRealtimeClient>();
builder.Services.AddSingleton<IInstalledAppInventoryCollector, WindowsInstalledAppInventoryCollector>();
builder.Services.AddSingleton<IInstalledAppReporter, HttpInstalledAppReporter>();
builder.Services.AddSingleton<IAgentUpdateClient, HttpAgentUpdateClient>();
builder.Services.AddSingleton<IAgentComponentVersionProvider, AgentComponentVersionProvider>();
builder.Services.AddSingleton<IUpdateArtifactDownloader, HttpUpdateArtifactDownloader>();
builder.Services.AddSingleton<IUpdatePackageVerifier, Sha256UpdatePackageVerifier>();
builder.Services.AddSingleton<IUpdateInstallStateStore, FileUpdateInstallStateStore>();
builder.Services.AddSingleton<IUpdateInstallExecutor, ExternalProcessUpdateInstaller>();
builder.Services.AddSingleton<IUpdateRollbackExecutor, ExternalProcessUpdateRollbackExecutor>();
builder.Services.AddSingleton<IAgentRestartScheduler, ExternalProcessAgentRestartScheduler>();
builder.Services.AddSingleton<IUpdateInstaller, SafeUpdateInstaller>();
builder.Services.AddSingleton<IAgentUpdateCoordinator, AgentUpdateCoordinator>();
builder.Services.AddSingleton<IUpdateRecoveryService, UpdateRecoveryService>();
builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<AgentUpdateWorker>();
builder.Services.AddHostedService<NamedPipePlayerShellCommandServer>();

var host = builder.Build();
host.Run();
