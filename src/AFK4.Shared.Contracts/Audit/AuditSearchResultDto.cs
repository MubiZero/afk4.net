namespace AFK4.Shared.Contracts.Audit;

public sealed record AuditSearchResultDto(
    IReadOnlyList<AuditRecordDto> Records,
    int Limit);
