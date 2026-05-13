namespace AFK4.Platform.Api.Data;

public sealed class PackageDefinitionEntity
{
    public Guid PackageDefinitionId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string CurrencyCode { get; set; } = "TJS";

    public long PriceMinorUnits { get; set; }

    public int IncludedSeconds { get; set; }

    public int BonusSeconds { get; set; }

    public int ExpiresAfterDays { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
