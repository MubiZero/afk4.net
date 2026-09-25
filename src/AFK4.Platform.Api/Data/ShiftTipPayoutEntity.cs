namespace AFK4.Platform.Api.Data;

/// <summary>
/// Чаевые смены выданы наличными: ссылка на выдачу из кассы. Без неё смена не знала бы, что уже
/// выдано, и одну сумму можно было бы выдать дважды.
/// </summary>
public sealed class ShiftTipPayoutEntity
{
    public Guid ShiftTipPayoutId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid ShiftId { get; set; }

    public Guid CashMovementId { get; set; }

    public long AmountMinorUnits { get; set; }

    public Guid CreatedByStaffUserId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
