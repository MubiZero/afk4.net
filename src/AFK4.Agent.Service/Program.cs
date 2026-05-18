using AFK4.Agent.Service;
using AFK4.Agent.Service.Enforcement;
using AFK4.Agent.Service.Shell;
using AFK4.Agent.Service.Updates;
using Microsoft.Extensions.Hosting.WindowsServices;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection("Agent"));
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "AFK4.Agent.Service";
});
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpClient("platform");
builder.Services.AddSingleton<ISessionLeaseStore, FileSessionLeaseStore>();
builder.Services.AddSingleton<IAgentRuntimeStateStore, AgentRuntimeStateStore>();
builder.Services.AddSingleton<SessionLeaseValidator>();
builder.Services.AddSingleton<IWorkstationLockController, WorkstationLockController>();
builder.Services.AddSingleton<ISessionEnforcementCoordinator, SessionEnforcementCoordinator>();
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
