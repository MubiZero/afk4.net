# Operator «Клиенты» — S0 (рефактор + типизация + dev-mock) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Подготовить раздел «Клиенты» к редизайну: типизировать API-клиент игроков реальными DTO, завести feature-папку `src/players/` с протестированной model-поверхностью, и заполнить dev-mock (профиль/история/пакеты), не меняя поведение.

**Architecture:** Slice S0 спеки `docs/superpowers/specs/2026-06-22-operator-clients-overhaul-design.md`. Чистый derisk-рефактор: ноль изменений в UI-логике. Создаём `src/players/playersModel.ts` (зеркало паттерна `src/booking/`), куда переезжают players-эксклюзивные чистые функции и откуда ре-экспортятся общие мапперы (они остаются в `operatorHelpers.ts`, т.к. используются ещё POS/Брони/Картой — переносить нельзя без циклов). API-клиент `api/clients/players.ts` получает настоящие DTO вместо `Record<string, unknown>`. Dev-mock получает данные кошелька/истории/пакетов, чтобы редизайн было видно в превью.

**Tech Stack:** React + TypeScript (Vite), тесты на `bun test` (happy-dom + jest-dom, НЕ vitest). i18n `@afk4/i18n`, деньги `@afk4/money`.

## Global Constraints

- **Bun:** все команды через `/home/fedya/.bun/bin/bun`. Тесты — `bun test` (НЕ vitest). Сборка/тайпчек — `bun run build` (= `tsc` + `vite`); сами тесты НЕ тайпчекают, поэтому тайп-ошибки ловит только `bun run build`.
- **Рабочая директория фронта:** `/home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web`.
- **Ветка:** `feat/operator-clients-overhaul` (уже создана, в ней лежит спека).
- **Деньги:** суммы в DTO — в minor units (целые). Форматирование — существующими `formatMinorUnits`/`formatMoney`/`currencySymbol` из `@afk4/money`/`operatorHelpers`; своих форматтеров не плодить.
- **Без смены поведения:** S0 не меняет рендер/логику экрана. Изменения — только типы, расположение чистых функций, и данные мока. Существующие тесты должны остаться зелёными.
- **Feature-folder:** следовать паттерну `src/booking/` (`*Model.ts` + co-located `*.test.ts`).
- **Никаких AI-подписей** в коммитах.
- **DTO зеркалят контракты** `src/AFK4.Shared.Contracts/` (camelCase в TS ↔ PascalCase в C#).

---

### Task 1: Типизировать API-клиент игроков реальными DTO

Заменить `Record<string, unknown>`-заглушки в `api/clients/players.ts` на DTO, зеркалящие контракты (`PlayerSearchResultDto`, `PlayerAccountDto`, `WalletSummaryDto`, `LedgerEntryDto`, `PlayerPackageDto`) и точные request-типы (поля, которые реально шлёт `BackendPlayersWorkspace`).

**Files:**
- Modify: `src/api/clients/players.ts` (целиком — типы + клиент)
- Create: `src/api/clients/players.test.ts`

**Interfaces:**
- Consumes: `Guid`, `MoneyDto` из `../types`; `PlatformApiClient` из `../../platformApi`.
- Produces (импортируются из `operatorApiClients.ts`, который делает `export * from './api/clients/players'`):
  - `PlayerSearchResultDto`, `PlayerAccountDto`, `WalletSummaryDto`, `LedgerEntryDto`, `PlayerPackageDto`
  - `CreatePlayerAccountRequest`, `TopUpWalletRequest`, `PayDebtRequest`, `PurchasePackageRequest`
  - `createPlayerClient(api): { searchPlayers, createPlayer, getWalletSummary, getPlayerPackages, purchasePackage, topUpWallet, payDebt }` — сигнатуры маршрутов НЕ меняются.

- [ ] **Step 1: Написать падающий тест** `src/api/clients/players.test.ts`

```ts
import { describe, expect, it } from 'bun:test';
import { createPlayerClient } from './players';

function fakeApi() {
  const calls: Array<{ method: string; path: string; body?: unknown }> = [];
  const api = {
    get: async <T,>(path: string, query?: unknown) => {
      calls.push({ method: 'GET', path, body: query });
      return [] as unknown as T;
    },
    post: async <T,>(path: string, body: unknown) => {
      calls.push({ method: 'POST', path, body });
      return body as T;
    },
    patch: async <T,>() => ({} as T)
  };
  return { api, calls };
}

const branchId = 'acfc0212-967f-4d84-94be-9003387b09c2';
const playerId = '12121212-1212-1212-1212-121212121212';
const organizationId = '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08';

describe('createPlayerClient', () => {
  it('maps wallet top-up and debt-payment routes with typed bodies', async () => {
    const { api, calls } = fakeApi();
    const client = createPlayerClient(api as never);

    await client.getWalletSummary(playerId);
    await client.topUpWallet(playerId, {
      organizationId,
      amount: { currencyCode: 'TJS', minorUnits: 10000 },
      reason: 'Касса',
      idempotencyKey: 'idem-top'
    });
    await client.payDebt(playerId, {
      organizationId,
      amount: { currencyCode: 'TJS', minorUnits: 3500 },
      reason: 'Возврат долга',
      idempotencyKey: 'idem-debt'
    });

    expect(calls.map((c) => `${c.method} ${c.path}`)).toEqual([
      `GET /api/players/${playerId}/wallet-summary`,
      `POST /api/players/${playerId}/wallet/top-ups`,
      `POST /api/players/${playerId}/debts/payments`
    ]);
    expect(calls[1].body).toMatchObject({ reason: 'Касса', amount: { minorUnits: 10000 } });
  });
});
```

- [ ] **Step 2: Запустить тест — убедиться, что падает**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/api/clients/players.test.ts`
Expected: PASS уже сейчас (маршруты не меняются) — это smoke-якорь. Если упадёт — значит сломали сигнатуру; чинить. (Тест защищает маршруты/тела при перетипизации в Step 3.)

- [ ] **Step 3: Переписать `src/api/clients/players.ts` с реальными DTO**

```ts
import { PlatformApiClient } from '../../platformApi';
import type { Guid, MoneyDto } from '../types';

// Зеркала контрактов AFK4.Shared.Contracts (camelCase).
export interface PlayerSearchResultDto {
  playerAccountId: Guid;
  displayName: string;
  phoneNumber: string | null;
  walletBalanceMinorUnits: number;
  debtBalanceMinorUnits: number;
  activePackageCount: number;
  isActive: boolean;
}

export interface PlayerAccountDto {
  playerAccountId: Guid;
  organizationId: Guid;
  homeBranchId: Guid;
  displayName: string;
  phoneNumber: string | null;
  isActive: boolean;
  createdAtUtc: string;
}

export interface LedgerEntryDto {
  ledgerEntryId: Guid;
  organizationId: Guid;
  branchId: Guid;
  playerAccountId: Guid;
  sessionId: Guid | null;
  playerPackageId: Guid | null;
  entryType: string;
  accountType: string;
  amount: MoneyDto;
  quantitySeconds: number;
  description: string;
  reason: string;
  reversesLedgerEntryId: Guid | null;
  createdByStaffUserId: Guid;
  createdAtUtc: string;
}

export interface WalletSummaryDto {
  playerAccountId: Guid;
  walletBalance: MoneyDto;
  debtBalance: MoneyDto;
  recentEntries: LedgerEntryDto[];
}

export interface PlayerPackageDto {
  playerPackageId: Guid;
  name: string;
  purchasedPrice: MoneyDto;
  includedSeconds: number;
  bonusSeconds: number;
  remainingIncludedSeconds: number;
  remainingBonusSeconds: number;
  purchasedAtUtc: string;
  expiresAtUtc: string | null;
}

export interface CreatePlayerAccountRequest {
  organizationId: Guid;
  displayName: string;
  phoneNumber: string | null;
  idempotencyKey: string;
}

export interface TopUpWalletRequest {
  organizationId: Guid;
  amount: MoneyDto;
  reason: string;
  idempotencyKey: string;
}

export interface PayDebtRequest {
  organizationId: Guid;
  amount: MoneyDto;
  reason: string;
  idempotencyKey: string;
}

export interface PurchasePackageRequest {
  organizationId: Guid;
  packageDefinitionId: Guid;
  idempotencyKey: string;
}

export function createPlayerClient(api: PlatformApiClient) {
  return {
    searchPlayers(branchId: Guid, query: string, limit: number): Promise<PlayerSearchResultDto[]> {
      return api.get<PlayerSearchResultDto[]>(`/api/branches/${branchId}/players`, { query, limit });
    },
    createPlayer(branchId: Guid, request: CreatePlayerAccountRequest): Promise<PlayerAccountDto> {
      return api.post<PlayerAccountDto, CreatePlayerAccountRequest>(`/api/branches/${branchId}/players`, request);
    },
    getWalletSummary(playerAccountId: Guid): Promise<WalletSummaryDto> {
      return api.get<WalletSummaryDto>(`/api/players/${playerAccountId}/wallet-summary`);
    },
    getPlayerPackages(playerAccountId: Guid): Promise<PlayerPackageDto[]> {
      return api.get<PlayerPackageDto[]>(`/api/players/${playerAccountId}/packages`);
    },
    purchasePackage(playerAccountId: Guid, request: PurchasePackageRequest): Promise<PlayerPackageDto> {
      return api.post<PlayerPackageDto, PurchasePackageRequest>(`/api/players/${playerAccountId}/packages/purchases`, request);
    },
    topUpWallet(playerAccountId: Guid, request: TopUpWalletRequest): Promise<WalletSummaryDto> {
      return api.post<WalletSummaryDto, TopUpWalletRequest>(`/api/players/${playerAccountId}/wallet/top-ups`, request);
    },
    payDebt(playerAccountId: Guid, request: PayDebtRequest): Promise<WalletSummaryDto> {
      return api.post<WalletSummaryDto, PayDebtRequest>(`/api/players/${playerAccountId}/debts/payments`, request);
    }
  };
}
```

- [ ] **Step 4: Запустить тест клиента + тайпчек всего проекта**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/api/clients/players.test.ts && /home/fedya/.bun/bin/bun run build`
Expected: тест PASS; `bun run build` без ошибок (`readString`/`readNumber`/`readMoney`/`readArray` принимают `unknown`, поэтому `BackendPlayersWorkspace` компилируется; request-литералы совпадают с новыми строгими типами). Если tsc ругается на лишнее/недостающее поле в литерале запроса в `BackendPlayersWorkspace.tsx` — привести литерал к типу (НЕ ослаблять тип обратно до `Record`).

- [ ] **Step 5: Коммит**

```bash
cd /home/fedya/projects/afk4.net
git add src/AFK4.Operator.App.Web/src/api/clients/players.ts src/AFK4.Operator.App.Web/src/api/clients/players.test.ts
git commit -m "refactor(operator-clients): типизировать API-клиент игроков реальными DTO"
```

---

### Task 2: Feature-папка `src/players/` + characterization-тесты

Завести модуль `src/players/playersModel.ts`: перенести players-эксклюзивные чистые функции (`fixturePlayers`, `playerStatusLabel`) из `operatorHelpers.ts`, ре-экспортировать общие мапперы (`projectPlayerClient`, `playerPackageLabel`, тип `PlayerClientItem`) — они **остаются** в `operatorHelpers.ts` (их импортируют POS/Брони/Карта; перенос дал бы цикл). Запереть поведение тестами. Переключить импорты `BackendPlayersWorkspace.tsx` на новый модуль.

**Files:**
- Create: `src/players/playersModel.ts`
- Create: `src/players/playersModel.test.ts`
- Modify: `src/operatorHelpers.ts` (удалить `fixturePlayers` + `playerStatusLabel`, строки 1323-1348)
- Modify: `src/BackendPlayersWorkspace.tsx:10-32` (импорты)

**Interfaces:**
- Consumes: `formatMinorUnits`, `projectPlayerClient`, `playerPackageLabel`, тип `PlayerClientItem`, тип `TFunc` из `../operatorHelpers`.
- Produces: `src/players/playersModel.ts` экспортит — `fixturePlayers(currencyCode: string, t: TFunc): PlayerClientItem[]`, `playerStatusLabel(status: string, t: TFunc): string`, и ре-экспорт `projectPlayerClient`, `playerPackageLabel`, тип `PlayerClientItem`.

- [ ] **Step 1: Написать падающий тест** `src/players/playersModel.test.ts`

```ts
import { describe, expect, it } from 'bun:test';
import { fixturePlayers, playerStatusLabel, projectPlayerClient } from './playersModel';
import type { TFunc } from '../operatorHelpers';

// Стаб переводчика: возвращает ключ, игнорируя параметры — тесты проверяют только
// структурные поля проекции, не локализованный текст.
const t = ((key: string) => key) as unknown as TFunc;

describe('playerStatusLabel', () => {
  it('maps known status keys to localized keys and passes through unknown', () => {
    expect(playerStatusLabel('vip', t)).toBe('op.players.status.vip');
    expect(playerStatusLabel('debt', t)).toBe('op.players.status.debt');
    expect(playerStatusLabel('inactive', t)).toBe('op.players.status.inactive');
    expect(playerStatusLabel('mystery', t)).toBe('mystery');
  });
});

describe('fixturePlayers', () => {
  it('returns three offline-fixture clients with stable tones', () => {
    const players = fixturePlayers('TJS', t);
    expect(players).toHaveLength(3);
    expect(players.map((p) => p.tone)).toEqual(['vip', 'active', 'debt']);
    expect(players.every((p) => p.source === 'fixture')).toBe(true);
  });
});

describe('projectPlayerClient', () => {
  it('derives status/tone from debt and package counts', () => {
    const debtor = projectPlayerClient(
      { playerAccountId: 'p1', displayName: 'Olim', walletBalanceMinorUnits: 0, debtBalanceMinorUnits: 3500, activePackageCount: 0, isActive: true },
      t
    );
    expect(debtor.status).toBe('debt');
    expect(debtor.tone).toBe('debt');
    expect(debtor.debtMinorUnits).toBe(3500);
    expect(debtor.source).toBe('backend');

    const withPackages = projectPlayerClient(
      { playerAccountId: 'p2', displayName: 'Madina', walletBalanceMinorUnits: 46000, debtBalanceMinorUnits: 0, activePackageCount: 2, isActive: true },
      t
    );
    expect(withPackages.status).toBe('package');
    expect(withPackages.balanceMinorUnits).toBe(46000);

    const inactive = projectPlayerClient(
      { playerAccountId: 'p3', displayName: 'Ghost', walletBalanceMinorUnits: 0, debtBalanceMinorUnits: 0, activePackageCount: 0, isActive: false },
      t
    );
    expect(inactive.status).toBe('inactive');
  });
});
```

- [ ] **Step 2: Запустить тест — убедиться, что падает**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/players/playersModel.test.ts`
Expected: FAIL — `Cannot find module './playersModel'`.

- [ ] **Step 3: Создать `src/players/playersModel.ts`**

```ts
// Players-feature model surface (зеркало паттерна src/booking/). Общие мапперы
// (projectPlayerClient/playerPackageLabel/PlayerClientItem) остаются в operatorHelpers,
// т.к. их используют POS/Брони/Карта — здесь только ре-экспорт, чтобы у фичи был
// единый импорт. Players-эксклюзивные чистые функции живут здесь.
import { formatMinorUnits, type PlayerClientItem, type TFunc } from '../operatorHelpers';

export { projectPlayerClient, playerPackageLabel, type PlayerClientItem } from '../operatorHelpers';

export function fixturePlayers(currencyCode: string, t: TFunc): PlayerClientItem[] {
  const example = t('op.helper.player.fixture.example');
  return [
    { name: 'Madina S.', status: 'vip', balanceMinorUnits: 46000, debtMinorUnits: 0, last: example, tone: 'vip', detail: t('op.helper.player.fixture.localCard'), phoneNumber: '+992 90 555 22 11', source: 'fixture' },
    { name: 'Amir K.', status: 'active', balanceMinorUnits: 12000, debtMinorUnits: 0, last: example, tone: 'active', detail: formatMinorUnits(12000, currencyCode), phoneNumber: '', source: 'fixture' },
    { name: 'Olim K.', status: 'debt', balanceMinorUnits: 0, debtMinorUnits: 3500, last: example, tone: 'debt', detail: t('op.helper.player.fixture.debtDetail'), phoneNumber: '', source: 'fixture' }
  ];
}

// Maps the stable status key from projectPlayerClient/fixturePlayers to a localized label.
export function playerStatusLabel(status: string, t: TFunc): string {
  switch (status) {
    case 'vip':
      return t('op.players.status.vip');
    case 'active':
      return t('op.players.status.active');
    case 'debt':
      return t('op.players.status.debt');
    case 'package':
      return t('op.players.status.package');
    case 'inactive':
      return t('op.players.status.inactive');
    default:
      return status;
  }
}
```

- [ ] **Step 4: Удалить перенесённые функции из `operatorHelpers.ts`**

Удалить из `src/operatorHelpers.ts` блок `fixturePlayers` (строки ~1323-1330) и `playerStatusLabel` вместе с комментарием над ней (строки ~1332-1348). НЕ трогать `PlayerClientItem`, `projectPlayerClient`, `playerPackageLabel`, `packageOptionLabel` — они остаются. Убедиться, что `TFunc` и `formatMinorUnits` всё ещё экспортируются (они нужны новому модулю).

- [ ] **Step 5: Переключить импорты в `src/BackendPlayersWorkspace.tsx`**

В импорт-блоке из `'./operatorHelpers'` (строки 10-32) **удалить** `fixturePlayers`, `playerStatusLabel`, `playerPackageLabel`, `type PlayerClientItem`, `projectPlayerClient`. Добавить новую строку импорта сразу после блока:

```ts
import { fixturePlayers, playerPackageLabel, playerStatusLabel, projectPlayerClient, type PlayerClientItem } from './players/playersModel';
```

(Остальные символы — `createAuthenticatedOperatorClients`, `formatMinorUnits`, `formatMoney`, `readString` и т.д. — оставить импортируемыми из `./operatorHelpers`.)

- [ ] **Step 6: Прогнать тесты модуля + тайпчек**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/players/playersModel.test.ts && /home/fedya/.bun/bin/bun run build`
Expected: тесты PASS; `bun run build` без ошибок (никто, кроме `BackendPlayersWorkspace`, не импортировал `fixturePlayers`/`playerStatusLabel`; остальные импортёры общих мапперов не затронуты).

- [ ] **Step 7: Коммит**

```bash
cd /home/fedya/projects/afk4.net
git add src/AFK4.Operator.App.Web/src/players/ src/AFK4.Operator.App.Web/src/operatorHelpers.ts src/AFK4.Operator.App.Web/src/BackendPlayersWorkspace.tsx
git commit -m "refactor(operator-clients): завести src/players/ модуль с тестами модели"
```

---

### Task 3: Заполнить dev-mock данными клиента

Дать dev-mock реальные ответы на `/wallet-summary` (баланс/долг + история разных типов), `/packages` (пакет с бонусом) и осмысленные ответы на write-эндпоинты (top-up/debt/purchase), чтобы профиль/история/пакеты и действия были видны в `bun run dev`-превью. Без этого редизайн нечего полировать (спека, раздел dev-mock).

**Files:**
- Modify: `src/devMockBackend.ts` (добавить fixtures + ветки роутинга в `devMockFetch`, строки ~285-307)
- Create: `src/devMockBackend.test.ts`

**Interfaces:**
- Consumes: существующие хелперы `json`, `money`, `minutesAgoUtc`, `noContent`, константы `ORG`, `BRANCH`, `FAR_FUTURE` (все уже в файле).
- Produces: `devMockFetch` дополнительно отвечает на `GET .../wallet-summary`, `GET /api/players/{id}/packages`, `POST .../wallet/top-ups`, `POST .../debts/payments`, `POST .../packages/purchases`.

- [ ] **Step 1: Написать падающий тест** `src/devMockBackend.test.ts`

```ts
import { describe, expect, it } from 'bun:test';
import { devMockFetch } from './devMockBackend';

const playerId = 'pl-1';

describe('devMockFetch player data', () => {
  it('returns a populated wallet summary with varied ledger entries', async () => {
    const res = await devMockFetch(`https://x/api/players/${playerId}/wallet-summary`);
    const body = await res.json();
    expect(body.walletBalance.minorUnits).toBeGreaterThan(0);
    expect(Array.isArray(body.recentEntries)).toBe(true);
    expect(body.recentEntries.length).toBeGreaterThanOrEqual(3);
    const types = new Set(body.recentEntries.map((e: { entryType: string }) => e.entryType));
    expect(types.size).toBeGreaterThanOrEqual(3); // несколько разных типов операций
  });

  it('returns player packages with bonus seconds', async () => {
    const res = await devMockFetch(`https://x/api/players/${playerId}/packages`);
    const body = await res.json();
    expect(body.length).toBeGreaterThanOrEqual(1);
    expect(body[0].bonusSeconds).toBeGreaterThan(0);
  });

  it('echoes a wallet summary when topping up', async () => {
    const res = await devMockFetch(`https://x/api/players/${playerId}/wallet/top-ups`, { method: 'POST' });
    const body = await res.json();
    expect(body.walletBalance).toBeDefined();
  });
});
```

- [ ] **Step 2: Запустить тест — убедиться, что падает**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/devMockBackend.test.ts`
Expected: FAIL — wallet-summary сейчас попадает в fallback `json([])`, у массива нет `.walletBalance`.

