namespace AFK4.Platform.Api.Platform.Health;

public sealed class PlatformAlertOptions
{
    public const string ConfigurationSection = "Alerts";

    /// <summary>
    /// Номера для аварийных SMS. Лежат в конфигурации, а не в базе: у сотрудника платформы
    /// телефона нет вообще, и заводить поле, экран ввода и подтверждение номера ради
    /// аварийного канала для команды из единиц человек — это переоткрывать волну A.
    /// Цена решения: смена дежурного номера требует деплоя. Пустой список — не ошибка.
    /// </summary>
    public IReadOnlyList<string> SmsRecipients { get; set; } = [];
}
