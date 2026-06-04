# Клиенты / CRM + денежные операции — дизайн (sub-project 2, блок 6)

> Раздел `/club/clients` клуб-овнерской консоли `AFK4.Platform.Web`. Полный (не-MVP) продукт.
> Дата: 2026-05-30. Часть платформенного редизайна (см. `[[platform-web-redesign]]`).

## Цель

Экран **«Клиенты»** для управления игровыми аккаунтами клиента (PlayerAccount): поиск,
создание, просмотр карточки с балансами и историей, денежные операции (пополнение,
оплата долга, ручная коррекция, возврат) и пакеты клиента (покупка + остатки).

## Гейтинг и роли

- Nav-item `clients` — **`ownerOnly: false`** (виден и менеджерам, и владельцам). Переключить `soon: true → false`.
- Действия гейтятся по конкретным permission-строкам бэкенда, НЕ по роли:
  - `players.view` — поиск/список/карточка (базовый доступ к экрану).
  - `players.create` — кнопка «Создать клиента».
  - `billing.view` — wallet-summary + пакеты клиента (читается при открытии карточки).
  - `billing.wallet.top_up` — пополнение.
  - `billing.debt.pay` — оплата долга.
  - `billing.manual_correction` — ручная коррекция.
  - `billing.refund` — возврат проводки.
  - `packages.purchase` — покупка пакета.
- Кнопка/действие, на которое нет permission, не рендерится (как owner-gating в других экранах).
- Если нет `players.view` — экран рендерит `<EmptyState>` с понятным текстом (нет доступа).

## Бэкенд-контракты (источник истины)

### Сущности (camelCase на проводе)
- **PlayerAccountDto** `{ playerAccountId, organizationId, homeBranchId, displayName, phoneNumber?, isActive, createdAtUtc }`.
- **PlayerSearchResultDto** `{ playerAccountId, displayName, phoneNumber?, walletBalanceMinorUnits, debtBalanceMinorUnits, activePackageCount, isActive }`.
- **WalletSummaryDto** `{ playerAccountId, walletBalance: MoneyDto, debtBalance: MoneyDto, recentEntries: LedgerEntryDto[] }` (последние 25, новые первыми).
- **LedgerEntryDto** `{ ledgerEntryId, organizationId, branchId, playerAccountId, sessionId?, playerPackageId?, entryType, accountType, amount: MoneyDto, quantitySeconds, description, reason, reversesLedgerEntryId?, createdByStaffUserId, createdAtUtc }`.
- **PlayerPackageDto** `{ playerPackageId, packageDefinitionId, playerAccountId, name, purchasedPrice: MoneyDto, includedSeconds, bonusSeconds, remainingIncludedSeconds, remainingBonusSeconds, purchasedAtUtc, expiresAtUtc? }`.
- **MoneyDto** `{ currencyCode, minorUnits }` (минорные единицы; на фронте уже зеркалится как `MoneyMinor`).
- `accountType` ∈ `wallet | debt | package_time | bonus_time`.
- `entryType` ∈ `top_up | gameplay_charge | package_purchase | package_consumption | bonus_grant | bonus_consumption | refund | manual_correction | postpaid_debt | debt_payment | reversal`.

### Маршруты
| Verb | Путь | Запрос | Ответ | Permission |
|---|---|---|---|---|
| GET | `/api/branches/{branchId}/players?query=&limit=` | — | `PlayerSearchResultDto[]` | `players.view` |
| POST | `/api/branches/{branchId}/players` | `CreatePlayerAccountRequest` | `PlayerAccountDto` | `players.create` |
| GET | `/api/players/{playerAccountId}/wallet-summary` | — | `WalletSummaryDto` | `billing.view` |
| POST | `/api/players/{playerAccountId}/wallet/top-ups` | `TopUpWalletRequest` | `LedgerEntryDto` | `billing.wallet.top_up` |
| POST | `/api/players/{playerAccountId}/debts/payments` | `PayDebtRequest` | `LedgerEntryDto` | `billing.debt.pay` |
| POST | `/api/players/{playerAccountId}/ledger/manual-corrections` | `ManualLedgerCorrectionRequest` | `LedgerEntryDto` | `billing.manual_correction` |
| POST | `/api/players/{playerAccountId}/ledger/{ledgerEntryId}/refunds` | `RefundLedgerEntryRequest` | `LedgerEntryDto` | `billing.refund` |
| GET | `/api/players/{playerAccountId}/packages` | — | `PlayerPackageDto[]` | `billing.view` |
| POST | `/api/players/{playerAccountId}/packages/purchases` | `PurchasePackageRequest` | `PlayerPackageDto` | `packages.purchase` |

### Request DTO (точные поля)
- **CreatePlayerAccountRequest** `{ organizationId, displayName, phoneNumber?, idempotencyKey }` (сверено по `AFK4.Shared.Contracts/Billing/CreatePlayerAccountRequest.cs`).
- **TopUpWalletRequest** `{ organizationId, amount: MoneyDto, reason, idempotencyKey }`.
- **PayDebtRequest** `{ organizationId, amount: MoneyDto, reason, idempotencyKey }`.
- **ManualLedgerCorrectionRequest** `{ organizationId, accountType, amount: MoneyDto, quantitySeconds, reason, idempotencyKey }`.
- **RefundLedgerEntryRequest** `{ organizationId, ledgerEntryId, amount: MoneyDto, reason, idempotencyKey }`.
- **PurchasePackageRequest** `{ organizationId, packageDefinitionId, idempotencyKey }`.

