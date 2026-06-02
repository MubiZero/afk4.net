namespace AFK4.Platform.Api.Sessions;

/// <summary>
/// Resolves the effective offline grace window (minutes) for a branch (offline-resilience spec §6.1,
/// D3/D4). Effective = per-branch override ?? global default, hard-clamped to <c>[1, 120]</c> so a
/// misconfiguration can never let a PC run unpaid indefinitely — the clamp is enforced here, at the
/// read boundary, regardless of how the stored value got there.
/// </summary>
public static class GraceLeasePolicy
{
    public const int MinGraceMinutes = 1;
    public const int MaxGraceMinutes = 120;

    public static int Resolve(int? branchOverrideMinutes, int globalDefaultMinutes) =>
        Math.Clamp(branchOverrideMinutes ?? globalDefaultMinutes, MinGraceMinutes, MaxGraceMinutes);
}
