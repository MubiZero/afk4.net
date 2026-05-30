# Clients/CRM Foundation (Plan 6a) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the read+create foundation of the `/club/clients` screen — search players, create a player, and view a selected client's card (balances + recent ledger history, read-only).

**Architecture:** Same pattern as the Monetization tabs: hand-written API types + `clubApi` wrappers → pure model module → load-only `use*` hooks (discriminated union `{loading|error|ready}` + `retry`) → presentational components. The screen is master-detail: a search box + results table; selecting a row mounts a `ClientDetail` card. Balance/history live in a `WalletPanel` child that is mounted only when the user has `billing.view`, so the wallet-summary fetch never fires without permission. Money mutations and per-customer packages come in plans 6b/6c.

**Tech Stack:** React 19, TypeScript, Vite, Vitest 4 + jsdom + @testing-library/react (`globals: false` — import `{ it, expect, vi }` from `'vitest'`). Money via `src/club/money.ts` (`minorToMajor`). i18n RU/EN parity enforced by a test.

**Spec:** `docs/superpowers/specs/2026-05-30-platform-web-club-clients-crm-design.md`

**Key constraints (from backend recon):**
- Customer = `PlayerAccount` (account-level: `displayName`, `phoneNumber?`, `isActive`). **No update endpoint** — show "editing unavailable" note.
- Search row (`PlayerSearchResult`) carries balances as **minor units with NO currency code** → list shows balances via `formatNumber` (plain numbers), not `formatCurrency`.
- Wallet summary (`WalletSummary`) DOES carry currency → detail card uses `formatCurrency`.
- History = **last 25** entries from `wallet-summary` only (no paginated endpoint) → "last 25" note.
- All money on the wire is `MoneyMinor {currencyCode, minorUnits}`.

**npm cwd:** `D:\afk4.net\src\AFK4.Platform.Web` (all `npm`/`git` commands below assume this directory).

---

### Task 1: i18n keys (clients + ledger labels)

**Files:**
- Modify: `src/i18n/messages.ts` (insert into both `ru` block before line `  },` at the end of `ru`, and `en` block before the final `  }`)
- Modify: `src/i18n/messages.test.ts` (add a parity-coverage block)

- [ ] **Step 1: Add the parity test block first (it will fail until keys exist)**

In `src/i18n/messages.test.ts`, append this `it(...)` block after the existing `'includes the loyalty (packages) keys'` test (after its closing `});`, before the end of file):

```ts
it('includes the clients/CRM keys', () => {
  for (const key of [
    'clients.search.placeholder', 'clients.search.label', 'clients.create', 'clients.create.title',
    'clients.create.submit', 'clients.field.displayName', 'clients.field.phone',
    'clients.empty', 'clients.noAccess', 'clients.selectHint', 'clients.editUnavailable',
    'clients.col.name', 'clients.col.phone', 'clients.col.wallet', 'clients.col.debt',
    'clients.col.packages', 'clients.col.status', 'clients.status.active', 'clients.status.inactive',
    'clients.billing.noAccess', 'clients.balance.wallet', 'clients.balance.debt',
    'clients.history.title', 'clients.history.empty', 'clients.history.note',
    'clients.history.col.date', 'clients.history.col.type', 'clients.history.col.account',
    'clients.history.col.amount', 'clients.history.col.minutes', 'clients.history.col.reason',
    'ledger.type.top_up', 'ledger.type.gameplay_charge', 'ledger.type.package_purchase',
    'ledger.type.package_consumption', 'ledger.type.bonus_grant', 'ledger.type.bonus_consumption',
    'ledger.type.refund', 'ledger.type.manual_correction', 'ledger.type.postpaid_debt',
    'ledger.type.debt_payment', 'ledger.type.reversal',
    'ledger.account.wallet', 'ledger.account.debt', 'ledger.account.package_time', 'ledger.account.bonus_time'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});
```

- [ ] **Step 2: Run the parity test to verify it fails**

