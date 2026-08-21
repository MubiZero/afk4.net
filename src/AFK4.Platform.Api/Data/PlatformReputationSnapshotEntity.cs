namespace AFK4.Platform.Api.Data;

/// <summary>
/// Суточный снимок сетевой репутации личности. Оператор видит точное число, но вчерашнее — и
/// задержка здесь и есть защита приватности: клуб, опрашивающий живой счётчик каждую минуту,
/// увидел бы «+1» ровно в тот момент, когда человек сел за ПК у соседа, и узнал бы, где тот
/// играет, не получив ни одного названия клуба.
/// </summary>
public sealed class PlatformReputationSnapshotEntity
{
    public Guid PlatformPersonId { get; set; }

    public int NetworkVisits { get; set; }

    public int NetworkNoShows { get; set; }

    public DateTimeOffset CalculatedAtUtc { get; set; }
}
