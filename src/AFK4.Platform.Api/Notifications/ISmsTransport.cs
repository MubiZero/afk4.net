namespace AFK4.Platform.Api.Notifications;

public interface ISmsTransport
{
    Task SendAsync(SmsMessage message, CancellationToken cancellationToken);
}

/// <summary>
/// Одно SMS так, как его принимает шлюз: не текст, а идентификатор заранее одобренного шаблона
/// плюс значения плейсхолдеров. Свободный текст payom.tj отклоняет — формулировка сообщения
/// проходит модерацию и живёт на их стороне, а не в нашей переменной.
/// </summary>
public sealed record SmsMessage(
    string ToPhoneNumber,
    string TemplateId,
    IReadOnlyDictionary<string, string> Variables);

public sealed class SmsTransportException(bool isPermanent, string message) : Exception(message)
{
    public bool IsPermanent { get; } = isPermanent;
}
