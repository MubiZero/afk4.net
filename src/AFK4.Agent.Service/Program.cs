using AFK4.Agent.Service;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection("Agent"));
builder.Services.AddHttpClient("platform");
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
