namespace AFK4.Platform.Api.AntiFraud;

/// <summary>
/// Cap/policy scopes for high-risk money actions. Refund, manual correction and debt write-off all
/// share the single <see cref="HighRisk"/> bucket — the D5 daily cap is the *sum* across the three —
/// while the StaffMoneyCapEntity schema keeps a scope column open for future per-action caps.
/// </summary>
public static class MoneyActionScopes
{
    public const string HighRisk = "high_risk";
}
