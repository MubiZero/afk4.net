namespace AFK4.Shared.Contracts.Devices;

/// <summary>
/// Игрок позвал оператора со своей машины. Приходит от агента: устройство известно всегда, а
/// сессии может не быть вовсе — кнопка есть и на запертом экране.
/// </summary>
public sealed record DeviceAssistanceRequest(
    Guid OrganizationId,
    Guid BranchId,
    Guid DeviceId,
    DateTimeOffset RequestedAtUtc);

/// <summary>Состояние вызова после обращения: когда позвали. Null — вызова нет.</summary>
public sealed record DeviceAssistanceStateDto(
    Guid DeviceId,
    DateTimeOffset? AssistanceRequestedAtUtc);
