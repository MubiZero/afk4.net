# Club Монетизация — Тарифы (Plan 5a) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up the club **Монетизация** screen with its first working tab — **Тарифы** (list active tariffs, create a tariff with its first pricing version, edit a tariff's name/active state and pricing) — on existing backend endpoints, plus the shared `money.ts` minor↔major helper, the `MonetizationScreen` tab shell (Товары/Лояльность as labelled "soon" placeholders), and the `clubMonetization` route/nav wiring.

**Architecture:** A pure `money.ts` helper converts backend minor units ↔ display major units. A pure `tariffsModel.ts` maps the `TariffOptionDto` read model to display rows and builds the create/update request bodies (major→minor). A `useTariffs` hook loads the options list into a discriminated-union state with `retry` (mutations are done directly by the dialog, matching the existing `CreateOperatorDialog` pattern). `TariffFormDialog` performs create (POST tariff → POST first version) and edit (PATCH tariff + PATCH version), server-confirmed with toasts. `TariffsTab` renders the table + create/edit triggers, gated by a `canManage` flag. `MonetizationScreen` is a `Tabs` shell. `App.tsx`/`nav.ts` wire the owner-gated `clubMonetization` route.

**Tech Stack:** React 19 + TypeScript, Vite, Vitest 4 + jsdom + @testing-library/react (`globals: false` → import `it`/`expect`/`vi` from `'vitest'` per test file), shadcn/ui primitives under `src/components/ui/`, Tailwind v4, i18n RU primary / EN secondary. npm cwd: `D:\afk4.net\src\AFK4.Platform.Web`. Path alias `@/` → `src/`. `App.tsx` uses RELATIVE imports.

---

## Backend contracts (verified 2026-05-30 — do NOT modify)

