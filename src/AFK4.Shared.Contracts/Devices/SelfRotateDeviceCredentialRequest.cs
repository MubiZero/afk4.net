namespace AFK4.Shared.Contracts.Devices;

/// <summary>
/// Агент просит выдать себе новый ключ, предъявив действующий заголовком. Организация и филиал
/// в теле — те же, что в сердцебиении: сервер сверяет ключ именно с этой машиной, а не просто
/// «с каким-нибудь».
/// </summary>
public sealed record SelfRotateDeviceCredentialRequest(
    Guid OrganizationId,
    Guid BranchId,
    Guid DeviceId);
