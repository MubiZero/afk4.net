using System.Runtime.InteropServices;
using System.Text.Json;

namespace AFK4.Agent.Service.Power;

/// <summary>
/// Был ли ПК обесточен (docs/roadmap/power-outage-resume.md). Агент раз в цикл помнит «жив сейчас» вместе с
/// аптаймом; при нормальной остановке службы (выключение Windows, обновление) пишет «остановлен чисто».
/// Включился, а аптайм меньше запомненного (машина перезагружалась) и чистой остановки после «жив» не было —
/// значит, свет пропал (или ПК выдернули: что из двух, решает сервер по соседям). С какого момента —
/// уходит в сердцебиение, пока сервер его не примет.
/// </summary>
public sealed class PowerLossTracker
{
    private const string AliveFileName = "power-alive.json";
    private const string CleanStopFileName = "power-clean-stop.json";
    private const string PendingFileName = "power-loss.json";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly string alivePath;
    private readonly string cleanStopPath;
    private readonly string pendingPath;
    private readonly Func<long> uptimeMilliseconds;

    public PowerLossTracker(string stateDirectory, Func<long>? uptimeMilliseconds = null)
    {
        alivePath = Path.Combine(stateDirectory, AliveFileName);
        cleanStopPath = Path.Combine(stateDirectory, CleanStopFileName);
        pendingPath = Path.Combine(stateDirectory, PendingFileName);
        this.uptimeMilliseconds = uptimeMilliseconds ?? (() => Environment.TickCount64);
        PowerLostFromUtc = Read<PendingLoss>(pendingPath)?.FromUtc ?? Detect();
    }

    /// <summary>С какого момента ПК был обесточен; null — не был или сервер уже знает.</summary>
    public DateTimeOffset? PowerLostFromUtc { get; private set; }

    public long UptimeSeconds => uptimeMilliseconds() / 1000;

    public void MarkAlive(DateTimeOffset nowUtc) =>
        TryWrite(alivePath, new Alive(nowUtc, uptimeMilliseconds()));

    public void MarkCleanStop(DateTimeOffset nowUtc) =>
        TryWrite(cleanStopPath, new CleanStop(nowUtc));

    /// <summary>Сердцебиение с отметкой дошло: сервер её записал, больше не шлём.</summary>
    public void Reported()
    {
        if (PowerLostFromUtc is null)
        {
            return;
        }

        PowerLostFromUtc = null;
        try
        {
            File.Delete(pendingPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Не удалили — отметку пришлют ещё раз, сервер её второй раз не применит.
        }
    }

    private DateTimeOffset? Detect()
    {
        var alive = Read<Alive>(alivePath);
        if (alive is null || uptimeMilliseconds() >= alive.UptimeMilliseconds)
        {
            // Первый запуск или служба перезапустилась без перезагрузки машины — питание не пропадало.
            return null;
        }

        var cleanStop = Read<CleanStop>(cleanStopPath);
        if (cleanStop is not null && cleanStop.StoppedUtc >= alive.AliveUtc)
        {
            return null;
        }

        TryWrite(pendingPath, new PendingLoss(alive.AliveUtc));
        return alive.AliveUtc;
    }

    private static T? Read<T>(string path) where T : class
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static void TryWrite<T>(string path, T value)
    {
        try
        {
            AtomicFile.WriteAllText(path, JsonSerializer.Serialize(value, Json));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Диск недоступен — отключение света этот ПК просто не заметит.
        }
    }

    private sealed record Alive(DateTimeOffset AliveUtc, long UptimeMilliseconds);

    private sealed record CleanStop(DateTimeOffset StoppedUtc);

    private sealed record PendingLoss(DateTimeOffset FromUtc);
}

/// <summary>
/// Работает ли ПК от батареи ИБП. Windows видит ИБП, подключённый по USB, как батарею — тогда пропажа сети
/// в розетке видна сразу. Не видит (многие модели) — null, а свет сервер узнает по обесточенным ПК.
/// </summary>
public static class UpsPowerSource
{
    private const byte NoSystemBattery = 128;
    private const byte UnknownStatus = 255;

    public static bool? OnBattery()
    {
        if (!OperatingSystem.IsWindows() || !GetSystemPowerStatus(out var status))
        {
            return null;
        }

        if (status.BatteryFlag == UnknownStatus || (status.BatteryFlag & NoSystemBattery) != 0)
        {
            return null;
        }

        return status.ACLineStatus switch
        {
            0 => true,
            1 => false,
            _ => null
        };
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }
}
