using System.ComponentModel;

namespace AFK4.Localization;

/// <summary>
/// An observable wrapper that resolves a single catalog key against a
/// <see cref="ILocalizationService"/> and re-resolves (raising
/// <see cref="PropertyChanged"/>) whenever the active locale changes. The WPF
/// <c>{loc:T}</c> markup extension binds a control's text to <see cref="Value"/>,
/// so XAML literals update live on a locale switch — but the change-tracking logic
/// itself carries no WPF dependency and is unit-tested directly.
/// </summary>
public sealed class LocalizedString : INotifyPropertyChanged
{
    private readonly ILocalizationService service;
    private readonly string key;

    public LocalizedString(ILocalizationService service, string key)
    {
        this.service = service;
        this.key = key;
        service.LocaleChanged += OnLocaleChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Value => service.T(key);

    private void OnLocaleChanged(object? sender, EventArgs e) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
}
