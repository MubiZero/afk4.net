namespace AFK4.Shared.Contracts.Payments;

// GET response. The Hash key is never returned — only whether one is stored, so the UI can show
// "задан" without exposing the secret (mirrors how the dcgate apiKey is never round-tripped).
public sealed record EskhataMerchantConfigDto(
    string BaseUrl,
    string CompanyId,
    int MerchantId,
    bool HashKeySet,
    string Status);

// POST request. HashKey is optional: null/empty keeps the stored secret, a non-empty value
// replaces it. All four fields present (with a stored-or-supplied hash key) → Status "configured".
public sealed record UpdateEskhataMerchantConfigRequest(
    string BaseUrl,
    string CompanyId,
    int MerchantId,
    string? HashKey);
