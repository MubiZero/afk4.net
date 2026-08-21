namespace AFK4.Shared.Contracts.Reservations;

/// <summary>
/// «Не в этот раз» — сказанное клубом заявке, которую ещё не принимали.
///
/// Отдельно от отмены намеренно: игрок ничего не отменял, деньги ему возвращаются целиком при
/// любых настройках филиала, и в его сетевые числа этот отказ не попадает.
/// </summary>
/// <param name="ReasonCode">Код из <see cref="RejectReasonCodes"/>.</param>
/// <param name="Note">
/// Пояснение администратора своими словами. Обязательно при <see cref="RejectReasonCodes.Other"/>:
/// код «своими словами» без слов — тот же пустой отказ, от которого уходили.
/// </param>
public sealed record RejectReservationRequest(
    Guid OrganizationId,
    string ReasonCode,
    string? Note = null,
    int? ExpectedVersion = null);
