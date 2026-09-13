namespace AFK4.Platform.Api.Audit;

public static class AuditOutcome
{
    public const string Succeeded = "Succeeded";

    public const string Denied = "Denied";

    /// <summary>Действие было разрешено, но не удалось — например, письмо не ушло. Это не отказ
    /// в доступе, и в журнале это разные вещи.</summary>
    public const string Failed = "Failed";
}
