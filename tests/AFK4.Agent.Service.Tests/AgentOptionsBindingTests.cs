using System.Runtime.CompilerServices;

namespace AFK4.Agent.Service.Tests;

/// <summary>
/// Агент собирается Native AOT, и настройки ему заполняет генератор привязки. Свойство с <c>init</c> генератор молча
/// пропускает, а обычная сборка (и эти тесты) заполняет его отражением — сломалось бы только на ПК клуба.
/// </summary>
public sealed class AgentOptionsBindingTests
{
    [Theory]
    [InlineData(typeof(AgentOptions))]
    [InlineData(typeof(AgentLauncherAppOptions))]
    public void SettingsHaveNoInitOnlyProperties(Type options)
    {
        var initOnly = options.GetProperties()
            .Where(property => property.SetMethod?.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit)) == true)
            .Select(property => property.Name)
            .ToList();

        Assert.Empty(initOnly);
    }
}