## Архитектура экрана

Master-detail на одном экране `ClientsScreen`:

```
ClientsScreen
├── Поиск (Input, query) + кнопка «Создать клиента» (если players.create)
├── Таблица результатов (PlayerSearchResultDto): имя | телефон | кошелёк | долг | пакетов | статус
│     строка кликабельна → выбор клиента (selectedPlayerAccountId в useState)
└── Карточка выбранного клиента (ClientDetail), если выбран:
      ├── Шапка: имя, телефон, статус
      ├── Балансы: кошелёк / долг (из wallet-summary)
      ├── Действия (деньги): кнопки по permission → диалоги (план 6b)
      ├── Пакеты клиента (план 6c): список остатков + кнопка «Купить пакет»
      └── История: последние 25 проводок (recentEntries) — read-only, сноска про лимит
```

- Презентационные компоненты + load-only `use*` хуки (discriminated union `{loading|error|ready}+retry`,
  `useRef` клиента, deps по `[branchId, …, tick]`); мутации делают Dialog-компоненты напрямую и зовут refetch (`tick++`).
- Деньги: minor↔major через существующий `src/club/money.ts`. Время: сек↔мин через `Math.round`.
- `idempotencyKey` = `crypto.randomUUID()` в каждом create/mutation.
- `organizationId` приходит сверху (как в Монетизации), `branchId` из роута.

## Декомпозиция (3 плана, каждый — рабочий продукт)

### План 6a — Каркас CRM (read + create)
- `src/api/types.ts`: `PlayerAccount`, `PlayerSearchResult`, `WalletSummary`, `LedgerEntry`, `PlayerPackage`, `CreatePlayerAccountRequest` (+ переиспользовать `MoneyMinor`).
- `src/api/clubApi.ts`: `searchPlayers(branchId, query, limit)`, `createPlayer(branchId, req)`, `getWalletSummary(playerAccountId)`, `getPlayerPackages(playerAccountId)`.
- `src/club/clients/clientsModel.ts`: `toPlayerRows`, `buildCreatePlayerRequest`, `toLedgerRows` (форматирование entryType/accountType в RU-метки, amount→major, quantitySeconds→мин), `toBalanceView`.
- `useClientSearch.ts` (по query+branchId), `useWalletSummary.ts` (по playerAccountId).
- `CreateClientDialog.tsx` (displayName, phoneNumber, Switch не нужен — новый всегда активен).
- `ClientsScreen.tsx` (поиск + таблица + выбор), `ClientDetail.tsx` (шапка + балансы + история).
- Сноски: «Редактирование клиента недоступно», «История — последние 25 операций».
- Route `clubClients` в `App.tsx` (как `clubMonetization`, НО не owner-gated — гейт по `players.view`), `CLUB_SCREEN_TITLE.clubClients = 'Клиенты'`, nav `clients` `soon → false`.
- i18n RU/EN parity.

### План 6b — Денежные операции
- `clubApi.ts`: `topUpWallet`, `payDebt`, `createManualCorrection`, `refundLedgerEntry`.
- Модель: `buildTopUpRequest`, `buildPayDebtRequest`, `buildManualCorrectionRequest`, `buildRefundRequest` (major→minor, мин→сек где нужно).
- Диалоги: `TopUpDialog`, `PayDebtDialog`, `ManualCorrectionDialog` (Select accountType; поле суммы ИЛИ времени в зависимости от типа), `RefundDialog` (открывается из строки истории, предзаполнен amount проводки).
- Кнопки действий в `ClientDetail` гейтятся per-permission; refund-кнопка в строках истории (только для проводок, которые можно вернуть).
- После любой операции — refetch wallet-summary (`tick++`).
- i18n RU/EN parity.

### План 6c — Пакеты клиента
- `clubApi.ts`: `purchasePackage(playerAccountId, req)` (getPlayerPackages уже в 6a).
- Модель: `toPlayerPackageRows` (секунды→мин, срок), `buildPurchasePackageRequest`.
- `usePlayerPackages.ts` (по playerAccountId).
- `PurchasePackageDialog.tsx` (Select из `getPackageOptions` — активные определения пакетов; покупка по `packageDefinitionId`).
- Секция «Пакеты» в `ClientDetail`: список остатков (включённые/бонусные мин, срок) + кнопка «Купить» (если `packages.purchase`).
- i18n RU/EN parity.

## Известные ограничения бэкенда (показываем честно)
- **Нет update клиента** — `displayName`/`phoneNumber` нельзя изменить после создания. Сноска в карточке.
- **Нет полной истории** — только последние 25 проводок в wallet-summary. Сноска под таблицей истории.
- **Нет деактивации клиента** через API — `isActive` только читается. Показываем статус, без тоггла.
- **Нет отдельного balance-only эндпоинта** — балансы берём из wallet-summary.
- Категории/пакеты-определения управляются в Монетизации; здесь — только покупка для клиента.

## Тестирование
- Каждый план: модель-юниты (pure), хук-тесты (load/error/ready), компонент-тесты (Radix Tabs/Select в jsdom — как в Монетизации: `fireEvent.mouseDown` перед `click` для табов; Select-дропдауны не открываем, полагаемся на дефолт).
- Vitest `globals: false` → импортировать `{ it, expect, vi }`.
- **Build-гейт обязателен в конце каждого плана:** `npm run build` (`tsc -b && vite build`) — vitest НЕ проверяет типы.
- i18n parity-тест уже enforced; добавлять ключи в ru и en синхронно.
