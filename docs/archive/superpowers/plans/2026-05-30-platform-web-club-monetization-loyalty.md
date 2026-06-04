# Club Монетизация — Лояльность (Plan 5c) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the "Лояльность" placeholder in the Монетизация screen with a real prepaid-time-packages tab — list active packages, create a package, edit a package (incl. deactivate via `isActive`) — on existing backend endpoints; the last block of the Монетизация screen.

**Architecture:** Reuses `money.ts` (minor↔major, from 5a) and the `MoneyMinor` type (from 5b). A pure `packagesModel.ts` maps the `PackageOptionDto` read model to display rows (price→major, seconds→minutes) and builds the create/update request bodies (price→minor, minutes→seconds). A load-only `usePackages` hook returns the active packages in a discriminated-union state with `retry`. `PackageFormDialog` creates/edits a package, server-confirmed. `PackagesTab` ties them together with read-only gating. Rendered in `MonetizationScreen`'s Лояльность tab, gated by a `canManagePackages` flag.

**Tech Stack:** React 19 + TypeScript, Vite, Vitest 4 + jsdom + @testing-library/react (`globals: false` → import `it`/`expect`/`vi` from `'vitest'` per test file), shadcn/ui primitives under `src/components/ui/`, Tailwind v4, i18n RU primary / EN secondary. npm cwd: `D:\afk4.net\src\AFK4.Platform.Web`. Path alias `@/` → `src/`. `App.tsx` uses RELATIVE imports.

---

## Backend contracts (verified 2026-05-30 — do NOT modify)

Branch-scoped; camelCase wire; `organizationId` required in every create/update **body**; `branchId` from the route. **Read price is flat; write price is nested:** the read model `PackageOptionDto` carries `priceMinorUnits` + `currencyCode`, but the create/update bodies carry a nested `price: { currencyCode, minorUnits }`. Time fields are **seconds** (`includedSeconds`, `bonusSeconds`) plus `expiresAfterDays`.

| Method | Route | Permission | Body | Returns |
|---|---|---|---|---|
| GET | `/api/branches/{branchId}/packages/options` | `packages.view` | — | `PackageOptionDto[]` (active) |
| POST | `/api/branches/{branchId}/packages` | `packages.manage` | `CreatePackageDefinitionRequest` | `PackageDefinitionDto` |
| PATCH | `/api/branches/{branchId}/packages/{packageDefinitionId}` | `packages.manage` | `UpdatePackageDefinitionRequest` | `PackageDefinitionDto` |

`PackageOptionDto` (camelCase): `packageDefinitionId, name, currencyCode, priceMinorUnits, includedSeconds, bonusSeconds, expiresAfterDays`.

**Backend gaps (honest limitations):** options is **active-only** (no list-all / get-by-id), and there is **no DELETE**. → list carries an active-only note; "removal" = deactivate via `isActive`.

---

## File Structure

- `src/api/types.ts` — add `PackageOption`, `PackageDefinition`, `CreatePackageDefinitionRequest`, `UpdatePackageDefinitionRequest` (reuse existing `MoneyMinor`). (Task 2)
- `src/api/clubApi.ts` — add `getPackageOptions`, `createPackageDefinition`, `updatePackageDefinition`. (Task 2)
- `src/club/monetization/packages/packagesModel.ts` — pure mapping + request builders. (Task 3)
- `src/club/monetization/packages/usePackages.ts` — load options → rows + retry. (Task 4)
- `src/club/monetization/packages/PackageFormDialog.tsx` — create/edit a package. (Task 5)
- `src/club/monetization/packages/PackagesTab.tsx` — list + create/edit + read-only gating. (Task 6)
- `src/club/monetization/MonetizationScreen.tsx` + `src/App.tsx` — render the tab, thread `canManagePackages`. (Task 7)
- `src/i18n/messages.ts` — `loyalty.*` keys. (Task 1)

Colocated `*.test.ts(x)` for the model, hook, dialog, tab, wrappers, and the updated screen.

---

## Task 1: i18n keys

**Files:** Modify `src/i18n/messages.ts` (both `ru` and `en`); Test `src/i18n/messages.test.ts`.

- [ ] **Step 1: Add the failing test** — append to `src/i18n/messages.test.ts`:

```ts
it('includes the loyalty (packages) keys', () => {
  for (const key of [
    'loyalty.create', 'loyalty.create.title', 'loyalty.create.submit',
    'loyalty.edit.title', 'loyalty.edit.submit', 'loyalty.empty', 'loyalty.activeOnlyNote',
    'loyalty.col.name', 'loyalty.col.price', 'loyalty.col.included', 'loyalty.col.bonus', 'loyalty.col.expires',
    'loyalty.field.name', 'loyalty.field.price', 'loyalty.field.currency',
    'loyalty.field.included', 'loyalty.field.bonus', 'loyalty.field.expires', 'loyalty.field.active'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});
```

