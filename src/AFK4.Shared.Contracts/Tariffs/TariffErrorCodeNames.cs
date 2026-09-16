namespace AFK4.Shared.Contracts.Tariffs;

/// <summary>
/// Машинные имена отказов по тарифам. См. <see cref="Install.InstallErrorCodeNames"/> — та же
/// причина: отказ нужно назвать на языке того, кто его читает.
/// </summary>
public static class TariffErrorCodeNames
{
    /// <summary>Тариф с таким именем в филиале уже есть.</summary>
    public const string NameTaken = "tariff_name_taken";
}
