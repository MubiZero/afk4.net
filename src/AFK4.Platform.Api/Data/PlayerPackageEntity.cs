namespace AFK4.Platform.Api.Data;

public sealed class PlayerPackageEntity
{
    public Guid PlayerPackageId { get; set; }

    public Guid PackageDefinitionId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public Guid PlayerAccountId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string CurrencyCode { get; set; } = "TJS";

    public long PurchasedPriceMinorUnits { get; set; }

    public int IncludedSeconds { get; set; }

    public int BonusSeconds { get; set; }

    public DateTimeOffset PurchasedAtUtc { get; set; }

    public DateTimeOffset? ExpiresAtUtc { get; set; }
}
