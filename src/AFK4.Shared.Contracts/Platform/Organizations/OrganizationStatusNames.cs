namespace AFK4.Shared.Contracts.Platform.Organizations;

public static class OrganizationStatusNames
{
    public const string Active = "active";

    public const string Suspended = "suspended";

    public const string DeletionPending = "deletion_pending";

    /// <summary>
    /// Клуб ушёл и его данные стёрты. Терминальный статус: архивная строка нужна отчётности по
    /// деньгам, но отличать её от живой заявки на уход обязательно — иначе стёртый клуб выглядит
    /// как ещё живая заявка.
    /// </summary>
    public const string Purged = "purged";
}
