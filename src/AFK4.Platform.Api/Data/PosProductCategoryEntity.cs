namespace AFK4.Platform.Api.Data;

public sealed class PosProductCategoryEntity
{
    public Guid CategoryId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Видна ли категория на стойке и в магазине оболочки. Скрытая остаётся в каталоге
    /// вместе со своими товарами — её просто перестают предлагать.</summary>
    public bool IsActive { get; set; }

    /// <summary>Место в списке, по возрастанию. Задаётся перестановкой, шаг не значим.</summary>
    public int SortOrder { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
