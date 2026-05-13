namespace AFK4.Platform.Api.Data;

public sealed class TariffVersionEntity
{
    public Guid TariffVersionId { get; set; }

    public Guid TariffId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public int VersionNumber { get; set; }

    public string CurrencyCode { get; set; } = "TJS";

    public long PricePerMinuteMinorUnits { get; set; }

    public int MinimumBillableMinutes { get; set; }

    public int RoundingIncrementMinutes { get; set; }

    public DateTimeOffset EffectiveFromUtc { get; set; }

    public DateTimeOffset? RetiredAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
