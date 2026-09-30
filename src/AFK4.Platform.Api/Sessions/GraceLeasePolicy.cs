using AFK4.Shared.Contracts.Sessions;

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

    /// <summary>
    /// Конец оплаченного времени, который аренда может покрыть: только у идущей сессии с
    /// зафиксированным концом, и только пока он впереди. На паузе конец уезжает на время простоя,
    /// у открытого счёта его нет — там аренда остаётся окном <see cref="Resolve"/>.
    /// </summary>
    public static DateTimeOffset? PaidEnd(string sessionState, DateTimeOffset? endsAtUtc, DateTimeOffset now) =>
        sessionState == SessionStateNames.Active && endsAtUtc is { } end && end > now ? end : null;

    /// <summary>
    /// До какого момента сервер подписывает аренду. Без связи ПК держит сессию открытой ровно до
    /// срока аренды, поэтому для оплаченной сессии это её конец: игрок не теряет купленные минуты,
    /// а после конца машина запирается сама — так же, как её запирает сервер на связи. Подпись и
    /// проверка на ПК те же, агент срок не продлевает.
    /// </summary>
    public static DateTimeOffset ExpiresAt(
        string sessionState,
        DateTimeOffset? endsAtUtc,
        DateTimeOffset now,
        int graceMinutes) =>
        PaidEnd(sessionState, endsAtUtc, now) ?? now.AddMinutes(graceMinutes);
}