- [ ] **Step 2: Run `npm test -- messages`** → expect FAIL.

- [ ] **Step 3: Add the keys.** In the `ru` object:

```ts
    'loyalty.create': 'Создать пакет',
    'loyalty.create.title': 'Новый пакет',
    'loyalty.create.submit': 'Создать',
    'loyalty.edit.title': 'Редактировать пакет',
    'loyalty.edit.submit': 'Сохранить',
    'loyalty.empty': 'Пакеты ещё не созданы.',
    'loyalty.activeOnlyNote': 'Показаны только активные пакеты. Деактивированные не отображаются (на бэкенде нет отдельного списка).',
    'loyalty.col.name': 'Название',
    'loyalty.col.price': 'Цена',
    'loyalty.col.included': 'Включено, мин',
    'loyalty.col.bonus': 'Бонус, мин',
    'loyalty.col.expires': 'Срок, дн',
    'loyalty.field.name': 'Название',
    'loyalty.field.price': 'Цена',
    'loyalty.field.currency': 'Валюта',
    'loyalty.field.included': 'Включено минут',
    'loyalty.field.bonus': 'Бонусные минуты',
    'loyalty.field.expires': 'Срок действия (дней)',
    'loyalty.field.active': 'Активен',
```

In the `en` object (same keys):

```ts
    'loyalty.create': 'Create package',
    'loyalty.create.title': 'New package',
    'loyalty.create.submit': 'Create',
    'loyalty.edit.title': 'Edit package',
    'loyalty.edit.submit': 'Save',
    'loyalty.empty': 'No packages yet.',
    'loyalty.activeOnlyNote': 'Showing active packages only. Deactivated ones are not listed (the backend has no list-all endpoint).',
    'loyalty.col.name': 'Name',
    'loyalty.col.price': 'Price',
    'loyalty.col.included': 'Included, min',
    'loyalty.col.bonus': 'Bonus, min',
    'loyalty.col.expires': 'Expires, days',
    'loyalty.field.name': 'Name',
    'loyalty.field.price': 'Price',
    'loyalty.field.currency': 'Currency',
    'loyalty.field.included': 'Included minutes',
    'loyalty.field.bonus': 'Bonus minutes',
    'loyalty.field.expires': 'Valid for (days)',
    'loyalty.field.active': 'Active',
```

- [ ] **Step 4: Run `npm test -- messages`** → expect PASS.

- [ ] **Step 5: Commit**

```bash
git add src/i18n/messages.ts src/i18n/messages.test.ts
git commit -m "feat(club): add i18n keys for the loyalty (packages) tab"
```

---

## Task 2: Package types + clubApi wrappers

**Files:** Modify `src/api/types.ts`, `src/api/clubApi.ts`; Test `src/api/clubApi.packages.test.ts`.

- [ ] **Step 1: Write the failing test** — create `src/api/clubApi.packages.test.ts`:

```ts
import { it, expect, vi } from 'vitest';
import { ClubApiClient } from './clubApi';

function jsonResponse(body: unknown): Response {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } });
}
function makeClient(fetchImpl: typeof fetch): ClubApiClient {
  return new ClubApiClient({ baseUrl: 'https://api.test', fetchImpl, session: null, onSessionChanged: () => {} });
}

it('getPackageOptions GETs the branch options route', async () => {
  const fetchImpl = vi.fn(async () => jsonResponse([])) as unknown as typeof fetch;
  await makeClient(fetchImpl).getPackageOptions('b1');
  expect(fetchImpl).toHaveBeenCalledWith('https://api.test/api/branches/b1/packages/options', expect.objectContaining({ method: 'GET' }));
});

it('createPackageDefinition POSTs to the packages route', async () => {
  const fetchImpl = vi.fn(async () => jsonResponse({ packageDefinitionId: 'pk1' })) as unknown as typeof fetch;
  await makeClient(fetchImpl).createPackageDefinition('b1', {
    organizationId: 'org', name: 'Старт', price: { currencyCode: 'RUB', minorUnits: 50000 },
    includedSeconds: 3600, bonusSeconds: 0, expiresAfterDays: 30, idempotencyKey: 'k1'
  });
  const call = (fetchImpl as unknown as { mock: { calls: [string, RequestInit][] } }).mock.calls[0];
  expect(call[0]).toBe('https://api.test/api/branches/b1/packages');
  expect(call[1].method).toBe('POST');
  expect(JSON.parse(call[1].body as string)).toMatchObject({ organizationId: 'org', name: 'Старт', price: { currencyCode: 'RUB', minorUnits: 50000 } });
});

it('updatePackageDefinition PATCHes the package route', async () => {
  const fetchImpl = vi.fn(async () => jsonResponse({ packageDefinitionId: 'pk1' })) as unknown as typeof fetch;
  await makeClient(fetchImpl).updatePackageDefinition('b1', 'pk1', {
    organizationId: 'org', name: 'Старт', price: { currencyCode: 'RUB', minorUnits: 60000 },
    includedSeconds: 3600, bonusSeconds: 0, expiresAfterDays: 30, isActive: true
  });
  const call = (fetchImpl as unknown as { mock: { calls: [string, RequestInit][] } }).mock.calls[0];
  expect(call[0]).toBe('https://api.test/api/branches/b1/packages/pk1');
  expect(call[1].method).toBe('PATCH');
});
```

