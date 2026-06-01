using AFK4.Localization;

namespace AFK4.Localization.Tests;

public sealed class LocalizationServiceTests
{
    private static LocalizationService Build(string initialLocale) => new(
        new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            [Locales.Ru] = new Dictionary<string, string>
            {
                ["shell.state.locked"] = "Этот ПК заблокирован.",
                ["only.ru"] = "только ру",
            },
            [Locales.En] = new Dictionary<string, string>
            {
                ["shell.state.locked"] = "This PC is locked.",
            },
            [Locales.Tg] = new Dictionary<string, string>
            {
                ["shell.state.locked"] = "Ин компютер қулф аст.",
            },
        },
        initialLocale);

    [Fact]
    public void T_ResolvesKeyInCurrentLocale()
    {
        var svc = Build(Locales.En);

        Assert.Equal("This PC is locked.", svc.T("shell.state.locked"));
    }

    [Fact]
    public void T_FallsBackFromTgToRu_WhenTgMissingKey()
    {
        var svc = Build(Locales.Tg);

        Assert.Equal("только ру", svc.T("only.ru"));
    }

    [Fact]
    public void T_EnDoesNotFallBackToRu_ReturnsKeyWhenMissing()
    {
        var svc = Build(Locales.En);

        Assert.Equal("only.ru", svc.T("only.ru"));
    }

    [Fact]
    public void T_ReturnsKey_WhenMissingEverywhere()
    {
        var svc = Build(Locales.Ru);

        Assert.Equal("nope.absent", svc.T("nope.absent"));
    }

    [Fact]
    public void SetLocale_ChangesCurrentLocaleAndRaisesEvent()
    {
        var svc = Build(Locales.Ru);
        var raised = 0;
        svc.LocaleChanged += (_, _) => raised++;

        svc.SetLocale(Locales.En);

        Assert.Equal(Locales.En, svc.CurrentLocale);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void SetLocale_ClampsUnknownToDefault()
    {
        var svc = Build(Locales.Ru);

        svc.SetLocale("fr");

        Assert.Equal(Locales.Default, svc.CurrentLocale);
    }

    [Fact]
    public void FormatCurrency_HonorsExplicitCodeAndConvertsMinorUnits()
    {
        var svc = Build(Locales.En);

        var result = svc.FormatCurrency(150_000, "TJS");

        // 150000 minor → 1500 major; explicit code, not a culture symbol.
        Assert.Contains("TJS", result);
        Assert.Contains("1,500", result);
    }

    [Fact]
    public void FormatCurrency_IsLocaleAware()
    {
        var en = Build(Locales.En).FormatCurrency(150_000, "TJS");
        var ru = Build(Locales.Ru).FormatCurrency(150_000, "TJS");

        // ru-RU groups with a non-breaking space, not a comma → different rendering.
        Assert.NotEqual(en, ru);
        Assert.Contains("TJS", ru);
    }

    [Fact]
    public void FormatNumber_UsesLocaleDecimalSeparator()
    {
        Assert.Contains("0.5", Build(Locales.En).FormatNumber(0.5m));
        Assert.Contains("0,5", Build(Locales.Ru).FormatNumber(0.5m));
    }

    [Fact]
    public void FormatDate_IsLocaleAwareNotInvariant()
    {
        var instant = new DateTimeOffset(2026, 6, 1, 13, 5, 0, TimeSpan.Zero);

        var en = Build(Locales.En).FormatDate(instant);
        var ru = Build(Locales.Ru).FormatDate(instant);

        Assert.NotEqual(en, ru);
        Assert.Contains("01.06.2026", ru);
    }
}
