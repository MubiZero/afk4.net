namespace AFK4.Shared.Contracts.Platform.Organizations;

public static class OrganizationPlanCodeNames
{
    /// <summary>Бесплатно: до 10 ПК, 1 зал, 3 сотрудника, с рекламой платформы.</summary>
    public const string Free = "free";

    /// <summary>10 сомони в месяц за каждый ПК сверх десяти; без лимитов и рекламы.</summary>
    public const string PerPc = "per_pc";

    // Прежняя сетка — снята с продажи (спека тарифов клуба, §2); клубы на ней остаются.
    public const string Starter = "starter";

    public const string Growth = "growth";

    public const string Scale = "scale";
}