- [ ] **Step 2: Run `npm test -- clubApi.packages`** → expect FAIL.

- [ ] **Step 3a: Append the types to `src/api/types.ts`** (reuse the existing `MoneyMinor`):

```ts
export interface PackageOption {
  packageDefinitionId: string;
  name: string;
  currencyCode: string;
  priceMinorUnits: number;
  includedSeconds: number;
  bonusSeconds: number;
  expiresAfterDays: number;
}

export interface PackageDefinition {
  packageDefinitionId: string;
  organizationId: string;
  branchId: string;
  name: string;
  price: MoneyMinor;
  includedSeconds: number;
  bonusSeconds: number;
  expiresAfterDays: number;
  isActive: boolean;
  createdAtUtc: string;
}

export interface CreatePackageDefinitionRequest {
  organizationId: string;
  name: string;
  price: MoneyMinor;
  includedSeconds: number;
  bonusSeconds: number;
  expiresAfterDays: number;
  idempotencyKey: string;
}

export interface UpdatePackageDefinitionRequest {
  organizationId: string;
  name: string;
  price: MoneyMinor;
  includedSeconds: number;
  bonusSeconds: number;
  expiresAfterDays: number;
  isActive: boolean;
}
```

- [ ] **Step 3b: Add the wrappers to `src/api/clubApi.ts`.** Extend the `import type { ... } from './types';` block (keep it alphabetized) to add: `CreatePackageDefinitionRequest, PackageDefinition, PackageOption, UpdatePackageDefinitionRequest`. Then add these methods after the catalog wrappers, before the private `send`:

```ts
  public getPackageOptions(branchId: string): Promise<PackageOption[]> {
    return this.send<PackageOption[]>('GET', `/api/branches/${encodeURIComponent(branchId)}/packages/options`);
  }

  public createPackageDefinition(branchId: string, request: CreatePackageDefinitionRequest): Promise<PackageDefinition> {
    return this.send<PackageDefinition>('POST', `/api/branches/${encodeURIComponent(branchId)}/packages`, request);
  }

  public updatePackageDefinition(branchId: string, packageDefinitionId: string, request: UpdatePackageDefinitionRequest): Promise<PackageDefinition> {
    return this.send<PackageDefinition>(
      'PATCH',
      `/api/branches/${encodeURIComponent(branchId)}/packages/${encodeURIComponent(packageDefinitionId)}`,
      request
    );
  }
```

- [ ] **Step 4: Run `npm test -- clubApi.packages`** → expect PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/api/types.ts src/api/clubApi.ts src/api/clubApi.packages.test.ts
git commit -m "feat(club): add package types and clubApi wrappers"
```

---

## Task 3: packagesModel (pure)

**Files:** Create `src/club/monetization/packages/packagesModel.ts`; Test `src/club/monetization/packages/packagesModel.test.ts`.

`includedSeconds`/`bonusSeconds` are shown and entered as **minutes** (÷60 for display, ×60 for the request).

- [ ] **Step 1: Write the failing test** — create `src/club/monetization/packages/packagesModel.test.ts`:

```ts
import { it, expect } from 'vitest';
import type { PackageOption } from '@/api/types';
import {
  toPackageRows, buildCreatePackageRequest, buildUpdatePackageRequest, type PackageFormValues
} from './packagesModel';

const option: PackageOption = {
  packageDefinitionId: 'pk1', name: 'Старт', currencyCode: 'RUB', priceMinorUnits: 50000,
  includedSeconds: 3600, bonusSeconds: 600, expiresAfterDays: 30
};

const form: PackageFormValues = {
  name: '  Старт  ', currencyCode: 'RUB', price: 600, includedMinutes: 60, bonusMinutes: 10, expiresAfterDays: 30
};

it('maps options to rows: price to major units, seconds to minutes', () => {
  expect(toPackageRows([option])[0]).toEqual({
    packageDefinitionId: 'pk1', name: 'Старт', currencyCode: 'RUB',
    price: 500, includedMinutes: 60, bonusMinutes: 10, expiresAfterDays: 30
  });
});

it('builds a create request: price to minor units, minutes to seconds, trims name', () => {
  expect(buildCreatePackageRequest('org', form, 'idem')).toEqual({
    organizationId: 'org', name: 'Старт', price: { currencyCode: 'RUB', minorUnits: 60000 },
    includedSeconds: 3600, bonusSeconds: 600, expiresAfterDays: 30, idempotencyKey: 'idem'
  });
});