- [ ] **Step 3: Добавить fixtures в `src/devMockBackend.ts`**

Сразу после функции `players()` (после строки ~256) добавить:

```ts
// История операций клиента для превью: разные типы записей (пополнение, списание за игру,
// покупка пакета, погашение долга, бонус) с человекочитаемыми описаниями.
function ledgerEntries() {
  const staff = '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134';
  const base = (over: Record<string, unknown>) => ({
    organizationId: ORG, branchId: BRANCH, playerAccountId: 'pl-1', sessionId: null,
    playerPackageId: null, quantitySeconds: 0, reversesLedgerEntryId: null, createdByStaffUserId: staff, ...over
  });
  return [
    base({ ledgerEntryId: 'le-1', entryType: 'top_up', accountType: 'wallet', amount: money(50000), description: 'Пополнение кошелька', reason: 'Касса', createdAtUtc: minutesAgoUtc(180) }),
    base({ ledgerEntryId: 'le-2', entryType: 'gameplay_charge', accountType: 'wallet', amount: money(-12000), description: 'Списание за игру', reason: 'Сессия PC-03', quantitySeconds: 3600, createdAtUtc: minutesAgoUtc(120) }),
    base({ ledgerEntryId: 'le-3', entryType: 'package_purchase', accountType: 'wallet', amount: money(-25000), description: 'Покупка пакета «Ночной 5ч»', reason: 'Пакет', createdAtUtc: minutesAgoUtc(90) }),
    base({ ledgerEntryId: 'le-4', entryType: 'bonus_grant', accountType: 'bonus_time', amount: money(0), quantitySeconds: 1800, description: 'Бонус 30 мин', reason: 'Лояльность', createdAtUtc: minutesAgoUtc(90) }),
    base({ ledgerEntryId: 'le-5', entryType: 'debt_payment', accountType: 'debt', amount: money(3500), description: 'Погашение долга', reason: 'Касса', createdAtUtc: minutesAgoUtc(30) })
  ];
}

function walletSummary() {
  return { playerAccountId: 'pl-1', walletBalance: money(45000), debtBalance: money(0), recentEntries: ledgerEntries() };
}

function playerPackages() {
  return [
    { playerPackageId: 'pp-1', name: 'Ночной 5ч', purchasedPrice: money(25000), includedSeconds: 18000, bonusSeconds: 1800, remainingIncludedSeconds: 9000, remainingBonusSeconds: 1800, purchasedAtUtc: minutesAgoUtc(1440), expiresAtUtc: FAR_FUTURE }
  ];
}
```