Branch-scoped routes; wire format is **camelCase**; money in **minor units** (long → JS `number`). `organizationId` is required in every create/update **body** (validated against the caller's org); `branchId` comes from the route only.

| Method | Route | Permission | Body | Returns |
|---|---|---|---|---|
| GET | `/api/branches/{branchId}/tariffs/options` | `tariffs.view` | — | `TariffOptionDto[]` (active versions) |
| POST | `/api/branches/{branchId}/tariffs` | `tariffs.manage` | `CreateTariffRequest` | `TariffDto` |
| POST | `/api/branches/{branchId}/tariffs/{tariffId}/versions` | `tariffs.manage` | `CreateTariffVersionRequest` | `TariffVersionDto` |
| PATCH | `/api/branches/{branchId}/tariffs/{tariffId}` | `tariffs.manage` | `UpdateTariffRequest` | `TariffDto` |
| PATCH | `/api/branches/{branchId}/tariffs/{tariffId}/versions/{tariffVersionId}` | `tariffs.manage` | `UpdateTariffVersionRequest` | `TariffVersionDto` |

`TariffOptionDto` (camelCase): `tariffId, tariffVersionId, name, tariffRuleVersionId, versionNumber, currencyCode, pricePerMinuteMinorUnits, minimumBillableMinutes, roundingIncrementMinutes, effectiveFromUtc`.

**Deferred this plan (per spec — optional):** the `POST /tariffs/calculate` price calculator. Not built in 5a.

---

## File Structure

- `src/club/money.ts` — pure `minorToMajor`/`majorToMinor`. (Task 2; shared by 5b/5c later.)
- `src/api/types.ts` — add the tariff TS interfaces. (Task 3)
- `src/api/clubApi.ts` — add 5 tariff wrapper methods. (Task 3)
- `src/club/monetization/tariffs/tariffsModel.ts` — pure mapping + request builders. (Task 4)
- `src/club/monetization/tariffs/useTariffs.ts` — load options → rows + retry. (Task 5)
- `src/club/monetization/tariffs/TariffFormDialog.tsx` — create/edit dialog. (Task 6)
- `src/club/monetization/tariffs/TariffsTab.tsx` — list + triggers + read-only gating. (Task 7)
- `src/club/monetization/MonetizationScreen.tsx` — Tabs shell. (Task 8)
- `src/App.tsx`, `src/club/nav.ts` — route + nav wiring. (Task 9)
- `src/i18n/messages.ts` — `monetization.*` + `tariffs.*` keys. (Task 1)

Colocated `*.test.ts(x)` for money, model, hook, dialog, tab, screen, and the clubApi wrappers.

---

## Task 1: i18n keys

**Files:**
- Modify: `src/i18n/messages.ts` (both `ru` and `en`)
- Test: `src/i18n/messages.test.ts`

- [ ] **Step 1: Add the failing test**

In `src/i18n/messages.test.ts`, append:

```ts
it('includes the monetization + tariffs keys', () => {
  for (const key of [
    'monetization.tab.tariffs', 'monetization.tab.products', 'monetization.tab.loyalty',
    'monetization.soon', 'monetization.ownerOnly',
    'tariffs.create', 'tariffs.create.title', 'tariffs.create.submit',
    'tariffs.edit.title', 'tariffs.edit.submit', 'tariffs.empty', 'tariffs.activeOnlyNote',
    'tariffs.col.name', 'tariffs.col.price', 'tariffs.col.minMinutes', 'tariffs.col.rounding', 'tariffs.col.effectiveFrom',
    'tariffs.field.name', 'tariffs.field.pricePerMinute', 'tariffs.field.minMinutes', 'tariffs.field.rounding', 'tariffs.field.currency', 'tariffs.field.active'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});
```

- [ ] **Step 2: Run it to confirm it fails**

Run: `npm test -- messages`
Expected: FAIL (keys missing).

- [ ] **Step 3: Add the keys**

In `src/i18n/messages.ts`, add to the `ru` object (place near the other `nav.*`/club keys — exact location doesn't matter as long as it's inside the `ru` object literal):

```ts
    'monetization.tab.tariffs': 'Тарифы',
    'monetization.tab.products': 'Товары',
    'monetization.tab.loyalty': 'Лояльность',
    'monetization.soon': 'Скоро',
    'monetization.ownerOnly': 'Раздел доступен только владельцу.',
    'tariffs.create': 'Создать тариф',
    'tariffs.create.title': 'Новый тариф',
    'tariffs.create.submit': 'Создать',
    'tariffs.edit.title': 'Редактировать тариф',
    'tariffs.edit.submit': 'Сохранить',
    'tariffs.empty': 'Тарифы ещё не созданы.',
    'tariffs.activeOnlyNote': 'Показаны только активные тарифы. Деактивированные не отображаются (на бэкенде нет отдельного списка).',
    'tariffs.col.name': 'Название',
    'tariffs.col.price': 'Цена за минуту',
    'tariffs.col.minMinutes': 'Мин. минут',
    'tariffs.col.rounding': 'Округление, мин',
    'tariffs.col.effectiveFrom': 'Действует с',
    'tariffs.field.name': 'Название',
    'tariffs.field.pricePerMinute': 'Цена за минуту',
    'tariffs.field.minMinutes': 'Минимум минут',
    'tariffs.field.rounding': 'Округление (минут)',
    'tariffs.field.currency': 'Валюта',
    'tariffs.field.active': 'Активен',
```

And the matching `en` keys (same key names) in the `en` object:

```ts
    'monetization.tab.tariffs': 'Tariffs',
    'monetization.tab.products': 'Products',
    'monetization.tab.loyalty': 'Loyalty',
    'monetization.soon': 'Coming soon',
    'monetization.ownerOnly': 'This section is available to the owner only.',
    'tariffs.create': 'Create tariff',
    'tariffs.create.title': 'New tariff',
    'tariffs.create.submit': 'Create',
    'tariffs.edit.title': 'Edit tariff',
    'tariffs.edit.submit': 'Save',
    'tariffs.empty': 'No tariffs yet.',
    'tariffs.activeOnlyNote': 'Showing active tariffs only. Deactivated ones are not listed (the backend has no list-all endpoint).',
    'tariffs.col.name': 'Name',
    'tariffs.col.price': 'Price per minute',
    'tariffs.col.minMinutes': 'Min. minutes',
    'tariffs.col.rounding': 'Rounding, min',
    'tariffs.col.effectiveFrom': 'Effective from',
    'tariffs.field.name': 'Name',
    'tariffs.field.pricePerMinute': 'Price per minute',
    'tariffs.field.minMinutes': 'Minimum minutes',
    'tariffs.field.rounding': 'Rounding (minutes)',
    'tariffs.field.currency': 'Currency',
    'tariffs.field.active': 'Active',
```

- [ ] **Step 4: Run the tests**

Run: `npm test -- messages`
Expected: PASS (new test + the existing ru/en parity test).

- [ ] **Step 5: Commit**

```bash
git add src/i18n/messages.ts src/i18n/messages.test.ts
git commit -m "feat(club): add i18n keys for monetization + tariffs"
```

---

## Task 2: money.ts helper

**Files:**
- Create: `src/club/money.ts`
- Test: `src/club/money.test.ts`

- [ ] **Step 1: Write the failing test**

Create `src/club/money.test.ts`:

```ts
import { it, expect } from 'vitest';
import { minorToMajor, majorToMinor } from './money';

it('converts minor units to major units', () => {
  expect(minorToMajor(12345)).toBe(123.45);
  expect(minorToMajor(250)).toBe(2.5);
  expect(minorToMajor(0)).toBe(0);
});

it('converts major units to minor units, rounding to the nearest minor unit', () => {
  expect(majorToMinor(99.99)).toBe(9999);
  expect(majorToMinor(2.5)).toBe(250);
  expect(majorToMinor(0)).toBe(0);
  expect(majorToMinor(1.005)).toBe(101); // guards float drift: 1.005*100 = 100.4999.. → rounds to 101 via the epsilon-safe impl below
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `npm test -- money`
Expected: FAIL (import not resolved).

- [ ] **Step 3: Write the implementation**

Create `src/club/money.ts`:

```ts
/** Backend stores money as integer minor units (e.g. kopecks). These helpers
 * convert to/from the major units shown in and entered through the UI. */
export function minorToMajor(minorUnits: number): number {
  return minorUnits / 100;
}

export function majorToMinor(major: number): number {
  // Round on a value nudged by a tiny epsilon so that values like 1.005 — which
  // are stored as 1.00499999… in IEEE-754 — round up as a human expects.
  return Math.round((major + Number.EPSILON) * 100);
}
```

- [ ] **Step 4: Run the tests**

Run: `npm test -- money`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/money.ts src/club/money.test.ts
git commit -m "feat(club): add money minor/major conversion helper"
```

---

## Task 3: Tariff types + clubApi wrappers

**Files:**
- Modify: `src/api/types.ts` (append the tariff interfaces)
- Modify: `src/api/clubApi.ts` (add 5 methods + extend the type import)
- Test: `src/api/clubApi.tariffs.test.ts`

- [ ] **Step 1: Write the failing test**

Create `src/api/clubApi.tariffs.test.ts`:

```ts
import { it, expect, vi } from 'vitest';
import { ClubApiClient } from './clubApi';

function jsonResponse(body: unknown): Response {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } });
}

function makeClient(fetchImpl: typeof fetch): ClubApiClient {
  return new ClubApiClient({ baseUrl: 'https://api.test', fetchImpl, session: null, onSessionChanged: () => {} });
}

it('getTariffOptions GETs the branch options route', async () => {
  const fetchImpl = vi.fn(async () => jsonResponse([])) as unknown as typeof fetch;
  await makeClient(fetchImpl).getTariffOptions('b1');
  expect(fetchImpl).toHaveBeenCalledWith('https://api.test/api/branches/b1/tariffs/options', expect.objectContaining({ method: 'GET' }));
});

it('createTariff POSTs the body to the tariffs route', async () => {
  const fetchImpl = vi.fn(async () => jsonResponse({ tariffId: 't1' })) as unknown as typeof fetch;
  await makeClient(fetchImpl).createTariff('b1', { organizationId: 'org', name: 'Day', idempotencyKey: 'k1' });
  const call = (fetchImpl as unknown as { mock: { calls: [string, RequestInit][] } }).mock.calls[0];
  expect(call[0]).toBe('https://api.test/api/branches/b1/tariffs');
  expect(call[1].method).toBe('POST');
  expect(JSON.parse(call[1].body as string)).toEqual({ organizationId: 'org', name: 'Day', idempotencyKey: 'k1' });
});

it('createTariffVersion POSTs to the versions route', async () => {
  const fetchImpl = vi.fn(async () => jsonResponse({ tariffVersionId: 'v1' })) as unknown as typeof fetch;
  await makeClient(fetchImpl).createTariffVersion('b1', 't1', {
    organizationId: 'org', tariffId: 't1', currencyCode: 'RUB', pricePerMinuteMinorUnits: 250,
    minimumBillableMinutes: 1, roundingIncrementMinutes: 1, effectiveFromUtc: '2026-01-01T00:00:00.000Z', idempotencyKey: 'k2'
  });
  const call = (fetchImpl as unknown as { mock: { calls: [string, RequestInit][] } }).mock.calls[0];
  expect(call[0]).toBe('https://api.test/api/branches/b1/tariffs/t1/versions');
  expect(call[1].method).toBe('POST');
});

it('updateTariffVersion PATCHes the version route', async () => {
  const fetchImpl = vi.fn(async () => jsonResponse({ tariffVersionId: 'v1' })) as unknown as typeof fetch;
  await makeClient(fetchImpl).updateTariffVersion('b1', 't1', 'v1', {
    organizationId: 'org', currencyCode: 'RUB', pricePerMinuteMinorUnits: 300,
    minimumBillableMinutes: 1, roundingIncrementMinutes: 1, effectiveFromUtc: '2026-01-01T00:00:00.000Z', isActive: true
  });
  const call = (fetchImpl as unknown as { mock: { calls: [string, RequestInit][] } }).mock.calls[0];
  expect(call[0]).toBe('https://api.test/api/branches/b1/tariffs/t1/versions/v1');
  expect(call[1].method).toBe('PATCH');
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `npm test -- clubApi.tariffs`
Expected: FAIL (methods not defined).

- [ ] **Step 3a: Add the types**

Append to `src/api/types.ts`:

```ts
export interface TariffOption {
  tariffId: string;
  tariffVersionId: string;
  name: string;
  tariffRuleVersionId: string;
  versionNumber: number;
  currencyCode: string;
  pricePerMinuteMinorUnits: number;
  minimumBillableMinutes: number;
  roundingIncrementMinutes: number;
  effectiveFromUtc: string;
}

export interface Tariff {
  tariffId: string;
  organizationId: string;
  branchId: string;
  name: string;
  isActive: boolean;
  createdAtUtc: string;
}

export interface TariffVersion {
  tariffVersionId: string;
  tariffId: string;
  versionNumber: number;
  currencyCode: string;
  pricePerMinuteMinorUnits: number;
  minimumBillableMinutes: number;
  roundingIncrementMinutes: number;
  effectiveFromUtc: string;
  retiredAtUtc: string | null;
  createdAtUtc: string;
}

export interface CreateTariffRequest {
  organizationId: string;
  name: string;
  idempotencyKey: string;
}

export interface UpdateTariffRequest {
  organizationId: string;
  name: string;
  isActive: boolean;
}

export interface CreateTariffVersionRequest {
  organizationId: string;
  tariffId: string;
  currencyCode: string;
  pricePerMinuteMinorUnits: number;
  minimumBillableMinutes: number;
  roundingIncrementMinutes: number;
  effectiveFromUtc: string;
  idempotencyKey: string;
}

export interface UpdateTariffVersionRequest {
  organizationId: string;
  currencyCode: string;
  pricePerMinuteMinorUnits: number;
  minimumBillableMinutes: number;
  roundingIncrementMinutes: number;
  effectiveFromUtc: string;
  isActive: boolean;
}
```

- [ ] **Step 3b: Add the clubApi wrappers**

In `src/api/clubApi.ts`, extend the `import type { ... } from './types';` block to also import:

```ts
  CreateTariffRequest,
  CreateTariffVersionRequest,
  Tariff,
  TariffOption,
  TariffVersion,
  UpdateTariffRequest,
  UpdateTariffVersionRequest,
```

Then add these methods to the `ClubApiClient` class (e.g. right after `resetStaffPassword`, before the private `send`):

```ts
  public getTariffOptions(branchId: string): Promise<TariffOption[]> {
    return this.send<TariffOption[]>('GET', `/api/branches/${encodeURIComponent(branchId)}/tariffs/options`);
  }

  public createTariff(branchId: string, request: CreateTariffRequest): Promise<Tariff> {
    return this.send<Tariff>('POST', `/api/branches/${encodeURIComponent(branchId)}/tariffs`, request);
  }

  public createTariffVersion(branchId: string, tariffId: string, request: CreateTariffVersionRequest): Promise<TariffVersion> {
    return this.send<TariffVersion>(
      'POST',
      `/api/branches/${encodeURIComponent(branchId)}/tariffs/${encodeURIComponent(tariffId)}/versions`,
      request
    );
  }

  public updateTariff(branchId: string, tariffId: string, request: UpdateTariffRequest): Promise<Tariff> {
    return this.send<Tariff>(
      'PATCH',
      `/api/branches/${encodeURIComponent(branchId)}/tariffs/${encodeURIComponent(tariffId)}`,
      request
    );
  }

  public updateTariffVersion(branchId: string, tariffId: string, tariffVersionId: string, request: UpdateTariffVersionRequest): Promise<TariffVersion> {
    return this.send<TariffVersion>(
      'PATCH',
      `/api/branches/${encodeURIComponent(branchId)}/tariffs/${encodeURIComponent(tariffId)}/versions/${encodeURIComponent(tariffVersionId)}`,
      request
    );
  }
```

- [ ] **Step 4: Run the tests**

Run: `npm test -- clubApi.tariffs`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add src/api/types.ts src/api/clubApi.ts src/api/clubApi.tariffs.test.ts
git commit -m "feat(club): add tariff types and clubApi wrappers"
```

---

## Task 4: tariffsModel (pure)

**Files:**
- Create: `src/club/monetization/tariffs/tariffsModel.ts`
- Test: `src/club/monetization/tariffs/tariffsModel.test.ts`

- [ ] **Step 1: Write the failing test**

Create `src/club/monetization/tariffs/tariffsModel.test.ts`:

```ts
import { it, expect } from 'vitest';
import type { TariffOption } from '@/api/types';
import {
  toTariffRows, buildCreateTariffRequest, buildCreateVersionRequest,
  buildUpdateTariffRequest, buildUpdateVersionRequest, type TariffFormValues
} from './tariffsModel';

const option: TariffOption = {
  tariffId: 't1', tariffVersionId: 'v1', name: 'Дневной', tariffRuleVersionId: 'rv1', versionNumber: 1,
  currencyCode: 'RUB', pricePerMinuteMinorUnits: 250, minimumBillableMinutes: 5, roundingIncrementMinutes: 1,
  effectiveFromUtc: '2026-01-01T00:00:00.000Z'
};

const form: TariffFormValues = {
  name: '  Дневной  ', currencyCode: 'RUB', pricePerMinute: 3, minimumBillableMinutes: 5, roundingIncrementMinutes: 1
};

it('maps options to rows with price in major units', () => {
  const rows = toTariffRows([option]);
  expect(rows).toHaveLength(1);
  expect(rows[0]).toMatchObject({ tariffId: 't1', tariffVersionId: 'v1', name: 'Дневной', pricePerMinute: 2.5, minimumBillableMinutes: 5 });
});

it('builds a create-tariff request, trimming the name', () => {
  expect(buildCreateTariffRequest('org', '  Дневной ', 'idem')).toEqual({ organizationId: 'org', name: 'Дневной', idempotencyKey: 'idem' });
});

it('builds a create-version request converting price to minor units', () => {
  expect(buildCreateVersionRequest('org', 't1', form, '2026-02-01T00:00:00.000Z', 'idem2')).toEqual({
    organizationId: 'org', tariffId: 't1', currencyCode: 'RUB', pricePerMinuteMinorUnits: 300,
    minimumBillableMinutes: 5, roundingIncrementMinutes: 1, effectiveFromUtc: '2026-02-01T00:00:00.000Z', idempotencyKey: 'idem2'
  });
});

it('builds an update-tariff request', () => {
  expect(buildUpdateTariffRequest('org', ' Ночной ', false)).toEqual({ organizationId: 'org', name: 'Ночной', isActive: false });
});

it('builds an update-version request converting price to minor units', () => {
  expect(buildUpdateVersionRequest('org', form, '2026-02-01T00:00:00.000Z', true)).toEqual({
    organizationId: 'org', currencyCode: 'RUB', pricePerMinuteMinorUnits: 300,
    minimumBillableMinutes: 5, roundingIncrementMinutes: 1, effectiveFromUtc: '2026-02-01T00:00:00.000Z', isActive: true
  });
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `npm test -- tariffsModel`
Expected: FAIL (import not resolved).

- [ ] **Step 3: Write the implementation**

Create `src/club/monetization/tariffs/tariffsModel.ts`:

```ts
import type {
  CreateTariffRequest, CreateTariffVersionRequest, TariffOption,
  UpdateTariffRequest, UpdateTariffVersionRequest
} from '@/api/types';
import { majorToMinor, minorToMajor } from '../../money';

export interface TariffRow {
  tariffId: string;
  tariffVersionId: string;
  name: string;
  currencyCode: string;
  pricePerMinute: number; // major units, for display
  minimumBillableMinutes: number;
  roundingIncrementMinutes: number;
  effectiveFromUtc: string;
  versionNumber: number;
}

export interface TariffFormValues {
  name: string;
  currencyCode: string;
  pricePerMinute: number; // major units, as entered
  minimumBillableMinutes: number;
  roundingIncrementMinutes: number;
}

export function toTariffRows(options: TariffOption[]): TariffRow[] {
  return options.map(o => ({
    tariffId: o.tariffId,
    tariffVersionId: o.tariffVersionId,
    name: o.name,
    currencyCode: o.currencyCode,
    pricePerMinute: minorToMajor(o.pricePerMinuteMinorUnits),
    minimumBillableMinutes: o.minimumBillableMinutes,
    roundingIncrementMinutes: o.roundingIncrementMinutes,
    effectiveFromUtc: o.effectiveFromUtc,
    versionNumber: o.versionNumber
  }));
}

export function buildCreateTariffRequest(organizationId: string, name: string, idempotencyKey: string): CreateTariffRequest {
  return { organizationId, name: name.trim(), idempotencyKey };
}

export function buildCreateVersionRequest(
  organizationId: string, tariffId: string, form: TariffFormValues, effectiveFromUtc: string, idempotencyKey: string
): CreateTariffVersionRequest {
  return {
    organizationId,
    tariffId,
    currencyCode: form.currencyCode,
    pricePerMinuteMinorUnits: majorToMinor(form.pricePerMinute),
    minimumBillableMinutes: form.minimumBillableMinutes,
    roundingIncrementMinutes: form.roundingIncrementMinutes,
    effectiveFromUtc,
    idempotencyKey
  };
}

export function buildUpdateTariffRequest(organizationId: string, name: string, isActive: boolean): UpdateTariffRequest {
  return { organizationId, name: name.trim(), isActive };
}

export function buildUpdateVersionRequest(
  organizationId: string, form: TariffFormValues, effectiveFromUtc: string, isActive: boolean
): UpdateTariffVersionRequest {
  return {
    organizationId,
    currencyCode: form.currencyCode,
    pricePerMinuteMinorUnits: majorToMinor(form.pricePerMinute),
    minimumBillableMinutes: form.minimumBillableMinutes,
    roundingIncrementMinutes: form.roundingIncrementMinutes,
    effectiveFromUtc,
    isActive
  };
}
```

- [ ] **Step 4: Run the tests**

Run: `npm test -- tariffsModel`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/monetization/tariffs/tariffsModel.ts src/club/monetization/tariffs/tariffsModel.test.ts
git commit -m "feat(club): add tariffs model (rows + request builders)"
```

---

## Task 5: useTariffs hook

**Files:**
- Create: `src/club/monetization/tariffs/useTariffs.ts`
- Test: `src/club/monetization/tariffs/useTariffs.test.ts`

- [ ] **Step 1: Write the failing test**

Create `src/club/monetization/tariffs/useTariffs.test.ts`:

```ts
import { it, expect, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import type { TariffOption } from '@/api/types';
import { useTariffs } from './useTariffs';

const option: TariffOption = {
  tariffId: 't1', tariffVersionId: 'v1', name: 'Дневной', tariffRuleVersionId: 'rv1', versionNumber: 1,
  currencyCode: 'RUB', pricePerMinuteMinorUnits: 250, minimumBillableMinutes: 1, roundingIncrementMinutes: 1,
  effectiveFromUtc: '2026-01-01T00:00:00.000Z'
};

it('loads tariff options into rows', async () => {
  const client = { getTariffOptions: vi.fn(async () => [option]) };
  const { result } = renderHook(() => useTariffs(client as never, 'b1'));
  await waitFor(() => expect(result.current.status).toBe('ready'));
  if (result.current.status !== 'ready') throw new Error('not ready');
  expect(result.current.rows.map(r => r.name)).toEqual(['Дневной']);
  expect(result.current.rows[0].pricePerMinute).toBe(2.5);
});

it('reports an error when the load fails', async () => {
  const client = { getTariffOptions: vi.fn(async () => { throw new Error('boom'); }) };
  const { result } = renderHook(() => useTariffs(client as never, 'b1'));
  await waitFor(() => expect(result.current.status).toBe('error'));
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `npm test -- useTariffs`
Expected: FAIL (import not resolved).

- [ ] **Step 3: Write the implementation**

Create `src/club/monetization/tariffs/useTariffs.ts`:

```ts
import { useCallback, useEffect, useRef, useState } from 'react';
import type { ClubApiClient } from '@/api/clubApi';
import { toTariffRows, type TariffRow } from './tariffsModel';

type Loadable = Pick<ClubApiClient, 'getTariffOptions'>;

export type TariffsState =
  | { status: 'loading' }
  | { status: 'error'; retry: () => void }
  | { status: 'ready'; rows: TariffRow[]; retry: () => void };

export function useTariffs(client: Loadable, branchId: string): TariffsState {
  const [tick, setTick] = useState(0);
  const [phase, setPhase] = useState<'loading' | 'error' | 'ready'>('loading');
  const [rows, setRows] = useState<TariffRow[]>([]);
  const clientRef = useRef(client);
  clientRef.current = client;

  const retry = useCallback(() => setTick(t => t + 1), []);

  useEffect(() => {
    let cancelled = false;
    setPhase('loading');
    clientRef.current.getTariffOptions(branchId)
      .then(options => { if (!cancelled) { setRows(toTariffRows(options)); setPhase('ready'); } })
      .catch(() => { if (!cancelled) setPhase('error'); });
    return () => { cancelled = true; };
  }, [branchId, tick]);

  if (phase === 'loading') return { status: 'loading' };
  if (phase === 'error') return { status: 'error', retry };
  return { status: 'ready', rows, retry };
}
```

- [ ] **Step 4: Run the tests**

Run: `npm test -- useTariffs`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/monetization/tariffs/useTariffs.ts src/club/monetization/tariffs/useTariffs.test.ts
git commit -m "feat(club): add useTariffs hook (load options into rows)"
```

---

## Task 6: TariffFormDialog (create + edit)

**Files:**
- Create: `src/club/monetization/tariffs/TariffFormDialog.tsx`
- Test: `src/club/monetization/tariffs/TariffFormDialog.test.tsx`

The dialog handles both modes. **Create** calls `createTariff` then `createTariffVersion` (server-confirmed two-step). **Edit** calls `updateTariff` and `updateTariffVersion`. The effective-from timestamp defaults to "now" (`new Date().toISOString()`), computed at submit — there is no date field in v1. `idempotencyKey` is `crypto.randomUUID()` (available in the Node/jsdom test env).

- [ ] **Step 1: Write the failing test**

Create `src/club/monetization/tariffs/TariffFormDialog.test.tsx`:

```tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import type { TariffRow } from './tariffsModel';
import { TariffFormDialog } from './TariffFormDialog';

function client(overrides: Record<string, unknown> = {}) {
  return {
    createTariff: vi.fn(async () => ({ tariffId: 't1' })),
    createTariffVersion: vi.fn(async () => ({ tariffVersionId: 'v1' })),
    updateTariff: vi.fn(async () => ({})),
    updateTariffVersion: vi.fn(async () => ({})),
    ...overrides
  };
}

function renderDialog(props: Record<string, unknown>) {
  render(
    <I18nProvider><ToastProvider>
      <TariffFormDialog open branchId="b1" organizationId="org" onOpenChange={() => {}} onDone={() => {}} {...props} />
    </ToastProvider></I18nProvider>
  );
}

it('creates a tariff then its first version', async () => {
  const c = client();
  renderDialog({ mode: 'create', client: c });
  fireEvent.change(screen.getByLabelText('Название'), { target: { value: 'Дневной' } });
  fireEvent.change(screen.getByLabelText('Цена за минуту'), { target: { value: '2.5' } });
  fireEvent.click(screen.getByRole('button', { name: 'Создать' }));
  await waitFor(() => expect(c.createTariff).toHaveBeenCalledWith('b1', expect.objectContaining({ organizationId: 'org', name: 'Дневной' })));
  await waitFor(() => expect(c.createTariffVersion).toHaveBeenCalledWith('b1', 't1', expect.objectContaining({ pricePerMinuteMinorUnits: 250, organizationId: 'org', tariffId: 't1' })));
});

it('updates the tariff and its version in edit mode', async () => {
  const c = client();
  const initial: TariffRow = {
    tariffId: 't1', tariffVersionId: 'v1', name: 'Дневной', currencyCode: 'RUB',
    pricePerMinute: 2.5, minimumBillableMinutes: 1, roundingIncrementMinutes: 1,
    effectiveFromUtc: '2026-01-01T00:00:00.000Z', versionNumber: 1
  };
  renderDialog({ mode: 'edit', client: c, initial });
  fireEvent.change(screen.getByLabelText('Цена за минуту'), { target: { value: '3' } });
  fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));
  await waitFor(() => expect(c.updateTariff).toHaveBeenCalledWith('b1', 't1', expect.objectContaining({ name: 'Дневной', isActive: true })));
  await waitFor(() => expect(c.updateTariffVersion).toHaveBeenCalledWith('b1', 't1', 'v1', expect.objectContaining({ pricePerMinuteMinorUnits: 300 })));
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `npm test -- TariffFormDialog`
Expected: FAIL (import not resolved).

- [ ] **Step 3: Write the implementation**

Create `src/club/monetization/tariffs/TariffFormDialog.tsx`:

```tsx
import { useState } from 'react';
import { Dialog, DialogContent, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Switch } from '@/components/ui/switch';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import {
  buildCreateTariffRequest, buildCreateVersionRequest, buildUpdateTariffRequest, buildUpdateVersionRequest,
  type TariffFormValues, type TariffRow
} from './tariffsModel';

type Actions = Pick<ClubApiClient, 'createTariff' | 'createTariffVersion' | 'updateTariff' | 'updateTariffVersion'>;

export function TariffFormDialog({ open, mode, branchId, organizationId, client, initial, onOpenChange, onDone }: {
  open: boolean;
  mode: 'create' | 'edit';
  branchId: string;
  organizationId: string;
  client: Actions;
  initial?: TariffRow;
  onOpenChange: (open: boolean) => void;
  onDone: () => void;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [name, setName] = useState(initial?.name ?? '');
  const [currency, setCurrency] = useState(initial?.currencyCode ?? 'RUB');
  const [pricePerMinute, setPricePerMinute] = useState(String(initial?.pricePerMinute ?? '1'));
  const [minMinutes, setMinMinutes] = useState(String(initial?.minimumBillableMinutes ?? '1'));
  const [rounding, setRounding] = useState(String(initial?.roundingIncrementMinutes ?? '1'));
  const [active, setActive] = useState(true);
  const [pending, setPending] = useState(false);

  const valid = name.trim() !== '' && currency.trim() !== '' && Number(pricePerMinute) >= 0 && Number(minMinutes) >= 0 && Number(rounding) >= 0;

  function formValues(): TariffFormValues {
    return {
      name,
      currencyCode: currency.trim(),
      pricePerMinute: Number(pricePerMinute),
      minimumBillableMinutes: Number(minMinutes),
      roundingIncrementMinutes: Number(rounding)
    };
  }

  async function submit() {
    setPending(true);
    const effectiveFromUtc = new Date().toISOString();
    try {
      if (mode === 'create') {
        const tariff = await client.createTariff(branchId, buildCreateTariffRequest(organizationId, name, crypto.randomUUID()));
        await client.createTariffVersion(
          branchId, tariff.tariffId,
          buildCreateVersionRequest(organizationId, tariff.tariffId, formValues(), effectiveFromUtc, crypto.randomUUID())
        );
      } else if (initial !== undefined) {
        await client.updateTariff(branchId, initial.tariffId, buildUpdateTariffRequest(organizationId, name, active));
        await client.updateTariffVersion(
          branchId, initial.tariffId, initial.tariffVersionId,
          buildUpdateVersionRequest(organizationId, formValues(), effectiveFromUtc, active)
        );
      }
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
        <DialogTitle>{mode === 'create' ? t('tariffs.create.title') : t('tariffs.edit.title')}</DialogTitle>
        <div className="flex flex-col gap-3">
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('tariffs.field.name')}</span>
            <Input aria-label={t('tariffs.field.name')} value={name} onChange={e => setName(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('tariffs.field.pricePerMinute')}</span>
            <Input aria-label={t('tariffs.field.pricePerMinute')} value={pricePerMinute} onChange={e => setPricePerMinute(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('tariffs.field.currency')}</span>
            <Input aria-label={t('tariffs.field.currency')} value={currency} onChange={e => setCurrency(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('tariffs.field.minMinutes')}</span>
            <Input aria-label={t('tariffs.field.minMinutes')} value={minMinutes} onChange={e => setMinMinutes(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('tariffs.field.rounding')}</span>
            <Input aria-label={t('tariffs.field.rounding')} value={rounding} onChange={e => setRounding(e.target.value)} />
          </label>
          {mode === 'edit' && (
            <label className="flex items-center gap-2 text-sm">
              <Switch checked={active} aria-label={t('tariffs.field.active')} onCheckedChange={setActive} />
              {t('tariffs.field.active')}
            </label>
          )}
        </div>
        <DialogFooter>
          <Button variant="outline" disabled={pending} onClick={() => onOpenChange(false)}>{t('common.cancel')}</Button>
          <Button disabled={pending || !valid} onClick={() => void submit()}>
            {mode === 'create' ? t('tariffs.create.submit') : t('tariffs.edit.submit')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
```

Note on primitives: this reuses `Switch` (added in the Settings plan) and `onCheckedChange` returning a `boolean` — confirm `src/components/ui/switch.tsx` exposes `checked` + `onCheckedChange(checked: boolean)`. If the Switch's `onCheckedChange` signature differs, adapt the handler (do not change the test). If `Switch` does not exist, STOP and report BLOCKED.

- [ ] **Step 4: Run the tests**

Run: `npm test -- TariffFormDialog`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/monetization/tariffs/TariffFormDialog.tsx src/club/monetization/tariffs/TariffFormDialog.test.tsx
git commit -m "feat(club): add TariffFormDialog (create + edit tariff/version)"
```

---

## Task 7: TariffsTab

**Files:**
- Create: `src/club/monetization/tariffs/TariffsTab.tsx`
- Test: `src/club/monetization/tariffs/TariffsTab.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `src/club/monetization/tariffs/TariffsTab.test.tsx`:

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import type { TariffOption } from '@/api/types';
import { TariffsTab } from './TariffsTab';

const option: TariffOption = {
  tariffId: 't1', tariffVersionId: 'v1', name: 'Дневной', tariffRuleVersionId: 'rv1', versionNumber: 1,
  currencyCode: 'RUB', pricePerMinuteMinorUnits: 250, minimumBillableMinutes: 1, roundingIncrementMinutes: 1,
  effectiveFromUtc: '2026-01-01T00:00:00.000Z'
};

function fakeClient() {
  return {
    getTariffOptions: vi.fn(async () => [option]),
    createTariff: vi.fn(async () => ({ tariffId: 't1' })),
    createTariffVersion: vi.fn(async () => ({ tariffVersionId: 'v1' })),
    updateTariff: vi.fn(async () => ({})),
    updateTariffVersion: vi.fn(async () => ({}))
  };
}

function renderTab(canManage: boolean) {
  const client = fakeClient();
  render(
    <I18nProvider><ToastProvider>
      <TariffsTab client={client as never} branchId="b1" organizationId="org" canManage={canManage} />
    </ToastProvider></I18nProvider>
  );
  return { client };
}

it('renders tariff rows', async () => {
  renderTab(true);
  expect(await screen.findByText('Дневной')).toBeInTheDocument();
});

it('opens the create dialog when managing', async () => {
  renderTab(true);
  await screen.findByText('Дневной');
  fireEvent.click(screen.getByRole('button', { name: 'Создать тариф' }));
  expect(await screen.findByRole('button', { name: 'Создать' })).toBeInTheDocument();
});

it('hides the create trigger when read-only', async () => {
  renderTab(false);
  await screen.findByText('Дневной');
  expect(screen.queryByRole('button', { name: 'Создать тариф' })).not.toBeInTheDocument();
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `npm test -- TariffsTab`
Expected: FAIL (import not resolved).

- [ ] **Step 3: Write the implementation**

Create `src/club/monetization/tariffs/TariffsTab.tsx`:

```tsx
import { useState } from 'react';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { Button } from '@/components/ui/button';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import { useTariffs } from './useTariffs';
import { TariffFormDialog } from './TariffFormDialog';
import type { TariffRow } from './tariffsModel';

type Client = Pick<ClubApiClient,
  'getTariffOptions' | 'createTariff' | 'createTariffVersion' | 'updateTariff' | 'updateTariffVersion'>;

export function TariffsTab({ client, branchId, organizationId, canManage }: {
  client: Client;
  branchId: string;
  organizationId: string;
  canManage: boolean;
}) {
  const { t, formatNumber, formatCurrency } = useI18n();
  const state = useTariffs(client, branchId);
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<TariffRow | null>(null);

  if (state.status === 'loading') return <LoadingCards count={2} />;
  if (state.status === 'error') return <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />;

  const { rows, retry } = state;

  return (
    <div className="flex flex-col gap-4">
      {canManage && (
        <div className="flex justify-end">
          <Button onClick={() => setCreating(true)}>{t('tariffs.create')}</Button>
        </div>
      )}

      {rows.length === 0 ? (
        <EmptyState message={t('tariffs.empty')} />
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('tariffs.col.name')}</TableHead>
              <TableHead>{t('tariffs.col.price')}</TableHead>
              <TableHead>{t('tariffs.col.minMinutes')}</TableHead>
              <TableHead>{t('tariffs.col.rounding')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {rows.map(row => (
              <TableRow key={row.tariffId} data-clickable={canManage ? 'true' : undefined}
                onClick={canManage ? () => setEditing(row) : undefined}>
                <TableCell className="font-medium">{row.name}</TableCell>
                <TableCell className="tabular-nums">{formatCurrency(row.pricePerMinute, row.currencyCode)}</TableCell>
                <TableCell className="tabular-nums">{formatNumber(row.minimumBillableMinutes)}</TableCell>
                <TableCell className="tabular-nums">{formatNumber(row.roundingIncrementMinutes)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      <p className="text-xs text-muted-foreground">{t('tariffs.activeOnlyNote')}</p>

      {creating && (
        <TariffFormDialog
          open mode="create" branchId={branchId} organizationId={organizationId} client={client}
          onOpenChange={o => { if (!o) setCreating(false); }}
          onDone={() => retry()}
        />
      )}
      {editing !== null && (
        <TariffFormDialog
          key={editing.tariffVersionId}
          open mode="edit" branchId={branchId} organizationId={organizationId} client={client} initial={editing}
          onOpenChange={o => { if (!o) setEditing(null); }}
          onDone={() => retry()}
        />
      )}
    </div>
  );
}
```

- [ ] **Step 4: Run the tests**

Run: `npm test -- TariffsTab`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/monetization/tariffs/TariffsTab.tsx src/club/monetization/tariffs/TariffsTab.test.tsx
git commit -m "feat(club): add TariffsTab (list + create/edit triggers, read-only gating)"
```

---

## Task 8: MonetizationScreen shell

**Files:**
- Create: `src/club/monetization/MonetizationScreen.tsx`
- Test: `src/club/monetization/MonetizationScreen.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `src/club/monetization/MonetizationScreen.test.tsx`:

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import type { TariffOption } from '@/api/types';
import { MonetizationScreen } from './MonetizationScreen';

const option: TariffOption = {
  tariffId: 't1', tariffVersionId: 'v1', name: 'Дневной', tariffRuleVersionId: 'rv1', versionNumber: 1,
  currencyCode: 'RUB', pricePerMinuteMinorUnits: 250, minimumBillableMinutes: 1, roundingIncrementMinutes: 1,
  effectiveFromUtc: '2026-01-01T00:00:00.000Z'
};

function setup() {
  const client = { getTariffOptions: vi.fn(async () => [option]) };
  render(
    <I18nProvider><ToastProvider>
      <MonetizationScreen client={client as never} branchId="b1" organizationId="org" canManageTariffs />
    </ToastProvider></I18nProvider>
  );
}

it('shows tariffs in the first tab', async () => {
  setup();
  expect(await screen.findByText('Дневной')).toBeInTheDocument();
});

it('shows a placeholder on the products tab', async () => {
  setup();
  await screen.findByText('Дневной');
  const tab = screen.getByRole('tab', { name: 'Товары' });
  fireEvent.mouseDown(tab);
  fireEvent.click(tab);
  expect(await screen.findByText('Скоро')).toBeInTheDocument();
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `npm test -- MonetizationScreen`
Expected: FAIL (import not resolved).

- [ ] **Step 3: Write the implementation**

Create `src/club/monetization/MonetizationScreen.tsx`:

```tsx
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import { TariffsTab } from './tariffs/TariffsTab';

export function MonetizationScreen({ client, branchId, organizationId, canManageTariffs }: {
  client: ClubApiClient;
  branchId: string;
  organizationId: string;
  canManageTariffs: boolean;
}) {
  const { t } = useI18n();
  return (
    <Tabs defaultValue="tariffs">
      <TabsList>
        <TabsTrigger value="tariffs">{t('monetization.tab.tariffs')}</TabsTrigger>
        <TabsTrigger value="products">{t('monetization.tab.products')}</TabsTrigger>
        <TabsTrigger value="loyalty">{t('monetization.tab.loyalty')}</TabsTrigger>
      </TabsList>
      <TabsContent value="tariffs">
        <TariffsTab client={client} branchId={branchId} organizationId={organizationId} canManage={canManageTariffs} />
      </TabsContent>
      <TabsContent value="products">
        <p className="text-sm text-muted-foreground">{t('monetization.soon')}</p>
      </TabsContent>
      <TabsContent value="loyalty">
        <p className="text-sm text-muted-foreground">{t('monetization.soon')}</p>
      </TabsContent>
    </Tabs>
  );
}
```

- [ ] **Step 4: Run the tests**

Run: `npm test -- MonetizationScreen`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/monetization/MonetizationScreen.tsx src/club/monetization/MonetizationScreen.test.tsx
git commit -m "feat(club): add MonetizationScreen tab shell with Тарифы tab"
```

---

## Task 9: Route + nav wiring; full suite + build gate

**Files:**
- Modify: `src/App.tsx` (route union, resolution, title map, `pathForRoute`, `isClubRoute`, `ClubArea` render, import)
- Modify: `src/club/nav.ts` (flip `monetization` `soon` to `false`)

- [ ] **Step 1: Add the route to the union and the import**

In `src/App.tsx`, add the import near the other club screen imports (line ~13-14, RELATIVE path):

```tsx
import { MonetizationScreen } from './club/monetization/MonetizationScreen';
```

In the `ClubRoute` union (around line 40-50), add a member:

```tsx
  | { kind: 'clubMonetization' }
```

- [ ] **Step 2: Wire resolution, title, path, and the route guard**

In `resolvePlatformRoute` (the `allowsClubRoutes` block, around line 484), add after the `/club/settings` case:

```tsx
    if (path === '/club/monetization') {
      return { route: { kind: 'clubMonetization' } };
    }
```

In `CLUB_SCREEN_TITLE` (around line 301), add:

```tsx
  clubMonetization: 'Монетизация',
```

In `pathForRoute` (around line 319), add a case:

```tsx
    case 'clubMonetization':
      return '/club/monetization';
```

In `isClubRoute` (around line 615), add a clause:

```tsx
    || route.kind === 'clubMonetization'
```

- [ ] **Step 3: Render the screen (owner-gated, mirroring clubSettings)**

In `ClubArea`'s render (around line 375), insert a branch right after the `clubVenue` branch and before `clubSettings`:

```tsx
      ) : route.kind === 'clubMonetization' ? (
        role === 'owner' ? (
          <MonetizationScreen
            client={clubClient}
            branchId={activeBranchId}
            organizationId={session.organizationId}
            canManageTariffs={session.permissions.includes('tariffs.manage')}
          />
        ) : (
          <EmptyState message={t('monetization.ownerOnly')} />
        )
```

(`EmptyState` is already imported and used by the `clubSettings` branch.)

- [ ] **Step 4: Enable the nav item**

In `src/club/nav.ts`, change the `monetization` item's `soon: true` to `soon: false`:

```ts
      { key: 'monetization', labelKey: 'nav.monetization', path: '/club/monetization', ownerOnly: true, soon: false },
```

- [ ] **Step 5: Run the full suite**

Run: `npm test`
Expected: all pass. If a routing test (e.g. `src/App.test.tsx` or similar) enumerates club nav paths and previously asserted `/club/monetization` resolves to `notFound`, update that expectation to `{ kind: 'clubMonetization' }`. If no such test exists, nothing to change.

- [ ] **Step 6: Run the build**

Run: `npm run build`
Expected: clean `tsc -b && vite build` (no type errors).

- [ ] **Step 7: Commit**

```bash
git add src/App.tsx src/club/nav.ts
git commit -m "feat(club): wire owner-gated clubMonetization route + enable nav"
```

---

## Self-Review

**Spec coverage** (against `2026-05-30-platform-web-club-monetization-design.md`):
- Branch-scoped Монетизация screen, Tabs shell → Task 8; route/nav wiring + owner gating → Task 9. ✓
- Money in minor units, shared helper + tests → Task 2; used in model (Task 4) and display (Task 7). ✓
- Тарифы: list active (`getTariffOptions`) → Tasks 3/5/7; create (tariff → first version) and edit (tariff + version) → Task 6; active-only note → Tasks 1/7. ✓
- No new backend contracts; new clubApi wrappers + camelCase types → Task 3. ✓
- Idempotency key per create → Task 6 (`crypto.randomUUID()`). ✓
- Role gating via `tariffs.manage` (`canManage`/`canManageTariffs`) → Tasks 7/8/9; read-only hides create/edit → Task 7. ✓
- Data-region states (loading/error/empty) → Tasks 5/7. ✓
- Товары/Лояльность as labelled placeholders this plan → Task 8 (`monetization.soon`); full tabs land in 5b/5c. ✓

**Deliberate deviations from the spec (acceptable, documented):**
- **Deactivation UX:** the spec suggested `ConfirmDialog` for deactivation. Tariffs have no delete and edit already PATCHes name+active together, so deactivation here is the `Active` Switch inside the edit dialog (still server-confirmed, no optimistic success). No separate ConfirmDialog. Catalog/packages plans (5b/5c) can use ConfirmDialog where a discrete deactivate affordance fits better.
- **Price calculator deferred:** the spec marked `calculateTariff` as an optional convenience; it's not built in 5a (no wrapper/UI) to keep the plan tight. Can be added later.

**Placeholder scan:** no TBD/"handle edge cases"; every code step is complete with real code and exact commands.

**Type consistency:** `TariffOption`/`Tariff`/`TariffVersion` + the four request types defined in Task 3 are consumed unchanged in Tasks 4/6. `TariffFormValues`/`TariffRow` defined in Task 4 are consumed in Tasks 5/6/7. `useTariffs(client, branchId): TariffsState` (Task 5) consumed in Task 7. `TariffFormDialog` props `{ open, mode, branchId, organizationId, client, initial?, onOpenChange, onDone }` (Task 6) match Task 7's two render sites. `TariffsTab` props `{ client, branchId, organizationId, canManage }` (Task 7) match Task 8. `MonetizationScreen` props `{ client, branchId, organizationId, canManageTariffs }` (Task 8) match Task 9's call site. Wrapper names (`getTariffOptions`/`createTariff`/`createTariffVersion`/`updateTariff`/`updateTariffVersion`) are identical across Tasks 3/6/7. Routes/permission strings match the verified backend table.

---

## Execution Handoff

Two execution options:

1. **Subagent-Driven (recommended)** — fresh subagent per task, two-stage review between tasks.
2. **Inline Execution** — execute tasks in this session with checkpoints.