it('builds an update request with isActive', () => {
  expect(buildUpdatePackageRequest('org', form, false)).toEqual({
    organizationId: 'org', name: 'Старт', price: { currencyCode: 'RUB', minorUnits: 60000 },
    includedSeconds: 3600, bonusSeconds: 600, expiresAfterDays: 30, isActive: false
  });
});
```

- [ ] **Step 2: Run `npm test -- packagesModel`** → expect FAIL.

- [ ] **Step 3: Write the implementation** — create `src/club/monetization/packages/packagesModel.ts`:

```ts
import type {
  CreatePackageDefinitionRequest, PackageOption, UpdatePackageDefinitionRequest
} from '@/api/types';
import { majorToMinor, minorToMajor } from '../../money';

export interface PackageRow {
  packageDefinitionId: string;
  name: string;
  currencyCode: string;
  price: number; // major units, for display
  includedMinutes: number;
  bonusMinutes: number;
  expiresAfterDays: number;
}

export interface PackageFormValues {
  name: string;
  currencyCode: string;
  price: number; // major units, as entered
  includedMinutes: number;
  bonusMinutes: number;
  expiresAfterDays: number;
}

export function toPackageRows(options: PackageOption[]): PackageRow[] {
  return options.map(o => ({
    packageDefinitionId: o.packageDefinitionId,
    name: o.name,
    currencyCode: o.currencyCode,
    price: minorToMajor(o.priceMinorUnits),
    includedMinutes: Math.round(o.includedSeconds / 60),
    bonusMinutes: Math.round(o.bonusSeconds / 60),
    expiresAfterDays: o.expiresAfterDays
  }));
}

export function buildCreatePackageRequest(organizationId: string, form: PackageFormValues, idempotencyKey: string): CreatePackageDefinitionRequest {
  return {
    organizationId,
    name: form.name.trim(),
    price: { currencyCode: form.currencyCode, minorUnits: majorToMinor(form.price) },
    includedSeconds: Math.round(form.includedMinutes * 60),
    bonusSeconds: Math.round(form.bonusMinutes * 60),
    expiresAfterDays: form.expiresAfterDays,
    idempotencyKey
  };
}

export function buildUpdatePackageRequest(organizationId: string, form: PackageFormValues, isActive: boolean): UpdatePackageDefinitionRequest {
  return {
    organizationId,
    name: form.name.trim(),
    price: { currencyCode: form.currencyCode, minorUnits: majorToMinor(form.price) },
    includedSeconds: Math.round(form.includedMinutes * 60),
    bonusSeconds: Math.round(form.bonusMinutes * 60),
    expiresAfterDays: form.expiresAfterDays,
    isActive
  };
}
```

- [ ] **Step 4: Run `npm test -- packagesModel`** → expect PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/monetization/packages/packagesModel.ts src/club/monetization/packages/packagesModel.test.ts
git commit -m "feat(club): add packages model (rows + request builders)"
```

---

## Task 4: usePackages hook

**Files:** Create `src/club/monetization/packages/usePackages.ts`; Test `src/club/monetization/packages/usePackages.test.ts`.

- [ ] **Step 1: Write the failing test** — create `src/club/monetization/packages/usePackages.test.ts`:

```ts
import { it, expect, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import type { PackageOption } from '@/api/types';
import { usePackages } from './usePackages';

const option: PackageOption = {
  packageDefinitionId: 'pk1', name: 'Старт', currencyCode: 'RUB', priceMinorUnits: 50000,
  includedSeconds: 3600, bonusSeconds: 600, expiresAfterDays: 30
};

it('loads package options into rows', async () => {
  const client = { getPackageOptions: vi.fn(async () => [option]) };
  const { result } = renderHook(() => usePackages(client as never, 'b1'));
  await waitFor(() => expect(result.current.status).toBe('ready'));
  if (result.current.status !== 'ready') throw new Error('not ready');
  expect(result.current.rows.map(r => r.name)).toEqual(['Старт']);
  expect(result.current.rows[0].price).toBe(500);
  expect(result.current.rows[0].includedMinutes).toBe(60);
});

it('reports an error when the load fails', async () => {
  const client = { getPackageOptions: vi.fn(async () => { throw new Error('boom'); }) };
  const { result } = renderHook(() => usePackages(client as never, 'b1'));
  await waitFor(() => expect(result.current.status).toBe('error'));
});
```

- [ ] **Step 2: Run `npm test -- usePackages`** → expect FAIL.

- [ ] **Step 3: Write the implementation** — create `src/club/monetization/packages/usePackages.ts`:

