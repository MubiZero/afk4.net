using AFK4.Platform.Api.Platform.Billing;
using Xunit;

namespace AFK4.Platform.Api.Tests;

/// Уведомление человек видит чаще любого экрана, поэтому деньги в нём должны выглядеть так же,
/// как в приложении: «50,00 с.», а не «50.00 TJS».
public sealed class MoneyFormattingTests
{
    [Fact]
    public void ToDisplayString_Russian_UsesCommaAndSomoniSign()
    {
        Assert.Equal("50,00 с.", MoneyFormatting.ToDisplayString(5000, "TJS", "ru"));
    }

    [Fact]
    public void ToDisplayString_English_UsesDot()
    {
        Assert.Equal("50.00 с.", MoneyFormatting.ToDisplayString(5000, "TJS", "en"));
    }

    /// Выдуманный знак хуже честного кода: незнакомую валюту показываем как есть.
    [Fact]
    public void ToDisplayString_UnknownCurrency_FallsBackToItsCode()
    {
        Assert.Equal("50,00 KZT", MoneyFormatting.ToDisplayString(5000, "KZT", "ru"));
    }

    [Fact]
    public void ToDisplayString_UnknownLocale_FallsBackToRussian()
    {
        Assert.Equal("50,00 с.", MoneyFormatting.ToDisplayString(5000, "TJS", "клингонский"));
    }

    /// Копейки не срезаются — это кошелёк игрока: «120 с.» вместо «120,50 с.» теряет деньги.
    [Fact]
    public void ToDisplayString_KeepsMinorUnits()
    {
        Assert.Equal("120,50 с.", MoneyFormatting.ToDisplayString(12050, "TJS", "ru"));
    }

    /// Прежний вид остаётся для писем и разборов: там нужна машинная форма без знака.
    [Fact]
    public void ToMajorString_StaysInvariant()
    {
        Assert.Equal("50.00", MoneyFormatting.ToMajorString(5000, "TJS"));
    }
}
