namespace AFK4.Shared.Contracts.Payments;

// GET-ответ: PAN не возвращаем — только факт наличия и last4.
public sealed record DcPayLinkConfigDto(
    bool CardSet,
    string CardLast4,
    string CommentTemplate,
    bool IsActive);

// POST-запрос: CardNumber опционален — null/пусто сохраняет прежнюю карту, непустой заменяет.
public sealed record UpdateDcPayLinkConfigRequest(
    string? CardNumber,
    string CommentTemplate,
    bool IsActive);
