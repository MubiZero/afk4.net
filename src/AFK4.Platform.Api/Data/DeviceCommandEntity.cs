namespace AFK4.Platform.Api.Data;

public sealed class DeviceCommandEntity
{
    public Guid CommandId { get; set; }

    public Guid DeviceId { get; set; }

    public string Type { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = "{}";

    public string Status { get; set; } = string.Empty;

    public string? Message { get; set; }

    /// <summary>
    /// Машинное имя исхода от агента (<see cref="AFK4.Shared.Contracts.Devices.DeviceCommandOutcomeNames"/>).
    /// Строка-сообщение рядом остаётся деталью для инженера, а по этому имени клуб показывает
    /// исход на своём языке. Null у команд, которые ещё не дошли до устройства.
    /// </summary>
    public string? Outcome { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
