namespace AFK4.Platform.Api.Data;

public sealed class TariffEntity
{
    public Guid TariffId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// В какие дни недели действует тариф: биты с понедельника (1) по воскресенье (64).
    /// <c>0</c> — каждый день; это же значение у всех тарифов, заведённых до расписаний.
    /// </summary>
    public int AppliesOnDaysMask { get; set; }

    /// <summary>
    /// Окно местного времени филиала, минуты от полуночи. Оба <c>null</c> — тариф действует
    /// круглые сутки. Начало больше конца означает переход через полночь: ночной тариф с 22:00
    /// до 06:00 иначе не выразить, а он клубу нужен ровно так же, как утренний.
    /// </summary>
    public int? AppliesFromMinuteOfDay { get; set; }

    public int? AppliesToMinuteOfDay { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
