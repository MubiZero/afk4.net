namespace AFK4.WebHost;

/// <summary>Что и как показывает окно. Размеры — в точках при масштабе 100%; окно пересчитывает их под масштаб Windows.</summary>
public sealed class WebHostOptions
{
    public required string Title { get; init; }

    /// <summary>Папка собранного экрана (index.html и assets/). Отдаётся по <c>https://{VirtualHost}/</c>.</summary>
    public required string ContentRoot { get; init; }

    /// <summary>Имя своего «сайта» экрана, например <c>setup.afk4.local</c>: в сеть такие запросы не уходят.</summary>
    public required string VirtualHost { get; init; }

    /// <summary>Сервер разработки Vite (localhost) вместо папки — только при разработке.</summary>
    public Uri? DevServerUrl { get; init; }

    /// <summary>Скрипт до скриптов страницы: настройки экрана от программы (window.__AFK4_…__).</summary>
    public string? InitScript { get; init; }

    /// <summary>Папка данных движка: cookies и localStorage. Папка программы для записи закрыта.</summary>
    public required string DataFolder { get; init; }

    public int Width { get; init; } = 1100;
    public int Height { get; init; } = 740;
    public int MinWidth { get; init; } = 640;
    public int MinHeight { get; init; } = 480;

    /// <summary>Без рамки Windows: заголовок и кнопки рисует сам экран (команды окна через мост).</summary>
    public bool Frameless { get; init; }

    /// <summary>Цвет под страницей, пока она грузится, и тонкой полосы, за которую тянут край окна без рамки.</summary>
    public uint BackgroundRgb { get; init; } = 0x0B0F0E;
}
