namespace AFK4.Platform.Api.Data;

public sealed class ShiftEntity
{
    public Guid ShiftId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public Guid OpenedByStaffUserId { get; set; }

    public Guid? ClosedByStaffUserId { get; set; }

    public string State { get; set; } = string.Empty;

    public string CurrencyCode { get; set; } = string.Empty;

    public long StartingCashMinorUnits { get; set; }

    public long CountedCashMinorUnits { get; set; }

    public long ExpectedCashMinorUnits { get; set; }

    public long DifferenceMinorUnits { get; set; }

    public string OpeningNote { get; set; } = string.Empty;

    public string ClosingNote { get; set; } = string.Empty;

    public DateTimeOffset OpenedAtUtc { get; set; }

    public DateTimeOffset? ClosedAtUtc { get; set; }
}