Run: `npm test -- messages`
Expected: FAIL — `messages.ru['clients.search.placeholder']` is undefined (and the existing identical-key-sets test still passes because we haven't desynced yet).

- [ ] **Step 3: Add the RU keys**

In `src/i18n/messages.ts`, the `ru` block currently ends:
```ts
    'loyalty.field.active': 'Активен'
  },
  en: {
```
Change the `'loyalty.field.active'` line to add a trailing comma and insert the new keys before the `  },`:

```ts
    'loyalty.field.active': 'Активен',
    'clients.search.placeholder': 'Поиск по имени или телефону',
    'clients.search.label': 'Поиск клиентов',
    'clients.create': 'Создать клиента',
    'clients.create.title': 'Новый клиент',
    'clients.create.submit': 'Создать',
    'clients.field.displayName': 'Имя',
    'clients.field.phone': 'Телефон',
    'clients.empty': 'Клиенты не найдены.',
    'clients.noAccess': 'Недостаточно прав для просмотра клиентов.',
    'clients.selectHint': 'Выберите клиента, чтобы увидеть баланс и историю.',
    'clients.editUnavailable': 'Редактирование данных клиента недоступно.',
    'clients.col.name': 'Имя',
    'clients.col.phone': 'Телефон',
    'clients.col.wallet': 'Кошелёк',
    'clients.col.debt': 'Долг',
    'clients.col.packages': 'Пакеты',
    'clients.col.status': 'Статус',
    'clients.status.active': 'Активен',
    'clients.status.inactive': 'Неактивен',
    'clients.billing.noAccess': 'Просмотр баланса недоступен.',
    'clients.balance.wallet': 'Кошелёк',
    'clients.balance.debt': 'Долг',
    'clients.history.title': 'История операций',
    'clients.history.empty': 'Операций пока нет.',
    'clients.history.note': 'Показаны последние 25 операций.',
    'clients.history.col.date': 'Дата',
    'clients.history.col.type': 'Операция',
    'clients.history.col.account': 'Счёт',
    'clients.history.col.amount': 'Сумма',
    'clients.history.col.minutes': 'Минуты',
    'clients.history.col.reason': 'Причина',
    'ledger.type.top_up': 'Пополнение',
    'ledger.type.gameplay_charge': 'Списание за игру',
    'ledger.type.package_purchase': 'Покупка пакета',
    'ledger.type.package_consumption': 'Расход пакета',
    'ledger.type.bonus_grant': 'Начисление бонуса',
    'ledger.type.bonus_consumption': 'Расход бонуса',
    'ledger.type.refund': 'Возврат',
    'ledger.type.manual_correction': 'Ручная коррекция',
    'ledger.type.postpaid_debt': 'Долг (постоплата)',
    'ledger.type.debt_payment': 'Оплата долга',
    'ledger.type.reversal': 'Сторно',
    'ledger.account.wallet': 'Кошелёк',
    'ledger.account.debt': 'Долг',
    'ledger.account.package_time': 'Пакетное время',
    'ledger.account.bonus_time': 'Бонусное время'
  },
  en: {
```

- [ ] **Step 4: Add the EN keys**

In the `en` block, the file currently ends:
```ts
    'loyalty.field.active': 'Active'
  }
} as const;
```
Change the `'loyalty.field.active'` line to add a trailing comma and insert the new keys before the `  }`:

```ts
    'loyalty.field.active': 'Active',
    'clients.search.placeholder': 'Search by name or phone',
    'clients.search.label': 'Search clients',
    'clients.create': 'Add client',
    'clients.create.title': 'New client',
    'clients.create.submit': 'Create',
    'clients.field.displayName': 'Name',
    'clients.field.phone': 'Phone',
    'clients.empty': 'No clients found.',
    'clients.noAccess': 'You do not have permission to view clients.',
    'clients.selectHint': 'Select a client to view balance and history.',
    'clients.editUnavailable': 'Editing client details is not available.',
    'clients.col.name': 'Name',
    'clients.col.phone': 'Phone',
    'clients.col.wallet': 'Wallet',
    'clients.col.debt': 'Debt',
    'clients.col.packages': 'Packages',
    'clients.col.status': 'Status',
    'clients.status.active': 'Active',
    'clients.status.inactive': 'Inactive',
    'clients.billing.noAccess': 'Balance view is not available.',
    'clients.balance.wallet': 'Wallet',
    'clients.balance.debt': 'Debt',
    'clients.history.title': 'Transaction history',
    'clients.history.empty': 'No transactions yet.',
    'clients.history.note': 'Showing the last 25 transactions.',
    'clients.history.col.date': 'Date',
    'clients.history.col.type': 'Type',
    'clients.history.col.account': 'Account',
    'clients.history.col.amount': 'Amount',
    'clients.history.col.minutes': 'Minutes',
    'clients.history.col.reason': 'Reason',
    'ledger.type.top_up': 'Top-up',
    'ledger.type.gameplay_charge': 'Gameplay charge',
    'ledger.type.package_purchase': 'Package purchase',
    'ledger.type.package_consumption': 'Package usage',
    'ledger.type.bonus_grant': 'Bonus grant',
    'ledger.type.bonus_consumption': 'Bonus usage',
    'ledger.type.refund': 'Refund',
    'ledger.type.manual_correction': 'Manual correction',
    'ledger.type.postpaid_debt': 'Postpaid debt',
    'ledger.type.debt_payment': 'Debt payment',
    'ledger.type.reversal': 'Reversal',
    'ledger.account.wallet': 'Wallet',
    'ledger.account.debt': 'Debt',
    'ledger.account.package_time': 'Package time',
    'ledger.account.bonus_time': 'Bonus time'
  }
} as const;
```

- [ ] **Step 5: Run the i18n tests to verify they pass**

Run: `npm test -- messages`
Expected: PASS (both the new coverage test and the identical-key-sets test).

- [ ] **Step 6: Commit**

```bash
git add src/i18n/messages.ts src/i18n/messages.test.ts
git commit -m "feat(clients): i18n keys for clients/CRM screen + ledger labels"
```

---

### Task 2: API types + clubApi wrappers

**Files:**
- Modify: `src/api/types.ts` (append after the `UpdatePackageDefinitionRequest` interface, end of file ~line 538)
- Modify: `src/api/clubApi.ts` (add type imports to the existing `import type { … } from './types'` block; add 4 methods after `updatePackageDefinition`, ~line 317)

- [ ] **Step 1: Add the types**

Append to `src/api/types.ts`:

```ts

export interface PlayerAccount {
  playerAccountId: string;
  organizationId: string;
  homeBranchId: string;
  displayName: string;
  phoneNumber: string | null;
  isActive: boolean;
  createdAtUtc: string;
}

export interface PlayerSearchResult {
  playerAccountId: string;
  displayName: string;
  phoneNumber: string | null;
  walletBalanceMinorUnits: number;
  debtBalanceMinorUnits: number;
  activePackageCount: number;
  isActive: boolean;
}

export interface LedgerEntry {
  ledgerEntryId: string;
  organizationId: string;
  branchId: string;
  playerAccountId: string;
  sessionId: string | null;
  playerPackageId: string | null;
  entryType: string;
  accountType: string;
  amount: MoneyMinor;
  quantitySeconds: number;
  description: string;
  reason: string;
  reversesLedgerEntryId: string | null;
  createdByStaffUserId: string;
  createdAtUtc: string;
}

export interface WalletSummary {
  playerAccountId: string;
  walletBalance: MoneyMinor;
  debtBalance: MoneyMinor;
  recentEntries: LedgerEntry[];
}

export interface PlayerPackage {
  playerPackageId: string;
  packageDefinitionId: string;
  playerAccountId: string;
  name: string;
  purchasedPrice: MoneyMinor;
  includedSeconds: number;
  bonusSeconds: number;
  remainingIncludedSeconds: number;
  remainingBonusSeconds: number;
  purchasedAtUtc: string;
  expiresAtUtc: string | null;
}

export interface CreatePlayerAccountRequest {
  organizationId: string;
  displayName: string;
  phoneNumber: string | null;
  idempotencyKey: string;
}
```

- [ ] **Step 2: Add the type imports to clubApi.ts**

In `src/api/clubApi.ts`, the import block `import type { … } from './types'` is alphabetized. Add these names in alphabetical position:
- `CreatePlayerAccountRequest,` (after `CreatePackageDefinitionRequest,`)
- `PlayerAccount,` (after `PackageOption,`)
- `PlayerPackage,` (after `PlayerAccount,`)
- `PlayerSearchResult,` (after `PlayerPackage,`)
- `WalletSummary,` (after `UpdateTariffVersionRequest,`)

The result must compile; exact ordering within the block does not affect correctness (TypeScript does not require sorted imports), so if unsure, just add all five names anywhere inside the existing braces.

- [ ] **Step 3: Add the 4 methods**

In `src/api/clubApi.ts`, insert after the `updatePackageDefinition` method (just before the `private async send<T>` method):

```ts
  public searchPlayers(branchId: string, query: string, limit = 20): Promise<PlayerSearchResult[]> {
    const qs = new URLSearchParams({ query, limit: String(limit) });
    return this.send<PlayerSearchResult[]>(
      'GET',
      `/api/branches/${encodeURIComponent(branchId)}/players?${qs.toString()}`
    );
  }

  public createPlayer(branchId: string, request: CreatePlayerAccountRequest): Promise<PlayerAccount> {
    return this.send<PlayerAccount>('POST', `/api/branches/${encodeURIComponent(branchId)}/players`, request);
  }

  public getWalletSummary(playerAccountId: string): Promise<WalletSummary> {
    return this.send<WalletSummary>('GET', `/api/players/${encodeURIComponent(playerAccountId)}/wallet-summary`);
  }

  public getPlayerPackages(playerAccountId: string): Promise<PlayerPackage[]> {
    return this.send<PlayerPackage[]>('GET', `/api/players/${encodeURIComponent(playerAccountId)}/packages`);
  }
```

- [ ] **Step 4: Verify it compiles (no test yet — wrappers are exercised by later tasks)**

Run: `npx tsc -b`
Expected: PASS (no type errors).

- [ ] **Step 5: Commit**

```bash
git add src/api/types.ts src/api/clubApi.ts
git commit -m "feat(clients): add player/wallet API types and clubApi wrappers"
```

---

### Task 3: clientsModel (pure)

**Files:**
- Create: `src/club/clients/clientsModel.ts`
- Test: `src/club/clients/clientsModel.test.ts`

- [ ] **Step 1: Write the failing test**

Create `src/club/clients/clientsModel.test.ts`:

```ts
import { it, expect } from 'vitest';
import type { LedgerEntry, PlayerSearchResult, WalletSummary } from '@/api/types';
import { toPlayerRows, toBalanceView, toLedgerRows, buildCreatePlayerRequest } from './clientsModel';

const result: PlayerSearchResult = {
  playerAccountId: 'p1', displayName: 'Иван', phoneNumber: '+992900',
  walletBalanceMinorUnits: 50000, debtBalanceMinorUnits: 1500, activePackageCount: 2, isActive: true
};

const entry: LedgerEntry = {
  ledgerEntryId: 'l1', organizationId: 'org', branchId: 'b1', playerAccountId: 'p1',
  sessionId: null, playerPackageId: null, entryType: 'top_up', accountType: 'wallet',
  amount: { currencyCode: 'TJS', minorUnits: 50000 }, quantitySeconds: 0,
  description: 'd', reason: 'Касса', reversesLedgerEntryId: null,
  createdByStaffUserId: 's1', createdAtUtc: '2026-05-30T10:00:00.000Z'
};

const summary: WalletSummary = {
  playerAccountId: 'p1',
  walletBalance: { currencyCode: 'TJS', minorUnits: 50000 },
  debtBalance: { currencyCode: 'TJS', minorUnits: 1500 },
  recentEntries: [entry]
};

it('maps search results to rows with major-unit balances and empty phone fallback', () => {
  expect(toPlayerRows([result])[0]).toEqual({
    playerAccountId: 'p1', displayName: 'Иван', phone: '+992900',
    walletMajor: 500, debtMajor: 15, activePackageCount: 2, isActive: true
  });
  const noPhone = toPlayerRows([{ ...result, phoneNumber: null }])[0];
  expect(noPhone.phone).toBe('');
});

it('maps a wallet summary to a balance view', () => {
  expect(toBalanceView(summary)).toEqual({
    walletMajor: 500, walletCurrency: 'TJS', debtMajor: 15, debtCurrency: 'TJS'
  });
});

it('maps ledger entries to rows: minor to major, seconds to minutes', () => {
  expect(toLedgerRows([{ ...entry, quantitySeconds: 1800 }])[0]).toEqual({
    ledgerEntryId: 'l1', createdAtUtc: '2026-05-30T10:00:00.000Z',
    entryType: 'top_up', accountType: 'wallet', amountMajor: 500, currencyCode: 'TJS',
    quantityMinutes: 30, reason: 'Касса'
  });
});

it('builds a create request: trims fields, empty phone becomes null', () => {
  expect(buildCreatePlayerRequest('org', '  Иван  ', '  +992900  ', 'idem')).toEqual({
    organizationId: 'org', displayName: 'Иван', phoneNumber: '+992900', idempotencyKey: 'idem'
  });
  expect(buildCreatePlayerRequest('org', 'Иван', '   ', 'idem').phoneNumber).toBeNull();
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `npm test -- clientsModel`
Expected: FAIL — `./clientsModel` cannot be resolved.

- [ ] **Step 3: Write the implementation**

Create `src/club/clients/clientsModel.ts`:

```ts
import type { CreatePlayerAccountRequest, LedgerEntry, PlayerSearchResult, WalletSummary } from '@/api/types';
import { minorToMajor } from '../money';

export interface PlayerRow {
  playerAccountId: string;
  displayName: string;
  phone: string;
  walletMajor: number;
  debtMajor: number;
  activePackageCount: number;
  isActive: boolean;
}

export interface BalanceView {
  walletMajor: number;
  walletCurrency: string;
  debtMajor: number;
  debtCurrency: string;
}

export interface LedgerRow {
  ledgerEntryId: string;
  createdAtUtc: string;
  entryType: string;
  accountType: string;
  amountMajor: number;
  currencyCode: string;
  quantityMinutes: number;
  reason: string;
}

export function toPlayerRows(results: PlayerSearchResult[]): PlayerRow[] {
  return results.map(r => ({
    playerAccountId: r.playerAccountId,
    displayName: r.displayName,
    phone: r.phoneNumber ?? '',
    walletMajor: minorToMajor(r.walletBalanceMinorUnits),
    debtMajor: minorToMajor(r.debtBalanceMinorUnits),
    activePackageCount: r.activePackageCount,
    isActive: r.isActive
  }));
}

export function toBalanceView(summary: WalletSummary): BalanceView {
  return {
    walletMajor: minorToMajor(summary.walletBalance.minorUnits),
    walletCurrency: summary.walletBalance.currencyCode,
    debtMajor: minorToMajor(summary.debtBalance.minorUnits),
    debtCurrency: summary.debtBalance.currencyCode
  };
}

export function toLedgerRows(entries: LedgerEntry[]): LedgerRow[] {
  return entries.map(e => ({
    ledgerEntryId: e.ledgerEntryId,
    createdAtUtc: e.createdAtUtc,
    entryType: e.entryType,
    accountType: e.accountType,
    amountMajor: minorToMajor(e.amount.minorUnits),
    currencyCode: e.amount.currencyCode,
    quantityMinutes: Math.round(e.quantitySeconds / 60),
    reason: e.reason
  }));
}

export function buildCreatePlayerRequest(
  organizationId: string,
  displayName: string,
  phoneNumber: string,
  idempotencyKey: string
): CreatePlayerAccountRequest {
  const trimmedPhone = phoneNumber.trim();
  return {
    organizationId,
    displayName: displayName.trim(),
    phoneNumber: trimmedPhone === '' ? null : trimmedPhone,
    idempotencyKey
  };
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `npm test -- clientsModel`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/clients/clientsModel.ts src/club/clients/clientsModel.test.ts
git commit -m "feat(clients): pure clientsModel (rows, balance view, ledger rows, create request)"
```

---

### Task 4: useClientSearch hook

**Files:**
- Create: `src/club/clients/useClientSearch.ts`
- Test: `src/club/clients/useClientSearch.test.ts`

- [ ] **Step 1: Write the failing test**

Create `src/club/clients/useClientSearch.test.ts`:

```ts
import { it, expect, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import type { PlayerSearchResult } from '@/api/types';
import { useClientSearch } from './useClientSearch';

const result: PlayerSearchResult = {
  playerAccountId: 'p1', displayName: 'Иван', phoneNumber: '+992900',
  walletBalanceMinorUnits: 50000, debtBalanceMinorUnits: 0, activePackageCount: 0, isActive: true
};

it('loads search results into rows', async () => {
  const client = { searchPlayers: vi.fn(async () => [result]) };
  const { result: hook } = renderHook(() => useClientSearch(client as never, 'b1', ''));
  await waitFor(() => expect(hook.current.status).toBe('ready'));
  if (hook.current.status !== 'ready') throw new Error('not ready');
  expect(hook.current.rows.map(r => r.displayName)).toEqual(['Иван']);
  expect(client.searchPlayers).toHaveBeenCalledWith('b1', '', 20);
});

it('passes the query through to the API', async () => {
  const client = { searchPlayers: vi.fn(async () => []) };
  renderHook(() => useClientSearch(client as never, 'b1', 'иван'));
  await waitFor(() => expect(client.searchPlayers).toHaveBeenCalledWith('b1', 'иван', 20));
});

it('reports an error when the load fails', async () => {
  const client = { searchPlayers: vi.fn(async () => { throw new Error('boom'); }) };
  const { result: hook } = renderHook(() => useClientSearch(client as never, 'b1', ''));
  await waitFor(() => expect(hook.current.status).toBe('error'));
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `npm test -- useClientSearch`
Expected: FAIL — `./useClientSearch` cannot be resolved.

- [ ] **Step 3: Write the implementation**

Create `src/club/clients/useClientSearch.ts`:

```ts
import { useCallback, useEffect, useRef, useState } from 'react';
import type { ClubApiClient } from '@/api/clubApi';
import { toPlayerRows, type PlayerRow } from './clientsModel';

type Loadable = Pick<ClubApiClient, 'searchPlayers'>;

export type ClientSearchState =
  | { status: 'loading' }
  | { status: 'error'; retry: () => void }
  | { status: 'ready'; rows: PlayerRow[]; retry: () => void };

export function useClientSearch(client: Loadable, branchId: string, query: string): ClientSearchState {
  const [tick, setTick] = useState(0);
  const [phase, setPhase] = useState<'loading' | 'error' | 'ready'>('loading');
  const [rows, setRows] = useState<PlayerRow[]>([]);
  const clientRef = useRef(client);
  clientRef.current = client;

  const retry = useCallback(() => setTick(t => t + 1), []);

  useEffect(() => {
    let cancelled = false;
    setPhase('loading');
    clientRef.current.searchPlayers(branchId, query, 20)
      .then(results => { if (!cancelled) { setRows(toPlayerRows(results)); setPhase('ready'); } })
      .catch(() => { if (!cancelled) setPhase('error'); });
    return () => { cancelled = true; };
  }, [branchId, query, tick]);

  if (phase === 'loading') return { status: 'loading' };
  if (phase === 'error') return { status: 'error', retry };
  return { status: 'ready', rows, retry };
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `npm test -- useClientSearch`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/clients/useClientSearch.ts src/club/clients/useClientSearch.test.ts
git commit -m "feat(clients): useClientSearch load-only hook"
```

---

### Task 5: useWalletSummary hook

**Files:**
- Create: `src/club/clients/useWalletSummary.ts`
- Test: `src/club/clients/useWalletSummary.test.ts`

- [ ] **Step 1: Write the failing test**

Create `src/club/clients/useWalletSummary.test.ts`:

```ts
import { it, expect, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import type { WalletSummary } from '@/api/types';
import { useWalletSummary } from './useWalletSummary';

const summary: WalletSummary = {
  playerAccountId: 'p1',
  walletBalance: { currencyCode: 'TJS', minorUnits: 50000 },
  debtBalance: { currencyCode: 'TJS', minorUnits: 0 },
  recentEntries: [{
    ledgerEntryId: 'l1', organizationId: 'org', branchId: 'b1', playerAccountId: 'p1',
    sessionId: null, playerPackageId: null, entryType: 'top_up', accountType: 'wallet',
    amount: { currencyCode: 'TJS', minorUnits: 50000 }, quantitySeconds: 0,
    description: 'd', reason: 'Касса', reversesLedgerEntryId: null,
    createdByStaffUserId: 's1', createdAtUtc: '2026-05-30T10:00:00.000Z'
  }]
};

it('loads a wallet summary into balance and ledger rows', async () => {
  const client = { getWalletSummary: vi.fn(async () => summary) };
  const { result } = renderHook(() => useWalletSummary(client as never, 'p1'));
  await waitFor(() => expect(result.current.status).toBe('ready'));
  if (result.current.status !== 'ready') throw new Error('not ready');
  expect(result.current.balance.walletMajor).toBe(500);
  expect(result.current.ledger.map(r => r.entryType)).toEqual(['top_up']);
  expect(client.getWalletSummary).toHaveBeenCalledWith('p1');
});

it('reports an error when the load fails', async () => {
  const client = { getWalletSummary: vi.fn(async () => { throw new Error('boom'); }) };
  const { result } = renderHook(() => useWalletSummary(client as never, 'p1'));
  await waitFor(() => expect(result.current.status).toBe('error'));
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `npm test -- useWalletSummary`
Expected: FAIL — `./useWalletSummary` cannot be resolved.

- [ ] **Step 3: Write the implementation**

Create `src/club/clients/useWalletSummary.ts`:

```ts
import { useCallback, useEffect, useRef, useState } from 'react';
import type { ClubApiClient } from '@/api/clubApi';
import { toBalanceView, toLedgerRows, type BalanceView, type LedgerRow } from './clientsModel';

type Loadable = Pick<ClubApiClient, 'getWalletSummary'>;

export type WalletSummaryState =
  | { status: 'loading' }
  | { status: 'error'; retry: () => void }
  | { status: 'ready'; balance: BalanceView; ledger: LedgerRow[]; retry: () => void };

export function useWalletSummary(client: Loadable, playerAccountId: string): WalletSummaryState {
  const [tick, setTick] = useState(0);
  const [phase, setPhase] = useState<'loading' | 'error' | 'ready'>('loading');
  const [balance, setBalance] = useState<BalanceView | null>(null);
  const [ledger, setLedger] = useState<LedgerRow[]>([]);
  const clientRef = useRef(client);
  clientRef.current = client;

  const retry = useCallback(() => setTick(t => t + 1), []);

  useEffect(() => {
    let cancelled = false;
    setPhase('loading');
    clientRef.current.getWalletSummary(playerAccountId)
      .then(summary => {
        if (!cancelled) {
          setBalance(toBalanceView(summary));
          setLedger(toLedgerRows(summary.recentEntries));
          setPhase('ready');
        }
      })
      .catch(() => { if (!cancelled) setPhase('error'); });
    return () => { cancelled = true; };
  }, [playerAccountId, tick]);

  if (phase === 'loading' || balance === null) return { status: 'loading' };
  if (phase === 'error') return { status: 'error', retry };
  return { status: 'ready', balance, ledger, retry };
}
```

Note: the `balance === null` guard in the `loading` branch keeps TypeScript's control-flow narrowing happy (so `balance` is non-null in the `ready` return) and also covers the first render before the effect resolves.

- [ ] **Step 4: Run the test to verify it passes**

Run: `npm test -- useWalletSummary`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/clients/useWalletSummary.ts src/club/clients/useWalletSummary.test.ts
git commit -m "feat(clients): useWalletSummary load-only hook"
```

---

### Task 6: CreateClientDialog

**Files:**
- Create: `src/club/clients/CreateClientDialog.tsx`
- Test: `src/club/clients/CreateClientDialog.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `src/club/clients/CreateClientDialog.test.tsx`:

```tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { CreateClientDialog } from './CreateClientDialog';

function setup() {
  const client = { createPlayer: vi.fn(async () => ({ playerAccountId: 'p2' })) };
  const onDone = vi.fn();
  render(
    <I18nProvider><ToastProvider>
      <CreateClientDialog
        open branchId="b1" organizationId="org" client={client as never}
        onOpenChange={() => {}} onDone={onDone}
      />
    </ToastProvider></I18nProvider>
  );
  return { client, onDone };
}

it('disables submit until a name is entered', () => {
  setup();
  expect(screen.getByRole('button', { name: 'Создать' })).toBeDisabled();
  fireEvent.change(screen.getByLabelText('Имя'), { target: { value: 'Иван' } });
  expect(screen.getByRole('button', { name: 'Создать' })).toBeEnabled();
});

it('creates a player and reports done', async () => {
  const { client, onDone } = setup();
  fireEvent.change(screen.getByLabelText('Имя'), { target: { value: 'Иван' } });
  fireEvent.change(screen.getByLabelText('Телефон'), { target: { value: '+992900' } });
  fireEvent.click(screen.getByRole('button', { name: 'Создать' }));
  await waitFor(() => expect(client.createPlayer).toHaveBeenCalled());
  expect(client.createPlayer.mock.calls[0][0]).toBe('b1');
  expect(client.createPlayer.mock.calls[0][1]).toMatchObject({
    organizationId: 'org', displayName: 'Иван', phoneNumber: '+992900'
  });
  await waitFor(() => expect(onDone).toHaveBeenCalled());
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `npm test -- CreateClientDialog`
Expected: FAIL — `./CreateClientDialog` cannot be resolved.

- [ ] **Step 3: Write the implementation**

Create `src/club/clients/CreateClientDialog.tsx`:

```tsx
import { useState } from 'react';
import { Dialog, DialogContent, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import { buildCreatePlayerRequest } from './clientsModel';

type Actions = Pick<ClubApiClient, 'createPlayer'>;

export function CreateClientDialog({ open, branchId, organizationId, client, onOpenChange, onDone }: {
  open: boolean;
  branchId: string;
  organizationId: string;
  client: Actions;
  onOpenChange: (open: boolean) => void;
  onDone: () => void;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [displayName, setDisplayName] = useState('');
  const [phone, setPhone] = useState('');
  const [pending, setPending] = useState(false);

  const valid = displayName.trim() !== '';

  async function submit() {
    setPending(true);
    try {
      await client.createPlayer(branchId, buildCreatePlayerRequest(organizationId, displayName, phone, crypto.randomUUID()));
      toast({ title: t('toast.saved'), variant: 'success' });
      onDone();
      onOpenChange(false);
    } catch {
      toast({ title: t('toast.failed'), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogTitle>{t('clients.create.title')}</DialogTitle>
        <div className="flex flex-col gap-3">
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('clients.field.displayName')}</span>
            <Input aria-label={t('clients.field.displayName')} value={displayName} onChange={e => setDisplayName(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('clients.field.phone')}</span>
            <Input aria-label={t('clients.field.phone')} value={phone} onChange={e => setPhone(e.target.value)} />
          </label>
        </div>
        <DialogFooter>
          <Button variant="outline" disabled={pending} onClick={() => onOpenChange(false)}>{t('common.cancel')}</Button>
          <Button disabled={pending || !valid} onClick={() => void submit()}>{t('clients.create.submit')}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `npm test -- CreateClientDialog`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/clients/CreateClientDialog.tsx src/club/clients/CreateClientDialog.test.tsx
git commit -m "feat(clients): CreateClientDialog"
```

---

### Task 7: WalletPanel (balances + history)

**Files:**
- Create: `src/club/clients/WalletPanel.tsx`
- Test: `src/club/clients/WalletPanel.test.tsx`

This component owns the `useWalletSummary` hook and renders balances + the last-25 ledger table. It is mounted only when the caller has `billing.view` (the parent decides), so the hook never fires without permission. A typed lookup maps `entryType`/`accountType` enum strings to i18n labels with a raw-string fallback for any future enum value.

- [ ] **Step 1: Write the failing test**

Create `src/club/clients/WalletPanel.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import type { WalletSummary } from '@/api/types';
import { WalletPanel } from './WalletPanel';

const summary: WalletSummary = {
  playerAccountId: 'p1',
  walletBalance: { currencyCode: 'TJS', minorUnits: 50000 },
  debtBalance: { currencyCode: 'TJS', minorUnits: 1500 },
  recentEntries: [{
    ledgerEntryId: 'l1', organizationId: 'org', branchId: 'b1', playerAccountId: 'p1',
    sessionId: null, playerPackageId: null, entryType: 'top_up', accountType: 'wallet',
    amount: { currencyCode: 'TJS', minorUnits: 50000 }, quantitySeconds: 0,
    description: 'd', reason: 'Касса', reversesLedgerEntryId: null,
    createdByStaffUserId: 's1', createdAtUtc: '2026-05-30T10:00:00.000Z'
  }]
};

function renderPanel(client: { getWalletSummary: () => Promise<WalletSummary> }) {
  render(
    <I18nProvider>
      <WalletPanel client={client as never} playerAccountId="p1" />
    </I18nProvider>
  );
}

it('shows balances and a translated ledger entry type', async () => {
  renderPanel({ getWalletSummary: vi.fn(async () => summary) });
  expect(await screen.findByText('Пополнение')).toBeInTheDocument();
  expect(screen.getByText('Касса')).toBeInTheDocument();
  expect(screen.getByText('История операций')).toBeInTheDocument();
});

it('shows an empty message when there is no history', async () => {
  renderPanel({ getWalletSummary: vi.fn(async () => ({ ...summary, recentEntries: [] })) });
  expect(await screen.findByText('Операций пока нет.')).toBeInTheDocument();
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `npm test -- WalletPanel`
Expected: FAIL — `./WalletPanel` cannot be resolved.

- [ ] **Step 3: Write the implementation**

Create `src/club/clients/WalletPanel.tsx`:

```tsx
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { MessageKey } from '@/i18n/messages';
import type { ClubApiClient } from '@/api/clubApi';
import { useWalletSummary } from './useWalletSummary';

type Client = Pick<ClubApiClient, 'getWalletSummary'>;

const ENTRY_TYPE_KEY: Record<string, MessageKey> = {
  top_up: 'ledger.type.top_up',
  gameplay_charge: 'ledger.type.gameplay_charge',
  package_purchase: 'ledger.type.package_purchase',
  package_consumption: 'ledger.type.package_consumption',
  bonus_grant: 'ledger.type.bonus_grant',
  bonus_consumption: 'ledger.type.bonus_consumption',
  refund: 'ledger.type.refund',
  manual_correction: 'ledger.type.manual_correction',
  postpaid_debt: 'ledger.type.postpaid_debt',
  debt_payment: 'ledger.type.debt_payment',
  reversal: 'ledger.type.reversal'
};

const ACCOUNT_TYPE_KEY: Record<string, MessageKey> = {
  wallet: 'ledger.account.wallet',
  debt: 'ledger.account.debt',
  package_time: 'ledger.account.package_time',
  bonus_time: 'ledger.account.bonus_time'
};

export function WalletPanel({ client, playerAccountId }: { client: Client; playerAccountId: string }) {
  const { t, formatCurrency, formatNumber, formatDate } = useI18n();
  const state = useWalletSummary(client, playerAccountId);

  if (state.status === 'loading') return <LoadingCards count={2} />;
  if (state.status === 'error') return <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />;

  const { balance, ledger } = state;
  const entryLabel = (type: string): string => (ENTRY_TYPE_KEY[type] ? t(ENTRY_TYPE_KEY[type]) : type);
  const accountLabel = (type: string): string => (ACCOUNT_TYPE_KEY[type] ? t(ACCOUNT_TYPE_KEY[type]) : type);

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap gap-6">
        <div>
          <p className="text-xs text-muted-foreground">{t('clients.balance.wallet')}</p>
          <p className="text-lg font-semibold tabular-nums">{formatCurrency(balance.walletMajor, balance.walletCurrency)}</p>
        </div>
        <div>
          <p className="text-xs text-muted-foreground">{t('clients.balance.debt')}</p>
          <p className="text-lg font-semibold tabular-nums">{formatCurrency(balance.debtMajor, balance.debtCurrency)}</p>
        </div>
      </div>

      <div>
        <h3 className="mb-2 text-sm font-medium">{t('clients.history.title')}</h3>
        {ledger.length === 0 ? (
          <EmptyState message={t('clients.history.empty')} />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('clients.history.col.date')}</TableHead>
                <TableHead>{t('clients.history.col.type')}</TableHead>
                <TableHead>{t('clients.history.col.account')}</TableHead>
                <TableHead>{t('clients.history.col.amount')}</TableHead>
                <TableHead>{t('clients.history.col.minutes')}</TableHead>
                <TableHead>{t('clients.history.col.reason')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {ledger.map(row => (
                <TableRow key={row.ledgerEntryId}>
                  <TableCell>{formatDate(row.createdAtUtc)}</TableCell>
                  <TableCell>{entryLabel(row.entryType)}</TableCell>
                  <TableCell>{accountLabel(row.accountType)}</TableCell>
                  <TableCell className="tabular-nums">{formatCurrency(row.amountMajor, row.currencyCode)}</TableCell>
                  <TableCell className="tabular-nums">{row.quantityMinutes === 0 ? '—' : formatNumber(row.quantityMinutes)}</TableCell>
                  <TableCell>{row.reason}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
        <p className="mt-2 text-xs text-muted-foreground">{t('clients.history.note')}</p>
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `npm test -- WalletPanel`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/clients/WalletPanel.tsx src/club/clients/WalletPanel.test.tsx
git commit -m "feat(clients): WalletPanel — balances and last-25 ledger history"
```

---

### Task 8: ClientDetail (header + conditional WalletPanel)

**Files:**
- Create: `src/club/clients/ClientDetail.tsx`
- Test: `src/club/clients/ClientDetail.test.tsx`

`ClientDetail` shows the selected client's header (name, phone, status badge), the "editing unavailable" note, and — only when `canViewBilling` — the `WalletPanel`. When `canViewBilling` is false it shows a "balance view not available" note instead, so no wallet-summary fetch occurs.

- [ ] **Step 1: Write the failing test**

Create `src/club/clients/ClientDetail.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import type { PlayerRow } from './clientsModel';
import { ClientDetail } from './ClientDetail';

const player: PlayerRow = {
  playerAccountId: 'p1', displayName: 'Иван', phone: '+992900',
  walletMajor: 500, debtMajor: 0, activePackageCount: 1, isActive: true
};

function fakeClient() {
  return {
    getWalletSummary: vi.fn(async () => ({
      playerAccountId: 'p1',
      walletBalance: { currencyCode: 'TJS', minorUnits: 50000 },
      debtBalance: { currencyCode: 'TJS', minorUnits: 0 },
      recentEntries: []
    }))
  };
}

it('shows the header and the edit-unavailable note', () => {
  render(
    <I18nProvider>
      <ClientDetail client={fakeClient() as never} player={player} canViewBilling />
    </I18nProvider>
  );
  expect(screen.getByText('Иван')).toBeInTheDocument();
  expect(screen.getByText('Редактирование данных клиента недоступно.')).toBeInTheDocument();
});

it('renders the wallet panel when billing is permitted', async () => {
  render(
    <I18nProvider>
      <ClientDetail client={fakeClient() as never} player={player} canViewBilling />
    </I18nProvider>
  );
  expect(await screen.findByText('История операций')).toBeInTheDocument();
});

it('hides the wallet panel and shows a note when billing is not permitted', () => {
  const client = fakeClient();
  render(
    <I18nProvider>
      <ClientDetail client={client as never} player={player} canViewBilling={false} />
    </I18nProvider>
  );
  expect(screen.getByText('Просмотр баланса недоступен.')).toBeInTheDocument();
  expect(client.getWalletSummary).not.toHaveBeenCalled();
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `npm test -- ClientDetail`
Expected: FAIL — `./ClientDetail` cannot be resolved.

- [ ] **Step 3: Write the implementation**

Create `src/club/clients/ClientDetail.tsx`:

```tsx
import { Card } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import type { PlayerRow } from './clientsModel';
import { WalletPanel } from './WalletPanel';

type Client = Pick<ClubApiClient, 'getWalletSummary'>;

export function ClientDetail({ client, player, canViewBilling }: {
  client: Client;
  player: PlayerRow;
  canViewBilling: boolean;
}) {
  const { t } = useI18n();
  return (
    <Card className="flex flex-col gap-4 p-4">
      <div className="flex items-center justify-between gap-3">
        <div>
          <p className="text-lg font-semibold">{player.displayName}</p>
          <p className="text-sm text-muted-foreground">{player.phone === '' ? '—' : player.phone}</p>
        </div>
        <Badge variant={player.isActive ? 'default' : 'secondary'}>
          {player.isActive ? t('clients.status.active') : t('clients.status.inactive')}
        </Badge>
      </div>

      {canViewBilling ? (
        <WalletPanel client={client} playerAccountId={player.playerAccountId} />
      ) : (
        <p className="text-sm text-muted-foreground">{t('clients.billing.noAccess')}</p>
      )}

      <p className="text-xs text-muted-foreground">{t('clients.editUnavailable')}</p>
    </Card>
  );
}
```

Note: verify `Card` and `Badge` are exported from `@/components/ui/card` and `@/components/ui/badge` (they are used elsewhere in the app). `Badge` variants `default`/`secondary` are part of the shared Badge primitive.

- [ ] **Step 4: Run the test to verify it passes**

Run: `npm test -- ClientDetail`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/clients/ClientDetail.tsx src/club/clients/ClientDetail.test.tsx
git commit -m "feat(clients): ClientDetail — header + conditional WalletPanel"
```

---

### Task 9: ClientsScreen (search + table + selection + create)

**Files:**
- Create: `src/club/clients/ClientsScreen.tsx`
- Test: `src/club/clients/ClientsScreen.test.tsx`

Master-detail screen: search input, optional "Add client" button (when `canCreate`), results table (row click selects), and the `ClientDetail` card for the selected row. Balances in the list use `formatNumber` (search results carry no currency code).

- [ ] **Step 1: Write the failing test**

Create `src/club/clients/ClientsScreen.test.tsx`:

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import type { PlayerSearchResult, WalletSummary } from '@/api/types';
import { ClientsScreen } from './ClientsScreen';

const result: PlayerSearchResult = {
  playerAccountId: 'p1', displayName: 'Иван', phoneNumber: '+992900',
  walletBalanceMinorUnits: 50000, debtBalanceMinorUnits: 0, activePackageCount: 0, isActive: true
};

const summary: WalletSummary = {
  playerAccountId: 'p1',
  walletBalance: { currencyCode: 'TJS', minorUnits: 50000 },
  debtBalance: { currencyCode: 'TJS', minorUnits: 0 },
  recentEntries: []
};

function fakeClient() {
  return {
    searchPlayers: vi.fn(async () => [result]),
    getWalletSummary: vi.fn(async () => summary),
    createPlayer: vi.fn(async () => ({ playerAccountId: 'p2' }))
  };
}

function renderScreen(opts: { canCreate?: boolean; canViewBilling?: boolean } = {}) {
  render(
    <I18nProvider><ToastProvider>
      <ClientsScreen
        client={fakeClient() as never} branchId="b1" organizationId="org"
        canCreate={opts.canCreate ?? true} canViewBilling={opts.canViewBilling ?? true}
      />
    </ToastProvider></I18nProvider>
  );
}

it('lists search results', async () => {
  renderScreen();
  expect(await screen.findByText('Иван')).toBeInTheDocument();
});

it('selecting a row shows the client detail', async () => {
  renderScreen();
  fireEvent.click(await screen.findByText('Иван'));
  expect(await screen.findByText('Редактирование данных клиента недоступно.')).toBeInTheDocument();
});

it('shows the create trigger only when permitted', async () => {
  renderScreen({ canCreate: false });
  await screen.findByText('Иван');
  expect(screen.queryByRole('button', { name: 'Создать клиента' })).not.toBeInTheDocument();
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `npm test -- ClientsScreen`
Expected: FAIL — `./ClientsScreen` cannot be resolved.

- [ ] **Step 3: Write the implementation**

Create `src/club/clients/ClientsScreen.tsx`:

```tsx
import { useState } from 'react';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import { useClientSearch } from './useClientSearch';
import { CreateClientDialog } from './CreateClientDialog';
import { ClientDetail } from './ClientDetail';
import type { PlayerRow } from './clientsModel';

type Client = Pick<ClubApiClient, 'searchPlayers' | 'getWalletSummary' | 'createPlayer'>;

export function ClientsScreen({ client, branchId, organizationId, canCreate, canViewBilling }: {
  client: Client;
  branchId: string;
  organizationId: string;
  canCreate: boolean;
  canViewBilling: boolean;
}) {
  const { t, formatNumber } = useI18n();
  const [query, setQuery] = useState('');
  const [selected, setSelected] = useState<PlayerRow | null>(null);
  const [creating, setCreating] = useState(false);
  const state = useClientSearch(client, branchId, query);

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center gap-2">
        <Input
          aria-label={t('clients.search.label')}
          placeholder={t('clients.search.placeholder')}
          value={query}
          onChange={e => setQuery(e.target.value)}
        />
        {canCreate && <Button onClick={() => setCreating(true)}>{t('clients.create')}</Button>}
      </div>

      {state.status === 'loading' ? (
        <LoadingCards count={3} />
      ) : state.status === 'error' ? (
        <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />
      ) : state.rows.length === 0 ? (
        <EmptyState message={t('clients.empty')} />
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('clients.col.name')}</TableHead>
              <TableHead>{t('clients.col.phone')}</TableHead>
              <TableHead>{t('clients.col.wallet')}</TableHead>
              <TableHead>{t('clients.col.debt')}</TableHead>
              <TableHead>{t('clients.col.packages')}</TableHead>
              <TableHead>{t('clients.col.status')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {state.rows.map(row => (
              <TableRow key={row.playerAccountId} data-clickable="true" onClick={() => setSelected(row)}>
                <TableCell className="font-medium">{row.displayName}</TableCell>
                <TableCell>{row.phone === '' ? '—' : row.phone}</TableCell>
                <TableCell className="tabular-nums">{formatNumber(row.walletMajor)}</TableCell>
                <TableCell className="tabular-nums">{formatNumber(row.debtMajor)}</TableCell>
                <TableCell className="tabular-nums">{formatNumber(row.activePackageCount)}</TableCell>
                <TableCell>
                  <Badge variant={row.isActive ? 'default' : 'secondary'}>
                    {row.isActive ? t('clients.status.active') : t('clients.status.inactive')}
                  </Badge>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      {selected !== null ? (
        <ClientDetail key={selected.playerAccountId} client={client} player={selected} canViewBilling={canViewBilling} />
      ) : (
        <p className="text-sm text-muted-foreground">{t('clients.selectHint')}</p>
      )}

      {creating && (
        <CreateClientDialog
          open branchId={branchId} organizationId={organizationId} client={client}
          onOpenChange={o => { if (!o) setCreating(false); }}
          onDone={() => state.status === 'ready' && state.retry()}
        />
      )}
    </div>
  );
}
```

Note: `state.retry` is only available in `ready`/`error` states. The `onDone` guard `state.status === 'ready' && state.retry()` refetches the list after a create. The `key={selected.playerAccountId}` on `ClientDetail` forces a fresh mount (and fresh wallet fetch) when switching clients.

- [ ] **Step 4: Run the test to verify it passes**

Run: `npm test -- ClientsScreen`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/clients/ClientsScreen.tsx src/club/clients/ClientsScreen.test.tsx
git commit -m "feat(clients): ClientsScreen master-detail (search, table, selection, create)"
```

---

### Task 10: Route + nav wiring + full suite + build gate

**Files:**
- Modify: `src/club/nav.ts:22` (flip `clients` `soon: true → false`)
- Modify: `src/App.tsx` (add `clubClients` to: `ClubRoute` union, `CLUB_SCREEN_TITLE`, `pathForRoute`, route resolution, `isClubRoute`, `ClubArea` render; import `ClientsScreen`)

- [ ] **Step 1: Enable the nav item**

In `src/club/nav.ts`, line 22, change:
```ts
      { key: 'clients', labelKey: 'nav.clients', path: '/club/clients', ownerOnly: false, soon: true },
```
to:
```ts
      { key: 'clients', labelKey: 'nav.clients', path: '/club/clients', ownerOnly: false, soon: false },
```

- [ ] **Step 2: Add the import and route union member in App.tsx**

Add the import near the other club screen imports (after the `MonetizationScreen` import at `src/App.tsx:15`):
```ts
import { ClientsScreen } from './club/clients/ClientsScreen';
```

In the `ClubRoute` union (`src/App.tsx:41-52`), add after `{ kind: 'clubVenue' }`:
```ts
  | { kind: 'clubClients' }
```

- [ ] **Step 3: Add the screen title and path mapping**

In `CLUB_SCREEN_TITLE` (`src/App.tsx:303`), add after `clubVenue: 'Зал и ПК',`:
```ts
  clubClients: 'Клиенты',
```

In `pathForRoute` (`src/App.tsx:322`), add after the `clubVenue` case:
```ts
    case 'clubClients':
      return '/club/clients';
```

- [ ] **Step 4: Add route resolution and isClubRoute membership**

In `resolvePlatformRoute` (`src/App.tsx`, after the `/club/venue` block ~line 501), add:
```ts
    if (path === '/club/clients') {
      return { route: { kind: 'clubClients' } };
    }
```

In `isClubRoute` (`src/App.tsx:636`), add after `|| route.kind === 'clubVenue'`:
```ts
    || route.kind === 'clubClients'
```

- [ ] **Step 5: Render the screen in ClubArea**

In `ClubArea` (`src/App.tsx`), add a branch after the `clubVenue` render block and before the `clubMonetization` block. Insert this between the `</VenueScreen>`-closing `)` and `: route.kind === 'clubMonetization' ? (`:

```tsx
      ) : route.kind === 'clubClients' ? (
        session.permissions.includes('players.view') ? (
          <ClientsScreen
            client={clubClient}
            branchId={activeBranchId}
            organizationId={session.organizationId}
            canCreate={session.permissions.includes('players.create')}
            canViewBilling={session.permissions.includes('billing.view')}
          />
        ) : (
          <EmptyState message={t('clients.noAccess')} />
        )
```

Verify `EmptyState` is already imported in `App.tsx` (it is used by the `clubMonetization`/`clubSettings` owner-gate branches). If not, add `import { EmptyState } from './components/ui/states';`.

- [ ] **Step 6: Run the route resolution + full suite**

Run: `npm test`
Expected: PASS — all suites green (the new clients suites plus the existing ~227 tests; total grows).

- [ ] **Step 7: Build gate (this is the real type check — vitest does NOT type-check)**

Run: `npm run build`
Expected: PASS — `tsc -b && vite build` completes with no type errors.

If `tsc` reports an error in any `.tsx`/`.test.tsx`, fix it before committing. Common pitfalls seen in prior plans: a test render helper spreading an untyped object (type props via `ComponentProps<typeof X>`), or referencing a field that does not exist on a type.

- [ ] **Step 8: Commit**

```bash
git add src/App.tsx src/club/nav.ts
git commit -m "feat(clients): wire /club/clients route, nav item, and permission gate"
```

---

## Self-Review notes (for the executor)

- **Spec coverage:** search (T4/T9), create (T6/T9), card with balances + history (T7/T8), permission gating incl. `players.view` screen gate + `billing.view` panel gate + `players.create` button gate (T9/T10), honest notes (edit-unavailable T8, last-25 T7), nav/route (T10). Per-customer packages and money mutations are intentionally out of scope (plans 6b/6c).
- **Type consistency:** `PlayerRow`/`BalanceView`/`LedgerRow` defined in T3 are consumed unchanged by T4/T5/T7/T8/T9. Hook state unions match the `usePackages` precedent. `CreatePlayerAccountRequest` matches the C# record exactly (`organizationId, displayName, phoneNumber?, idempotencyKey`).
- **Build gate is mandatory** at T10 — `npm test` (esbuild) does not type-check; only `npm run build` (`tsc -b`) does.
