// Проба Native AOT (docs/roadmap/windows7.md): может ли агент жить одним файлом без .NET на Windows 7–11, x86 и x64.
// Проверяет то, на чём держится настоящий агент: службу Windows, HTTPS к серверу, WMI, SignalR, канал с экраном игрока
// со списком доступа, JSON контрактов и подпись P-256. Отчёт — в stdout и в C:\ProgramData\AFK4Probe\aot-report.txt.
//   AgentProbe.exe [https://api.afk4.net]      — в консоли
//   sc create AFK4AotProbe binPath= "...\AgentProbe.exe"  — службой (отчёт тот же, в файл)
using System.IO.Pipes;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AFK4.Shared.Contracts.Devices;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WmiLight;

var platform = args.FirstOrDefault(arg => arg.StartsWith("https://", StringComparison.Ordinal)) ?? "https://api.afk4.net";
if (Microsoft.Extensions.Hosting.WindowsServices.WindowsServiceHelpers.IsWindowsService())
{
    var builder = Host.CreateApplicationBuilder(args);
    builder.Services.AddWindowsService(options => options.ServiceName = "AFK4AotProbe");
    builder.Services.AddHostedService(_ => new ProbeService(platform));
    builder.Build().Run();
    return;
}

Console.Write(await Probe.RunAsync(platform, "console"));

sealed class ProbeService(string platform) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var report = await Probe.RunAsync(platform, "service");
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "AFK4Probe");
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, "aot-report.txt"), report, stoppingToken);
    }
}

static class Probe
{
    public static async Task<string> RunAsync(string platform, string mode)
    {
        var report = new StringBuilder();
        async Task Step(string name, Func<Task<string>> step)
        {
            try
            {
                report.AppendLine($"{name}: ok {await step()}");
            }
            catch (Exception exception)
            {
                report.AppendLine($"{name}: FAIL {exception.GetType().Name}: {exception.Message}");
            }
        }

        report.AppendLine($"mode {mode}, {RuntimeInformation.OSDescription}, {RuntimeInformation.ProcessArchitecture}, AOT {!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported}");
        await Step("https platform", () => Fetch(platform));
        await Step("https site", () => Fetch("https://afk4.net/"));
        await Step("wmi", () => Task.FromResult(Wmi()));
        await Step("json contract", () => Task.FromResult(Json()));
        await Step("ecdsa p-256", () => Task.FromResult(Ecdsa()));
        await Step("pipe with ACL", PipeRoundTrip);
        await Step("signalr negotiate", () => SignalR(platform));
        return report.ToString();
    }

    private static async Task<string> Fetch(string url)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        using var response = await client.GetAsync(url);
        return $"{(int)response.StatusCode} HTTP/{response.Version}";
    }

    private static string Wmi()
    {
        using var connection = new WmiConnection();
        var os = connection.CreateQuery("SELECT Caption, Version FROM Win32_OperatingSystem").First();
        var cpu = connection.CreateQuery("SELECT Name FROM Win32_Processor").First();
        return $"{os["Caption"]} {os["Version"]} · {cpu["Name"]}";
    }

    private static string Json()
    {
        var request = new DeviceHealthReportRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow,
            new DeviceHealthDto(CpuLoadPercent: 12, MemoryUsedPercent: 40, BoardTemperatureC: null, Gpus: [new GpuHealthDto("GPU", 61, 40, 12)], Disks: [], Volumes: [new VolumeSpaceDto("C:", 30, 67, true)]));
        var json = JsonSerializer.Serialize(request, ProbeJson.Default.DeviceHealthReportRequest);
        var back = JsonSerializer.Deserialize(json, ProbeJson.Default.DeviceHealthReportRequest)!;
        return $"{json.Length} bytes, gpu {back.Health.Gpus[0].TemperatureC} °C";
    }

    private static string Ecdsa()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pem = key.ExportSubjectPublicKeyInfoPem();
        using var verifier = ECDsa.Create();
        verifier.ImportFromPem(pem);
        var data = Encoding.UTF8.GetBytes("afk4");
        return verifier.VerifyData(data, key.SignData(data, HashAlgorithmName.SHA256), HashAlgorithmName.SHA256) ? "signed and verified" : "MISMATCH";
    }

    private static async Task<string> PipeRoundTrip()
    {
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.InteractiveSid, null), PipeAccessRights.ReadWrite, AccessControlType.Allow));
        await using var server = NamedPipeServerStreamAcl.Create("afk4-aot-probe", PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0, security);
        var accept = server.WaitForConnectionAsync();
        await using var client = new NamedPipeClientStream(".", "afk4-aot-probe", PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(5000);
        await accept;
        await client.WriteAsync("hello"u8.ToArray());
        var buffer = new byte[5];
        await server.ReadExactlyAsync(buffer);
        return "echo " + Encoding.UTF8.GetString(buffer);
    }

    // Хаб ПК требует ключ ПК: без него сервер отвечает 401. Проба проверяет, что клиент SignalR под AOT дошёл до
    // сервера и разобрал ответ, — 401 здесь и есть ожидаемый итог.
    private static async Task<string> SignalR(string platform)
    {
        await using var connection = new HubConnectionBuilder().WithUrl(new Uri(new Uri(platform), "/hubs/devices")).Build();
        try
        {
            await connection.StartAsync();
            return "connected " + connection.ConnectionId;
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.Unauthorized)
        {
            return "reached the hub, 401 without device key (expected)";
        }
    }
}

[JsonSerializable(typeof(DeviceHealthReportRequest))]
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
partial class ProbeJson : JsonSerializerContext;
