namespace AFK4.Platform.Api.Notifications;

public sealed class SmsOptions
{
    public const string SectionName = "Sms";

    public string BaseUrl { get; set; } = "https://gateway.payom.tj";
    public string ApiToken { get; set; } = string.Empty;
    public string SenderName { get; set; } = "AFK4.NET";
    public int TimeoutSeconds { get; set; } = 15;
}

public static class SmsClientRegistration
{
    public const string HttpClientName = "payom-sms";
}
