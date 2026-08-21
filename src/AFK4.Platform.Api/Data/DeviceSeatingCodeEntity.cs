namespace AFK4.Platform.Api.Data;

/// <summary>
/// Код, который простаивающий ПК показывает на мониторе, чтобы человек мог сесть за него из
/// приложения.
///
/// Хранится открытым текстом намеренно. Это не пароль: код виден каждому, кто стоит в зале, — в
/// этом и весь смысл. Он доказывает не знание секрета, а присутствие перед экраном, и живёт
/// минуты. Хеширование дало бы вид защиты, ничего не защищая.
/// </summary>
public sealed class DeviceSeatingCodeEntity
{
    public Guid DeviceId { get; set; }

    public Guid OrganizationId { get; set; }

    public string Code { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
