namespace AFK4.Platform.Api.Platform.Health;

/// <summary>
/// Адрес получателя в списке провалов. Диагностике нужен домен и вид адреса — «письма на gmail.com
/// не уходят» видно и так, — а полный адрес человека админу платформы для этого не нужен.
/// </summary>
public static class RecipientMask
{
    public static string Apply(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return string.Empty;

        var value = address.Trim();
        var at = value.IndexOf('@', StringComparison.Ordinal);
        if (at > 0)
        {
            var local = value[..at];
            var domain = value[at..];
            return local.Length <= 1 ? $"*{domain}" : $"{local[0]}***{domain}";
        }

        // Телефон: код страны читается, абонент — нет.
        var digits = value.Where(char.IsDigit).Count();
        if (digits >= 6)
        {
            var head = value.Length >= 4 ? value[..4] : value;
            return $"{head}***{value[^2..]}";
        }

        return "***";
    }
}
