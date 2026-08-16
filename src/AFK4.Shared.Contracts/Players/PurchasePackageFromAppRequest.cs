namespace AFK4.Shared.Contracts.Players;

/// <summary>
/// The player buying a package for themselves. Only the idempotency key comes from the client: the
/// organization comes from the authenticated player and the branch and package from the route, so a
/// caller cannot buy in someone else's name or out of another club's price list.
/// </summary>
public sealed record PurchasePackageFromAppRequest(string IdempotencyKey);
