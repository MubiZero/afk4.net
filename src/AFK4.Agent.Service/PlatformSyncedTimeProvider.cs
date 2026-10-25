using System.Text.Json;

namespace AFK4.Agent.Service;

/// <summary>
/// Часы агента, подтянутые к часам платформы.
///
/// Аренда сессии приходит с сервера абсолютным временем: «действительна до 19:40 UTC». Агент
/// сравнивал его с часами самой машины — а на игровом ПК часы врут регулярно: севшая батарейка на
/// плате, выключенная синхронизация, ручная правка. Отстающие часы отдавали гостю лишние часы
/// бесплатно; спешащие запирали оплаченную машину сразу, а на попытку открыть её заново агент
/// отвечал «аренда просрочена» — в клубе это выглядело как поломка платформы.
///
/// Расхождение приезжает в каждом сердцебиении (<c>ServerTimeUtc</c>) и здесь превращается в
/// поправку.
///
/// До первого сердцебиения после запуска поправки нет, а это ровно утро после отключения света:
/// роутер ещё не поднялся, а у ПК с севшей батарейкой часы откатились на дату прошивки — и
/// оплаченная аренда выглядела действующей годами. Поэтому каждое сердцебиение запоминает на диске
/// время платформы и аптайм машины, и до новой связи время не опускается ниже «запомненное плюс
/// прошедшее с тех пор по аптайму». Это нижняя граница: выключенный ПК аптайм не копит, так что гость
/// получит самое большее длительность обесточки сверху, а не годы.
///
/// Измерения длительностей от этого не страдают: оба конца берутся из этих же часов, и постоянная
/// поправка в разности сокращается.
/// </summary>
public sealed class PlatformSyncedTimeProvider(
    TimeProvider source,
    string? stateDirectory = null,
    Func<long>? uptimeMilliseconds = null) : TimeProvider
{
    public const string MemoryFileName = "platform-clock.json";

    private readonly string? memoryPath = stateDirectory is null ? null : Path.Combine(stateDirectory, MemoryFileName);
    private readonly Func<long> uptime = uptimeMilliseconds ?? (() => Environment.TickCount64);
    private readonly PlatformClockMemory? remembered = Load(stateDirectory);
    private long offsetTicks;
    private int synchronized;

    /// <summary>
    /// Сколько нужно добавить к часам самой машины, чтобы получить время платформы. Ноль, пока
    /// сердцебиения не было ни одного.
    /// </summary>
    public TimeSpan Offset => TimeSpan.FromTicks(Interlocked.Read(ref offsetTicks));

    public override DateTimeOffset GetUtcNow()
    {
        var machineNow = source.GetUtcNow();
        if (Volatile.Read(ref synchronized) != 0 || remembered is null)
        {
            return machineNow + Offset;
        }

        // Тот же запуск Windows — прошло (аптайм − запомненный аптайм); аптайм меньше запомненного —
        // машина перезагружалась, и прошло не меньше всего нынешнего аптайма.
        var nowUptime = uptime();
        var elapsed = nowUptime >= remembered.UptimeMilliseconds ? nowUptime - remembered.UptimeMilliseconds : nowUptime;
        var floor = remembered.PlatformUtc + TimeSpan.FromMilliseconds(elapsed);
        return floor > machineNow ? floor : machineNow;
    }

    public override TimeZoneInfo LocalTimeZone => source.LocalTimeZone;

    public override long GetTimestamp() => source.GetTimestamp();

    public override long TimestampFrequency => source.TimestampFrequency;

    /// <summary>
    /// Поправка считается от часов самой машины, а не от уже поправленных: иначе она складывалась
    /// бы сама с собой.
    /// </summary>
    public void Synchronize(DateTimeOffset serverTimeUtc)
    {
        Interlocked.Exchange(ref offsetTicks, (serverTimeUtc - source.GetUtcNow()).Ticks);
        Volatile.Write(ref synchronized, 1);
        Remember(new PlatformClockMemory(serverTimeUtc, uptime()));
    }

    private void Remember(PlatformClockMemory memory)
    {
        if (memoryPath is null)
        {
            return;
        }

        try
        {
            AtomicFile.WriteAllText(memoryPath, JsonSerializer.Serialize(memory));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Не запомнили — следующее сердцебиение попробует снова; время сейчас и так верное.
        }
    }

    private static PlatformClockMemory? Load(string? stateDirectory)
    {
        if (stateDirectory is null)
        {
            return null;
        }

        try
        {
            var path = Path.Combine(stateDirectory, MemoryFileName);
            return File.Exists(path) ? JsonSerializer.Deserialize<PlatformClockMemory>(File.ReadAllText(path)) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private sealed record PlatformClockMemory(DateTimeOffset PlatformUtc, long UptimeMilliseconds);
}
