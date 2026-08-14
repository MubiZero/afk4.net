namespace AFK4.Platform.Api.Data;

/// <summary>
/// Телефон игрока, на который доходят пуши. Токен выдаёт FCM, живёт он до переустановки
/// приложения или чистки данных — поэтому это отдельная строка, а не поле аккаунта: у одного
/// игрока бывает телефон и планшет, и уведомление должно прийти на оба.
/// </summary>
public sealed class PlayerDeviceEntity
{
    public Guid PlayerDeviceId { get; set; }

    public Guid PlayerAccountId { get; set; }

    /// <summary>Токен FCM. Уникален: тот же телефон, перезашедший в другой аккаунт, меняет владельца строки, а не заводит вторую.</summary>
    public string PushToken { get; set; } = string.Empty;

    /// <summary>android / ios. Нужно для диагностики: «не доходит на iOS» — это первый вопрос.</summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>Язык приложения на этом устройстве. Пуш приходит на языке телефона, а не аккаунта.</summary>
    public string? Locale { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>Когда приложение последний раз подтвердило токен. По нему чистят мёртвые устройства.</summary>
    public DateTimeOffset LastSeenUtc { get; set; }
}
