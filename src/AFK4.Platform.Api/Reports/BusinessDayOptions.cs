namespace AFK4.Platform.Api.Reports;

/// <summary>
/// The timezone whose midnight-to-midnight defines a "business day" for owner-facing day rollups
/// (e.g. the daily summary digest). Single-region today (Tajikistan, <c>Asia/Dushanbe</c>, UTC+5, no DST):
/// a UTC day boundary would slice the local day at 05:00, pushing a night club's after-midnight takings
/// into the wrong day. Resolving the day in this zone keeps "yesterday" aligned with the operator's calendar.
/// A future multi-region rollout would resolve this per-branch from <c>BranchEntity.PreferredTimeZone</c>.
/// </summary>
public sealed class BusinessDayOptions
{
    public string TimeZone { get; set; } = "Asia/Dushanbe";
}
