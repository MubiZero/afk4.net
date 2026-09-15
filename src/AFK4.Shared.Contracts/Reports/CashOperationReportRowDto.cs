using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Reports;

public sealed record CashOperationReportRowDto(
    Guid OperationId,
    Guid OrganizationId,
    Guid BranchId,
    Guid? ShiftId,
    Guid CreatedByStaffUserId,
    string SourceType,
    string OperationType,
    MoneyDto CashImpact,
    string Reason,
    DateTimeOffset CreatedAtUtc,
    // Кто провёл операцию. Журнал кассы отвечает на вопрос «кто взял деньги», а идентификатор
    // сотрудника на этот вопрос не отвечает: показывать кассиру GUID — то же, что не показывать.
    string CreatedByDisplayName = "");
