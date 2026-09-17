namespace AFK4.Agent.Service;

/// <summary>Кто умеет принять поправку часов от платформы.</summary>
public interface IPlatformClockSynchronizer
{
    /// <summary>
    /// Сколько нужно добавить к часам самой машины, чтобы получить время платформы. Ноль, пока
    /// сердцебиения не было ни одного.
    /// </summary>
    TimeSpan Offset { get; }

    /// <summary>Принять время платформы и пересчитать поправку.</summary>
    void Synchronize(DateTimeOffset serverTimeUtc);
}

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
/// поправку. Пока сердцебиения не было ни одного, поправка нулевая — то есть ровно прежнее
/// поведение, а не выдуманное время.
///
/// Измерения длительностей от этого не страдают: оба конца берутся из этих же часов, и постоянная
/// поправка в разности сокращается.
/// </summary>
public sealed class PlatformSyncedTimeProvider(TimeProvider source) : TimeProvider, IPlatformClockSynchronizer
{
    private long offsetTicks;

    public TimeSpan Offset => TimeSpan.FromTicks(Interlocked.Read(ref offsetTicks));

    public override DateTimeOffset GetUtcNow() => source.GetUtcNow() + Offset;

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
    }
}
