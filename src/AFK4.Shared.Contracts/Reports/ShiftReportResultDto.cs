namespace AFK4.Shared.Contracts.Reports;

public sealed record ShiftReportResultDto(
    IReadOnlyList<ShiftReportRowDto> Rows,
    int Limit);
