namespace AFK4.Agent.Service.Enforcement;

/// <summary>
/// Машинные политики Windows, которые агент ставит на время блокировки и снимает после неё.
///
/// Вынесено за интерфейс не ради красоты: писать в HKLM может только служба, а проверять правило
/// хочется без реестра и без Windows. Реализация трогает ровно те значения, которые сама же и
/// поставила — политику, выставленную администратором клуба руками, агент не переписывает.
/// </summary>
public interface IMachinePolicyStore
{
    bool IsSupported { get; }

    /// <summary>Ставит значение политики. Возвращает false, если записать не удалось.</summary>
    bool Set(string valueName, int value);

    /// <summary>Убирает значение политики. Возвращает false, если убрать не удалось.</summary>
    bool Remove(string valueName);
}
