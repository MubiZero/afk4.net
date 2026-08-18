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
}
