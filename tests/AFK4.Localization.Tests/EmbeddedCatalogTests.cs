using AFK4.Localization;

namespace AFK4.Localization.Tests;

public sealed class EmbeddedCatalogTests
{
    [Fact]
    public void LoadEmbedded_ReadsCatalogAndResolvesKnownKey()
    {
        var svc = LocalizationService.LoadEmbedded(Locales.Ru);

        // "shell.signOut" exists in the shared catalog (locales/ru.json).
        Assert.Equal("Выйти", svc.T("shell.signOut"));
    }

    [Fact]
    public void LoadEmbedded_ResolvesEnglishForSameKey()
    {
        var svc = LocalizationService.LoadEmbedded(Locales.En);

        Assert.Equal("Sign out", svc.T("shell.signOut"));
    }
}
