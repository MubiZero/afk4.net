namespace AFK4.Shared.Contracts.Devices;

/// <summary>
/// ПК должен забрать заявку на вход: человек отсканировал QR с его монитора. Приходит в группу
/// устройства по SignalR и, на случай обрыва, в ответе на сердцебиение.
/// </summary>
public sealed record PlayerSignInClaimedDto(
    Guid ClaimId,
    DateTimeOffset ExpiresAtUtc);

/// <summary>ПК забирает заявку ключом устройства — в ответ токены, привязанные к этому ПК.</summary>
public sealed record DeviceRedeemSignInClaimRequest(
    Guid OrganizationId,
    Guid BranchId,
    Guid DeviceId);

public static class PlayerSignInClaimErrorCodeNames
{
    /// <summary>Заявки нет или она для другого ПК.</summary>
    public const string NotFound = "claim_not_found";

    /// <summary>ПК не успел забрать заявку за отведённое время.</summary>
    public const string Expired = "claim_expired";

    /// <summary>Заявку уже забрали: одна заявка — один вход.</summary>
    public const string AlreadyRedeemed = "claim_already_redeemed";
}
