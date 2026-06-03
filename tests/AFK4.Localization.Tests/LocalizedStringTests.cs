using System.ComponentModel;
using AFK4.Localization;

namespace AFK4.Localization.Tests;

public sealed class LocalizedStringTests
{
    private static LocalizationService Build(string initialLocale) => new(
        new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            [Locales.Ru] = new Dictionary<string, string> { ["shell.state.locked"] = "Этот ПК заблокирован." },
            [Locales.En] = new Dictionary<string, string> { ["shell.state.locked"] = "This PC is locked." },
            [Locales.Tg] = new Dictionary<string, string>(),
        },
        initialLocale);

    [Fact]
    public void Value_ResolvesKeyInCurrentLocale()
    {
        var loc = new LocalizedString(Build(Locales.Ru), "shell.state.locked");

        Assert.Equal("Этот ПК заблокирован.", loc.Value);
    }

    [Fact]
    public void Value_UpdatesAndRaisesPropertyChanged_OnLocaleChange()
    {
        var svc = Build(Locales.Ru);
        var loc = new LocalizedString(svc, "shell.state.locked");
        var raised = 0;
        ((INotifyPropertyChanged)loc).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LocalizedString.Value))
            {
                raised++;
            }
        };

        svc.SetLocale(Locales.En);

        Assert.Equal("This PC is locked.", loc.Value);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void Scope_HoldsCurrentService()
    {
        var svc = Build(Locales.Ru);

        LocalizationScope.Current = svc;

        Assert.Same(svc, LocalizationScope.Current);
    }
}
