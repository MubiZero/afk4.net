namespace AFK4.Platform.Api.Configuration;

public sealed class SupportAccessOptions
{
    public const string SectionName = "SupportAccess";

    /// <summary>
    /// Адрес админки клиента для входа под клиента. Один на среду: панель платформы сама
    /// его не знает, а держать адрес в двух местах — верный способ развести их со временем.
    /// </summary>
    public string OrganizationAdminBaseUrl { get; set; } = string.Empty;
}