```ts
import { useCallback, useEffect, useRef, useState } from 'react';
import type { ClubApiClient } from '@/api/clubApi';
import { toPackageRows, type PackageRow } from './packagesModel';

type Loadable = Pick<ClubApiClient, 'getPackageOptions'>;

export type PackagesState =
  | { status: 'loading' }
  | { status: 'error'; retry: () => void }
  | { status: 'ready'; rows: PackageRow[]; retry: () => void };

export function usePackages(client: Loadable, branchId: string): PackagesState {
  const [tick, setTick] = useState(0);
  const [phase, setPhase] = useState<'loading' | 'error' | 'ready'>('loading');
  const [rows, setRows] = useState<PackageRow[]>([]);
  const clientRef = useRef(client);
  clientRef.current = client;

  const retry = useCallback(() => setTick(t => t + 1), []);

  useEffect(() => {
    let cancelled = false;
    setPhase('loading');
    clientRef.current.getPackageOptions(branchId)
      .then(options => { if (!cancelled) { setRows(toPackageRows(options)); setPhase('ready'); } })
      .catch(() => { if (!cancelled) setPhase('error'); });
    return () => { cancelled = true; };
  }, [branchId, tick]);

  if (phase === 'loading') return { status: 'loading' };
  if (phase === 'error') return { status: 'error', retry };
  return { status: 'ready', rows, retry };
}
```

- [ ] **Step 4: Run `npm test -- usePackages`** → expect PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/monetization/packages/usePackages.ts src/club/monetization/packages/usePackages.test.ts
git commit -m "feat(club): add usePackages hook (load options into rows)"
```

---

## Task 5: PackageFormDialog (create + edit)

**Files:** Create `src/club/monetization/packages/PackageFormDialog.tsx`; Test `src/club/monetization/packages/PackageFormDialog.test.tsx`.

`crypto.randomUUID()` for the idempotency key; `Switch` for the active toggle (edit mode). Numeric fields are stored as strings and `Number()`-coerced.

- [ ] **Step 1: Write the failing test** — create `src/club/monetization/packages/PackageFormDialog.test.tsx`:

```tsx
import type { ComponentProps } from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import type { PackageRow } from './packagesModel';
import { PackageFormDialog } from './PackageFormDialog';

type DialogProps = ComponentProps<typeof PackageFormDialog>;

function client(overrides: Record<string, unknown> = {}) {
  return {
    createPackageDefinition: vi.fn(async () => ({ packageDefinitionId: 'pk1' })),
    updatePackageDefinition: vi.fn(async () => ({ packageDefinitionId: 'pk1' })),
    ...overrides
  };
}

function renderDialog(props: Record<string, unknown>) {
  const merged = {
    open: true, branchId: 'b1', organizationId: 'org',
    onOpenChange: () => {}, onDone: () => {},
    ...props
  } as unknown as DialogProps;
  render(<I18nProvider><ToastProvider><PackageFormDialog {...merged} /></ToastProvider></I18nProvider>);
}

it('creates a package with minor-unit price and seconds', async () => {
  const c = client();
  renderDialog({ mode: 'create', client: c });
  fireEvent.change(screen.getByLabelText('Название'), { target: { value: 'Старт' } });
  fireEvent.change(screen.getByLabelText('Цена'), { target: { value: '500' } });
  fireEvent.change(screen.getByLabelText('Включено минут'), { target: { value: '60' } });
  fireEvent.click(screen.getByRole('button', { name: 'Создать' }));
  await waitFor(() => expect(c.createPackageDefinition).toHaveBeenCalledWith('b1', expect.objectContaining({
    organizationId: 'org', name: 'Старт', price: { currencyCode: 'RUB', minorUnits: 50000 }, includedSeconds: 3600
  })));
});

