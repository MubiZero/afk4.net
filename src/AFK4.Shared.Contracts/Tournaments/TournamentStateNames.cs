namespace AFK4.Shared.Contracts.Tournaments;

/// <summary>Что с событием клуба прямо сейчас.</summary>
public static class TournamentStateNames
{
    /// Черновик: клуб составляет событие, игрок его не видит.
    public const string Draft = "draft";

    /// Опубликовано: событие видно в приложении и на него записываются.
    public const string Published = "published";

    /// Отменено клубом. Взносы возвращены всем записавшимся.
    public const string Cancelled = "cancelled";
}

/// <summary>Что с записью игрока на событие.</summary>
public static class TournamentRegistrationStateNames
{
    public const string Registered = "registered";

    public const string Cancelled = "cancelled";
}

/// <summary>
/// Коды отказов, по которым клиент собирает фразу. Текст отказа — дело клиента, а код — общий
/// язык сервера и приложения.
/// </summary>
public static class TournamentRefusalCodes
{
    public const string NotPublished = "tournament_not_published";
    public const string AlreadyStarted = "tournament_already_started";
    public const string Full = "tournament_full";
    public const string AlreadyRegistered = "tournament_already_registered";
    public const string NotRegistered = "tournament_not_registered";
    public const string InsufficientFunds = "insufficient_funds";
    public const string Cancelled = "tournament_cancelled";
}
