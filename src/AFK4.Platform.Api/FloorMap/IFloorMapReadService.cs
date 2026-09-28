using AFK4.Shared.Contracts.FloorMap;

namespace AFK4.Platform.Api.FloorMap;

public sealed record FloorMapReadResult(FloorMapDto FloorMap, string ETag);
