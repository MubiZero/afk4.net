using AFK4.Platform.Api.Notifications;
using Xunit;

namespace AFK4.Platform.Api.Tests.Notifications;

public sealed class StaffPasswordResetSmsTemplateTests
{
    private static readonly ITemplateProvider Provider = new EmbeddedTemplateProvider(defaultLocale: "ru");

    [Theory]
    [InlineData("ru")]
    [InlineData("en")]
    [InlineData("tg")]
    public void Template_PresentForLocale_WithCodePlaceholder(string locale)
    {
        var template = Provider.Get(NotificationTemplateKeys.StaffPasswordResetSms, locale);
        Assert.Contains("{{code}}", template.BodyText, StringComparison.Ordinal);
    }

    [Fact]
    public void Key_IsRegisteredInAll()
    {
        Assert.Contains(NotificationTemplateKeys.StaffPasswordResetSms, NotificationTemplateKeys.All);
        var exception = Record.Exception(() => Provider.EnsureKeysPresent(NotificationTemplateKeys.All));
        Assert.Null(exception);
    }

    [Fact]
    public void RuBody_StaysWithinOneSmsSegment()
    {
        var template = Provider.Get(NotificationTemplateKeys.StaffPasswordResetSms, "ru");
        var rendered = template.BodyText.Replace("{{code}}", "123456", StringComparison.Ordinal);
        Assert.True(rendered.Length <= 70, $"SMS body is {rendered.Length} chars: \"{rendered}\"");
    }
}
