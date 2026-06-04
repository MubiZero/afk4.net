# Client Packages (Plan 6c) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show a client's purchased packages (remaining included/bonus minutes + expiry) in the client card and let staff buy a package for the client — the final block of the Clients/CRM screen.

**Architecture:** Builds on Plans 6a/6b. A new `PackagesPanel` (sibling of `WalletPanel`, rendered inside `ClientDetail`'s `canViewBilling` branch) owns a `usePlayerPackages` hook that loads BOTH the client's packages (`getPlayerPackages`, player-scoped) and the branch's available package definitions (`getPackageOptions`, branch-scoped) in parallel. The panel renders the package list and a permission-gated "Buy package" button that opens `PurchasePackageDialog` (a Radix `Select` of available definitions → `purchasePackage`). After a purchase it refetches and calls the shared `onMutated`. `getPackageOptions` + `getPlayerPackages` wrappers and the `PackageOption`/`PlayerPackage` types already exist (6a); only `purchasePackage` + its request type are new.

**Tech Stack:** React 19, TypeScript, Vite, Vitest 4 + jsdom + @testing-library/react (`globals: false` — import `{ it, expect, vi }` from `'vitest'`). Time seconds↔minutes via `Math.round`. Radix `Select` for package choice (tests rely on the DEFAULT selection — do NOT open the dropdown in jsdom). i18n RU/EN parity enforced by a test.

**Spec:** `docs/superpowers/specs/2026-05-30-platform-web-club-clients-crm-design.md`

**Backend contracts (verified 2026-05-30 against C#):**
| Verb | Path | Request / Response | Permission |
|---|---|---|---|
| GET | `/api/players/{playerAccountId}/packages` | → `PlayerPackageDto[]` | `billing.view` (wrapper `getPlayerPackages` exists) |
| GET | `/api/branches/{branchId}/packages/options` | → `PackageOptionDto[]` | (wrapper `getPackageOptions` exists) |
| POST | `/api/players/{playerAccountId}/packages/purchases` | `PurchasePackageRequest(OrganizationId, PackageDefinitionId, IdempotencyKey)` → `PlayerPackageDto` | `packages.purchase` |

- `PlayerPackage` (frontend type, exists): `{ playerPackageId, packageDefinitionId, playerAccountId, name, purchasedPrice: MoneyMinor, includedSeconds, bonusSeconds, remainingIncludedSeconds, remainingBonusSeconds, purchasedAtUtc, expiresAtUtc: string | null }`.
- `PackageOption` (frontend type, exists): `{ packageDefinitionId, name, currencyCode, priceMinorUnits, includedSeconds, bonusSeconds, expiresAfterDays }`.
- `getPlayerPackages` requires `billing.view` → `PackagesPanel` mounts only inside `ClientDetail`'s `canViewBilling` branch (no 403).
- Buying is gated by `packages.purchase` (`canPurchase`).

**npm cwd:** `D:\afk4.net\src\AFK4.Platform.Web` (all `npm`/`git` commands assume this directory).

---

### Task 1: i18n keys (client packages)

**Files:**
- Modify: `src/i18n/messages.ts` (insert into both `ru` and `en` blocks, after the `money.submit` key added in Plan 6b, before the block-closing brace)
- Modify: `src/i18n/messages.test.ts` (add a parity-coverage block)

- [ ] **Step 1: Add the parity test block first.** In `src/i18n/messages.test.ts`, append after the `'includes the money-operations keys'` test:

```ts
it('includes the client-packages keys', () => {
  for (const key of [
    'clientPackages.title', 'clientPackages.empty', 'clientPackages.purchase',
    'clientPackages.purchase.title', 'clientPackages.purchase.submit', 'clientPackages.field.package',
    'clientPackages.col.name', 'clientPackages.col.included', 'clientPackages.col.bonus',
    'clientPackages.col.expires', 'clientPackages.noExpiry', 'clientPackages.noChoices'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});
```

- [ ] **Step 2: Run `npm test -- messages`** — expect FAIL (keys undefined).

- [ ] **Step 3: Add the RU keys.** The `ru` block currently ends:
```ts
    'money.submit': 'Подтвердить'
  },
  en: {
```
Change the `'money.submit'` line to add a trailing comma and insert before the `  },`:
```ts
    'money.submit': 'Подтвердить',
    'clientPackages.title': 'Пакеты',
    'clientPackages.empty': 'У клиента нет пакетов.',
    'clientPackages.purchase': 'Купить пакет',
    'clientPackages.purchase.title': 'Покупка пакета',
    'clientPackages.purchase.submit': 'Купить',
    'clientPackages.field.package': 'Пакет',
    'clientPackages.col.name': 'Название',
    'clientPackages.col.included': 'Остаток вкл., мин',
    'clientPackages.col.bonus': 'Остаток бонус., мин',
    'clientPackages.col.expires': 'Действует до',
    'clientPackages.noExpiry': 'Бессрочно',
    'clientPackages.noChoices': 'Нет доступных пакетов для покупки.'
  },
  en: {
```

- [ ] **Step 4: Add the EN keys.** The `en` block currently ends:
```ts
    'money.submit': 'Confirm'
  }
} as const;
```
Change the `'money.submit'` line to add a trailing comma and insert before the `  }`:
```ts
    'money.submit': 'Confirm',
    'clientPackages.title': 'Packages',
    'clientPackages.empty': 'This client has no packages.',
    'clientPackages.purchase': 'Buy package',
    'clientPackages.purchase.title': 'Buy package',
    'clientPackages.purchase.submit': 'Buy',
    'clientPackages.field.package': 'Package',
    'clientPackages.col.name': 'Name',
    'clientPackages.col.included': 'Remaining incl., min',
    'clientPackages.col.bonus': 'Remaining bonus, min',
    'clientPackages.col.expires': 'Expires',
    'clientPackages.noExpiry': 'No expiry',
    'clientPackages.noChoices': 'No packages available to buy.'
  }
} as const;
```

- [ ] **Step 5: Run `npm test -- messages`** — expect PASS.

- [ ] **Step 6: Commit**

```bash
git add src/i18n/messages.ts src/i18n/messages.test.ts
git commit -m "feat(clients): i18n keys for client packages"
```

---

### Task 2: PurchasePackageRequest type + clubApi wrapper

**Files:**
- Modify: `src/api/types.ts` (append at end of file)
- Modify: `src/api/clubApi.ts` (add import; add `purchasePackage` after `refundLedgerEntry`)

- [ ] **Step 1: Add the request type.** Append to `src/api/types.ts`:

```ts

export interface PurchasePackageRequest {
  organizationId: string;
  packageDefinitionId: string;
  idempotencyKey: string;
}
```

- [ ] **Step 2: Add the type import to clubApi.ts.** In the `import type { … } from './types'` block, add `PurchasePackageRequest`. Verify `PlayerPackage` is already imported (added in 6a, used by `getPlayerPackages`).

- [ ] **Step 3: Add the method.** In `src/api/clubApi.ts`, insert immediately AFTER the `refundLedgerEntry` method (added in 6b) and BEFORE `private async send<T>(`:

```ts
  public purchasePackage(playerAccountId: string, request: PurchasePackageRequest): Promise<PlayerPackage> {
    return this.send<PlayerPackage>('POST', `/api/players/${encodeURIComponent(playerAccountId)}/packages/purchases`, request);
  }
```

- [ ] **Step 4: Verify it compiles.** Run: `npx tsc -b` — expect exit 0.

- [ ] **Step 5: Commit**

```bash
git add src/api/types.ts src/api/clubApi.ts
git commit -m "feat(clients): purchasePackage request type and clubApi wrapper"
```

---

### Task 3: playerPackagesModel (pure)

**Files:**
- Create: `src/club/clients/playerPackagesModel.ts`
- Test: `src/club/clients/playerPackagesModel.test.ts`

- [ ] **Step 1: Write the failing test** — create `src/club/clients/playerPackagesModel.test.ts`:

```ts
import { it, expect } from 'vitest';
import type { PackageOption, PlayerPackage } from '@/api/types';
import { toPlayerPackageRows, toPackageChoices, buildPurchasePackageRequest } from './playerPackagesModel';

const pkg: PlayerPackage = {
  playerPackageId: 'pp1', packageDefinitionId: 'pd1', playerAccountId: 'p1', name: 'Старт',
  purchasedPrice: { currencyCode: 'TJS', minorUnits: 50000 },
  includedSeconds: 3600, bonusSeconds: 600, remainingIncludedSeconds: 1800, remainingBonusSeconds: 300,
  purchasedAtUtc: '2026-05-01T00:00:00.000Z', expiresAtUtc: '2026-06-01T00:00:00.000Z'
};

const option: PackageOption = {
  packageDefinitionId: 'pd1', name: 'Старт', currencyCode: 'TJS', priceMinorUnits: 50000,
  includedSeconds: 3600, bonusSeconds: 600, expiresAfterDays: 30
};

it('maps player packages to rows: remaining seconds to minutes', () => {
  expect(toPlayerPackageRows([pkg])[0]).toEqual({
    playerPackageId: 'pp1', name: 'Старт',
    remainingIncludedMinutes: 30, remainingBonusMinutes: 5, expiresAtUtc: '2026-06-01T00:00:00.000Z'
  });
});

it('keeps a null expiry as null', () => {
  expect(toPlayerPackageRows([{ ...pkg, expiresAtUtc: null }])[0].expiresAtUtc).toBeNull();
});

it('maps package options to purchase choices', () => {
  expect(toPackageChoices([option])).toEqual([{ packageDefinitionId: 'pd1', name: 'Старт' }]);
});

it('builds a purchase request', () => {
  expect(buildPurchasePackageRequest('org', 'pd1', 'idem')).toEqual({
    organizationId: 'org', packageDefinitionId: 'pd1', idempotencyKey: 'idem'
  });
});
```

- [ ] **Step 2: Run `npm test -- playerPackagesModel`** — expect FAIL (module not found).

- [ ] **Step 3: Write the implementation** — create `src/club/clients/playerPackagesModel.ts`:

```ts
import type { PackageOption, PlayerPackage, PurchasePackageRequest } from '@/api/types';

export interface PlayerPackageRow {
  playerPackageId: string;
  name: string;
  remainingIncludedMinutes: number;
  remainingBonusMinutes: number;
  expiresAtUtc: string | null;
}

export interface PackageChoice {
  packageDefinitionId: string;
  name: string;
}

export function toPlayerPackageRows(packages: PlayerPackage[]): PlayerPackageRow[] {
  return packages.map(p => ({
    playerPackageId: p.playerPackageId,
    name: p.name,
    remainingIncludedMinutes: Math.round(p.remainingIncludedSeconds / 60),
    remainingBonusMinutes: Math.round(p.remainingBonusSeconds / 60),
    expiresAtUtc: p.expiresAtUtc
  }));
}

export function toPackageChoices(options: PackageOption[]): PackageChoice[] {
  return options.map(o => ({ packageDefinitionId: o.packageDefinitionId, name: o.name }));
}

export function buildPurchasePackageRequest(
  organizationId: string,
  packageDefinitionId: string,
  idempotencyKey: string
): PurchasePackageRequest {
  return { organizationId, packageDefinitionId, idempotencyKey };
}
```

- [ ] **Step 4: Run `npm test -- playerPackagesModel`** — expect PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/clients/playerPackagesModel.ts src/club/clients/playerPackagesModel.test.ts
git commit -m "feat(clients): pure playerPackagesModel (rows, choices, purchase request)"
```

---

### Task 4: usePlayerPackages hook

**Files:**
- Create: `src/club/clients/usePlayerPackages.ts`
- Test: `src/club/clients/usePlayerPackages.test.ts`

Loads the client's packages and the branch's available package definitions in parallel; exposes both as ready-state data plus `retry`.

- [ ] **Step 1: Write the failing test** — create `src/club/clients/usePlayerPackages.test.ts`:

```ts
import { it, expect, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import type { PackageOption, PlayerPackage } from '@/api/types';
import { usePlayerPackages } from './usePlayerPackages';

const pkg: PlayerPackage = {
  playerPackageId: 'pp1', packageDefinitionId: 'pd1', playerAccountId: 'p1', name: 'Старт',
  purchasedPrice: { currencyCode: 'TJS', minorUnits: 50000 },
  includedSeconds: 3600, bonusSeconds: 600, remainingIncludedSeconds: 1800, remainingBonusSeconds: 300,
  purchasedAtUtc: '2026-05-01T00:00:00.000Z', expiresAtUtc: null
};

const option: PackageOption = {
  packageDefinitionId: 'pd1', name: 'Старт', currencyCode: 'TJS', priceMinorUnits: 50000,
  includedSeconds: 3600, bonusSeconds: 600, expiresAfterDays: 30
};

it('loads player packages and purchase choices', async () => {
  const client = {
    getPlayerPackages: vi.fn(async () => [pkg]),
    getPackageOptions: vi.fn(async () => [option])
  };
  const { result } = renderHook(() => usePlayerPackages(client as never, 'p1', 'b1'));
  await waitFor(() => expect(result.current.status).toBe('ready'));
  if (result.current.status !== 'ready') throw new Error('not ready');
  expect(result.current.rows.map(r => r.name)).toEqual(['Старт']);
  expect(result.current.choices).toEqual([{ packageDefinitionId: 'pd1', name: 'Старт' }]);
  expect(client.getPlayerPackages).toHaveBeenCalledWith('p1');
  expect(client.getPackageOptions).toHaveBeenCalledWith('b1');
});

it('reports an error when a load fails', async () => {
  const client = {
    getPlayerPackages: vi.fn(async () => { throw new Error('boom'); }),
    getPackageOptions: vi.fn(async () => [option])
  };
  const { result } = renderHook(() => usePlayerPackages(client as never, 'p1', 'b1'));
  await waitFor(() => expect(result.current.status).toBe('error'));
});
```

- [ ] **Step 2: Run `npm test -- usePlayerPackages`** — expect FAIL (module not found).

- [ ] **Step 3: Write the implementation** — create `src/club/clients/usePlayerPackages.ts`:

```ts
import { useCallback, useEffect, useRef, useState } from 'react';
import type { ClubApiClient } from '@/api/clubApi';
import { toPackageChoices, toPlayerPackageRows, type PackageChoice, type PlayerPackageRow } from './playerPackagesModel';

type Loadable = Pick<ClubApiClient, 'getPlayerPackages' | 'getPackageOptions'>;

export type PlayerPackagesState =
  | { status: 'loading' }
  | { status: 'error'; retry: () => void }
  | { status: 'ready'; rows: PlayerPackageRow[]; choices: PackageChoice[]; retry: () => void };

export function usePlayerPackages(client: Loadable, playerAccountId: string, branchId: string): PlayerPackagesState {
  const [tick, setTick] = useState(0);
  const [phase, setPhase] = useState<'loading' | 'error' | 'ready'>('loading');
  const [rows, setRows] = useState<PlayerPackageRow[]>([]);
  const [choices, setChoices] = useState<PackageChoice[]>([]);
  const clientRef = useRef(client);
  clientRef.current = client;

  const retry = useCallback(() => setTick(t => t + 1), []);

  useEffect(() => {
    let cancelled = false;
    setPhase('loading');
    Promise.all([
      clientRef.current.getPlayerPackages(playerAccountId),
      clientRef.current.getPackageOptions(branchId)
    ])
      .then(([packages, options]) => {
        if (!cancelled) {
          setRows(toPlayerPackageRows(packages));
          setChoices(toPackageChoices(options));
          setPhase('ready');
        }
      })
      .catch(() => { if (!cancelled) setPhase('error'); });
    return () => { cancelled = true; };
  }, [playerAccountId, branchId, tick]);

  if (phase === 'error') return { status: 'error', retry };
  if (phase === 'loading') return { status: 'loading' };
  return { status: 'ready', rows, choices, retry };
}
```

- [ ] **Step 4: Run `npm test -- usePlayerPackages`** — expect PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/clients/usePlayerPackages.ts src/club/clients/usePlayerPackages.test.ts
git commit -m "feat(clients): usePlayerPackages hook (packages + purchase choices)"
```

---

### Task 5: PurchasePackageDialog

**Files:**
- Create: `src/club/clients/PurchasePackageDialog.tsx`
- Test: `src/club/clients/PurchasePackageDialog.test.tsx`

Receives the available `choices` as a prop (loaded by the panel). A Radix `Select` defaults to the first choice; the test relies on that default (it does not open the dropdown). When there are no choices, it shows a note and disables submit.

- [ ] **Step 1: Write the failing test** — create `src/club/clients/PurchasePackageDialog.test.tsx`:

```tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { PurchasePackageDialog } from './PurchasePackageDialog';

function setup(choices = [{ packageDefinitionId: 'pd1', name: 'Старт' }]) {
  const client = {
    purchasePackage: vi.fn<(id: string, req: object) => Promise<object>>(async () => ({ playerPackageId: 'pp9' }))
  };
  const onDone = vi.fn();
  render(
    <I18nProvider><ToastProvider>
      <PurchasePackageDialog
        open client={client as never} playerAccountId="p1" organizationId="org"
        choices={choices} onOpenChange={() => {}} onDone={onDone}
      />
    </ToastProvider></I18nProvider>
  );
  return { client, onDone };
}

it('purchases the default-selected package', async () => {
  const { client, onDone } = setup();
  fireEvent.click(screen.getByRole('button', { name: 'Купить' }));
  await waitFor(() => expect(client.purchasePackage).toHaveBeenCalled());
  expect(client.purchasePackage.mock.calls[0][0]).toBe('p1');
  expect(client.purchasePackage.mock.calls[0][1]).toMatchObject({
    organizationId: 'org', packageDefinitionId: 'pd1'
  });
  await waitFor(() => expect(onDone).toHaveBeenCalled());
});

it('disables submit and shows a note when there are no choices', () => {
  setup([]);
  expect(screen.getByText('Нет доступных пакетов для покупки.')).toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Купить' })).toBeDisabled();
});
```

- [ ] **Step 2: Run `npm test -- PurchasePackageDialog`** — expect FAIL (module not found).

- [ ] **Step 3: Write the implementation** — create `src/club/clients/PurchasePackageDialog.tsx`:

```tsx
import { useState } from 'react';
import { Dialog, DialogContent, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import { buildPurchasePackageRequest, type PackageChoice } from './playerPackagesModel';

type Actions = Pick<ClubApiClient, 'purchasePackage'>;

export function PurchasePackageDialog({ open, client, playerAccountId, organizationId, choices, onOpenChange, onDone }: {
  open: boolean;
  client: Actions;
  playerAccountId: string;
  organizationId: string;
  choices: PackageChoice[];
  onOpenChange: (open: boolean) => void;
  onDone: () => void;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [selected, setSelected] = useState(choices[0]?.packageDefinitionId ?? '');
  const [pending, setPending] = useState(false);

  const valid = selected !== '';

  async function submit() {
    setPending(true);
    try {
      await client.purchasePackage(playerAccountId, buildPurchasePackageRequest(organizationId, selected, crypto.randomUUID()));
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
        <DialogTitle>{t('clientPackages.purchase.title')}</DialogTitle>
        {choices.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t('clientPackages.noChoices')}</p>
        ) : (
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('clientPackages.field.package')}</span>
            <Select value={selected} onValueChange={setSelected}>
              <SelectTrigger aria-label={t('clientPackages.field.package')}>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {choices.map(c => (
                  <SelectItem key={c.packageDefinitionId} value={c.packageDefinitionId}>{c.name}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </label>
        )}
        <DialogFooter>
          <Button variant="outline" disabled={pending} onClick={() => onOpenChange(false)}>{t('common.cancel')}</Button>
          <Button disabled={pending || !valid} onClick={() => void submit()}>{t('clientPackages.purchase.submit')}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
```

- [ ] **Step 4: Run `npm test -- PurchasePackageDialog`** — expect PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/clients/PurchasePackageDialog.tsx src/club/clients/PurchasePackageDialog.test.tsx
git commit -m "feat(clients): PurchasePackageDialog"
```

---

### Task 6: PackagesPanel

**Files:**
- Create: `src/club/clients/PackagesPanel.tsx`
- Test: `src/club/clients/PackagesPanel.test.tsx`

Owns `usePlayerPackages`; renders the package list + a permission-gated "Buy package" button opening `PurchasePackageDialog`. After a purchase: `retry()` + optional `onMutated()`.

- [ ] **Step 1: Write the failing test** — create `src/club/clients/PackagesPanel.test.tsx`:

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import type { PackageOption, PlayerPackage } from '@/api/types';
import { PackagesPanel } from './PackagesPanel';

const pkg: PlayerPackage = {
  playerPackageId: 'pp1', packageDefinitionId: 'pd1', playerAccountId: 'p1', name: 'Старт',
  purchasedPrice: { currencyCode: 'TJS', minorUnits: 50000 },
  includedSeconds: 3600, bonusSeconds: 600, remainingIncludedSeconds: 1800, remainingBonusSeconds: 300,
  purchasedAtUtc: '2026-05-01T00:00:00.000Z', expiresAtUtc: null
};

const option: PackageOption = {
  packageDefinitionId: 'pd1', name: 'Старт', currencyCode: 'TJS', priceMinorUnits: 50000,
  includedSeconds: 3600, bonusSeconds: 600, expiresAfterDays: 30
};

function fakeClient() {
  return {
    getPlayerPackages: vi.fn(async () => [pkg]),
    getPackageOptions: vi.fn(async () => [option]),
    purchasePackage: vi.fn(async () => ({ playerPackageId: 'pp9' }))
  };
}

function renderPanel(canPurchase: boolean) {
  render(
    <I18nProvider><ToastProvider>
      <PackagesPanel client={fakeClient() as never} playerAccountId="p1" branchId="b1" organizationId="org" canPurchase={canPurchase} />
    </ToastProvider></I18nProvider>
  );
}

it('lists the client packages', async () => {
  renderPanel(false);
  expect(await screen.findByText('Старт')).toBeInTheDocument();
  expect(screen.getByText('Пакеты')).toBeInTheDocument();
});

it('hides the purchase trigger when not permitted', async () => {
  renderPanel(false);
  await screen.findByText('Старт');
  expect(screen.queryByRole('button', { name: 'Купить пакет' })).not.toBeInTheDocument();
});

it('opens the purchase dialog when permitted', async () => {
  renderPanel(true);
  await screen.findByText('Старт');
  fireEvent.click(screen.getByRole('button', { name: 'Купить пакет' }));
  expect(await screen.findByText('Покупка пакета')).toBeInTheDocument();
});
```

- [ ] **Step 2: Run `npm test -- PackagesPanel`** — expect FAIL (module not found).

- [ ] **Step 3: Write the implementation** — create `src/club/clients/PackagesPanel.tsx`:

```tsx
import { useState } from 'react';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { Button } from '@/components/ui/button';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import { usePlayerPackages } from './usePlayerPackages';
import { PurchasePackageDialog } from './PurchasePackageDialog';

type Client = Pick<ClubApiClient, 'getPlayerPackages' | 'getPackageOptions' | 'purchasePackage'>;

export function PackagesPanel({ client, playerAccountId, branchId, organizationId, canPurchase, onMutated }: {
  client: Client;
  playerAccountId: string;
  branchId: string;
  organizationId: string;
  canPurchase: boolean;
  onMutated?: () => void;
}) {
  const { t, formatNumber, formatDate } = useI18n();
  const state = usePlayerPackages(client, playerAccountId, branchId);
  const [purchasing, setPurchasing] = useState(false);

  if (state.status === 'loading') return <LoadingCards count={1} />;
  if (state.status === 'error') return <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />;

  const { rows, choices, retry } = state;

  return (
    <div className="flex flex-col gap-2">
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-medium">{t('clientPackages.title')}</h3>
        {canPurchase && <Button size="sm" onClick={() => setPurchasing(true)}>{t('clientPackages.purchase')}</Button>}
      </div>

      {rows.length === 0 ? (
        <EmptyState message={t('clientPackages.empty')} />
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('clientPackages.col.name')}</TableHead>
              <TableHead>{t('clientPackages.col.included')}</TableHead>
              <TableHead>{t('clientPackages.col.bonus')}</TableHead>
              <TableHead>{t('clientPackages.col.expires')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {rows.map(row => (
              <TableRow key={row.playerPackageId}>
                <TableCell className="font-medium">{row.name}</TableCell>
                <TableCell className="tabular-nums">{formatNumber(row.remainingIncludedMinutes)}</TableCell>
                <TableCell className="tabular-nums">{formatNumber(row.remainingBonusMinutes)}</TableCell>
                <TableCell>{row.expiresAtUtc === null ? t('clientPackages.noExpiry') : formatDate(row.expiresAtUtc)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      {purchasing && (
        <PurchasePackageDialog
          open client={client} playerAccountId={playerAccountId} organizationId={organizationId} choices={choices}
          onOpenChange={o => { if (!o) setPurchasing(false); }}
          onDone={() => { retry(); onMutated?.(); }}
        />
      )}
    </div>
  );
}
```

- [ ] **Step 4: Run `npm test -- PackagesPanel`** — expect PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/clients/PackagesPanel.tsx src/club/clients/PackagesPanel.test.tsx
git commit -m "feat(clients): PackagesPanel — client packages list + purchase"
```

---

### Task 7: Integrate PackagesPanel into ClientDetail; thread props; full suite + build gate

**Files:**
- Modify: `src/club/clients/ClientDetail.tsx` (render `PackagesPanel`; add `branchId`/`canPurchase`; expand client Pick)
- Modify: `src/club/clients/ClientDetail.test.tsx` (pass `branchId`; provide the package methods on the fake client)
- Modify: `src/club/clients/ClientsScreen.tsx` (pass `branchId`/`canPurchase`; expand client Pick)
- Modify: `src/App.tsx` (pass `canPurchase` from `session.permissions`)

- [ ] **Step 1: Update `ClientDetail.tsx`.** It must accept `branchId` + `canPurchase`, render `PackagesPanel` inside the `canViewBilling` branch (below `WalletPanel`), expand its client `Pick`, and forward `onMutated` to both panels. Final version:

```tsx
import { Card } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import type { PlayerRow } from './clientsModel';
import { WalletPanel, type MoneyPerms } from './WalletPanel';
import { PackagesPanel } from './PackagesPanel';

type Client = Pick<ClubApiClient,
  'getWalletSummary' | 'topUpWallet' | 'payDebt' | 'createManualCorrection' | 'refundLedgerEntry'
  | 'getPlayerPackages' | 'getPackageOptions' | 'purchasePackage'>;

export function ClientDetail({ client, player, branchId, organizationId, canViewBilling, moneyPerms, canPurchase, onMutated }: {
  client: Client;
  player: PlayerRow;
  branchId: string;
  organizationId: string;
  canViewBilling: boolean;
  moneyPerms?: MoneyPerms;
  canPurchase?: boolean;
  onMutated?: () => void;
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
        <>
          <WalletPanel
            client={client} playerAccountId={player.playerAccountId} organizationId={organizationId}
            moneyPerms={moneyPerms} onMutated={onMutated}
          />
          <PackagesPanel
            client={client} playerAccountId={player.playerAccountId} branchId={branchId} organizationId={organizationId}
            canPurchase={canPurchase ?? false} onMutated={onMutated}
          />
        </>
      ) : (
        <p className="text-sm text-muted-foreground">{t('clients.billing.noAccess')}</p>
      )}

      <p className="text-xs text-muted-foreground">{t('clients.editUnavailable')}</p>
    </Card>
  );
}
```

- [ ] **Step 2: Update `ClientDetail.test.tsx`.** The fake client must now also provide the package methods, and each render needs `branchId`. Update the `fakeClient()` helper to add:
```tsx
    getPlayerPackages: vi.fn(async () => []),
    getPackageOptions: vi.fn(async () => []),
    purchasePackage: vi.fn(async () => ({ playerPackageId: 'pp9' }))
```
(alongside the existing `getWalletSummary`). Add `branchId="b1"` to each `<ClientDetail ... />` render. The existing assertions (header text, edit-unavailable note, wallet panel render, no-billing note + `getWalletSummary` not called) remain valid — note the "billing not permitted" test still asserts `getWalletSummary` was not called; since `PackagesPanel` is also inside the `canViewBilling` branch, `getPlayerPackages` is likewise not called, so add `expect(client.getPlayerPackages).not.toHaveBeenCalled();` to that test for completeness.

- [ ] **Step 3: Update `ClientsScreen.tsx`.** Add `canPurchase?: boolean` to its props; expand its client `Pick` to include the package methods; pass `branchId` and `canPurchase` to `ClientDetail`.
  - Expand the `Client` type. Change:
    ```tsx
    type Client = Pick<ClubApiClient,
      'searchPlayers' | 'getWalletSummary' | 'createPlayer' | 'topUpWallet' | 'payDebt' | 'createManualCorrection' | 'refundLedgerEntry'>;
    ```
    to additionally include `'getPlayerPackages' | 'getPackageOptions' | 'purchasePackage'`. (The exact current Pick may differ — read the file and ADD these three method names to whatever Pick is there.)
  - Add `canPurchase` to destructured props + props type: `canPurchase?: boolean;`
  - Update the `<ClientDetail ... />` usage to add `branchId={branchId}` and `canPurchase={canPurchase}` (keep existing `key`, `client`, `player`, `organizationId`, `canViewBilling`, `moneyPerms`, `onMutated`):
    ```tsx
    <ClientDetail
      key={selected.playerAccountId}
      client={client}
      player={selected}
      branchId={branchId}
      organizationId={organizationId}
      canViewBilling={canViewBilling}
      moneyPerms={moneyPerms}
      canPurchase={canPurchase}
      onMutated={() => { if (state.status === 'ready') state.retry(); }}
    />
    ```
  (`ClientsScreen.test.tsx` does not pass `canPurchase` — optional — but its `fakeClient()` must provide the new methods if the detail renders. The existing ClientsScreen test selects a row and asserts the edit-unavailable note with `canViewBilling` defaulting to true, so `PackagesPanel` will mount and call `getPlayerPackages`/`getPackageOptions`. ADD `getPlayerPackages: vi.fn(async () => [])`, `getPackageOptions: vi.fn(async () => [])`, `purchasePackage: vi.fn(async () => ({}))` to that test's `fakeClient()`.)

- [ ] **Step 4: Update `App.tsx`.** In the `clubClients` render branch, add `canPurchase` to the `<ClientsScreen ... />` usage:
```tsx
            canPurchase={session.permissions.includes('packages.purchase')}
```
(place it alongside `canCreate`/`canViewBilling`/`moneyPerms`).

- [ ] **Step 5: Run the full suite.** Run: `npm test` — expect ALL green.

- [ ] **Step 6: BUILD GATE (the real type check — vitest does NOT type-check).** Run: `npm run build` (`tsc -b && vite build`), expect exit 0.
  - If `tsc` errors, fix before committing. Watch for: a client `Pick` not expanded somewhere in the chain (`ClientsScreen`→`ClientDetail`→panels must all include the package methods); a `vi.fn(async () => …)` whose `mock.calls` is an empty tuple (give an explicit signature).

- [ ] **Step 7: Commit**

```bash
git add src/App.tsx src/club/clients/ClientDetail.tsx src/club/clients/ClientDetail.test.tsx src/club/clients/ClientsScreen.tsx src/club/clients/ClientsScreen.test.tsx
git commit -m "feat(clients): show client packages and purchase in the client card"
```

---

## Self-Review notes (for the executor)

- **Spec coverage:** client packages list with remaining included/bonus minutes + expiry (T3/T6), purchase a package by definition (T2/T5/T6), permission gating (`billing.view` mounts the panel via the existing `canViewBilling` branch; `packages.purchase` gates the buy button via `canPurchase`), list refresh after purchase (`onMutated`, T6/T7). This completes the Clients/CRM screen (6a foundation + 6b money + 6c packages).
- **Type consistency:** `PlayerPackageRow`/`PackageChoice` defined in T3 are consumed by T4/T5/T6. `usePlayerPackages` loads `getPlayerPackages(playerAccountId)` + `getPackageOptions(branchId)` in parallel. `PurchasePackageRequest` matches the C# record (`organizationId, packageDefinitionId, idempotencyKey`).
- **Pick threading:** adding the package methods to the `client` Pick ripples `ClientsScreen`→`ClientDetail`→`PackagesPanel`; all three Picks must include `getPlayerPackages`/`getPackageOptions`/`purchasePackage` (done in T6/T7) or `tsc -b` breaks. (Same lesson as 6b.)
- **jsdom/Radix:** `PurchasePackageDialog`'s `Select` is tested only at its default (first choice); the dropdown is never opened. The empty-choices path is covered by a dedicated test.
- **Build gate is mandatory** at Task 7 — `npm test` (esbuild) does not type-check; only `npm run build` (`tsc -b`) does.
- **After this plan:** the Clients/CRM block (6a/6b/6c) is fully complete. Remaining sub-project-2 work: Plan 7 (Отчёты + Профиль + redesigned Установка, then delete `ClubDashboard`).
