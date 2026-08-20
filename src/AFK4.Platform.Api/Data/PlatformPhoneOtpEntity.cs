namespace AFK4.Platform.Api.Data;

public enum PlatformPhoneOtpPurpose
{
    Registration = 0,
    SignIn = 1,
}

/// <summary>
/// Одноразовый код, отправленный на номер. Отличается от <see cref="PlayerPhoneOtpEntity"/> одним,
/// но решающим: ключ здесь — сам номер, а не клубный счёт. У человека, который скачал приложение
/// дома, никакого счёта ещё нет, а лимит на отправку ему нужен ровно такой же — иначе незнакомый
/// номер получил бы неограниченную рассылку за счёт клубов.
/// </summary>
public sealed class PlatformPhoneOtpEntity
{
    public Guid PlatformPhoneOtpId { get; set; }

    /// <summary>Номер в нормализованной форме (только цифры), на который ушёл код.</summary>
    public string Phone { get; set; } = string.Empty;

    public PlatformPhoneOtpPurpose Purpose { get; set; }

    /// <summary>SHA-256 hex от шестизначного кода. Открытый текст не хранится никогда.</summary>
    public string CodeHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public int AttemptCount { get; set; }

    public DateTimeOffset? ConsumedAtUtc { get; set; }
}
