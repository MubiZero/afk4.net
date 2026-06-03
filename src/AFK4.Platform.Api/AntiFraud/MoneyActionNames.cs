namespace AFK4.Platform.Api.AntiFraud;

/// <summary>Persisted string names for <see cref="MoneyActionType"/> (anti-fraud spec §5.2).</summary>
public static class MoneyActionTypeNames
{
    public const string Refund = "refund";
    public const string ManualCorrection = "manual_correction";
    public const string DebtWriteOff = "debt_write_off";

    public static string From(MoneyActionType type) => type switch
    {
        MoneyActionType.Refund => Refund,
        MoneyActionType.ManualCorrection => ManualCorrection,
        MoneyActionType.DebtWriteOff => DebtWriteOff,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown money action type."),
    };
}

/// <summary>Lifecycle states of a <c>MoneyActionRequestEntity</c> (anti-fraud spec §5.2).</summary>
public static class MoneyActionRequestStateNames
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Expired = "expired";
}
