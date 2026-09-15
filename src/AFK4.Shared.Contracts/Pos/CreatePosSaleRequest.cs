namespace AFK4.Shared.Contracts.Pos;

/// <summary>
/// Строка чека в запросе на его создание: товар и сколько штук.
/// </summary>
/// <remarks>
/// Это НЕ <see cref="PosSaleLineDto"/>. Имя товара, цену за штуку и сумму строки сервер берёт из
/// каталога и присланному не верит (см. EfPosService.CreateSaleAsync) — а раз так, требовать их в
/// запросе значит предлагать клиенту назначить цену и делать вид, что она чего-то стоит. Стойка
/// шлёт ровно то, что знает сама.
/// </remarks>
public sealed record CreatePosSaleLineDto(Guid ProductId, int Quantity);

public sealed record CreatePosSaleRequest(
    Guid OrganizationId,
    Guid ShiftId,
    IReadOnlyList<CreatePosSaleLineDto> Lines,
    string IdempotencyKey,
    Guid? PlayerAccountId = null,
    // When set, attaches this sale to an open session tab (settled at checkout).
    Guid? SessionId = null);
