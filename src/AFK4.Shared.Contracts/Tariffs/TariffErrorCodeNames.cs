namespace AFK4.Shared.Contracts.Tariffs;

/// <summary>
/// Машинные имена отказов по тарифам. См. <see cref="Install.InstallErrorCodeNames"/> — та же
/// причина: отказ нужно назвать на языке того, кто его читает.
/// </summary>
public static class TariffErrorCodeNames
{
    /// <summary>Тариф с таким именем в филиале уже есть.</summary>
    public const string NameTaken = "tariff_name_taken";

    /// <summary>Эту версию тарифа уже использовали сессии — редактировать нельзя, только новую версию.</summary>
    public const string VersionInUse = "tariff_version_in_use";

    /// <summary>Выбранный тариф сняли с публикации или он принадлежит другому филиалу.</summary>
    public const string NotAvailable = "tariff_not_available";
}
