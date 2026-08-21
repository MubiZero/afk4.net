namespace AFK4.Platform.Api.Data;

/// <summary>
/// Как филиал принимает гостей: сам подтверждает брони или смотрит их глазами, сколько времени
/// обещает на ответ, с кого просит предоплату и сколько держит место после начала.
///
/// Это решение клуба, а не платформы: платформенный рубильник онлайн-броней
/// (<c>PlatformFeatureNames.OnlineBooking</c>) остаётся тем, чем был, — правом организации на
/// функцию целиком. Строки нет — значит филиал работает на значениях по умолчанию.
/// </summary>
public sealed class BranchBookingSettingsEntity
{
    public Guid BranchId { get; set; }

    public Guid OrganizationId { get; set; }

    /// <summary>«auto» — подтверждаем сами, «manual» — смотрит администратор, «off» — брони закрыты.</summary>
    public string AcceptanceMode { get; set; } = string.Empty;

    /// <summary>Сколько минут клуб обещает игроку на ответ по заявке.</summary>
    public int RespondWithinMinutes { get; set; }

    public bool RequirePrepaymentFromNewGuests { get; set; }

    public int MaxActiveReservationsForNewGuests { get; set; }

    /// <summary>Со скольких визитов в этот филиал гость перестаёт считаться новым.</summary>
    public int RegularAfterVisits { get; set; }

    /// <summary>Сколько минут после начала брони место ещё держится за опаздывающим.</summary>
    public int HoldSeatAfterStartMinutes { get; set; }

    public bool KeepPrepaymentOnNoShow { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid UpdatedByStaffUserId { get; set; }
}
