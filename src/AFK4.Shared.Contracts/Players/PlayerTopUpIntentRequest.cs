using System;

namespace AFK4.Shared.Contracts.Players;

// Player requests a wallet top-up.
// CurrencyCode defaults to "TJS" when null or blank.
// Method ∈ { "counter", "dcgate", "eskhata" }; null/blank → "counter" (operator-confirmed at the desk).
//
// BranchId — филиал, в который человек придёт. Он нужен только в первом действии в клубе, где
// счёта ещё нет: у сети с несколькими филиалами сервер не гадает, куда записать счёт. Поле
// необязательное — клуб с одним филиалом называть нечего, а у человека со счётом филиал уже
// известен, и присланный не переписывает его.
public sealed record PlayerTopUpIntentRequest(
    long AmountMinorUnits,
    string? CurrencyCode,
    string? Method = null,
    Guid? BranchId = null);
