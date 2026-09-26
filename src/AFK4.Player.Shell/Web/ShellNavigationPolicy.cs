namespace AFK4.Player.Shell.Web;

/// <summary>
/// Куда странице оболочки можно уходить: только в саму себя. Ссылка из витрины или из описания
/// игры не должна открыть браузер поверх киоска — оттуда до рабочего стола один шаг.
/// </summary>
public static class ShellNavigationPolicy
{
    public static bool IsAllowed(string? targetUri, string? appSource)
    {
        if (!Uri.TryCreate(targetUri, UriKind.Absolute, out var target)
            || !Uri.TryCreate(appSource, UriKind.Absolute, out var app))
        {
            return false;
        }

        return string.Equals(target.Scheme, app.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(target.Host, app.Host, StringComparison.OrdinalIgnoreCase)
            && target.Port == app.Port;
    }
}
