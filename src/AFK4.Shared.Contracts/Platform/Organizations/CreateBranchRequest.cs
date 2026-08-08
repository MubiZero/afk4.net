namespace AFK4.Shared.Contracts.Platform.Organizations;

/// <summary>Заявка на добавление филиала существующему клубу.</summary>
public sealed record CreateBranchRequest(
    string Slug,
    string Name,
    string City,
    string? PreferredTimeZone);
