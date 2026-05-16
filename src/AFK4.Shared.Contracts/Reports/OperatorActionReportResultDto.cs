namespace AFK4.Shared.Contracts.Reports;

public sealed record OperatorActionReportResultDto(
    IReadOnlyList<OperatorActionReportRowDto> Rows,
    int Limit,
    int TotalActionCount);
