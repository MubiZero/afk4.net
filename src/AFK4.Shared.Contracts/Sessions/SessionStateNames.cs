namespace AFK4.Shared.Contracts.Sessions;

public static class SessionStateNames
{
    public const string Requested = "requested";
    public const string Active = "active";
    public const string Paused = "paused";
    public const string Ending = "ending";
    public const string Ended = "ended";
    public const string Failed = "failed";
    public const string Reconciled = "reconciled";
}

/// <summary>Почему сессия на паузе, если не по кнопке администратора.</summary>
public static class SessionPauseReasonNames
{
    /// <summary>
    /// В клубе отключали свет дольше получаса: время гостя возвращено, сессия ждёт его. Такую паузу не
    /// закрывает правило долгой паузы — гость мог уйти переждать.
    /// </summary>
    public const string Power = "power";
}
