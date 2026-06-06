using AFK4.Platform.Api.Notifications;

namespace AFK4.Platform.Api.Tests.Notifications;

public sealed class EmbeddedTemplateProviderTests
{
    private static readonly ITemplateProvider Provider = new EmbeddedTemplateProvider(defaultLocale: "ru");

    [Fact]
    public void Get_ReturnsTemplateForRequestedLocale()
    {
        var ru = Provider.Get(NotificationTemplateKeys.Test, "ru");
        var en = Provider.Get(NotificationTemplateKeys.Test, "en");

        Assert.Equal("Проверка уведомлений AFK4.NET", ru.Subject);
        Assert.Equal("AFK4.NET notification check", en.Subject);
        Assert.Contains("{{recipient}}", en.BodyText, StringComparison.Ordinal);
        Assert.Contains("{{recipient}}", en.BodyHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void Get_FallsBackToDefaultLocaleWhenRequestedLocaleMissing()
    {
        var fallback = Provider.Get(NotificationTemplateKeys.Test, "fr");

        Assert.Equal("Проверка уведомлений AFK4.NET", fallback.Subject);
    }

    [Fact]
    public void Get_ThrowsForUnknownTemplateKey()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            Provider.Get("nope.does_not_exist", "ru"));

        Assert.Contains("nope.does_not_exist", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureKeysPresent_PassesForRegistryKeys()
    {
        var exception = Record.Exception(() => Provider.EnsureKeysPresent(NotificationTemplateKeys.All));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureKeysPresent_ThrowsListingMissingKeys()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            Provider.EnsureKeysPresent(["notification.test", "ghost.key"]));

        Assert.Contains("ghost.key", exception.Message, StringComparison.Ordinal);
    }
}
