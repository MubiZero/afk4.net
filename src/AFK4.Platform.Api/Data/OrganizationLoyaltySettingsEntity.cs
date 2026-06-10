namespace AFK4.Platform.Api.Data;

public sealed class OrganizationLoyaltySettingsEntity
{
    public Guid OrganizationId { get; set; }
    public bool TopUpEnabled { get; set; }
    public int TopUpPercentBasisPoints { get; set; }
    public bool ShopEnabled { get; set; }
    public int ShopPercentBasisPoints { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
