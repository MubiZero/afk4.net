namespace AFK4.Platform.Api.Data;

public sealed class OrganizationLoyaltySettingsEntity
{
    public Guid OrganizationId { get; set; }
    public bool TopUpEnabled { get; set; }
    public int TopUpPercentBasisPoints { get; set; }
    public bool ShopEnabled { get; set; }
    public int ShopPercentBasisPoints { get; set; }

    // Cashback on game-time spend: the amount charged for a played/prepaid session.
    public bool SessionEnabled { get; set; }
    public int SessionPercentBasisPoints { get; set; }

    // Limits applied to every accrual regardless of source. Zero disables the limit.
    // CashbackCapMinorUnits: max cashback granted per single accrual (ceiling on one event).
    // MinimumSourceMinorUnits: the source amount must reach this to earn any cashback.
    public long CashbackCapMinorUnits { get; set; }
    public long MinimumSourceMinorUnits { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
