namespace AFK4.Platform.Api.Data;

/// <summary>
/// Мнение тарифа о фиче. Отсутствие строки означает «у тарифа мнения нет» — тогда решает
/// умолчание фичи. Пустая таблица = сегодняшнее поведение.
/// </summary>
public sealed class PlanFeatureEntity
{
    public Guid PlanFeatureId { get; set; }

    public string PlanCode { get; set; } = string.Empty;

    public string FeatureKey { get; set; } = string.Empty;

    public bool IsIncluded { get; set; }
}