it('updates a package in edit mode', async () => {
  const c = client();
  const initial: PackageRow = {
    packageDefinitionId: 'pk1', name: 'Старт', currencyCode: 'RUB', price: 500,
    includedMinutes: 60, bonusMinutes: 0, expiresAfterDays: 30
  };
  renderDialog({ mode: 'edit', client: c, initial });
  fireEvent.change(screen.getByLabelText('Цена'), { target: { value: '600' } });
  fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));
  await waitFor(() => expect(c.updatePackageDefinition).toHaveBeenCalledWith('b1', 'pk1', expect.objectContaining({
    name: 'Старт', price: { currencyCode: 'RUB', minorUnits: 60000 }, includedSeconds: 3600, expiresAfterDays: 30, isActive: true
  })));
});
```

- [ ] **Step 2: Run `npm test -- PackageFormDialog`** → expect FAIL.

- [ ] **Step 3: Write the implementation** — create `src/club/monetization/packages/PackageFormDialog.tsx`:

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
  buildCreatePackageRequest, buildUpdatePackageRequest, type PackageFormValues, type PackageRow
} from './packagesModel';

type Actions = Pick<ClubApiClient, 'createPackageDefinition' | 'updatePackageDefinition'>;

export function PackageFormDialog({ open, mode, branchId, organizationId, client, initial, onOpenChange, onDone }: {
  open: boolean;
  mode: 'create' | 'edit';
  branchId: string;
  organizationId: string;
  client: Actions;
  initial?: PackageRow;
  onOpenChange: (open: boolean) => void;
  onDone: () => void;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [name, setName] = useState(initial?.name ?? '');
  const [currency, setCurrency] = useState(initial?.currencyCode ?? 'RUB');
  const [price, setPrice] = useState(String(initial?.price ?? '0'));
  const [includedMinutes, setIncludedMinutes] = useState(String(initial?.includedMinutes ?? '0'));
  const [bonusMinutes, setBonusMinutes] = useState(String(initial?.bonusMinutes ?? '0'));
  const [expiresAfterDays, setExpiresAfterDays] = useState(String(initial?.expiresAfterDays ?? '0'));
  const [active, setActive] = useState(initial?.isActive ?? true);
  const [pending, setPending] = useState(false);

  const valid = name.trim() !== '' && currency.trim() !== ''
    && Number(price) >= 0 && Number(includedMinutes) >= 0 && Number(bonusMinutes) >= 0 && Number(expiresAfterDays) >= 0;

  function formValues(): PackageFormValues {
    return {
      name,
      currencyCode: currency.trim(),
      price: Number(price),
      includedMinutes: Number(includedMinutes),
      bonusMinutes: Number(bonusMinutes),
      expiresAfterDays: Number(expiresAfterDays)
    };
  }

  async function submit() {
    setPending(true);
    try {
      if (mode === 'create') {
        await client.createPackageDefinition(branchId, buildCreatePackageRequest(organizationId, formValues(), crypto.randomUUID()));
      } else if (initial !== undefined) {
        await client.updatePackageDefinition(branchId, initial.packageDefinitionId, buildUpdatePackageRequest(organizationId, formValues(), active));
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
        <DialogTitle>{mode === 'create' ? t('loyalty.create.title') : t('loyalty.edit.title')}</DialogTitle>
        <div className="flex flex-col gap-3">
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('loyalty.field.name')}</span>
            <Input aria-label={t('loyalty.field.name')} value={name} onChange={e => setName(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('loyalty.field.price')}</span>
            <Input aria-label={t('loyalty.field.price')} value={price} onChange={e => setPrice(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('loyalty.field.currency')}</span>
            <Input aria-label={t('loyalty.field.currency')} value={currency} onChange={e => setCurrency(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('loyalty.field.included')}</span>
            <Input aria-label={t('loyalty.field.included')} value={includedMinutes} onChange={e => setIncludedMinutes(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('loyalty.field.bonus')}</span>
            <Input aria-label={t('loyalty.field.bonus')} value={bonusMinutes} onChange={e => setBonusMinutes(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('loyalty.field.expires')}</span>
            <Input aria-label={t('loyalty.field.expires')} value={expiresAfterDays} onChange={e => setExpiresAfterDays(e.target.value)} />
          </label>
          {mode === 'edit' && (
            <label className="flex items-center gap-2 text-sm">
              <Switch checked={active} aria-label={t('loyalty.field.active')} onCheckedChange={setActive} />
              {t('loyalty.field.active')}
            </label>
          )}
        </div>
        <DialogFooter>
          <Button variant="outline" disabled={pending} onClick={() => onOpenChange(false)}>{t('common.cancel')}</Button>
          <Button disabled={pending || !valid} onClick={() => void submit()}>
            {mode === 'create' ? t('loyalty.create.submit') : t('loyalty.edit.submit')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
```

- [ ] **Step 4: Run `npm test -- PackageFormDialog`** → expect PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/monetization/packages/PackageFormDialog.tsx src/club/monetization/packages/PackageFormDialog.test.tsx
git commit -m "feat(club): add PackageFormDialog (create + edit package)"
```

---

## Task 6: PackagesTab

**Files:** Create `src/club/monetization/packages/PackagesTab.tsx`; Test `src/club/monetization/packages/PackagesTab.test.tsx`.

- [ ] **Step 1: Write the failing test** — create `src/club/monetization/packages/PackagesTab.test.tsx`:

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import type { PackageOption } from '@/api/types';
import { PackagesTab } from './PackagesTab';

const option: PackageOption = {
  packageDefinitionId: 'pk1', name: 'Старт', currencyCode: 'RUB', priceMinorUnits: 50000,
  includedSeconds: 3600, bonusSeconds: 600, expiresAfterDays: 30
};

function fakeClient() {
  return {
    getPackageOptions: vi.fn(async () => [option]),
    createPackageDefinition: vi.fn(async () => ({ packageDefinitionId: 'pk2' })),
    updatePackageDefinition: vi.fn(async () => ({ packageDefinitionId: 'pk1' }))
  };
}

function renderTab(canManage: boolean) {
  render(
    <I18nProvider><ToastProvider>
      <PackagesTab client={fakeClient() as never} branchId="b1" organizationId="org" canManage={canManage} />
    </ToastProvider></I18nProvider>
  );
}

it('renders package rows', async () => {
  renderTab(true);
  expect(await screen.findByText('Старт')).toBeInTheDocument();
});

it('opens the create dialog when managing', async () => {
  renderTab(true);
  await screen.findByText('Старт');
  fireEvent.click(screen.getByRole('button', { name: 'Создать пакет' }));
  expect(await screen.findByRole('button', { name: 'Создать' })).toBeInTheDocument();
});

it('hides the create trigger when read-only', async () => {
  renderTab(false);
  await screen.findByText('Старт');
  expect(screen.queryByRole('button', { name: 'Создать пакет' })).not.toBeInTheDocument();
});
```

