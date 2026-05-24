namespace AFK4.Shared.Contracts.Identity;

public sealed record OwnerCodeIssuedResponse(
    string OwnerCode,
    string CodeSuffix,
    DateTimeOffset ExpiresAtUtc);
