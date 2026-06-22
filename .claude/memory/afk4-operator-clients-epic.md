---
name: afk4-operator-clients-epic
description: "Operator redesign Этап 3 «Клиенты» — активный эпик: решения дизайна, слайсы S0..S3+S1b, ветка, бэк-факты"
metadata: 
  node_type: memory
  type: project
  originSessionId: af6ceac8-6e34-4042-8419-c1df5efb0a7c
---

Этап 3 «Клиенты» программы редизайна Operator (см. [[operator-redesign-phase0-decisions]]) — **в работе** на ветке `feat/operator-clients-overhaul`. **S0 готов → PR #103** (типизация API-клиента игроков, feature-папка `src/players/`+тесты, заполнен dev-mock; no-behavior-change, 359/359 тестов). Дальше: S1 (визуал master-detail) — план ещё не написан. Раздел = воркспейс `players` (`BackendPlayersWorkspace.tsx`, ~650 строк, **НЕ заглушка**: поиск/профиль/кошелёк/пакеты/долг/создание уже работают). Сущность на бэке = **Player/PlayerAccount**, в вебе зовётся client.

**Объём (полный овэрхол): и редизайн, и функц-дыры.** Решения:
- **Подход A** — master-detail (список слева + богатая карточка с табами Кошелёк/Пакеты/История) + drawer/модалки для редкого/чувствительного (паттерн `BookingDrawer`).
- **4 фичи:** богатая история; power-tools (ручная корректировка **только деньги** + **полный** возврат); PIN клиента; правка профиля + деактивация (**только soft**, не hard-delete).
- **+S1b: серверная keyset-пагинация истории** (курсор `(CreatedAtUtc, LedgerEntryId)`) + серверный фильтр по типу. Возвращает «честный» фильтр истории.
- **YAGNI-срезы:** сегмент «Новые»/«Спящие» (нет `CreatedAtUtc`/last-visit в `PlayerSearchResultDto`); бонус-минуты в корректировке (это эпик лояльности); частичный возврат; нарядная полоса портфельных метрик.

**Слайсы (каждый = свой PR):** S0 рефактор+типизация+dev-mock+тесты → S1 визуал master-detail+богатый рендер истории (поверх RecentEntries)+человеческие пакеты → S1b ledger-эндпоинт+фильтр+«показать ещё» → S2 power-tools+PIN → S3 правка профиля+деактивация (новый бэкенд PATCH).

**Док-канон:** `docs/superpowers/specs/2026-06-22-operator-clients-overhaul-design.md` + `docs/superpowers/plans/2026-06-22-operator-clients-s0-refactor.md`.

**Бэк-факты (проверены 2026-06-22):**
- Операторские эндпоинты — `src/AFK4.Platform.Api/Endpoints/PlayerManagementEndpoints.cs`. **Есть:** create, pin (`SetPlayerPinRequest`, ≥4 симв., право `players.create`), search, wallet-summary, top-ups, debts/payments, ledger/{id}/refunds (`RefundLedgerEntryRequest`), ledger/manual-corrections (`ManualLedgerCorrectionRequest`: AccountType wallet/debt/bonus_time + Amount + QuantitySeconds + Reason). **НЕТ:** операторского PATCH правки имени/телефона и toggle деактивации (есть только self-service `PATCH /api/me/profile`) → S3 их добавляет.
- `wallet-summary` отдаёт только `RecentEntries` (снимок, не история) → отсюда нужда в S1b `/ledger`.
- Права воркспейса `players` агрегированы в `operatorPermissions.ts`; точное право для correction/refund подтвердить при S2 (кандидаты `billing.money_action.approve`/`billing.wallet.top_up`).
- Рефактор: `projectPlayerClient`/`PlayerClientItem`/`playerPackageLabel` шарятся POS/Бронями/Картой → НЕ переносить из `operatorHelpers.ts` (цикл); `players/playersModel.ts` их ре-экспортит, переезжают только `fixturePlayers`/`playerStatusLabel`.
