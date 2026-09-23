using AFK4.Shared.Contracts.Operator;

namespace AFK4.Platform.Api.Search;

/// <summary>
/// Какие виды находок оператору вообще можно показывать. Считается из его прав в филиале:
/// палитра не должна становиться обходом прав — то, чего человеку не видно в разделе, не должно
/// находиться и поиском.
/// </summary>
public sealed record BranchSearchScope(
    bool Seats,
    bool Players,
    bool Reservations,
    bool Receipts,
    bool Orders)
{
    public bool Nothing => !Seats && !Players && !Reservations && !Receipts && !Orders;
}

public interface IBranchSearchService
{
    /// <param name="perKindLimit">
    /// Сколько находок брать каждого вида. Ограничение на вид, а не на весь список: в зале на
    /// двести мест «PC-1…PC-200» съели бы весь лимит, и чек, который человек и набирал, не
    /// поместился бы.
    /// </param>
    Task<IReadOnlyList<BranchSearchResultDto>> SearchAsync(
        Guid organizationId,
        Guid branchId,
        string? query,
        BranchSearchScope scope,
        int perKindLimit,
        CancellationToken cancellationToken);
}
