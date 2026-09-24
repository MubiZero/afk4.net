namespace AFK4.Shared.Contracts.Devices;

/// <summary>
/// Игрок входит на самом ПК: номер и ПИН-код. Идёт от агента с ключом устройства, а не с
/// публичного входа: сервер знает, на каком ПК вошли, привязывает токены к этому ПК и считает
/// попытки на устройство, а не на адрес всего клуба за одним роутером.
/// </summary>
public sealed record DevicePlayerSignInRequest(
    Guid OrganizationId,
    Guid BranchId,
    Guid DeviceId,
    string PhoneNumber,
    string Pin);

public static class DevicePlayerSignInErrorCodeNames
{
    /// <summary>
    /// Номер или ПИН-код не подошли. Причина не уточняется: «нет такого номера» — это ответ на
    /// вопрос, кто в этой сети играет.
    /// </summary>
    public const string SignInRefused = "sign_in_refused";

    /// <summary>С этого ПК слишком много неудачных попыток; ответ несёт, когда можно снова.</summary>
    public const string TooManyAttempts = "too_many_attempts";

    /// <summary>На ПК идёт чужая сессия: вход верный, но открыть вошедшему нечего.</summary>
    public const string SessionNotYours = "session_not_yours";

    /// <summary>Клуб закрыл этот ПК на обслуживание: входить на нём некуда.</summary>
    public const string DeviceInMaintenance = "device_in_maintenance";
}

/// <summary>Отказ входа на ПК. <see cref="RetryAfterUtc"/> — только у too_many_attempts.</summary>
public sealed record DevicePlayerSignInErrorDto(
    // Одно из DevicePlayerSignInErrorCodeNames.
    string Error,
    DateTimeOffset? RetryAfterUtc = null);
