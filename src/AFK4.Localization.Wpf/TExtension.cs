using System;
using System.Windows.Data;
using System.Windows.Markup;
using AFK4.Localization;

namespace AFK4.Localization.Wpf;

/// <summary>
/// XAML markup extension <c>{loc:T Key=...}</c> that binds a control property to a
/// catalog key resolved through the ambient <see cref="LocalizationScope.Current"/>
/// service. Because it provides a one-way <see cref="Binding"/> to a
/// <see cref="LocalizedString"/>, the bound text re-resolves automatically when the
/// active locale changes — no per-control wiring needed in code-behind.
/// </summary>
/// <remarks>
/// The class is named <c>TExtension</c> so XAML resolves the literal <c>{loc:T}</c>
/// (WPF strips the <c>Extension</c> suffix), mirroring the React <c>t(key)</c> call.
/// </remarks>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TExtension : MarkupExtension
{
    public TExtension()
    {
    }

    public TExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var service = LocalizationScope.Current;
        if (service is null)
        {
            // No service wired (e.g. design-time) → degrade to the key string (P6).
            return Key;
        }

        var binding = new Binding(nameof(LocalizedString.Value))
        {
            Source = new LocalizedString(service, Key),
            Mode = BindingMode.OneWay,
        };

        return binding.ProvideValue(serviceProvider);
    }
}
