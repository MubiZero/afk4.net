using AFK4.Platform.Api.Notifications;
using Xunit;

namespace AFK4.Platform.Api.Tests.Notifications;

public sealed class StaffPhoneVerificationTemplateTests
{
    private static readonly ITemplateProvider Provider = new EmbeddedTemplateProvider(defaultLocale: "ru");

    [Theory]
    [InlineData("ru")]
    [InlineData("en")]
    [InlineData("tg")]
    public void Template_PresentForLocale_WithCodePlaceholder(string locale)
    {
        var template = Provider.Get(NotificationTemplateKeys.StaffPhoneVerification, locale);
        Assert.Contains("{{code}}", template.BodyText, StringComparison.Ordinal);
    }

    [Fact]
    public void Key_IsRegisteredInAll()
    {
        Assert.Contains(NotificationTemplateKeys.StaffPhoneVerification, NotificationTemplateKeys.All);
        var exception = Record.Exception(() => Provider.EnsureKeysPresent(NotificationTemplateKeys.All));
        Assert.Null(exception);
    }

    [Fact]
    public void RuBody_StaysWithinOneSmsSegment()
    {
        // After substituting a 6-digit code, the Cyrillic SMS should fit one ~67-char segment.
        var template = Provider.Get(NotificationTemplateKeys.StaffPhoneVerification, "ru");
        var rendered = template.BodyText.Replace("{{code}}", "123456", StringComparison.Ordinal);
        Assert.True(rendered.Length <= 70, $"SMS body is {rendered.Length} chars: \"{rendered}\"");
    }
}
