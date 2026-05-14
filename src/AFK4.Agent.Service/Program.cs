using AFK4.Agent.Service;
using AFK4.Agent.Service.Enforcement;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection("Agent"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpClient("platform");
builder.Services.AddSingleton<ISessionLeaseStore, FileSessionLeaseStore>();
builder.Services.AddSingleton<IAgentRuntimeStateStore, AgentRuntimeStateStore>();
builder.Services.AddSingleton<SessionLeaseValidator>();
builder.Services.AddSingleton<ISessionReconciliationReporter, SessionReconciliationReporter>();
builder.Services.AddSingleton<IDeviceCommandHandler, DefaultDeviceCommandHandler>();
builder.Services.AddSingleton<IDeviceRealtimeClient, DeviceRealtimeClient>();
builder.Services.AddSingleton<IInstalledAppInventoryCollector, WindowsInstalledAppInventoryCollector>();
builder.Services.AddSingleton<IInstalledAppReporter, HttpInstalledAppReporter>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
