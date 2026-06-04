# Club Branch Switching + «Все филиалы» Rollup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the no-op branch-switcher pilot with real branch switching (real names, persisted selection), and turn the «Все филиалы» nav item into a real client-side aggregated rollup dashboard with per-branch KPIs, branch open (switch + navigate), and branch rename — all on existing backend contracts.

**Architecture:** A small `useActiveBranch` hook holds the selected branch id (persisted to `localStorage`, validated against the session's `branchIds`) and drives every branch-scoped screen (Overview, Venue, Settings). A `useBranchDirectory` hook fetches branch profiles so the switcher shows real names. The «Все филиалы» screen aggregates each branch's dashboard summary client-side (no new endpoint) into a pure view-model, renders per-branch KPI cards + a totals strip, opens a branch by switching the active branch and navigating to Overview, and renames via a dialog over `updateBranchProfile`. Wiring lives in `App.tsx`'s `ClubArea`.

**Tech Stack:** React 19 + TypeScript, Vite, Vitest 4 + jsdom + @testing-library/react (`globals: false` → import `it`/`expect`/`vi`/`beforeEach` from `'vitest'` in every test file), shadcn/ui primitives under `src/components/ui/`, Tailwind v4, i18n RU primary / EN secondary. npm cwd: `D:\afk4.net\src\AFK4.Platform.Web`. Path alias `@/` → `src/`. `App.tsx` uses RELATIVE imports.

---

## Scope

**In scope (all on contracts that already exist):**
- Real branch switching: persisted, validated active-branch state; switcher drives Overview/Venue/Settings.
- Real branch names in the switcher (via `getBranchProfile`).
- «Все филиалы» aggregated rollup: per-branch KPI cards + totals, built client-side from `getDashboardSummary` + `getBranchProfile` per branch.
- Open a branch (switch active branch + navigate to Overview).
- Rename a branch (`updateBranchProfile`), server-confirmed, via a dialog.
- Enable the `branches` nav item.

**Out of scope / deferred (with reasons):**
- **Branch *add* and *deactivate*** — the backend exposes **no** create/delete/deactivate branch endpoint (branches are provisioned only through tenant onboarding). Per the design spec's gap rule ("deferred behind a clearly-labelled placeholder — it must not fabricate backend success"), the screen shows a disabled, clearly-labelled "add branch" affordance and offers no deactivate action. Do **not** invent a client method or fake success.
- **Deleting `ClubDashboard` / `LegacyClubScreen`** — `LegacyClubScreen` still renders the working `InstallScreen` for the `clubInstall` route (`src/components/ClubDashboard.tsx:243-245`). Установка has no redesigned replacement yet, so the component must stay. Deletion is gated on Установка being redesigned (a later plan). The legacy per-branch deep routes (`clubBranchDetail/FloorMap/Devices/PendingDevices/Operators`) are left intact for the same reason — they share that file and are now only reachable by direct URL, which is harmless.
- **Карта зала** floor-map editor, Монетизация, Клиенты/CRM, Отчёты, Профиль — their own later plans.

**Known minor limitation (acceptable):** renaming a branch refetches the rollup immediately, but the sidebar switcher's cached name updates on the next mount/reload (the directory is fetched once per `ClubArea` mount). This is cosmetic and intentionally not addressed here.

---

## File Structure

- `src/club/branches/useActiveBranch.ts` — selected-branch state, validation, `localStorage` persistence. (Task 2)
- `src/club/branches/branchRollupModel.ts` — pure view-model builder for the rollup. (Task 3)
- `src/club/branches/useBranchRollup.ts` — loads per-branch profile+summary, builds the rollup view-model. (Task 4)
- `src/club/branches/useBranchDirectory.ts` — loads branch profiles into an id→{name,city} map for the switcher. (Task 5)
- `src/club/branches/RenameBranchDialog.tsx` — rename dialog over `updateBranchProfile`. (Task 6)
- `src/club/branches/BranchesScreen.tsx` — the «Все филиалы» screen. (Task 7)
- `src/i18n/messages.ts` — new `branches.*` keys (RU+EN). (Task 1)
- `src/club/nav.ts` — enable the `branches` item. (Task 9)
- `src/App.tsx` — wire active branch + real names + render `BranchesScreen` for `clubBranches`. (Task 8)

Each file has a colocated `*.test.ts(x)`.

---

## Task 1: i18n keys for branches

**Files:**
- Modify: `src/i18n/messages.ts` (add keys to both `ru` and `en` objects)
- Test: `src/i18n/messages.test.ts`

- [ ] **Step 1: Add the failing test**

In `src/i18n/messages.test.ts`, add a new test after the existing ones:

```ts
it('includes the new branches keys', () => {
  for (const key of [
    'branches.unnamed', 'branches.totals.title', 'branches.totals.branches',
    'branches.open', 'branches.rename', 'branches.rename.title',
    'branches.add', 'branches.add.unavailable', 'branches.card.error', 'branches.empty'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});
```

- [ ] **Step 2: Run it to confirm it fails**

Run: `npm test -- messages`
Expected: FAIL (keys not present; also the ru/en parity test fails once you add to one side — add to both in Step 3).

- [ ] **Step 3: Add the keys to both locales**

In `src/i18n/messages.ts`, in the `ru` object, immediately before the closing `'roles.unknown': 'Роль'` line is the end of the ru block — instead add these right after `'roles.unknown': 'Роль'` (add a comma after it):

```ts
    'roles.unknown': 'Роль',
    'branches.unnamed': 'Филиал',
    'branches.totals.title': 'Сводка по филиалам',
    'branches.totals.branches': 'Филиалов',
    'branches.open': 'Открыть',
    'branches.rename': 'Переименовать',
    'branches.rename.title': 'Переименовать филиал',
    'branches.add': 'Добавить филиал',
    'branches.add.unavailable': 'Создание филиалов выполняется при подключении — обратитесь в поддержку.',
    'branches.card.error': 'Не удалось загрузить данные филиала.',
    'branches.empty': 'Филиалы не найдены.'
```

In the `en` object, after `'roles.unknown': 'Role'` (add a comma after it):

```ts
    'roles.unknown': 'Role',
    'branches.unnamed': 'Branch',
    'branches.totals.title': 'Branches summary',
    'branches.totals.branches': 'Branches',
    'branches.open': 'Open',
    'branches.rename': 'Rename',
    'branches.rename.title': 'Rename branch',
    'branches.add': 'Add branch',
    'branches.add.unavailable': 'Adding branches happens during onboarding — contact support.',
    'branches.card.error': 'Failed to load this branch.',
    'branches.empty': 'No branches found.'
```

- [ ] **Step 4: Run the tests**

Run: `npm test -- messages`
Expected: PASS (both the new test and the ru/en parity test).

- [ ] **Step 5: Commit**

```bash
git add src/i18n/messages.ts src/i18n/messages.test.ts
git commit -m "feat(club): add i18n keys for branches rollup screen"
```

---

## Task 2: useActiveBranch hook

**Files:**
- Create: `src/club/branches/useActiveBranch.ts`
- Test: `src/club/branches/useActiveBranch.test.ts`

- [ ] **Step 1: Write the failing test**

Create `src/club/branches/useActiveBranch.test.ts`:

```ts
import { it, expect, beforeEach } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { useActiveBranch } from './useActiveBranch';

beforeEach(() => { localStorage.clear(); });

it('defaults to the first branch when nothing is stored', () => {
  const { result } = renderHook(() => useActiveBranch(['a', 'b']));
  expect(result.current.activeBranchId).toBe('a');
});

it('restores a stored branch that is still available', () => {
  localStorage.setItem('afk4.club.activeBranchId', 'b');
  const { result } = renderHook(() => useActiveBranch(['a', 'b']));
  expect(result.current.activeBranchId).toBe('b');
});

it('ignores a stored branch that is no longer available', () => {
  localStorage.setItem('afk4.club.activeBranchId', 'z');
  const { result } = renderHook(() => useActiveBranch(['a', 'b']));
  expect(result.current.activeBranchId).toBe('a');
});

it('select changes the active branch and persists it', () => {
  const { result } = renderHook(() => useActiveBranch(['a', 'b']));
  act(() => result.current.select('b'));
  expect(result.current.activeBranchId).toBe('b');
  expect(localStorage.getItem('afk4.club.activeBranchId')).toBe('b');
});

it('select ignores a branch that is not in the list', () => {
  const { result } = renderHook(() => useActiveBranch(['a', 'b']));
  act(() => result.current.select('z'));
  expect(result.current.activeBranchId).toBe('a');
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `npm test -- useActiveBranch`
Expected: FAIL ("Failed to resolve import './useActiveBranch'").

- [ ] **Step 3: Write the implementation**

Create `src/club/branches/useActiveBranch.ts`:

```ts
import { useCallback, useEffect, useState } from 'react';

const STORAGE_KEY = 'afk4.club.activeBranchId';

function readStored(): string | null {
  try {
    return typeof localStorage === 'undefined' ? null : localStorage.getItem(STORAGE_KEY);
  } catch {
    return null;
  }
}

function writeStored(branchId: string): void {
  try {
    if (typeof localStorage !== 'undefined') localStorage.setItem(STORAGE_KEY, branchId);
  } catch {
    /* ignore */
  }
}

export interface ActiveBranch {
  activeBranchId: string;
  select: (branchId: string) => void;
}

export function useActiveBranch(branchIds: readonly string[]): ActiveBranch {
  const [activeBranchId, setActiveBranchId] = useState<string>(() => {
    const stored = readStored();
    if (stored !== null && branchIds.includes(stored)) return stored;
    return branchIds[0] ?? '';
  });

  // Keep the selection valid if the set of available branches changes.
  useEffect(() => {
    if (activeBranchId !== '' && branchIds.includes(activeBranchId)) return;
    setActiveBranchId(branchIds[0] ?? '');
  }, [branchIds, activeBranchId]);

  const select = useCallback((branchId: string) => {
    if (!branchIds.includes(branchId)) return;
    setActiveBranchId(branchId);
    writeStored(branchId);
  }, [branchIds]);

  return { activeBranchId, select };
}
```

- [ ] **Step 4: Run the tests**

Run: `npm test -- useActiveBranch`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/branches/useActiveBranch.ts src/club/branches/useActiveBranch.test.ts
git commit -m "feat(club): add useActiveBranch hook with persisted selection"
```

---

## Task 3: buildBranchRollup view-model builder

**Files:**
- Create: `src/club/branches/branchRollupModel.ts`
- Test: `src/club/branches/branchRollupModel.test.ts`

- [ ] **Step 1: Write the failing test**

Create `src/club/branches/branchRollupModel.test.ts`:

```ts
import { it, expect } from 'vitest';
import type { OperatorDashboardSummary } from '@/api/types';
import { buildBranchRollup, type BranchRollupEntry } from './branchRollupModel';

function summary(online: number, offline: number, sessions: number, alerts: number, revenue: number): OperatorDashboardSummary {
  return {
    organizationId: 'org', branchId: 'b', fromUtc: '', toUtc: '', generatedAtUtc: '',
    utilization: { totalSeats: 10, activeSessions: sessions, endingSessions: 0, onlineDevices: online, offlineDevices: offline, sessionStarts: 0, utilizationPercent: 0 },
    alertPressure: { pendingCommands: 0, failedCommands: 0, offlineDevices: offline, endingSessions: 0, totalAlerts: alerts },
    revenue: { posNetSales: { amount: 0, currencyCode: 'RUB' }, gameplayRevenue: { amount: 0, currencyCode: 'RUB' }, totalRevenue: { amount: revenue, currencyCode: 'RUB' }, posCheckCount: 0, newPlayerCount: 0 }
  };
}

it('maps a branch summary into KPI fields', () => {
  const entries: BranchRollupEntry[] = [{ branchId: 'a', name: 'Центр', city: 'Москва', summary: summary(5, 1, 2, 3, 1000) }];
  const vm = buildBranchRollup(entries);
  expect(vm.rows[0]).toEqual({
    branchId: 'a', name: 'Центр', city: 'Москва',
    kpis: { devicesOnline: { online: 5, total: 6 }, activeSessions: 2, revenueToday: { amount: 1000, currencyCode: 'RUB' }, attention: 3 }
  });
});

it('sums totals across loaded branches and counts all rows', () => {
  const entries: BranchRollupEntry[] = [
    { branchId: 'a', name: 'A', city: '', summary: summary(5, 1, 2, 3, 1000) },
    { branchId: 'b', name: 'B', city: '', summary: summary(2, 0, 1, 4, 500) }
  ];
  const vm = buildBranchRollup(entries);
  expect(vm.totals).toEqual({
    branches: 2,
    devicesOnline: { online: 7, total: 8 },
    activeSessions: 3,
    revenue: { amount: 1500, currencyCode: 'RUB' },
    attention: 7
  });
});

it('counts a failed branch in the count but excludes it from totals and marks its kpis null', () => {
  const entries: BranchRollupEntry[] = [
    { branchId: 'a', name: 'A', city: '', summary: summary(5, 1, 2, 3, 1000) },
    { branchId: 'b', name: 'B', city: '', summary: null }
  ];
  const vm = buildBranchRollup(entries);
  expect(vm.rows[1].kpis).toBeNull();
  expect(vm.totals.branches).toBe(2);
  expect(vm.totals.devicesOnline).toEqual({ online: 5, total: 6 });
  expect(vm.totals.revenue).toEqual({ amount: 1000, currencyCode: 'RUB' });
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `npm test -- branchRollupModel`
Expected: FAIL ("Failed to resolve import './branchRollupModel'").

- [ ] **Step 3: Write the implementation**

Create `src/club/branches/branchRollupModel.ts`:

```ts
import type { Money, OperatorDashboardSummary } from '@/api/types';

export interface BranchKpis {
  devicesOnline: { online: number; total: number };
  activeSessions: number;
  revenueToday: Money;
  attention: number;
}

export interface BranchRollupRow {
  branchId: string;
  name: string;
  city: string;
  kpis: BranchKpis | null; // null => this branch failed to load
}

export interface BranchRollupTotals {
  branches: number;
  devicesOnline: { online: number; total: number };
  activeSessions: number;
  revenue: Money;
  attention: number;
}

export interface BranchRollupViewModel {
  rows: BranchRollupRow[];
  totals: BranchRollupTotals;
}

export interface BranchRollupEntry {
  branchId: string;
  name: string;
  city: string;
  summary: OperatorDashboardSummary | null;
}

function toKpis(summary: OperatorDashboardSummary): BranchKpis {
  return {
    devicesOnline: {
      online: summary.utilization.onlineDevices,
      total: summary.utilization.onlineDevices + summary.utilization.offlineDevices
    },
    activeSessions: summary.utilization.activeSessions,
    revenueToday: summary.revenue.totalRevenue,
    attention: summary.alertPressure.totalAlerts
  };
}

export function buildBranchRollup(entries: BranchRollupEntry[]): BranchRollupViewModel {
  const rows: BranchRollupRow[] = entries.map(e => ({
    branchId: e.branchId,
    name: e.name,
    city: e.city,
    kpis: e.summary === null ? null : toKpis(e.summary)
  }));

  let online = 0;
  let total = 0;
  let activeSessions = 0;
  let attention = 0;
  let revenueAmount = 0;
  let currencyCode = '';
  for (const row of rows) {
    if (row.kpis === null) continue;
    online += row.kpis.devicesOnline.online;
    total += row.kpis.devicesOnline.total;
    activeSessions += row.kpis.activeSessions;
    attention += row.kpis.attention;
    revenueAmount += row.kpis.revenueToday.amount;
    if (currencyCode === '') currencyCode = row.kpis.revenueToday.currencyCode;
  }

  return {
    rows,
    totals: {
      branches: rows.length,
      devicesOnline: { online, total },
      activeSessions,
      revenue: { amount: revenueAmount, currencyCode },
      attention
    }
  };
}
```

- [ ] **Step 4: Run the tests**

Run: `npm test -- branchRollupModel`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/branches/branchRollupModel.ts src/club/branches/branchRollupModel.test.ts
git commit -m "feat(club): add branch rollup view-model builder"
```

---

## Task 4: useBranchRollup hook

**Files:**
- Create: `src/club/branches/useBranchRollup.ts`
- Test: `src/club/branches/useBranchRollup.test.ts`

Note: the hook resolves to `ready` even when individual branches fail (their `kpis` is `null`); per-branch failures are surfaced in the card, not as a whole-screen error. `Promise.all` over per-branch promises that each swallow their own errors (`.catch(() => null)`) never rejects.

- [ ] **Step 1: Write the failing test**

Create `src/club/branches/useBranchRollup.test.ts`:

```ts
import { it, expect, vi } from 'vitest';
import { waitFor, renderHook } from '@testing-library/react';
import type { OperatorDashboardSummary } from '@/api/types';
import { useBranchRollup } from './useBranchRollup';

function summary(id: string, online: number): OperatorDashboardSummary {
  return {
    organizationId: 'org', branchId: id, fromUtc: '', toUtc: '', generatedAtUtc: '',
    utilization: { totalSeats: 10, activeSessions: 1, endingSessions: 0, onlineDevices: online, offlineDevices: 0, sessionStarts: 0, utilizationPercent: 0 },
    alertPressure: { pendingCommands: 0, failedCommands: 0, offlineDevices: 0, endingSessions: 0, totalAlerts: 0 },
    revenue: { posNetSales: { amount: 0, currencyCode: 'RUB' }, gameplayRevenue: { amount: 0, currencyCode: 'RUB' }, totalRevenue: { amount: 0, currencyCode: 'RUB' }, posCheckCount: 0, newPlayerCount: 0 }
  };
}

function client() {
  return {
    getBranchProfile: vi.fn(async (id: string) => ({ organizationId: 'org', branchId: id, name: id.toUpperCase(), city: 'Москва', createdAtUtc: '' })),
    getDashboardSummary: vi.fn(async (id: string) => summary(id, id === 'a' ? 5 : 3))
  };
}

it('loads each branch and builds a rollup', async () => {
  const { result } = renderHook(() => useBranchRollup(client() as never, ['a', 'b'], 'Филиал'));
  await waitFor(() => expect(result.current.status).toBe('ready'));
  if (result.current.status !== 'ready') throw new Error('not ready');
  expect(result.current.data.rows.map(r => r.name)).toEqual(['A', 'B']);
  expect(result.current.data.totals.devicesOnline).toEqual({ online: 8, total: 8 });
});

it('marks a branch whose summary fails as kpis null and uses the unnamed fallback when its profile fails', async () => {
  const c = client();
  c.getDashboardSummary = vi.fn(async (id: string) => {
    if (id === 'b') throw new Error('boom');
    return summary(id, 5);
  });
  c.getBranchProfile = vi.fn(async (id: string) => {
    if (id === 'b') throw new Error('boom');
    return { organizationId: 'org', branchId: id, name: id.toUpperCase(), city: 'Москва', createdAtUtc: '' };
  });
  const { result } = renderHook(() => useBranchRollup(c as never, ['a', 'b'], 'Филиал'));
  await waitFor(() => expect(result.current.status).toBe('ready'));
  if (result.current.status !== 'ready') throw new Error('not ready');
  const b = result.current.data.rows.find(r => r.branchId === 'b');
  expect(b?.kpis).toBeNull();
  expect(b?.name).toBe('Филиал');
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `npm test -- useBranchRollup`
Expected: FAIL ("Failed to resolve import './useBranchRollup'").

- [ ] **Step 3: Write the implementation**

Create `src/club/branches/useBranchRollup.ts`:

```ts
import { useCallback, useEffect, useRef, useState } from 'react';
import type { ClubApiClient } from '@/api/clubApi';
import { buildBranchRollup, type BranchRollupEntry, type BranchRollupViewModel } from './branchRollupModel';

export type BranchRollupState =
  | { status: 'loading'; retry: () => void }
  | { status: 'ready'; data: BranchRollupViewModel; retry: () => void };

type Loadable = Pick<ClubApiClient, 'getDashboardSummary' | 'getBranchProfile'>;

export function useBranchRollup(client: Loadable, branchIds: readonly string[], unnamedLabel: string): BranchRollupState {
  const [tick, setTick] = useState(0);
  const [state, setState] = useState<{ status: 'loading' | 'ready'; data?: BranchRollupViewModel }>({ status: 'loading' });
  const retry = useCallback(() => setTick(t => t + 1), []);
  const clientRef = useRef(client);
  clientRef.current = client;
  const key = branchIds.join(',');

  useEffect(() => {
    let cancelled = false;
    setState({ status: 'loading' });
    const c = clientRef.current;
    const ids = key === '' ? [] : key.split(',');
    void Promise.all(ids.map(async (branchId): Promise<BranchRollupEntry> => {
      const [profile, summary] = await Promise.all([
        c.getBranchProfile(branchId).catch(() => null),
        c.getDashboardSummary(branchId).catch(() => null)
      ]);
      return {
        branchId,
        name: profile?.name ?? unnamedLabel,
        city: profile?.city ?? '',
        summary
      };
    })).then(entries => {
      if (cancelled) return;
      setState({ status: 'ready', data: buildBranchRollup(entries) });
    });
    return () => { cancelled = true; };
  }, [key, tick, unnamedLabel]);

  if (state.status === 'ready') return { status: 'ready', data: state.data!, retry };
  return { status: 'loading', retry };
}
```

- [ ] **Step 4: Run the tests**

Run: `npm test -- useBranchRollup`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/branches/useBranchRollup.ts src/club/branches/useBranchRollup.test.ts
git commit -m "feat(club): add useBranchRollup aggregation hook"
```

---

## Task 5: useBranchDirectory hook

**Files:**
- Create: `src/club/branches/useBranchDirectory.ts`
- Test: `src/club/branches/useBranchDirectory.test.ts`

- [ ] **Step 1: Write the failing test**

Create `src/club/branches/useBranchDirectory.test.ts`:

```ts
import { it, expect, vi } from 'vitest';
import { waitFor, renderHook } from '@testing-library/react';
import { useBranchDirectory } from './useBranchDirectory';

it('builds a map of branch id to name and city', async () => {
  const client = {
    getBranchProfile: vi.fn(async (id: string) => ({ organizationId: 'org', branchId: id, name: id.toUpperCase(), city: 'Москва', createdAtUtc: '' }))
  };
  const { result } = renderHook(() => useBranchDirectory(client as never, ['a', 'b']));
  await waitFor(() => expect(Object.keys(result.current)).toHaveLength(2));
  expect(result.current.a).toEqual({ name: 'A', city: 'Москва' });
  expect(result.current.b).toEqual({ name: 'B', city: 'Москва' });
});

it('omits branches whose profile fails to load', async () => {
  const client = {
    getBranchProfile: vi.fn(async (id: string) => {
      if (id === 'b') throw new Error('boom');
      return { organizationId: 'org', branchId: id, name: 'A', city: 'Москва', createdAtUtc: '' };
    })
  };
  const { result } = renderHook(() => useBranchDirectory(client as never, ['a', 'b']));
  await waitFor(() => expect(result.current.a).toBeDefined());
  expect(result.current.b).toBeUndefined();
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `npm test -- useBranchDirectory`
Expected: FAIL ("Failed to resolve import './useBranchDirectory'").

- [ ] **Step 3: Write the implementation**

Create `src/club/branches/useBranchDirectory.ts`:

```ts
import { useEffect, useRef, useState } from 'react';
import type { ClubApiClient } from '@/api/clubApi';

export type BranchDirectory = Record<string, { name: string; city: string }>;

type Loadable = Pick<ClubApiClient, 'getBranchProfile'>;

export function useBranchDirectory(client: Loadable, branchIds: readonly string[]): BranchDirectory {
  const [directory, setDirectory] = useState<BranchDirectory>({});
  const clientRef = useRef(client);
  clientRef.current = client;
  const key = branchIds.join(',');

  useEffect(() => {
    let cancelled = false;
    const c = clientRef.current;
    const ids = key === '' ? [] : key.split(',');
    void Promise.all(ids.map(async (branchId) => {
      const profile = await c.getBranchProfile(branchId).catch(() => null);
      return profile === null ? null : { branchId, name: profile.name, city: profile.city };
    })).then(results => {
      if (cancelled) return;
      const next: BranchDirectory = {};
      for (const r of results) {
        if (r !== null) next[r.branchId] = { name: r.name, city: r.city };
      }
      setDirectory(next);
    });
    return () => { cancelled = true; };
  }, [key]);

  return directory;
}
```

- [ ] **Step 4: Run the tests**

Run: `npm test -- useBranchDirectory`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/branches/useBranchDirectory.ts src/club/branches/useBranchDirectory.test.ts
git commit -m "feat(club): add useBranchDirectory hook for switcher names"
```

---

## Task 6: RenameBranchDialog

**Files:**
- Create: `src/club/branches/RenameBranchDialog.tsx`
- Test: `src/club/branches/RenameBranchDialog.test.tsx`

This mirrors the established dialog + server-confirmed-toast pattern from `CreateOperatorDialog.tsx` (toast on result, no optimistic success, `pending` disables buttons). The parent (Task 7) remounts it per branch via a React `key`, so initial `useState` from props is correct.

- [ ] **Step 1: Write the failing test**

Create `src/club/branches/RenameBranchDialog.test.tsx`:

```tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { RenameBranchDialog } from './RenameBranchDialog';

function setup(client: { updateBranchProfile: ReturnType<typeof vi.fn> }, onDone = vi.fn(), onOpenChange = vi.fn()) {
  render(
    <I18nProvider><ToastProvider>
      <RenameBranchDialog open branchId="b1" organizationId="org" initialName="Центр" initialCity="Москва"
        client={client as never} onOpenChange={onOpenChange} onDone={onDone} />
    </ToastProvider></I18nProvider>
  );
  return { client, onDone, onOpenChange };
}

it('saves the trimmed name and city, then closes', async () => {
  const client = { updateBranchProfile: vi.fn().mockResolvedValue({}) };
  const { onDone, onOpenChange } = setup(client);
  fireEvent.change(screen.getByLabelText('Название филиала'), { target: { value: ' Новый центр ' } });
  fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));
  await waitFor(() => expect(client.updateBranchProfile).toHaveBeenCalledWith('b1', { organizationId: 'org', name: 'Новый центр', city: 'Москва' }));
  await waitFor(() => expect(onDone).toHaveBeenCalled());
  await waitFor(() => expect(onOpenChange).toHaveBeenCalledWith(false));
});

it('does not call onDone when the save fails', async () => {
  const client = { updateBranchProfile: vi.fn().mockRejectedValue(new Error('boom')) };
  const { onDone } = setup(client);
  fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));
  await waitFor(() => expect(client.updateBranchProfile).toHaveBeenCalled());
  expect(onDone).not.toHaveBeenCalled();
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `npm test -- RenameBranchDialog`
Expected: FAIL ("Failed to resolve import './RenameBranchDialog'").

- [ ] **Step 3: Write the implementation**

Create `src/club/branches/RenameBranchDialog.tsx`:

```tsx
import { useState } from 'react';
import { Dialog, DialogContent, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';

type Actions = Pick<ClubApiClient, 'updateBranchProfile'>;

export function RenameBranchDialog({ open, branchId, organizationId, initialName, initialCity, client, onOpenChange, onDone }: {
  open: boolean;
  branchId: string;
  organizationId: string;
  initialName: string;
  initialCity: string;
  client: Actions;
  onOpenChange: (open: boolean) => void;
  onDone: () => void;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [name, setName] = useState(initialName);
  const [city, setCity] = useState(initialCity);
  const [pending, setPending] = useState(false);

  const valid = name.trim() !== '' && city.trim() !== '';

  async function submit() {
    setPending(true);
    try {
      await client.updateBranchProfile(branchId, { organizationId, name: name.trim(), city: city.trim() });
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
        <DialogTitle>{t('branches.rename.title')}</DialogTitle>
        <div className="flex flex-col gap-3">
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('settings.branch.name')}</span>
            <Input aria-label={t('settings.branch.name')} value={name} onChange={e => setName(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('settings.branch.city')}</span>
            <Input aria-label={t('settings.branch.city')} value={city} onChange={e => setCity(e.target.value)} />
          </label>
        </div>
        <DialogFooter>
          <Button variant="outline" disabled={pending} onClick={() => onOpenChange(false)}>{t('common.cancel')}</Button>
          <Button disabled={pending || !valid} onClick={() => void submit()}>{t('common.save')}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
```

- [ ] **Step 4: Run the tests**

Run: `npm test -- RenameBranchDialog`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/branches/RenameBranchDialog.tsx src/club/branches/RenameBranchDialog.test.tsx
git commit -m "feat(club): add RenameBranchDialog over updateBranchProfile"
```

---

## Task 7: BranchesScreen («Все филиалы»)

**Files:**
- Create: `src/club/branches/BranchesScreen.tsx`
- Test: `src/club/branches/BranchesScreen.test.tsx`

The screen: a totals strip (KPI tiles), a grid of per-branch cards (each with KPIs, an "Открыть" button calling `onOpenBranch`, and a "Переименовать" button opening the rename dialog), a failed-branch card variant, an authoritative empty state, and a disabled "Добавить филиал" affordance with the labelled-unavailable helper text. The rename dialog is rendered only when a target is set and is keyed by branch id so it resets per branch.

- [ ] **Step 1: Write the failing test**

Create `src/club/branches/BranchesScreen.test.tsx`:

```tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { BranchesScreen } from './BranchesScreen';
import type { OperatorDashboardSummary } from '@/api/types';

function summary(id: string, online: number): OperatorDashboardSummary {
  return {
    organizationId: 'org', branchId: id, fromUtc: '', toUtc: '', generatedAtUtc: '',
    utilization: { totalSeats: 10, activeSessions: 2, endingSessions: 0, onlineDevices: online, offlineDevices: 1, sessionStarts: 0, utilizationPercent: 20 },
    alertPressure: { pendingCommands: 0, failedCommands: 0, offlineDevices: 1, endingSessions: 0, totalAlerts: 3 },
    revenue: { posNetSales: { amount: 0, currencyCode: 'RUB' }, gameplayRevenue: { amount: 0, currencyCode: 'RUB' }, totalRevenue: { amount: 1000, currencyCode: 'RUB' }, posCheckCount: 0, newPlayerCount: 0 }
  };
}

function fakeClient() {
  return {
    getBranchProfile: vi.fn(async (id: string) => ({ organizationId: 'org', branchId: id, name: id === 'a' ? 'Центр' : 'Юг', city: 'Москва', createdAtUtc: '' })),
    getDashboardSummary: vi.fn(async (id: string) => summary(id, id === 'a' ? 5 : 3)),
    updateBranchProfile: vi.fn()
  };
}

function setup(client = fakeClient(), onOpenBranch = vi.fn()) {
  render(
    <I18nProvider><ToastProvider>
      <BranchesScreen client={client as never} branchIds={['a', 'b']} organizationId="org" onOpenBranch={onOpenBranch} />
    </ToastProvider></I18nProvider>
  );
  return { client, onOpenBranch };
}

it('renders a card per branch with its real name', async () => {
  setup();
  expect(await screen.findByText('Центр')).toBeInTheDocument();
  expect(screen.getByText('Юг')).toBeInTheDocument();
});

it('opens a branch via its Открыть button', async () => {
  const { onOpenBranch } = setup();
  const openButtons = await screen.findAllByRole('button', { name: 'Открыть' });
  fireEvent.click(openButtons[0]);
  expect(onOpenBranch).toHaveBeenCalledWith('a');
});

it('renames a branch through the rename dialog', async () => {
  const client = fakeClient();
  client.updateBranchProfile = vi.fn().mockResolvedValue({});
  setup(client);
  const renameButtons = await screen.findAllByRole('button', { name: 'Переименовать' });
  fireEvent.click(renameButtons[0]);
  fireEvent.change(await screen.findByLabelText('Название филиала'), { target: { value: 'Новый центр' } });
  fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));
  await waitFor(() => expect(client.updateBranchProfile).toHaveBeenCalledWith('a', { organizationId: 'org', name: 'Новый центр', city: 'Москва' }));
});

it('shows the add-branch affordance as unavailable', async () => {
  setup();
  expect(await screen.findByText('Создание филиалов выполняется при подключении — обратитесь в поддержку.')).toBeInTheDocument();
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `npm test -- BranchesScreen`
Expected: FAIL ("Failed to resolve import './BranchesScreen'").

- [ ] **Step 3: Write the implementation**

Create `src/club/branches/BranchesScreen.tsx`:

```tsx
import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { LoadingCards, EmptyState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import { useBranchRollup } from './useBranchRollup';
import { RenameBranchDialog } from './RenameBranchDialog';

type Client = Pick<ClubApiClient, 'getDashboardSummary' | 'getBranchProfile' | 'updateBranchProfile'>;

interface RenameTarget { branchId: string; name: string; city: string; }

export function BranchesScreen({ client, branchIds, organizationId, onOpenBranch }: {
  client: Client;
  branchIds: readonly string[];
  organizationId: string;
  onOpenBranch: (branchId: string) => void;
}) {
  const { t, formatNumber, formatCurrency } = useI18n();
  const state = useBranchRollup(client, branchIds, t('branches.unnamed'));
  const [renameTarget, setRenameTarget] = useState<RenameTarget | null>(null);

  if (state.status === 'loading') return <LoadingCards />;

  const { rows, totals } = state.data;

  return (
    <div className="flex flex-col gap-5">
      <div className="grid grid-cols-2 gap-4 md:grid-cols-5">
        <Kpi label={t('branches.totals.branches')} value={formatNumber(totals.branches)} />
        <Kpi label={t('overview.kpi.devicesOnline')} value={`${formatNumber(totals.devicesOnline.online)} / ${formatNumber(totals.devicesOnline.total)}`} />
        <Kpi label={t('overview.kpi.activeSessions')} value={formatNumber(totals.activeSessions)} />
        <Kpi label={t('overview.kpi.revenueToday')} value={formatCurrency(totals.revenue.amount, totals.revenue.currencyCode)} />
        <Kpi label={t('overview.kpi.attention')} value={formatNumber(totals.attention)} />
      </div>

      {rows.length === 0 ? (
        <EmptyState message={t('branches.empty')} />
      ) : (
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-3">
          {rows.map(row => (
            <Card key={row.branchId}>
              <CardHeader>
                <CardTitle>{row.name}</CardTitle>
                <div className="text-xs text-muted-foreground">{row.city}</div>
              </CardHeader>
              <CardContent className="flex flex-col gap-3">
                {row.kpis === null ? (
                  <p className="text-sm text-muted-foreground">{t('branches.card.error')}</p>
                ) : (
                  <dl className="grid grid-cols-2 gap-2 text-sm">
                    <Stat label={t('overview.kpi.devicesOnline')} value={`${formatNumber(row.kpis.devicesOnline.online)} / ${formatNumber(row.kpis.devicesOnline.total)}`} />
                    <Stat label={t('overview.kpi.activeSessions')} value={formatNumber(row.kpis.activeSessions)} />
                    <Stat label={t('overview.kpi.revenueToday')} value={formatCurrency(row.kpis.revenueToday.amount, row.kpis.revenueToday.currencyCode)} />
                    <Stat label={t('overview.kpi.attention')} value={formatNumber(row.kpis.attention)} />
                  </dl>
                )}
                <div className="flex gap-2">
                  <Button onClick={() => onOpenBranch(row.branchId)}>{t('branches.open')}</Button>
                  <Button variant="outline" onClick={() => setRenameTarget({ branchId: row.branchId, name: row.name, city: row.city })}>
                    {t('branches.rename')}
                  </Button>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      <div className="flex flex-col items-start gap-2 border-t border-border pt-4">
        <Button disabled>{t('branches.add')}</Button>
        <p className="text-xs text-muted-foreground">{t('branches.add.unavailable')}</p>
      </div>

      {renameTarget !== null && (
        <RenameBranchDialog
          key={renameTarget.branchId}
          open
          branchId={renameTarget.branchId}
          organizationId={organizationId}
          initialName={renameTarget.name}
          initialCity={renameTarget.city}
          client={client}
          onOpenChange={(o) => { if (!o) setRenameTarget(null); }}
          onDone={() => state.retry()}
        />
      )}
    </div>
  );
}

function Kpi({ label, value }: { label: string; value: string }) {
  return (
    <Card><CardContent className="py-4">
      <div className="text-xs font-medium text-muted-foreground">{label}</div>
      <div className="mt-2 text-2xl font-bold tabular-nums">{value}</div>
    </CardContent></Card>
  );
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-muted-foreground">{label}</dt>
      <dd className="font-medium tabular-nums">{value}</dd>
    </div>
  );
}
```

- [ ] **Step 4: Run the tests**

Run: `npm test -- BranchesScreen`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/branches/BranchesScreen.tsx src/club/branches/BranchesScreen.test.tsx
git commit -m "feat(club): add Все филиалы rollup screen"
```

---

## Task 8: Wire active branch + real names + BranchesScreen into ClubArea

**Files:**
- Modify: `src/App.tsx` (the `ClubArea` component, lines ~339-394)
- Test: `src/App.branches.test.tsx`

- [ ] **Step 1: Write the failing route test**

Create `src/App.branches.test.tsx`:

```tsx
import { it, expect } from 'vitest';
import { resolvePlatformRoute, pathForRoute } from './App';

it('resolves /club/branches to the clubBranches route', () => {
  const { route } = resolvePlatformRoute('/club/branches', null, '', 'club');
  expect(route).toEqual({ kind: 'clubBranches' });
});

it('maps the clubBranches route back to /club/branches', () => {
  expect(pathForRoute({ kind: 'clubBranches' })).toBe('/club/branches');
});
```

(`clubBranches` resolution and `pathForRoute` already exist, so these two pass immediately — they lock the contract the wiring depends on. The behavioral change in this task is the `ClubArea` rewrite below; it is covered by the hook/screen unit tests from Tasks 2 and 7, since `ClubArea` is not exported.)

- [ ] **Step 2: Run it**

Run: `npm test -- App.branches`
Expected: PASS (2 tests).

- [ ] **Step 3: Add the imports**

In `src/App.tsx`, add these imports alongside the other club imports (near lines 11-16, using RELATIVE paths to match the file's convention):

```tsx
import { useActiveBranch } from './club/branches/useActiveBranch';
import { useBranchDirectory } from './club/branches/useBranchDirectory';
import { BranchesScreen } from './club/branches/BranchesScreen';
```

- [ ] **Step 4: Rewrite the ClubArea body**

In `src/App.tsx`, replace the head of `ClubArea` (the three lines currently reading):

```tsx
  const role = roleFromPermissions(session.permissions);
  const { t } = useI18n();
  const branchId = session.branchIds[0] ?? '';
  const branches = session.branchIds.map(id => ({ branchId: id, name: 'Филиал' }));
  const overviewState = useOverview(clubClient, branchId);
```

with:

```tsx
  const role = roleFromPermissions(session.permissions);
  const { t } = useI18n();
  const { activeBranchId, select } = useActiveBranch(session.branchIds);
  const directory = useBranchDirectory(clubClient, session.branchIds);
  const branches = session.branchIds.map(id => ({ branchId: id, name: directory[id]?.name ?? t('branches.unnamed') }));
  const overviewState = useOverview(clubClient, activeBranchId);
```

- [ ] **Step 5: Wire the switcher and thread the active branch**

Still in `ClubArea`, update the `AppShell` props and the screen children. Change `activeBranchId={branchId}` → `activeBranchId={activeBranchId}`, and `onSelectBranch={() => { /* single-branch pilot: no-op */ }}` → `onSelectBranch={select}`.

Then update the screen body. Replace the existing children block:

```tsx
      {route.kind === 'clubDashboard' ? (
        <OverviewScreen state={overviewState} />
      ) : route.kind === 'clubVenue' ? (
        <VenueScreen client={clubClient} branchId={branchId} />
      ) : route.kind === 'clubSettings' ? (
        role === 'owner' ? (
          <SettingsScreen
            client={clubClient}
            branchId={branchId}
            organizationId={session.organizationId}
            currentStaffUserId={session.staffUserId}
          />
        ) : (
          <EmptyState message={t('settings.ownerOnly')} />
        )
      ) : (
        <LegacyClubScreen
          client={clubClient}
          route={route}
          session={session}
          onNavigate={onNavigate}
        />
      )}
```

with (note the new `clubBranches` branch inserted before the legacy fallback, and `branchId` → `activeBranchId` for Venue/Settings):

```tsx
      {route.kind === 'clubDashboard' ? (
        <OverviewScreen state={overviewState} />
      ) : route.kind === 'clubVenue' ? (
        <VenueScreen client={clubClient} branchId={activeBranchId} />
      ) : route.kind === 'clubSettings' ? (
        role === 'owner' ? (
          <SettingsScreen
            client={clubClient}
            branchId={activeBranchId}
            organizationId={session.organizationId}
            currentStaffUserId={session.staffUserId}
          />
        ) : (
          <EmptyState message={t('settings.ownerOnly')} />
        )
      ) : route.kind === 'clubBranches' ? (
        <BranchesScreen
          client={clubClient}
          branchIds={session.branchIds}
          organizationId={session.organizationId}
          onOpenBranch={(id) => { select(id); onNavigate({ kind: 'clubDashboard' }, '/club'); }}
        />
      ) : (
        <LegacyClubScreen
          client={clubClient}
          route={route}
          session={session}
          onNavigate={onNavigate}
        />
      )}
```

- [ ] **Step 6: Run the full suite and the build**

Run: `npm test`
Expected: PASS (all files, including the existing `App.test.tsx`). If `App.test.tsx` asserted the old hardcoded "Филиал" switcher label or the legacy `clubBranches` body, update those assertions to match the new real-name switcher / new `BranchesScreen` (the directory resolves names asynchronously; assert via `findBy*`). Do not weaken unrelated assertions.

Run: `npm run build`
Expected: clean (`tsc -b && vite build` with no type errors). The `LegacyClubScreen` import stays — it still serves `clubInstall` and the legacy deep routes.

- [ ] **Step 7: Commit**

```bash
git add src/App.tsx src/App.branches.test.tsx
git commit -m "feat(club): real branch switching + route Все филиалы to BranchesScreen"
```

---

## Task 9: Enable the branches nav item

**Files:**
- Modify: `src/club/nav.ts:32`
- Test: `src/club/nav.test.ts`

- [ ] **Step 1: Add the failing test**

In `src/club/nav.test.ts`, add:

```ts
it('exposes the branches nav item as available (not soon)', () => {
  const item = clubNav.flatMap(g => g.items).find(i => i.key === 'branches');
  expect(item?.soon).toBe(false);
});
```

(If `clubNav` is not already imported in this test file, add `import { clubNav } from './nav';` — check the file's existing imports first and reuse them.)

- [ ] **Step 2: Run it to verify it fails**

Run: `npm test -- nav`
Expected: FAIL (`branches` item still has `soon: true`).

- [ ] **Step 3: Flip the flag**

In `src/club/nav.ts`, change line 32 from:

```ts
      { key: 'branches', labelKey: 'nav.branches', path: '/club/branches', ownerOnly: false, soon: true },
```

to:

```ts
      { key: 'branches', labelKey: 'nav.branches', path: '/club/branches', ownerOnly: false, soon: false },
```

- [ ] **Step 4: Run the tests**

Run: `npm test -- nav`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/club/nav.ts src/club/nav.test.ts
git commit -m "feat(club): enable the Все филиалы nav item"
```

---

## Task 10: Full suite + build gate

**Files:** none (verification only)

- [ ] **Step 1: Run the whole test suite**

Run: `npm test`
Expected: all test files pass (the prior baseline was 43 files / 133 tests; this plan adds ~6 files and ~18 tests).

- [ ] **Step 2: Run the build**

Run: `npm run build`
Expected: `tsc -b && vite build` complete with no errors.

- [ ] **Step 3: Commit (only if anything was left uncommitted)**

```bash
git add -A
git commit -m "chore(club): branch switching + rollup green on suite and build"
```

If there is nothing to commit, skip this step.

---

## Self-Review

**Spec coverage** (against `docs/superpowers/specs/2026-05-29-platform-web-club-console-design.md`, the «Все филиалы» + Multi-branch + polish-backlog items):
- "real branch switching" replacing the `onSelectBranch` no-op → Tasks 2, 8. ✓
- "Все филиали… aggregated overview dashboard (per-branch KPI/revenue side by side), aggregated client-side from existing branch-scoped endpoints — no new aggregation endpoints" → Tasks 3, 4, 7 (per-branch `getDashboardSummary`). ✓
- "Clicking a branch switches the branch context and navigates into it" → Task 8 `onOpenBranch`. ✓
- "branch CRUD list (open/add/deactivate)" → open ✓ (Task 7/8); **rename** ✓ (Task 6/7, via `updateBranchProfile`); **add/deactivate** have no backend contract → labelled-unavailable placeholder per the spec's gap rule (Task 7), documented in Scope. ✓ (honest gap, not fabricated)
- Data-region states (loading/empty/error) → `LoadingCards`/`EmptyState` + per-branch error card. ✓
- i18n RU+EN parity → Task 1 (parity test enforced). ✓
- Polish-backlog "replace the single-branch branch-switcher placeholder" → done. Polish-backlog "delete the now-orphaned ClubDashboard" → **deferred with documented reason** (LegacyClubScreen still renders the live InstallScreen for `clubInstall`; no redesigned Установка yet). Not a silent omission.

**Placeholder scan:** No "TBD/TODO/handle edge cases" left; every code step contains complete code; the one product-level "unavailable" affordance is an intentional, spec-sanctioned placeholder for a missing backend capability, with localized copy.

**Type consistency:** `BranchRollupEntry`/`BranchRollupRow`/`BranchKpis`/`BranchRollupTotals`/`BranchRollupViewModel` defined in Task 3, consumed unchanged in Tasks 4 and 7. `useActiveBranch` returns `{ activeBranchId, select }` (Task 2) consumed verbatim in Task 8. `useBranchRollup(client, branchIds, unnamedLabel)` and `useBranchDirectory(client, branchIds)` signatures match their call sites. `BranchesScreen` props `{ client, branchIds, organizationId, onOpenBranch }` match the Task 8 render. `RenameBranchDialog` props match the Task 7 render. `updateBranchProfile(branchId, { organizationId, name, city })` matches the existing `clubApi`/contract. Reused i18n keys (`overview.kpi.*`, `settings.branch.name/city`, `common.save/cancel`, `toast.saved/failed`) all already exist.

---

## Execution Handoff

Two execution options:

1. **Subagent-Driven (recommended)** — fresh subagent per task, two-stage review between tasks, fast iteration.
2. **Inline Execution** — execute tasks in this session with checkpoints.
