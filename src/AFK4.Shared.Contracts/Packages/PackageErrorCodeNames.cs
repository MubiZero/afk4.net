namespace AFK4.Shared.Contracts.Packages;

/// <summary>
/// Машинные имена отказов по пакетам времени. См. <see cref="Billing.BillingErrorCodeNames"/> —
/// та же причина: без кода отказ доезжает до Панели английской фразой сервера.
/// </summary>
public static class PackageErrorCodeNames
{
    /// <summary>Пакет с таким именем в филиале уже есть.</summary>
    public const string NameTaken = "package_name_taken";
}
