using AFK4.Shared.Contracts.FloorMap;

namespace AFK4.Shared.Contracts.Install;

public sealed record InstallDiscoverResponse(
    string OwnerDisplayName,
    IReadOnlyList<InstallBranchDto> Branches);

public sealed record InstallBranchDto(
    Guid BranchId,
    string Slug,
    string Name,
    FloorMapDto FloorMap,
    IReadOnlyList<Guid> FreeSeatIds);