- [ ] **Step 4: Добавить ветки роутинга в `devMockFetch`**

В `devMockFetch` (`src/devMockBackend.ts`), сразу перед строкой `const matched = route(url.pathname, method);` (~строка 297) вставить:

```ts
  if (url.pathname.endsWith('/wallet-summary') && method === 'GET') {
    return json(walletSummary());
  }
  if (url.pathname.includes('/players/') && url.pathname.endsWith('/packages') && method === 'GET') {
    return json(playerPackages());
  }
  if (url.pathname.endsWith('/packages/purchases') && method === 'POST') {
    return json(playerPackages()[0]);
  }
  if ((url.pathname.endsWith('/wallet/top-ups') || url.pathname.endsWith('/debts/payments')) && method === 'POST') {
    return json(walletSummary());
  }
```

(Порядок важен: ветка player-packages проверяет `/players/` в пути, чтобы не перехватывать branch-маршрут `/api/branches/{id}/packages`.)

- [ ] **Step 5: Прогнать тест мока + тайпчек**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/devMockBackend.test.ts && /home/fedya/.bun/bin/bun run build`
Expected: тесты PASS; `bun run build` чисто.

- [ ] **Step 6: Коммит**

```bash
cd /home/fedya/projects/afk4.net
git add src/AFK4.Operator.App.Web/src/devMockBackend.ts src/AFK4.Operator.App.Web/src/devMockBackend.test.ts
git commit -m "test(operator-clients): заполнить dev-mock кошельком/историей/пакетами клиента"
```

---

### Task 4: Полная верификация S0

Прогнать весь тест-сьют и тайпчек, глазами проверить превью (история/пакеты теперь видны), убедиться в ноль-регрессии.

**Files:** нет изменений (только проверки; при находках — отдельные фиксы в рамках затронутых задач).

- [ ] **Step 1: Полный прогон тестов фронта**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test`
Expected: всё зелёное. (Примечание: `App.test.tsx` в этом репо гоняется отдельным прогоном из-за утечки `mock.module` — если общий прогон его подхватывает и флакает, прогнать `bun test src/App.test.tsx` отдельно и убедиться, что зелёно.)

