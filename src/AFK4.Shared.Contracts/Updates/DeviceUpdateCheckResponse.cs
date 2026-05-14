namespace AFK4.Shared.Contracts.Updates;

public sealed record DeviceUpdateCheckResponse(
    DateTimeOffset ServerTimeUtc,
    IReadOnlyList<ComponentUpdateInstructionDto> Updates);