- [ ] **Step 2: Run `npm test -- PackagesTab`** → expect FAIL.

- [ ] **Step 3: Write the implementation** — create `src/club/monetization/packages/PackagesTab.tsx`:

```tsx
import { useState } from 'react';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { Button } from '@/components/ui/button';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import { usePackages } from './usePackages';
import { PackageFormDialog } from './PackageFormDialog';
import type { PackageRow } from './packagesModel';

type Client = Pick<ClubApiClient, 'getPackageOptions' | 'createPackageDefinition' | 'updatePackageDefinition'>;

export function PackagesTab({ client, branchId, organizationId, canManage }: {
  client: Client;
  branchId: string;
  organizationId: string;
  canManage: boolean;
}) {
  const { t, formatNumber, formatCurrency } = useI18n();
  const state = usePackages(client, branchId);
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<PackageRow | null>(null);

  if (state.status === 'loading') return <LoadingCards count={2} />;
  if (state.status === 'error') return <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />;

  const { rows, retry } = state;

  return (
    <div className="flex flex-col gap-4">
      {canManage && (
        <div className="flex justify-end">
          <Button onClick={() => setCreating(true)}>{t('loyalty.create')}</Button>
        </div>
      )}

      {rows.length === 0 ? (
        <EmptyState message={t('loyalty.empty')} />
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('loyalty.col.name')}</TableHead>
              <TableHead>{t('loyalty.col.price')}</TableHead>
              <TableHead>{t('loyalty.col.included')}</TableHead>
              <TableHead>{t('loyalty.col.bonus')}</TableHead>
              <TableHead>{t('loyalty.col.expires')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {rows.map(row => (
              <TableRow key={row.packageDefinitionId} data-clickable={canManage ? 'true' : undefined}
                onClick={canManage ? () => setEditing(row) : undefined}>
                <TableCell className="font-medium">{row.name}</TableCell>
                <TableCell className="tabular-nums">{formatCurrency(row.price, row.currencyCode)}</TableCell>
                <TableCell className="tabular-nums">{formatNumber(row.includedMinutes)}</TableCell>
                <TableCell className="tabular-nums">{formatNumber(row.bonusMinutes)}</TableCell>
                <TableCell className="tabular-nums">{formatNumber(row.expiresAfterDays)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      <p className="text-xs text-muted-foreground">{t('loyalty.activeOnlyNote')}</p>

      {creating && (
        <PackageFormDialog
          open mode="create" branchId={branchId} organizationId={organizationId} client={client}
          onOpenChange={o => { if (!o) setCreating(false); }}
          onDone={() => retry()}
        />
      )}
      {editing !== null && (
        <PackageFormDialog
          key={editing.packageDefinitionId}
          open mode="edit" branchId={branchId} organizationId={organizationId} client={client} initial={editing}
          onOpenChange={o => { if (!o) setEditing(null); }}
          onDone={() => retry()}
        />
      )}
    </div>
  );
}
```

- [ ] **Step 4: Run `npm test -- PackagesTab`** → expect PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/monetization/packages/PackagesTab.tsx src/club/monetization/packages/PackagesTab.test.tsx
git commit -m "feat(club): add PackagesTab (list + create/edit, read-only gating)"
```

---

## Task 7: Wire PackagesTab + thread canManagePackages; full suite + build gate

**Files:** Modify `src/club/monetization/MonetizationScreen.tsx`, `src/club/monetization/MonetizationScreen.test.tsx`, `src/App.tsx`.

- [ ] **Step 1: Update the MonetizationScreen test.** In `src/club/monetization/MonetizationScreen.test.tsx`: (a) add `getPackageOptions: vi.fn(async () => [])` to the fake client; (b) add `canManagePackages` to the rendered props; (c) add a test for the loyalty tab. Update the `setup` client + render to:

```tsx
  const client = { getTariffOptions: vi.fn(async () => [option]), getCatalog: vi.fn(async () => []), getPackageOptions: vi.fn(async () => []) };
  render(
    <I18nProvider><ToastProvider>
      <MonetizationScreen client={client as never} branchId="b1" organizationId="org" canManageTariffs canManageCatalog canManagePackages />
    </ToastProvider></I18nProvider>
  );