- [ ] **Step 2: Тайпчек + сборка**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run build`
Expected: без ошибок.

- [ ] **Step 3: Глазами в превью**

Открыть существующее WPF/браузер-превью (mock-режим): выбрать клиента → в карточке должны появиться баланс/долг, список пакетов (с человеческим временем — пока старый рендер, ок), и история с записями (пока сырой `entryType` — это чинит S1, в S0 главное, что данные доезжают). Это подтверждает, что mock питает экран.

- [ ] **Step 4: Финальный статус**

Run: `cd /home/fedya/projects/afk4.net && git log --oneline feat/operator-clients-overhaul -5`
Expected: видны 3 коммита S0 (+ коммит спеки). Сообщить пользователю, что S0 готов к ревью/PR.

---

## Self-Review

**Spec coverage (S0-часть спеки):**
- «Вынос монолита в `src/players/`» → Task 2 (playersModel + ре-экспорт; компонентный сплит UI отложен в S1, т.к. компоненты создаются заново при редизайне — пре-извлекать из переписываемого файла = выброшенная работа).
- «Типизировать слабый `api/clients/players.ts`» → Task 1.
- «dev-mock — заполнить дыры (wallet-summary/packages/writes)» → Task 3. (`/ledger`-мок — в S1b, т.к. сам эндпоинт появляется там.)
- «Тесты `playersModel.test.ts`» → Task 2; клиент → Task 1; мок → Task 3.
- «Без смены поведения» → во всех задачах шаги тайпчека/тестов это сторожат.

**Placeholder scan:** код приведён полностью в каждом шаге; маршруты/тела/типы конкретны. Ноль TBD.

**Type consistency:** `WalletSummaryDto`/`LedgerEntryDto`/`PlayerPackageDto` (Task 1) совпадают по полям с фикстурами мока (Task 3: `walletBalance`/`debtBalance`/`recentEntries`; `bonusSeconds`/`remainingIncludedSeconds` и т.д.). `fixturePlayers`/`playerStatusLabel`/`projectPlayerClient` сигнатуры в playersModel (Task 2) совпадают с импортом в тесте и в `BackendPlayersWorkspace`.

**Honest note:** S0 не трогает рендер истории/пакетов (сырой `entryType`/`state` остаются до S1). Это намеренно — S0 только подвозит данные и типы; «человеческий» рендер — S1.
