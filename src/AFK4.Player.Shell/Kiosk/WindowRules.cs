using AFK4.Shared.Contracts.Devices;

namespace AFK4.Player.Shell.Kiosk;

/// <summary>
/// Закрывать ли окно (профиль защиты, §6.3). Правило совпадает, если совпало всё, что в нём
/// названо: часть заголовка — без учёта регистра, класс окна — целиком. Свои окна оболочка не
/// закрывает никогда: правило «Командная строка» не должно гасить экран игрока с тем же словом.
/// </summary>
public static class WindowRules
{
    public static bool ShouldClose(IReadOnlyList<BlockedWindowRuleDto> rules, string title, string className, bool ownProcess)
    {
        if (ownProcess || rules.Count == 0)
        {
            return false;
        }

        foreach (var rule in rules)
        {
            var hasTitle = !string.IsNullOrWhiteSpace(rule.TitleContains);
            var hasClass = !string.IsNullOrWhiteSpace(rule.ClassName);
            if (!hasTitle && !hasClass)
            {
                continue;
            }

            var titleMatches = !hasTitle || title.Contains(rule.TitleContains!.Trim(), StringComparison.OrdinalIgnoreCase);
            var classMatches = !hasClass || string.Equals(className, rule.ClassName!.Trim(), StringComparison.OrdinalIgnoreCase);
            if (titleMatches && classMatches)
            {
                return true;
            }
        }

        return false;
    }
}
