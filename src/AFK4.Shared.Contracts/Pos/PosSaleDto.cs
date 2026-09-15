using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Receipts;
using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Shared.Contracts.Pos;

public sealed record PosSaleDto(
    Guid PosSaleId,
    Guid OrganizationId,
    Guid BranchId,
    Guid ShiftId,
    string State,
    IReadOnlyList<PosSaleLineDto> Lines,
    MoneyDto Total,
    Guid CreatedByStaffUserId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? PaidAtUtc,
    DateTimeOffset? RefundedAtUtc,
    DateTimeOffset? VoidedAtUtc,
    ReceiptDto? LatestReceipt = null,
    Guid? PlayerAccountId = null,
    Guid? ShopOrderId = null,
    /// <summary>
    /// Чем за чек заплатили. Пусто у черновика — его ещё не оплачивали.
    ///
    /// До этого поля стойка рисовала в карточке чека секцию «Оплаты», которая всегда оставалась
    /// пустой: она читала поле, которого в контракте не было. Строки оплат в базе лежали всё это
    /// время — их просто не клали в ответ.
    /// </summary>
    IReadOnlyList<PaymentPartDto>? Payments = null);