```

and add:

```tsx
it('shows packages on the loyalty tab', async () => {
  setup();
  await screen.findByText('Дневной');
  const tab = screen.getByRole('tab', { name: 'Лояльность' });
  fireEvent.mouseDown(tab);
  fireEvent.click(tab);
  expect(await screen.findByText('Пакеты ещё не созданы.')).toBeInTheDocument();
});
```

- [ ] **Step 2: Run `npm test -- MonetizationScreen`** → expect FAIL (prop missing / loyalty tab still placeholder).

- [ ] **Step 3: Update MonetizationScreen.** In `src/club/monetization/MonetizationScreen.tsx`:
- add the import: `import { PackagesTab } from './packages/PackagesTab';`
- add `canManagePackages: boolean` to the signature:
  ```tsx
  export function MonetizationScreen({ client, branchId, organizationId, canManageTariffs, canManageCatalog, canManagePackages }: {
    client: ClubApiClient;
    branchId: string;
    organizationId: string;
    canManageTariffs: boolean;
    canManageCatalog: boolean;
    canManagePackages: boolean;
  }) {
  ```
- replace the loyalty `TabsContent` body:
  ```tsx
  <TabsContent value="loyalty">
    <p className="text-sm text-muted-foreground">{t('monetization.soon')}</p>
  </TabsContent>
  ```
  with:
  ```tsx
  <TabsContent value="loyalty">
    <PackagesTab client={client} branchId={branchId} organizationId={organizationId} canManage={canManagePackages} />
  </TabsContent>
  ```

- [ ] **Step 4: Thread the prop from App.tsx.** In `src/App.tsx`, the `clubMonetization` render branch, add `canManagePackages`:

```tsx
            canManageCatalog={session.permissions.includes('pos.catalog.manage')}
            canManagePackages={session.permissions.includes('packages.manage')}
          />
```

- [ ] **Step 5: Run `npm test -- MonetizationScreen`** → expect PASS (3 tests).

- [ ] **Step 6: Run the full suite + build.**

Run: `npm test` → all pass.
Run: `npm run build` → clean `tsc -b && vite build`. (`tsc -b` is the real type check — vitest/esbuild does NOT type-check. The `*FormDialog.test.tsx` files already type their render-helper props via `ComponentProps<typeof X>`.)

- [ ] **Step 7: Commit**

```bash
git add src/club/monetization/MonetizationScreen.tsx src/club/monetization/MonetizationScreen.test.tsx src/App.tsx
git commit -m "feat(club): render the loyalty tab + thread packages.manage"
```

---

## Self-Review

**Spec coverage** (monetization design spec, Лояльность section):
- List active packages via `getPackageOptions` → Tasks 2/3/4/6. ✓
- Create + edit package incl. deactivate via `isActive` → Task 5 (Switch in edit mode). ✓
- Money in minor units (price→major display, →minor request via shared helper) → Tasks 2/3. ✓
- Time fields shown/entered as minutes (seconds on the wire) → Task 3. ✓
- Active-only note → Tasks 1/6. ✓
- No new backend contracts; new wrappers + camelCase types → Task 2. ✓
- Role gating via `packages.manage` → Tasks 6/7; read-only hides create/edit → Task 6. ✓
- Data-region states → Tasks 4/6. ✓
- Plugged into the Лояльность tab → Task 7. ✓

**Deliberate choices (documented):**
- **Time in minutes** in the UI (the wire is seconds) — natural for prepaid time; converted in the pure model.
- **Deactivation via the `Active` Switch** in the edit dialog (no DELETE on the backend), consistent with 5a/5b — no separate ConfirmDialog.

**Placeholder scan:** no TBD/"handle edge cases"; every code step is complete with real code and exact commands.

**Type consistency:** `PackageOption`/`PackageDefinition` + the two request types (Task 2, reusing `MoneyMinor`) are consumed unchanged in Tasks 3/5. `PackageRow`/`PackageFormValues` (Task 3) flow into Tasks 4/5/6. `usePackages(client, branchId): PackagesState` (Task 4) consumed in Task 6. `PackageFormDialog` props `{ open, mode, branchId, organizationId, client, initial?, onOpenChange, onDone }` (Task 5) match Task 6's two render sites. `PackagesTab` props `{ client, branchId, organizationId, canManage }` (Task 6) match Task 7. `MonetizationScreen` gains `canManagePackages`, satisfied by Task 7's App.tsx call site. Wrapper names (`getPackageOptions`/`createPackageDefinition`/`updatePackageDefinition`) identical across Tasks 2/5/6. Routes + `packages.manage`/`packages.view` match the verified backend table. After this plan the Монетизация screen is complete (all three tabs live).

---

## Execution Handoff

Two execution options:

1. **Subagent-Driven (recommended)** — fresh subagent per task, two-stage review between tasks.
2. **Inline Execution** — execute tasks in this session with checkpoints.
