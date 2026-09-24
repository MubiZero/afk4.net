namespace AFK4.Platform.Api.Data;

/// <summary>
/// Долгоживущий токен обновления для личности. Повторяет
/// <see cref="PlayerRefreshTokenEntity"/>, включая одноразовость: использованный токен
/// отзывается, а на его месте выдаётся новый.
/// </summary>
public sealed class PlatformPersonRefreshTokenEntity
{
    public Guid PlatformPersonRefreshTokenId { get; set; }

    public Guid PlatformPersonId { get; set; }

    public Guid? PinnedOrganizationId { get; set; }

    public byte[] TokenHash { get; set; } = [];

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? RevokedAtUtc { get; set; }

    /// <summary>
    /// ПК, на котором человек вошёл, — у входа с игрового ПК. null — телефон или веб. Токены ПК
    /// сервер гасит сам, когда за машиной больше некому сидеть (<see cref="DeviceSignedInAtUtc"/>).
    /// </summary>
    public Guid? DeviceId { get; set; }

    /// <summary>
    /// Когда человек вошёл на этом ПК. Переживает обновление токена: иначе хост, обновляющий
    /// токен каждые пять минут, навсегда оставался бы «только что вошедшим».
    /// </summary>
    public DateTimeOffset? DeviceSignedInAtUtc { get; set; }
}
