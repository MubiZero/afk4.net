using AFK4.Shared.Contracts.Shifts;
using AFK4.Shared.Contracts.Reports;

namespace AFK4.OrganizationAdmin.App.Shifts;

public interface IOperatorShiftApiClient
{
    Task<ShiftDto> OpenShiftAsync(
        Guid branchId,
        OpenShiftRequest request,
        CancellationToken cancellationToken);

    Task<ShiftDto?> GetCurrentShiftAsync(
        Guid branchId,
        CancellationToken cancellationToken);

    Task<CashMovementDto> RecordCashMovementAsync(
        Guid shiftId,
        RecordCashMovementRequest request,
        CancellationToken cancellationToken);

    Task<ShiftDto> CloseShiftAsync(
        Guid shiftId,
        CloseShiftRequest request,
        CancellationToken cancellationToken);

    Task<ShiftReportResultDto> GetShiftReportAsync(
        Guid branchId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? limit,
        CancellationToken cancellationToken);

    Task<SalesReportResultDto> GetSalesReportAsync(
        Guid branchId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? limit,
        CancellationToken cancellationToken);

    Task<GameplayTimeReportResultDto> GetGameplayTimeReportAsync(
        Guid branchId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? limit,
        CancellationToken cancellationToken);

    Task<CashOperationReportResultDto> GetCashOperationReportAsync(
        Guid branchId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? limit,
        CancellationToken cancellationToken);

    Task<OperatorActionReportResultDto> GetOperatorActionReportAsync(
        Guid branchId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? limit,
        CancellationToken cancellationToken);

    Task<string> ExportShiftReportCsvAsync(
        Guid branchId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? limit,
        CancellationToken cancellationToken);

    Task<string> ExportSalesReportCsvAsync(
        Guid branchId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? limit,
        CancellationToken cancellationToken);

    Task<string> ExportGameplayTimeReportCsvAsync(
        Guid branchId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? limit,
        CancellationToken cancellationToken);

    Task<string> ExportCashOperationReportCsvAsync(
        Guid branchId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? limit,
        CancellationToken cancellationToken);

    Task<string> ExportOperatorActionReportCsvAsync(
        Guid branchId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? limit,
        CancellationToken cancellationToken);
}

public sealed class UnconfiguredOperatorShiftApiClient : IOperatorShiftApiClient
{
    public Task<ShiftDto> OpenShiftAsync(
        Guid branchId,
        OpenShiftRequest request,
        CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    public Task<ShiftDto?> GetCurrentShiftAsync(
        Guid branchId,
        CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    public Task<CashMovementDto> RecordCashMovementAsync(
        Guid shiftId,
        RecordCashMovementRequest request,
        CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    public Task<ShiftDto> CloseShiftAsync(
        Guid shiftId,
        CloseShiftRequest request,
        CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    public Task<ShiftReportResultDto> GetShiftReportAsync(
        Guid branchId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? limit,
        CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    public Task<SalesReportResultDto> GetSalesReportAsync(
        Guid branchId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? limit,
        CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    public Task<GameplayTimeReportResultDto> GetGameplayTimeReportAsync(
        Guid branchId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? limit,
        CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    public Task<CashOperationReportResultDto> GetCashOperationReportAsync(
        Guid branchId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? limit,
        CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    public Task<OperatorActionReportResultDto> GetOperatorActionReportAsync(
        Guid branchId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? limit,
        CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    public Task<string> ExportShiftReportCsvAsync(
        Guid branchId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? limit,
        CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    public Task<string> ExportSalesReportCsvAsync(
        Guid branchId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? limit,
        CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    public Task<string> ExportGameplayTimeReportCsvAsync(
        Guid branchId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? limit,
        CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    public Task<string> ExportCashOperationReportCsvAsync(
        Guid branchId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? limit,
        CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    public Task<string> ExportOperatorActionReportCsvAsync(
        Guid branchId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? limit,
        CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    private static InvalidOperationException CreateException()
    {
        return new InvalidOperationException("Operator shift API client is not configured.");
    }
}
