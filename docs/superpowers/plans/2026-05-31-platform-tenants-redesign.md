# SP3 Plan 2 — Platform Tenants (list + detail drawer) Redesign

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the legacy inline `/admin/tenants` UI (`TenantList` + `TenantDetailView` + `StatusControl`/`PlanControl`/`LimitsControl`) with a design-system Tenants screen: a searchable/filterable `Table` plus a `Sheet` detail drawer (Overview + Status + Plan + Limits sections), mirroring `src/club/venue`.

**Architecture:** New module `src/platform/tenants/` following the house feature shape — pure `*Model.ts` builder + `use*` hooks (discriminated-union `loading|error|ready`, each carrying `retry`; `useRef(client)` + `[tick]` refetch + `cancelled` cleanup) + presentational components. `TenantsScreen` renders for BOTH the `tenantList` and `tenantDetail` routes; `tenantDetail` simply supplies a `selectedTenantId` that opens the drawer, preserving deep-links and the existing `initialInvite` history-state flow from new-tenant creation. Destructive status changes go through `ConfirmDialog` + `useToast`; no optimistic success.

**Scope reconciliation (locked):** Per the SP3 spec decomposition, owner-invites + support-notes are Plan 5, subscription/invoice sections are Plan 4, and the new-tenant flow is Plan 6. Therefore Plan 2:
- **Redesigns:** the list and the drawer shell + Overview + Status + Plan + Limits.
- **Embeds unchanged (interim):** the existing `OwnerInvitesSection`, `SupportNotesSection`, `HealthSection` inside the new drawer (they render with legacy CSS until Plans 4/5 — accepted wrap-then-replace, same as Plan 1 wrapped legacy `TenantList` in the new shell).
- **Deletes:** `TenantList.tsx`, `TenantDetail.tsx`, `StatusControl.tsx`, `PlanControl.tsx`, `LimitsControl.tsx` (+ their tests).
- **Leaves untouched:** `NewTenant.tsx` (Plan 6), `OwnerInvitesSection.tsx`, `SupportNotesSection.tsx`, `HealthSection.tsx` (Plan 4/5), `SignIn`/`AcceptInvite`.

**Tech Stack:** React 19 + Vite + Tailwind v4 + Radix; TypeScript; Vitest (`globals:false`); i18n via `useI18n()` (param-less `t(key)`, RU primary + EN fallback, ru/en key-parity enforced by `messages.test.ts`).

**Build gate (non-negotiable):** `npm run build` (= `tsc -b && vite build`) AND `npm test` must both be green. `tsc -b` is the real type gate — vitest/esbuild skip type-checks. Run both after every task. All commands run from `D:\afk4.net\src\AFK4.Platform.Web`.

---

## Reference facts (verified ground truth — do not re-derive)

**`PlatformApiClient` methods (in `src/api/platformApi.ts`):**
- `listTenants(): Promise<TenantSummary[]>`
- `getTenant(organizationId: string): Promise<TenantDetail>`
- `updateStatus(organizationId: string, status: string, reason: string): Promise<TenantDetail>`
- `updatePlan(organizationId: string, planCode: string, subscriptionStatus: string): Promise<TenantDetail>`
- `updateLimits(organizationId: string, limits: CreateTenantRequest['limits']): Promise<TenantDetail>`  (`limits` is `TenantLimits | null`)

**Types (in `src/api/types.ts`):**
```typescript
interface TenantSummary { organizationId: string; slug: string; name: string; status: string; planCode: string; subscriptionStatus: string; branchCount: number; createdAtUtc: string; updatedAtUtc: string; }
interface TenantDetail { organizationId: string; slug: string; name: string; status: string; statusReason: string | null; statusChangedAtUtc: string | null; planCode: string; subscriptionStatus: string; limits: TenantLimits; branches: TenantBranch[]; createdAtUtc: string; updatedAtUtc: string; }
interface TenantLimits { maxBranches: number | null; maxDevicesPerBranch: number | null; maxConcurrentSessions: number | null; maxStaffUsersPerBranch: number | null; }
interface TenantBranch { branchId: string; slug: string; name: string; city: string; createdAtUtc: string; }
const TenantStatus = { Active: 'active', Suspended: 'suspended', DeletionPending: 'deletion_pending' } as const;
const TenantPlanCode = { Starter: 'starter', Growth: 'growth', Scale: 'scale' } as const;
const SubscriptionStatus = { Trial: 'trial', Active: 'active', PastDue: 'past_due', Cancelled: 'cancelled' } as const;
```

**Design-system primitives:**
- `@/components/ui/table` → `Table, TableHeader, TableBody, TableRow, TableHead, TableCell`. Clickable rows: `<TableRow data-clickable="true" onClick={...}>`.
- `@/components/ui/sheet` → `Sheet` (= Radix Root), `SheetContent` (prop `closeLabel?`), `SheetTitle`, `SheetDescription`. Pattern: `<Sheet open={sel!==null} onOpenChange={o=>{if(!o) onClose();}}><SheetContent closeLabel={t('common.close')}>…</SheetContent></Sheet>`.
- `@/components/ui/badge` → `Badge` variant ∈ `'default'|'secondary'|'success'|'destructive'|'outline'|'ghost'|'link'` (there is **no `warning`** variant). The variant type is NOT exported yet — Task 2 adds it.
- `@/components/ui/input` → `Input` (use `aria-label`).
- `@/components/ui/select` → `Select, SelectTrigger, SelectValue, SelectContent, SelectItem`. Pattern: `<Select value={v} onValueChange={setV}><SelectTrigger aria-label={…}><SelectValue/></SelectTrigger><SelectContent><SelectItem value="x">…</SelectItem></SelectContent></Select>`.
- `@/components/ui/button` → `Button` variant ∈ `'default'|'destructive'|'outline'|…`.
- `@/components/ui/states` → `LoadingCards({count})`, `ErrorState({message,retryLabel,onRetry})`, `EmptyState({message})`.
- `@/components/shared/ConfirmDialog` → props `{ open, title, description?, confirmLabel, cancelLabel, reasonLabel?, destructive?, pending, onConfirm:(reason)=>void, onOpenChange:(open)=>void }`.
- `@/components/ui/toast` → `useToast()` → `{ toast: ({title, variant?:'success'|'error'}) => void }`. Must be inside `ToastProvider` (already wraps the app; tests must wrap with it).
- i18n: `const { t, formatNumber, formatCurrency, formatDate } = useI18n();` — **`t(key)` takes NO params**; build dynamic strings by concatenation. There is **no `formatDateTime`** — `formatDate(iso)` already renders `dateStyle:'medium' + timeStyle:'short'`.

**Reusable existing i18n keys (do NOT redefine):** `common.close`, `state.error`, `state.retry`.

**App.tsx (verbatim current PlatformArea body + routing) is in this repo at `src/App.tsx:465-560` and `:727-732` (`isAdminRoute`). The `AdminRoute` union, `navigateToTenantList/NewTenant/TenantDetail`, `onOpenTenant/onBackToTenants/onCreatedTenant` callbacks already exist and stay unchanged.**

---

## File structure

```
src/platform/tenants/
├── tenantsModel.ts          NEW  pure list builder + status/plan/subscription badge-variant & label-key maps
├── tenantsModel.test.ts     NEW
├── useTenants.ts            NEW  list hook (listTenants → union)
├── useTenants.test.tsx      NEW
├── useTenantDetail.ts       NEW  single-tenant hook (getTenant → union + apply(next))
├── useTenantDetail.test.tsx NEW
├── TenantsTable.tsx         NEW  presentational table (rows, selectedId, onSelect)
├── TenantsTable.test.tsx    NEW
├── TenantStatusSection.tsx  NEW  status change via ConfirmDialog + toast
├── TenantStatusSection.test.tsx NEW
├── TenantPlanSection.tsx    NEW  plan + subscription change + toast
├── TenantPlanSection.test.tsx NEW
├── TenantLimitsSection.tsx  NEW  limits change + toast
├── TenantLimitsSection.test.tsx NEW
├── TenantDrawer.tsx         NEW  Sheet content: Overview + 3 sections + embedded legacy sections
├── TenantDrawer.test.tsx    NEW
├── TenantsScreen.tsx        NEW  list + search/filter + Sheet wiring
└── TenantsScreen.test.tsx   NEW

src/i18n/messages.ts         MODIFY  add platform.tenants.* / platform.tenant.* keys (ru + en)
src/App.tsx                  MODIFY  PlatformArea renders TenantsScreen for tenantList|tenantDetail; drop legacy imports/branches
src/App.test.tsx             MODIFY  update admin tenant-list assertions to new screen anchors
src/preview/DemoApp.tsx      MODIFY (untracked scratch — keep tsc green, do NOT commit)

DELETE: src/components/{TenantList,TenantDetail,StatusControl,PlanControl,LimitsControl}.tsx + matching *.test.tsx
```

---

## Task 1: i18n keys for the Tenants area

**Files:**
- Modify: `src/i18n/messages.ts` (RU block tail near line 161; EN block mirror)
- Test: `src/i18n/messages.test.ts` (existing parity test — must stay green)

- [ ] **Step 1: Add the keys to BOTH the `ru` and `en` blocks.** Append these to the RU block (after the existing `platform.overview.*` keys) and add the English mirror in the EN block. The EN block must carry the **identical key-set** (parity test enforces this).

RU values:
```typescript
  'platform.tenants.new': 'Новый тенант',
  'platform.tenants.search': 'Поиск по названию или ключу',
  'platform.tenants.filter.status': 'Статус',
  'platform.tenants.filter.plan': 'Тариф',
  'platform.tenants.filter.all': 'Все',
  'platform.tenants.empty': 'Тенанты не найдены.',
  'platform.tenants.col.name': 'Название',
  'platform.tenants.col.slug': 'Ключ',
  'platform.tenants.col.status': 'Статус',
  'platform.tenants.col.plan': 'Тариф',
  'platform.tenants.col.subscription': 'Подписка',
  'platform.tenants.col.branches': 'Филиалы',
  'platform.tenants.col.updated': 'Обновлён',
  'platform.tenant.status.active': 'Активен',
  'platform.tenant.status.suspended': 'Приостановлен',
  'platform.tenant.status.deletionPending': 'Ожидает удаления',
  'platform.tenant.subscription.trial': 'Пробный',
  'platform.tenant.subscription.active': 'Активна',
  'platform.tenant.subscription.pastDue': 'Просрочена',
  'platform.tenant.subscription.cancelled': 'Отменена',
  'platform.tenant.drawer.error': 'Не удалось загрузить тенанта',
  'platform.tenant.section.overview': 'Обзор',
  'platform.tenant.overview.slug': 'Ключ',
  'platform.tenant.overview.created': 'Создан',
  'platform.tenant.overview.updated': 'Обновлён',
  'platform.tenant.overview.branches': 'Филиалы',
  'platform.tenant.overview.statusReason': 'Причина статуса',
  'platform.tenant.section.status': 'Статус',
  'platform.tenant.statusForm.newStatus': 'Новый статус',
  'platform.tenant.statusForm.apply': 'Изменить статус',
  'platform.tenant.statusForm.confirmTitle': 'Изменить статус тенанта?',
  'platform.tenant.statusForm.reason': 'Причина',
  'platform.tenant.statusForm.confirm': 'Применить',
  'platform.tenant.statusForm.cancel': 'Отмена',
  'platform.tenant.statusForm.updated': 'Статус обновлён',
  'platform.tenant.section.plan': 'Тариф и подписка',
  'platform.tenant.planForm.plan': 'Тариф',
  'platform.tenant.planForm.subscription': 'Подписка',
  'platform.tenant.planForm.apply': 'Применить',
  'platform.tenant.planForm.updated': 'Тариф обновлён',
  'platform.tenant.section.limits': 'Лимиты',
  'platform.tenant.limitsForm.maxBranches': 'Макс. филиалов',
  'platform.tenant.limitsForm.maxDevices': 'Макс. устройств на филиал',
  'platform.tenant.limitsForm.maxSessions': 'Макс. одновременных сессий',
  'platform.tenant.limitsForm.maxStaff': 'Макс. сотрудников на филиал',
  'platform.tenant.limitsForm.apply': 'Применить лимиты',
  'platform.tenant.limitsForm.updated': 'Лимиты обновлены',
  'platform.tenant.action.error': 'Не удалось сохранить изменения',
```

EN values (same keys):
```typescript
  'platform.tenants.new': 'New tenant',
  'platform.tenants.search': 'Search by name or key',
  'platform.tenants.filter.status': 'Status',
  'platform.tenants.filter.plan': 'Plan',
  'platform.tenants.filter.all': 'All',
  'platform.tenants.empty': 'No tenants found.',
  'platform.tenants.col.name': 'Name',
  'platform.tenants.col.slug': 'Key',
  'platform.tenants.col.status': 'Status',
  'platform.tenants.col.plan': 'Plan',
  'platform.tenants.col.subscription': 'Subscription',
  'platform.tenants.col.branches': 'Branches',
  'platform.tenants.col.updated': 'Updated',
  'platform.tenant.status.active': 'Active',
  'platform.tenant.status.suspended': 'Suspended',
  'platform.tenant.status.deletionPending': 'Deletion pending',
  'platform.tenant.subscription.trial': 'Trial',
  'platform.tenant.subscription.active': 'Active',
  'platform.tenant.subscription.pastDue': 'Past due',
  'platform.tenant.subscription.cancelled': 'Cancelled',
  'platform.tenant.drawer.error': 'Failed to load tenant',
  'platform.tenant.section.overview': 'Overview',
  'platform.tenant.overview.slug': 'Key',
  'platform.tenant.overview.created': 'Created',
  'platform.tenant.overview.updated': 'Updated',
  'platform.tenant.overview.branches': 'Branches',
  'platform.tenant.overview.statusReason': 'Status reason',
  'platform.tenant.section.status': 'Status',
  'platform.tenant.statusForm.newStatus': 'New status',
  'platform.tenant.statusForm.apply': 'Change status',
  'platform.tenant.statusForm.confirmTitle': 'Change tenant status?',
  'platform.tenant.statusForm.reason': 'Reason',
  'platform.tenant.statusForm.confirm': 'Apply',
  'platform.tenant.statusForm.cancel': 'Cancel',
  'platform.tenant.statusForm.updated': 'Status updated',
  'platform.tenant.section.plan': 'Plan & subscription',
  'platform.tenant.planForm.plan': 'Plan',
  'platform.tenant.planForm.subscription': 'Subscription',
  'platform.tenant.planForm.apply': 'Apply',
  'platform.tenant.planForm.updated': 'Plan updated',
  'platform.tenant.section.limits': 'Limits',
  'platform.tenant.limitsForm.maxBranches': 'Max branches',
  'platform.tenant.limitsForm.maxDevices': 'Max devices per branch',
  'platform.tenant.limitsForm.maxSessions': 'Max concurrent sessions',
  'platform.tenant.limitsForm.maxStaff': 'Max staff per branch',
  'platform.tenant.limitsForm.apply': 'Apply limits',
  'platform.tenant.limitsForm.updated': 'Limits updated',
  'platform.tenant.action.error': 'Failed to save changes',
```

> Note: reuse the existing `nav.platform.tenants` ('Тенанты'/'Tenants') for the screen title — no new title key needed.

- [ ] **Step 2: Run the parity + build gates.**

Run: `npm test -- src/i18n/messages.test.ts` → Expected: PASS (ru/en key-sets identical).
Run: `npm run build` → Expected: exit 0.

- [ ] **Step 3: Commit.**
```bash
git add src/i18n/messages.ts
git commit -m "feat(platform): i18n keys for tenants list + detail drawer"
```

---

## Task 2: `tenantsModel.ts` — pure list builder + badge maps

**Files:**
- Create: `src/platform/tenants/tenantsModel.ts`
- Test: `src/platform/tenants/tenantsModel.test.ts`

- [ ] **Step 1: Write the failing test.**
```typescript
import { describe, expect, it } from 'vitest';
import { buildTenantRows, type TenantsFilter } from './tenantsModel';
import type { TenantSummary } from '@/api/types';

function tenant(over: Partial<TenantSummary>): TenantSummary {
  return {
    organizationId: 'o1', slug: 'acme', name: 'Acme', status: 'active',
    planCode: 'starter', subscriptionStatus: 'active', branchCount: 1,
    createdAtUtc: '2026-01-01T00:00:00Z', updatedAtUtc: '2026-01-01T00:00:00Z', ...over
  };
}
const ALL: TenantsFilter = { query: '', status: 'all', plan: 'all' };

describe('buildTenantRows', () => {
  it('returns all tenants sorted by updatedAtUtc descending', () => {
    const rows = buildTenantRows(
      [tenant({ organizationId: 'a', updatedAtUtc: '2026-01-01T00:00:00Z' }),
       tenant({ organizationId: 'b', updatedAtUtc: '2026-03-01T00:00:00Z' })],
      ALL
    );
    expect(rows.map(r => r.organizationId)).toEqual(['b', 'a']);
  });

  it('filters by query over name and slug (case-insensitive)', () => {
    const rows = buildTenantRows(
      [tenant({ organizationId: 'a', name: 'Globex', slug: 'globex' }),
       tenant({ organizationId: 'b', name: 'Acme', slug: 'acme-key' })],
      { ...ALL, query: 'ACME' }
    );
    expect(rows.map(r => r.organizationId)).toEqual(['b']);
  });

  it('filters by status and plan', () => {
    const rows = buildTenantRows(
      [tenant({ organizationId: 'a', status: 'suspended', planCode: 'scale' }),
       tenant({ organizationId: 'b', status: 'active', planCode: 'scale' })],
      { ...ALL, status: 'active', plan: 'scale' }
    );
    expect(rows.map(r => r.organizationId)).toEqual(['b']);
  });
});
```

- [ ] **Step 2: Run it to verify it fails.** Run: `npm test -- src/platform/tenants/tenantsModel.test.ts` → Expected: FAIL (module not found).

- [ ] **Step 3: Implement `tenantsModel.ts`.**
```typescript
import type { TenantSummary } from '@/api/types';
import type { MessageKey } from '@/i18n/messages';
import type { BadgeVariant } from '@/components/ui/badge';

export interface TenantRow {
  organizationId: string;
  name: string;
  slug: string;
  status: string;
  planCode: string;
  subscriptionStatus: string;
  branchCount: number;
  updatedAtUtc: string;
}

export interface TenantsFilter {
  query: string;
  status: string; // 'all' | TenantStatus value
  plan: string;   // 'all' | plan code
}

export function buildTenantRows(tenants: TenantSummary[], filter: TenantsFilter): TenantRow[] {
  const q = filter.query.trim().toLowerCase();
  return tenants
    .filter(t => filter.status === 'all' || t.status === filter.status)
    .filter(t => filter.plan === 'all' || t.planCode === filter.plan)
    .filter(t => q === '' || t.name.toLowerCase().includes(q) || t.slug.toLowerCase().includes(q))
    .map(t => ({
      organizationId: t.organizationId,
      name: t.name,
      slug: t.slug,
      status: t.status,
      planCode: t.planCode,
      subscriptionStatus: t.subscriptionStatus,
      branchCount: t.branchCount,
      updatedAtUtc: t.updatedAtUtc
    }))
    .sort((a, b) => b.updatedAtUtc.localeCompare(a.updatedAtUtc));
}

export const STATUS_VARIANT: Record<string, BadgeVariant> = {
  active: 'success',
  suspended: 'destructive',
  deletion_pending: 'outline'
};
export const STATUS_LABEL: Record<string, MessageKey> = {
  active: 'platform.tenant.status.active',
  suspended: 'platform.tenant.status.suspended',
  deletion_pending: 'platform.tenant.status.deletionPending'
};

export const SUBSCRIPTION_VARIANT: Record<string, BadgeVariant> = {
  active: 'success',
  trial: 'secondary',
  past_due: 'destructive',
  cancelled: 'outline'
};
export const SUBSCRIPTION_LABEL: Record<string, MessageKey> = {
  trial: 'platform.tenant.subscription.trial',
  active: 'platform.tenant.subscription.active',
  past_due: 'platform.tenant.subscription.pastDue',
  cancelled: 'platform.tenant.subscription.cancelled'
};

export const PLAN_LABEL: Record<string, MessageKey> = {
  starter: 'platform.plan.starter',
  growth: 'platform.plan.growth',
  scale: 'platform.plan.scale'
};

export const STATUS_OPTIONS = ['active', 'suspended', 'deletion_pending'] as const;
export const PLAN_OPTIONS = ['starter', 'growth', 'scale'] as const;
export const SUBSCRIPTION_OPTIONS = ['trial', 'active', 'past_due', 'cancelled'] as const;
```

> `badge.tsx` does NOT define a `BadgeVariant` type today (it uses `VariantProps<typeof badgeVariants>` inline). As part of this step, add one derived export to `src/components/ui/badge.tsx` and include it in the existing `export { ... }` block:
> ```typescript
> export type BadgeVariant = NonNullable<VariantProps<typeof badgeVariants>['variant']>;
> ```
> (`VariantProps` is already imported in `badge.tsx`.) The rest of `badge.tsx` is unchanged.

- [ ] **Step 4: Run tests to verify pass.** Run: `npm test -- src/platform/tenants/tenantsModel.test.ts` → Expected: PASS.
- [ ] **Step 5: Build gate.** Run: `npm run build` → Expected: exit 0.
- [ ] **Step 6: Commit.**
```bash
git add src/platform/tenants/tenantsModel.ts src/platform/tenants/tenantsModel.test.ts src/components/ui/badge.tsx
git commit -m "feat(platform): tenants list view-model + badge maps"
```

---

## Task 3: `useTenants.ts` — list hook

**Files:**
- Create: `src/platform/tenants/useTenants.ts`
- Test: `src/platform/tenants/useTenants.test.tsx`

- [ ] **Step 1: Write the failing test.**
```typescript
import { describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor, act } from '@testing-library/react';
import { useTenants } from './useTenants';
import type { TenantSummary } from '@/api/types';

function summary(over: Partial<TenantSummary>): TenantSummary {
  return {
    organizationId: 'o1', slug: 'acme', name: 'Acme', status: 'active', planCode: 'starter',
    subscriptionStatus: 'active', branchCount: 1, createdAtUtc: '2026-01-01T00:00:00Z',
    updatedAtUtc: '2026-01-01T00:00:00Z', ...over
  };
}
function fakeClient(over: Partial<Record<'listTenants', unknown>> = {}) {
  return { listTenants: vi.fn().mockResolvedValue([summary({})]), ...over } as never;
}

describe('useTenants', () => {
  it('reaches ready with tenant data', async () => {
    const { result } = renderHook(() => useTenants(fakeClient()));
    await waitFor(() => expect(result.current.status).toBe('ready'));
    if (result.current.status === 'ready') expect(result.current.data).toHaveLength(1);
  });

  it('reaches error and retry reloads', async () => {
    const client = fakeClient({ listTenants: vi.fn().mockRejectedValueOnce(new Error('boom')).mockResolvedValue([summary({})]) });
    const { result } = renderHook(() => useTenants(client));
    await waitFor(() => expect(result.current.status).toBe('error'));
    act(() => result.current.retry());
    await waitFor(() => expect(result.current.status).toBe('ready'));
  });
});
```

- [ ] **Step 2: Run to verify it fails.** Run: `npm test -- src/platform/tenants/useTenants.test.tsx` → Expected: FAIL.

- [ ] **Step 3: Implement `useTenants.ts`.**
```typescript
import { useCallback, useEffect, useRef, useState } from 'react';
import type { PlatformApiClient } from '@/api/platformApi';
import type { TenantSummary } from '@/api/types';

export type TenantsState =
  | { status: 'loading'; retry: () => void }
  | { status: 'error'; message: string; retry: () => void }
  | { status: 'ready'; data: TenantSummary[]; retry: () => void };

type Loadable = Pick<PlatformApiClient, 'listTenants'>;

export function useTenants(client: Loadable): TenantsState {
  const [tick, setTick] = useState(0);
  const [state, setState] = useState<{ status: 'loading' | 'error' | 'ready'; data?: TenantSummary[]; message?: string }>({ status: 'loading' });
  const retry = useCallback(() => setTick(t => t + 1), []);
  const clientRef = useRef(client);
  clientRef.current = client;

  useEffect(() => {
    let cancelled = false;
    setState({ status: 'loading' });
    clientRef.current.listTenants()
      .then(tenants => { if (!cancelled) setState({ status: 'ready', data: tenants }); })
      .catch((err: unknown) => { if (!cancelled) setState({ status: 'error', message: err instanceof Error ? err.message : 'error' }); });
    return () => { cancelled = true; };
  }, [tick]);

  if (state.status === 'ready') return { status: 'ready', data: state.data!, retry };
  if (state.status === 'error') return { status: 'error', message: state.message ?? 'error', retry };
  return { status: 'loading', retry };
}
```

- [ ] **Step 4: Run tests.** Run: `npm test -- src/platform/tenants/useTenants.test.tsx` → Expected: PASS.
- [ ] **Step 5: Build gate.** Run: `npm run build` → Expected: exit 0.
- [ ] **Step 6: Commit.**
```bash
git add src/platform/tenants/useTenants.ts src/platform/tenants/useTenants.test.tsx
git commit -m "feat(platform): useTenants list hook"
```

---

## Task 4: `useTenantDetail.ts` — single-tenant hook with `apply`

**Files:**
- Create: `src/platform/tenants/useTenantDetail.ts`
- Test: `src/platform/tenants/useTenantDetail.test.tsx`

- [ ] **Step 1: Write the failing test.**
```typescript
import { describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor, act } from '@testing-library/react';
import { useTenantDetail } from './useTenantDetail';
import type { TenantDetail } from '@/api/types';

function detail(over: Partial<TenantDetail>): TenantDetail {
  return {
    organizationId: 'o1', slug: 'acme', name: 'Acme', status: 'active', statusReason: null,
    statusChangedAtUtc: null, planCode: 'starter', subscriptionStatus: 'active',
    limits: { maxBranches: null, maxDevicesPerBranch: null, maxConcurrentSessions: null, maxStaffUsersPerBranch: null },
    branches: [], createdAtUtc: '2026-01-01T00:00:00Z', updatedAtUtc: '2026-01-01T00:00:00Z', ...over
  };
}
function fakeClient(over: Partial<Record<'getTenant', unknown>> = {}) {
  return { getTenant: vi.fn().mockResolvedValue(detail({})), ...over } as never;
}

describe('useTenantDetail', () => {
  it('reaches ready and apply swaps the detail in place', async () => {
    const { result } = renderHook(() => useTenantDetail(fakeClient(), 'o1'));
    await waitFor(() => expect(result.current.status).toBe('ready'));
    act(() => { if (result.current.status === 'ready') result.current.apply(detail({ name: 'Renamed' })); });
    if (result.current.status === 'ready') expect(result.current.data.name).toBe('Renamed');
  });

  it('reaches error and retry reloads', async () => {
    const client = fakeClient({ getTenant: vi.fn().mockRejectedValueOnce(new Error('boom')).mockResolvedValue(detail({})) });
    const { result } = renderHook(() => useTenantDetail(client, 'o1'));
    await waitFor(() => expect(result.current.status).toBe('error'));
    act(() => result.current.retry());
    await waitFor(() => expect(result.current.status).toBe('ready'));
  });
});
```

- [ ] **Step 2: Run to verify it fails.** Run: `npm test -- src/platform/tenants/useTenantDetail.test.tsx` → Expected: FAIL.

- [ ] **Step 3: Implement `useTenantDetail.ts`.**
```typescript
import { useCallback, useEffect, useRef, useState } from 'react';
import type { PlatformApiClient } from '@/api/platformApi';
import type { TenantDetail } from '@/api/types';

export type TenantDetailState =
  | { status: 'loading'; retry: () => void }
  | { status: 'error'; message: string; retry: () => void }
  | { status: 'ready'; data: TenantDetail; apply: (next: TenantDetail) => void; retry: () => void };

type Loadable = Pick<PlatformApiClient, 'getTenant'>;

export function useTenantDetail(client: Loadable, organizationId: string): TenantDetailState {
  const [tick, setTick] = useState(0);
  const [state, setState] = useState<{ status: 'loading' | 'error' | 'ready'; data?: TenantDetail; message?: string }>({ status: 'loading' });
  const retry = useCallback(() => setTick(t => t + 1), []);
  const apply = useCallback((next: TenantDetail) => setState({ status: 'ready', data: next }), []);
  const clientRef = useRef(client);
  clientRef.current = client;

  useEffect(() => {
    let cancelled = false;
    setState({ status: 'loading' });
    clientRef.current.getTenant(organizationId)
      .then(d => { if (!cancelled) setState({ status: 'ready', data: d }); })
      .catch((err: unknown) => { if (!cancelled) setState({ status: 'error', message: err instanceof Error ? err.message : 'error' }); });
    return () => { cancelled = true; };
  }, [organizationId, tick]);

  if (state.status === 'ready') return { status: 'ready', data: state.data!, apply, retry };
  if (state.status === 'error') return { status: 'error', message: state.message ?? 'error', retry };
  return { status: 'loading', retry };
}
```

- [ ] **Step 4: Run tests.** Run: `npm test -- src/platform/tenants/useTenantDetail.test.tsx` → Expected: PASS.
- [ ] **Step 5: Build gate.** Run: `npm run build` → Expected: exit 0.
- [ ] **Step 6: Commit.**
```bash
git add src/platform/tenants/useTenantDetail.ts src/platform/tenants/useTenantDetail.test.tsx
git commit -m "feat(platform): useTenantDetail hook with in-place apply"
```

---

## Task 5: `TenantsTable.tsx` — presentational table

**Files:**
- Create: `src/platform/tenants/TenantsTable.tsx`
- Test: `src/platform/tenants/TenantsTable.test.tsx`

- [ ] **Step 1: Write the failing test.**
```typescript
import { render, screen, fireEvent } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { TenantsTable } from './TenantsTable';
import type { TenantRow } from './tenantsModel';

const rows: TenantRow[] = [{
  organizationId: 'o1', name: 'Acme', slug: 'acme', status: 'active',
  planCode: 'starter', subscriptionStatus: 'active', branchCount: 2, updatedAtUtc: '2026-01-01T00:00:00Z'
}];

it('renders rows and fires onSelect on row click', () => {
  const onSelect = vi.fn();
  render(<I18nProvider><TenantsTable rows={rows} selectedId={null} emptyMessage="none" onSelect={onSelect} /></I18nProvider>);
  fireEvent.click(screen.getByText('Acme'));
  expect(onSelect).toHaveBeenCalledWith('o1');
});

it('shows the empty message when there are no rows', () => {
  render(<I18nProvider><TenantsTable rows={[]} selectedId={null} emptyMessage="No tenants found." onSelect={() => {}} /></I18nProvider>);
  expect(screen.getByText('No tenants found.')).toBeInTheDocument();
});
```

- [ ] **Step 2: Run to verify it fails.** Run: `npm test -- src/platform/tenants/TenantsTable.test.tsx` → Expected: FAIL.

- [ ] **Step 3: Implement `TenantsTable.tsx`.**
```typescript
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { EmptyState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { TenantRow } from './tenantsModel';
import { STATUS_VARIANT, STATUS_LABEL, SUBSCRIPTION_VARIANT, SUBSCRIPTION_LABEL, PLAN_LABEL } from './tenantsModel';
import type { MessageKey } from '@/i18n/messages';

interface TenantsTableProps {
  rows: TenantRow[];
  selectedId: string | null;
  emptyMessage: string;
  onSelect: (organizationId: string) => void;
}

export function TenantsTable({ rows, selectedId, emptyMessage, onSelect }: TenantsTableProps) {
  const { t, formatNumber, formatDate } = useI18n();
  if (rows.length === 0) return <EmptyState message={emptyMessage} />;
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t('platform.tenants.col.name')}</TableHead>
          <TableHead>{t('platform.tenants.col.slug')}</TableHead>
          <TableHead>{t('platform.tenants.col.status')}</TableHead>
          <TableHead>{t('platform.tenants.col.plan')}</TableHead>
          <TableHead>{t('platform.tenants.col.subscription')}</TableHead>
          <TableHead>{t('platform.tenants.col.branches')}</TableHead>
          <TableHead>{t('platform.tenants.col.updated')}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {rows.map(row => (
          <TableRow
            key={row.organizationId}
            data-clickable="true"
            data-selected={row.organizationId === selectedId ? 'true' : undefined}
            onClick={() => onSelect(row.organizationId)}
          >
            <TableCell>{row.name}</TableCell>
            <TableCell><code>{row.slug}</code></TableCell>
            <TableCell>
              <Badge variant={STATUS_VARIANT[row.status] ?? 'secondary'}>
                {t((STATUS_LABEL[row.status] ?? 'platform.tenant.status.active') as MessageKey)}
              </Badge>
            </TableCell>
            <TableCell>{t((PLAN_LABEL[row.planCode] ?? 'platform.plan.starter') as MessageKey)}</TableCell>
            <TableCell>
              <Badge variant={SUBSCRIPTION_VARIANT[row.subscriptionStatus] ?? 'secondary'}>
                {t((SUBSCRIPTION_LABEL[row.subscriptionStatus] ?? 'platform.tenant.subscription.active') as MessageKey)}
              </Badge>
            </TableCell>
            <TableCell>{formatNumber(row.branchCount)}</TableCell>
            <TableCell>{formatDate(row.updatedAtUtc)}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}
```

> If `row.status`/`planCode`/`subscriptionStatus` is an unknown value, the `?? 'secondary'` variant and the `?? <known key>` label fall back gracefully (raw codes should not appear, but never crash). Keep the fallbacks.

- [ ] **Step 4: Run tests.** Run: `npm test -- src/platform/tenants/TenantsTable.test.tsx` → Expected: PASS.
- [ ] **Step 5: Build gate.** Run: `npm run build` → Expected: exit 0.
- [ ] **Step 6: Commit.**
```bash
git add src/platform/tenants/TenantsTable.tsx src/platform/tenants/TenantsTable.test.tsx
git commit -m "feat(platform): TenantsTable presentational component"
```

---

## Task 6: `TenantStatusSection.tsx` — status change via ConfirmDialog

**Files:**
- Create: `src/platform/tenants/TenantStatusSection.tsx`
- Test: `src/platform/tenants/TenantStatusSection.test.tsx`

**Behavior:** Select a new status. Clicking "Change status" opens `ConfirmDialog`. The dialog shows a reason field (`reasonLabel`) whenever the chosen status is NOT `active` (matches legacy: reason required for non-active). On confirm, call `client.updateStatus(orgId, status, reason)`, then `onUpdated(next)` + success toast. On failure, error toast; dialog stays open with `pending=false`.

- [ ] **Step 1: Write the failing test.**
```typescript
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { TenantStatusSection } from './TenantStatusSection';
import type { TenantDetail } from '@/api/types';

function detail(over: Partial<TenantDetail>): TenantDetail {
  return {
    organizationId: 'o1', slug: 'acme', name: 'Acme', status: 'active', statusReason: null,
    statusChangedAtUtc: null, planCode: 'starter', subscriptionStatus: 'active',
    limits: { maxBranches: null, maxDevicesPerBranch: null, maxConcurrentSessions: null, maxStaffUsersPerBranch: null },
    branches: [], createdAtUtc: '2026-01-01T00:00:00Z', updatedAtUtc: '2026-01-01T00:00:00Z', ...over
  };
}

it('confirms a suspend and calls updateStatus then onUpdated', async () => {
  const next = detail({ status: 'suspended' });
  const client = { updateStatus: vi.fn().mockResolvedValue(next) } as never;
  const onUpdated = vi.fn();
  render(
    <I18nProvider><ToastProvider>
      <TenantStatusSection client={client} tenant={detail({})} onUpdated={onUpdated} />
    </ToastProvider></I18nProvider>
  );
  // choose 'suspended' via the native-less Select: open + pick
  fireEvent.click(screen.getByLabelText('Новый статус'));
  fireEvent.click(await screen.findByText('Приостановлен'));
  fireEvent.click(screen.getByRole('button', { name: 'Изменить статус' }));
  fireEvent.click(await screen.findByRole('button', { name: 'Применить' }));
  await waitFor(() => expect(client.updateStatus).toHaveBeenCalledWith('o1', 'suspended', ''));
  expect(onUpdated).toHaveBeenCalledWith(next);
});
```

> The Radix Select interaction in jsdom may need `fireEvent.pointerDown`/`keyboard` rather than `click` — match whatever pattern the existing club tests use for `Select` (see `src/club/venue/DeviceDrawer.test.tsx`). If a robust Select interaction is impractical in jsdom, assert via the default selected value path instead (e.g. confirm with the initial status) — but still exercise the ConfirmDialog → updateStatus → onUpdated chain. The non-negotiable assertions are: ConfirmDialog opens, `updateStatus` is called, `onUpdated` fires.

- [ ] **Step 2: Run to verify it fails.** Run: `npm test -- src/platform/tenants/TenantStatusSection.test.tsx` → Expected: FAIL.

- [ ] **Step 3: Implement `TenantStatusSection.tsx`.**
```typescript
import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select';
import { ConfirmDialog } from '@/components/shared/ConfirmDialog';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { PlatformApiClient } from '@/api/platformApi';
import type { TenantDetail } from '@/api/types';
import { STATUS_OPTIONS, STATUS_LABEL } from './tenantsModel';

type Updater = Pick<PlatformApiClient, 'updateStatus'>;

interface Props {
  client: Updater;
  tenant: TenantDetail;
  onUpdated: (next: TenantDetail) => void;
}

export function TenantStatusSection({ client, tenant, onUpdated }: Props) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [status, setStatus] = useState(tenant.status);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [pending, setPending] = useState(false);

  const requiresReason = status !== 'active';

  async function submit(reason: string) {
    setPending(true);
    try {
      const next = await client.updateStatus(tenant.organizationId, status, reason);
      onUpdated(next);
      setConfirmOpen(false);
      toast({ title: t('platform.tenant.statusForm.updated'), variant: 'success' });
    } catch {
      toast({ title: t('platform.tenant.action.error'), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  return (
    <Card>
      <CardHeader><CardTitle>{t('platform.tenant.section.status')}</CardTitle></CardHeader>
      <CardContent className="flex flex-col gap-3">
        <label className="block text-sm">
          <span className="mb-1 block text-muted-foreground">{t('platform.tenant.statusForm.newStatus')}</span>
          <Select value={status} onValueChange={setStatus}>
            <SelectTrigger aria-label={t('platform.tenant.statusForm.newStatus')}><SelectValue /></SelectTrigger>
            <SelectContent>
              {STATUS_OPTIONS.map(s => <SelectItem key={s} value={s}>{t(STATUS_LABEL[s])}</SelectItem>)}
            </SelectContent>
          </Select>
        </label>
        <div>
          <Button onClick={() => setConfirmOpen(true)} disabled={status === tenant.status}>
            {t('platform.tenant.statusForm.apply')}
          </Button>
        </div>
      </CardContent>
      <ConfirmDialog
        open={confirmOpen}
        title={t('platform.tenant.statusForm.confirmTitle')}
        confirmLabel={t('platform.tenant.statusForm.confirm')}
        cancelLabel={t('platform.tenant.statusForm.cancel')}
        reasonLabel={requiresReason ? t('platform.tenant.statusForm.reason') : undefined}
        destructive={requiresReason}
        pending={pending}
        onConfirm={reason => void submit(reason)}
        onOpenChange={open => { if (!open) setConfirmOpen(false); }}
      />
    </Card>
  );
}
```

> Verify the exact `Card`/`CardHeader`/`CardTitle`/`CardContent` export names from `@/components/ui/card` before writing (they are used across `src/club/*`). If `CardTitle`/`CardHeader` don't exist, use the card shape used by `OverviewScreen.tsx` instead. Do not invent component names.

- [ ] **Step 4: Run tests.** Run: `npm test -- src/platform/tenants/TenantStatusSection.test.tsx` → Expected: PASS.
- [ ] **Step 5: Build gate.** Run: `npm run build` → Expected: exit 0.
- [ ] **Step 6: Commit.**
```bash
git add src/platform/tenants/TenantStatusSection.tsx src/platform/tenants/TenantStatusSection.test.tsx
git commit -m "feat(platform): tenant status section with confirm + toast"
```

---

## Task 7: `TenantPlanSection.tsx` — plan + subscription change

**Files:**
- Create: `src/platform/tenants/TenantPlanSection.tsx`
- Test: `src/platform/tenants/TenantPlanSection.test.tsx`

**Behavior:** Two `Select`s (plan, subscription). "Apply" calls `client.updatePlan(orgId, planCode, subscriptionStatus)` → `onUpdated(next)` + toast. No ConfirmDialog (matches legacy `PlanControl`, which applied directly).

- [ ] **Step 1: Write the failing test.**
```typescript
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { TenantPlanSection } from './TenantPlanSection';
import type { TenantDetail } from '@/api/types';

function detail(over: Partial<TenantDetail>): TenantDetail {
  return {
    organizationId: 'o1', slug: 'acme', name: 'Acme', status: 'active', statusReason: null,
    statusChangedAtUtc: null, planCode: 'starter', subscriptionStatus: 'active',
    limits: { maxBranches: null, maxDevicesPerBranch: null, maxConcurrentSessions: null, maxStaffUsersPerBranch: null },
    branches: [], createdAtUtc: '2026-01-01T00:00:00Z', updatedAtUtc: '2026-01-01T00:00:00Z', ...over
  };
}

it('applies plan change and calls onUpdated', async () => {
  const next = detail({ planCode: 'scale' });
  const client = { updatePlan: vi.fn().mockResolvedValue(next) } as never;
  const onUpdated = vi.fn();
  render(
    <I18nProvider><ToastProvider>
      <TenantPlanSection client={client} tenant={detail({})} onUpdated={onUpdated} />
    </ToastProvider></I18nProvider>
  );
  fireEvent.click(screen.getByRole('button', { name: 'Применить' }));
  await waitFor(() => expect(client.updatePlan).toHaveBeenCalledWith('o1', 'starter', 'active'));
  expect(onUpdated).toHaveBeenCalledWith(next);
});
```

- [ ] **Step 2: Run to verify it fails.** Run: `npm test -- src/platform/tenants/TenantPlanSection.test.tsx` → Expected: FAIL.

- [ ] **Step 3: Implement `TenantPlanSection.tsx`.**
```typescript
import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { PlatformApiClient } from '@/api/platformApi';
import type { TenantDetail } from '@/api/types';
import { PLAN_OPTIONS, PLAN_LABEL, SUBSCRIPTION_OPTIONS, SUBSCRIPTION_LABEL } from './tenantsModel';

type Updater = Pick<PlatformApiClient, 'updatePlan'>;

interface Props {
  client: Updater;
  tenant: TenantDetail;
  onUpdated: (next: TenantDetail) => void;
}

export function TenantPlanSection({ client, tenant, onUpdated }: Props) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [planCode, setPlanCode] = useState(tenant.planCode);
  const [subscription, setSubscription] = useState(tenant.subscriptionStatus);
  const [pending, setPending] = useState(false);

  async function submit() {
    setPending(true);
    try {
      const next = await client.updatePlan(tenant.organizationId, planCode, subscription);
      onUpdated(next);
      toast({ title: t('platform.tenant.planForm.updated'), variant: 'success' });
    } catch {
      toast({ title: t('platform.tenant.action.error'), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  return (
    <Card>
      <CardHeader><CardTitle>{t('platform.tenant.section.plan')}</CardTitle></CardHeader>
      <CardContent className="flex flex-col gap-3">
        <label className="block text-sm">
          <span className="mb-1 block text-muted-foreground">{t('platform.tenant.planForm.plan')}</span>
          <Select value={planCode} onValueChange={setPlanCode}>
            <SelectTrigger aria-label={t('platform.tenant.planForm.plan')}><SelectValue /></SelectTrigger>
            <SelectContent>
              {PLAN_OPTIONS.map(p => <SelectItem key={p} value={p}>{t(PLAN_LABEL[p])}</SelectItem>)}
            </SelectContent>
          </Select>
        </label>
        <label className="block text-sm">
          <span className="mb-1 block text-muted-foreground">{t('platform.tenant.planForm.subscription')}</span>
          <Select value={subscription} onValueChange={setSubscription}>
            <SelectTrigger aria-label={t('platform.tenant.planForm.subscription')}><SelectValue /></SelectTrigger>
            <SelectContent>
              {SUBSCRIPTION_OPTIONS.map(s => <SelectItem key={s} value={s}>{t(SUBSCRIPTION_LABEL[s])}</SelectItem>)}
            </SelectContent>
          </Select>
        </label>
        <div>
          <Button onClick={() => void submit()} disabled={pending}>{t('platform.tenant.planForm.apply')}</Button>
        </div>
      </CardContent>
    </Card>
  );
}
```

- [ ] **Step 4: Run tests.** Run: `npm test -- src/platform/tenants/TenantPlanSection.test.tsx` → Expected: PASS.
- [ ] **Step 5: Build gate.** Run: `npm run build` → Expected: exit 0.
- [ ] **Step 6: Commit.**
```bash
git add src/platform/tenants/TenantPlanSection.tsx src/platform/tenants/TenantPlanSection.test.tsx
git commit -m "feat(platform): tenant plan & subscription section"
```

---

## Task 8: `TenantLimitsSection.tsx` — limits change

**Files:**
- Create: `src/platform/tenants/TenantLimitsSection.tsx`
- Test: `src/platform/tenants/TenantLimitsSection.test.tsx`

**Behavior:** Four number inputs (blank = `null`). "Apply limits" calls `client.updateLimits(orgId, limits)` with `TenantLimits` (`null` for blanks) → `onUpdated(next)` + toast.

- [ ] **Step 1: Write the failing test.**
```typescript
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { TenantLimitsSection } from './TenantLimitsSection';
import type { TenantDetail } from '@/api/types';

function detail(over: Partial<TenantDetail>): TenantDetail {
  return {
    organizationId: 'o1', slug: 'acme', name: 'Acme', status: 'active', statusReason: null,
    statusChangedAtUtc: null, planCode: 'starter', subscriptionStatus: 'active',
    limits: { maxBranches: 3, maxDevicesPerBranch: null, maxConcurrentSessions: null, maxStaffUsersPerBranch: null },
    branches: [], createdAtUtc: '2026-01-01T00:00:00Z', updatedAtUtc: '2026-01-01T00:00:00Z', ...over
  };
}

it('submits limits with blanks coerced to null', async () => {
  const client = { updateLimits: vi.fn().mockResolvedValue(detail({})) } as never;
  const onUpdated = vi.fn();
  render(
    <I18nProvider><ToastProvider>
      <TenantLimitsSection client={client} tenant={detail({})} onUpdated={onUpdated} />
    </ToastProvider></I18nProvider>
  );
  fireEvent.click(screen.getByRole('button', { name: 'Применить лимиты' }));
  await waitFor(() => expect(client.updateLimits).toHaveBeenCalledWith('o1', {
    maxBranches: 3, maxDevicesPerBranch: null, maxConcurrentSessions: null, maxStaffUsersPerBranch: null
  }));
  expect(onUpdated).toHaveBeenCalled();
});
```

- [ ] **Step 2: Run to verify it fails.** Run: `npm test -- src/platform/tenants/TenantLimitsSection.test.tsx` → Expected: FAIL.

- [ ] **Step 3: Implement `TenantLimitsSection.tsx`.**
```typescript
import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { PlatformApiClient } from '@/api/platformApi';
import type { TenantDetail, TenantLimits } from '@/api/types';

type Updater = Pick<PlatformApiClient, 'updateLimits'>;

interface Props {
  client: Updater;
  tenant: TenantDetail;
  onUpdated: (next: TenantDetail) => void;
}

function toField(value: number | null): string {
  return value === null ? '' : String(value);
}
function toLimit(value: string): number | null {
  const trimmed = value.trim();
  if (trimmed === '') return null;
  const n = Number(trimmed);
  return Number.isFinite(n) ? n : null;
}

export function TenantLimitsSection({ client, tenant, onUpdated }: Props) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [maxBranches, setMaxBranches] = useState(toField(tenant.limits.maxBranches));
  const [maxDevices, setMaxDevices] = useState(toField(tenant.limits.maxDevicesPerBranch));
  const [maxSessions, setMaxSessions] = useState(toField(tenant.limits.maxConcurrentSessions));
  const [maxStaff, setMaxStaff] = useState(toField(tenant.limits.maxStaffUsersPerBranch));
  const [pending, setPending] = useState(false);

  async function submit() {
    setPending(true);
    const limits: TenantLimits = {
      maxBranches: toLimit(maxBranches),
      maxDevicesPerBranch: toLimit(maxDevices),
      maxConcurrentSessions: toLimit(maxSessions),
      maxStaffUsersPerBranch: toLimit(maxStaff)
    };
    try {
      const next = await client.updateLimits(tenant.organizationId, limits);
      onUpdated(next);
      toast({ title: t('platform.tenant.limitsForm.updated'), variant: 'success' });
    } catch {
      toast({ title: t('platform.tenant.action.error'), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  const field = (label: string, value: string, set: (v: string) => void) => (
    <label className="block text-sm">
      <span className="mb-1 block text-muted-foreground">{label}</span>
      <Input type="number" inputMode="numeric" aria-label={label} value={value} onChange={e => set(e.target.value)} />
    </label>
  );

  return (
    <Card>
      <CardHeader><CardTitle>{t('platform.tenant.section.limits')}</CardTitle></CardHeader>
      <CardContent className="flex flex-col gap-3">
        {field(t('platform.tenant.limitsForm.maxBranches'), maxBranches, setMaxBranches)}
        {field(t('platform.tenant.limitsForm.maxDevices'), maxDevices, setMaxDevices)}
        {field(t('platform.tenant.limitsForm.maxSessions'), maxSessions, setMaxSessions)}
        {field(t('platform.tenant.limitsForm.maxStaff'), maxStaff, setMaxStaff)}
        <div>
          <Button onClick={() => void submit()} disabled={pending}>{t('platform.tenant.limitsForm.apply')}</Button>
        </div>
      </CardContent>
    </Card>
  );
}
```

- [ ] **Step 4: Run tests.** Run: `npm test -- src/platform/tenants/TenantLimitsSection.test.tsx` → Expected: PASS.
- [ ] **Step 5: Build gate.** Run: `npm run build` → Expected: exit 0.
- [ ] **Step 6: Commit.**
```bash
git add src/platform/tenants/TenantLimitsSection.tsx src/platform/tenants/TenantLimitsSection.test.tsx
git commit -m "feat(platform): tenant limits section"
```

---

## Task 9: `TenantDrawer.tsx` — drawer body composing detail + sections

**Files:**
- Create: `src/platform/tenants/TenantDrawer.tsx`
- Test: `src/platform/tenants/TenantDrawer.test.tsx`

**Behavior:** Given `organizationId`, loads detail via `useTenantDetail`. Renders loading/error/ready. Ready: an Overview card (slug, created, updated, branches list, statusReason if present) + `TenantStatusSection` + `TenantPlanSection` + `TenantLimitsSection`, then the **embedded legacy** `OwnerInvitesSection` / `SupportNotesSection` / `HealthSection`. Every section's `onUpdated` calls the hook's `apply(next)` AND `onChanged()` (so the parent list can refresh). The full `PlatformApiClient` is passed (legacy sections need many methods).

- [ ] **Step 1: Write the failing test.**
```typescript
import { render, screen, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { TenantDrawer } from './TenantDrawer';
import type { TenantDetail } from '@/api/types';

function detail(over: Partial<TenantDetail>): TenantDetail {
  return {
    organizationId: 'o1', slug: 'acme', name: 'Acme', status: 'active', statusReason: null,
    statusChangedAtUtc: null, planCode: 'starter', subscriptionStatus: 'active',
    limits: { maxBranches: null, maxDevicesPerBranch: null, maxConcurrentSessions: null, maxStaffUsersPerBranch: null },
    branches: [], createdAtUtc: '2026-01-01T00:00:00Z', updatedAtUtc: '2026-01-01T00:00:00Z', ...over
  };
}
function client() {
  return {
    getTenant: vi.fn().mockResolvedValue(detail({})),
    updateStatus: vi.fn(), updatePlan: vi.fn(), updateLimits: vi.fn(),
    listOwnerInvites: vi.fn().mockResolvedValue([]),
    listSupportNotes: vi.fn().mockResolvedValue([]),
    getHealth: vi.fn().mockResolvedValue({
      organizationId: 'o1', status: 'active', branchCount: 0, deviceCount: 0, activeStaffUserCount: 0,
      latestStaffSignInAtUtc: null, latestMigration: null, recentErrorCount: 0, recentErrors: []
    })
  } as never;
}

it('loads the tenant and renders the section headers', async () => {
  render(
    <I18nProvider><ToastProvider>
      <TenantDrawer client={client()} organizationId="o1" initialInvite={null} onChanged={() => {}} />
    </ToastProvider></I18nProvider>
  );
  await waitFor(() => expect(screen.getByText('Тариф и подписка')).toBeInTheDocument());
  expect(screen.getByText('Лимиты')).toBeInTheDocument();
});

it('shows an error state when the tenant fails to load', async () => {
  const c = { ...client(), getTenant: vi.fn().mockRejectedValue(new Error('boom')) } as never;
  render(
    <I18nProvider><ToastProvider>
      <TenantDrawer client={c} organizationId="o1" initialInvite={null} onChanged={() => {}} />
    </ToastProvider></I18nProvider>
  );
  await waitFor(() => expect(screen.getByText('Не удалось загрузить тенанта')).toBeInTheDocument());
});
```

- [ ] **Step 2: Run to verify it fails.** Run: `npm test -- src/platform/tenants/TenantDrawer.test.tsx` → Expected: FAIL.

- [ ] **Step 3: Implement `TenantDrawer.tsx`.**
```typescript
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { LoadingCards, ErrorState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { PlatformApiClient } from '@/api/platformApi';
import type { OwnerInvite, TenantDetail } from '@/api/types';
import { useTenantDetail } from './useTenantDetail';
import { TenantStatusSection } from './TenantStatusSection';
import { TenantPlanSection } from './TenantPlanSection';
import { TenantLimitsSection } from './TenantLimitsSection';
import { OwnerInvitesSection } from '@/components/OwnerInvitesSection';
import { SupportNotesSection } from '@/components/SupportNotesSection';
import { HealthSection } from '@/components/HealthSection';

interface TenantDrawerProps {
  client: PlatformApiClient;
  organizationId: string;
  initialInvite: OwnerInvite | null;
  onChanged: () => void;
}

export function TenantDrawer({ client, organizationId, initialInvite, onChanged }: TenantDrawerProps) {
  const { t, formatDate } = useI18n();
  const state = useTenantDetail(client, organizationId);

  if (state.status === 'loading') return <LoadingCards count={3} />;
  if (state.status === 'error') {
    return <ErrorState message={t('platform.tenant.drawer.error')} retryLabel={t('state.retry')} onRetry={state.retry} />;
  }

  const tenant: TenantDetail = state.data;
  const handleUpdated = (next: TenantDetail) => { state.apply(next); onChanged(); };

  return (
    <div className="flex flex-col gap-4 overflow-y-auto">
      <Card>
        <CardHeader><CardTitle>{t('platform.tenant.section.overview')}</CardTitle></CardHeader>
        <CardContent className="flex flex-col gap-2 text-sm">
          <Field label={t('platform.tenant.overview.slug')}><code>{tenant.slug}</code></Field>
          <Field label={t('platform.tenant.overview.created')}>{formatDate(tenant.createdAtUtc)}</Field>
          <Field label={t('platform.tenant.overview.updated')}>{formatDate(tenant.updatedAtUtc)}</Field>
          <Field label={t('platform.tenant.overview.branches')}>
            {tenant.branches.length === 0 ? '—' : tenant.branches.map(b => b.name).join(', ')}
          </Field>
          {tenant.statusReason !== null && (
            <Field label={t('platform.tenant.overview.statusReason')}>{tenant.statusReason}</Field>
          )}
        </CardContent>
      </Card>

      <TenantStatusSection client={client} tenant={tenant} onUpdated={handleUpdated} />
      <TenantPlanSection client={client} tenant={tenant} onUpdated={handleUpdated} />
      <TenantLimitsSection client={client} tenant={tenant} onUpdated={handleUpdated} />

      {/* Interim: legacy sections embedded unchanged until Plans 4/5 redesign them. */}
      <OwnerInvitesSection client={client} organizationId={tenant.organizationId} branches={tenant.branches} initialInvite={initialInvite} />
      <SupportNotesSection client={client} organizationId={tenant.organizationId} />
      <HealthSection client={client} organizationId={tenant.organizationId} />
    </div>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex justify-between gap-3">
      <span className="text-muted-foreground">{label}</span>
      <span className="text-right">{children}</span>
    </div>
  );
}
```

> Confirm the exact `OwnerInvitesSection`/`SupportNotesSection`/`HealthSection` prop names against their files before wiring (they were verified as: OwnerInvites `{ client, organizationId, branches, initialInvite? }`; SupportNotes `{ client, organizationId }`; Health `{ client, organizationId }`). If `React` is not auto-imported, add `import type { ReactNode } from 'react';` and use `ReactNode` instead of `React.ReactNode`.

- [ ] **Step 4: Run tests.** Run: `npm test -- src/platform/tenants/TenantDrawer.test.tsx` → Expected: PASS.
- [ ] **Step 5: Build gate.** Run: `npm run build` → Expected: exit 0.
- [ ] **Step 6: Commit.**
```bash
git add src/platform/tenants/TenantDrawer.tsx src/platform/tenants/TenantDrawer.test.tsx
git commit -m "feat(platform): tenant drawer composing detail + sections"
```

---

## Task 10: `TenantsScreen.tsx` — list + search/filter + Sheet wiring

**Files:**
- Create: `src/platform/tenants/TenantsScreen.tsx`
- Test: `src/platform/tenants/TenantsScreen.test.tsx`

**Behavior:** Loads via `useTenants`. Renders a header row (search `Input` + status `Select` + plan `Select` + "New tenant" `Button`), then `TenantsTable` of `buildTenantRows(...)`. A `Sheet` is open iff `selectedTenantId !== null`; its `SheetContent` shows `SheetTitle` (selected tenant name) + `TenantDrawer`. Row click → `onOpenTenant(id)`. Sheet close → `onCloseTenant()`. The drawer's `onChanged` triggers `listState.retry()`.

- [ ] **Step 1: Write the failing test.**
```typescript
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { TenantsScreen } from './TenantsScreen';
import type { TenantSummary } from '@/api/types';

function summary(over: Partial<TenantSummary>): TenantSummary {
  return {
    organizationId: 'o1', slug: 'acme', name: 'Acme', status: 'active', planCode: 'starter',
    subscriptionStatus: 'active', branchCount: 1, createdAtUtc: '2026-01-01T00:00:00Z',
    updatedAtUtc: '2026-01-01T00:00:00Z', ...over
  };
}
function client(over: Record<string, unknown> = {}) {
  return {
    listTenants: vi.fn().mockResolvedValue([summary({ organizationId: 'o1', name: 'Acme' }), summary({ organizationId: 'o2', name: 'Globex', slug: 'globex' })]),
    getTenant: vi.fn(), updateStatus: vi.fn(), updatePlan: vi.fn(), updateLimits: vi.fn(),
    listOwnerInvites: vi.fn().mockResolvedValue([]), listSupportNotes: vi.fn().mockResolvedValue([]),
    getHealth: vi.fn().mockResolvedValue({ organizationId: 'o1', status: 'active', branchCount: 0, deviceCount: 0, activeStaffUserCount: 0, latestStaffSignInAtUtc: null, latestMigration: null, recentErrorCount: 0, recentErrors: [] }),
    ...over
  } as never;
}

function setup(props: Partial<Parameters<typeof TenantsScreen>[0]> = {}) {
  return render(
    <I18nProvider><ToastProvider>
      <TenantsScreen client={client()} selectedTenantId={null} initialInvite={null}
        onOpenTenant={() => {}} onCloseTenant={() => {}} onCreateTenant={() => {}} {...props} />
    </ToastProvider></I18nProvider>
  );
}

it('renders the tenant rows', async () => {
  setup();
  await waitFor(() => expect(screen.getByText('Acme')).toBeInTheDocument());
  expect(screen.getByText('Globex')).toBeInTheDocument();
});

it('filters rows by the search box', async () => {
  setup();
  await waitFor(() => expect(screen.getByText('Globex')).toBeInTheDocument());
  fireEvent.change(screen.getByLabelText('Поиск по названию или ключу'), { target: { value: 'globex' } });
  expect(screen.queryByText('Acme')).not.toBeInTheDocument();
  expect(screen.getByText('Globex')).toBeInTheDocument();
});

it('fires onOpenTenant when a row is clicked', async () => {
  const onOpenTenant = vi.fn();
  setup({ onOpenTenant });
  await waitFor(() => expect(screen.getByText('Acme')).toBeInTheDocument());
  fireEvent.click(screen.getByText('Acme'));
  expect(onOpenTenant).toHaveBeenCalledWith('o1');
});

it('opens the drawer when selectedTenantId is set', async () => {
  setup({ selectedTenantId: 'o1' });
  // drawer loads detail and shows a section header
  await waitFor(() => expect(screen.getByText('Тариф и подписка')).toBeInTheDocument());
});
```

- [ ] **Step 2: Run to verify it fails.** Run: `npm test -- src/platform/tenants/TenantsScreen.test.tsx` → Expected: FAIL.

- [ ] **Step 3: Implement `TenantsScreen.tsx`.**
```typescript
import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select';
import { Sheet, SheetContent, SheetTitle } from '@/components/ui/sheet';
import { LoadingCards, ErrorState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { PlatformApiClient } from '@/api/platformApi';
import type { OwnerInvite } from '@/api/types';
import { useTenants } from './useTenants';
import { buildTenantRows, STATUS_OPTIONS, STATUS_LABEL, PLAN_OPTIONS, PLAN_LABEL } from './tenantsModel';
import { TenantsTable } from './TenantsTable';
import { TenantDrawer } from './TenantDrawer';

interface TenantsScreenProps {
  client: PlatformApiClient;
  selectedTenantId: string | null;
  initialInvite: OwnerInvite | null;
  onOpenTenant: (organizationId: string) => void;
  onCloseTenant: () => void;
  onCreateTenant: () => void;
}

export function TenantsScreen({
  client, selectedTenantId, initialInvite, onOpenTenant, onCloseTenant, onCreateTenant
}: TenantsScreenProps) {
  const { t } = useI18n();
  const state = useTenants(client);
  const [query, setQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [planFilter, setPlanFilter] = useState('all');

  const selectedName =
    state.status === 'ready'
      ? state.data.find(x => x.organizationId === selectedTenantId)?.name ?? ''
      : '';

  return (
    <>
      <div className="mb-4 flex flex-wrap items-center gap-3">
        <Input
          aria-label={t('platform.tenants.search')}
          placeholder={t('platform.tenants.search')}
          value={query}
          onChange={e => setQuery(e.target.value)}
          className="max-w-xs"
        />
        <Select value={statusFilter} onValueChange={setStatusFilter}>
          <SelectTrigger aria-label={t('platform.tenants.filter.status')}><SelectValue /></SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t('platform.tenants.filter.all')}</SelectItem>
            {STATUS_OPTIONS.map(s => <SelectItem key={s} value={s}>{t(STATUS_LABEL[s])}</SelectItem>)}
          </SelectContent>
        </Select>
        <Select value={planFilter} onValueChange={setPlanFilter}>
          <SelectTrigger aria-label={t('platform.tenants.filter.plan')}><SelectValue /></SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t('platform.tenants.filter.all')}</SelectItem>
            {PLAN_OPTIONS.map(p => <SelectItem key={p} value={p}>{t(PLAN_LABEL[p])}</SelectItem>)}
          </SelectContent>
        </Select>
        <Button className="ml-auto" onClick={onCreateTenant}>{t('platform.tenants.new')}</Button>
      </div>

      {state.status === 'loading' ? (
        <LoadingCards count={3} />
      ) : state.status === 'error' ? (
        <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />
      ) : (
        <TenantsTable
          rows={buildTenantRows(state.data, { query, status: statusFilter, plan: planFilter })}
          selectedId={selectedTenantId}
          emptyMessage={t('platform.tenants.empty')}
          onSelect={onOpenTenant}
        />
      )}

      <Sheet open={selectedTenantId !== null} onOpenChange={open => { if (!open) onCloseTenant(); }}>
        <SheetContent closeLabel={t('common.close')}>
          {selectedTenantId !== null && (
            <>
              <SheetTitle>{selectedName}</SheetTitle>
              <TenantDrawer
                client={client}
                organizationId={selectedTenantId}
                initialInvite={initialInvite}
                onChanged={() => { if (state.status === 'ready') state.retry(); }}
              />
            </>
          )}
        </SheetContent>
      </Sheet>
    </>
  );
}
```

- [ ] **Step 4: Run tests.** Run: `npm test -- src/platform/tenants/TenantsScreen.test.tsx` → Expected: PASS.
- [ ] **Step 5: Build gate.** Run: `npm run build` → Expected: exit 0.
- [ ] **Step 6: Commit.**
```bash
git add src/platform/tenants/TenantsScreen.tsx src/platform/tenants/TenantsScreen.test.tsx
git commit -m "feat(platform): TenantsScreen list + filters + detail drawer"
```

---

## Task 11: Wire `TenantsScreen` into `App.tsx` `PlatformArea`

**Files:**
- Modify: `src/App.tsx` (`PlatformArea` render branches + imports)
- Modify: `src/App.test.tsx` (admin tenant-list assertions)
- Modify: `src/preview/DemoApp.tsx` (untracked scratch — keep green, do NOT commit)

- [ ] **Step 1: Update imports in `src/App.tsx`.** Remove these two lines (lines 30-31):
```typescript
import { TenantList } from './components/TenantList';
import { TenantDetailView } from './components/TenantDetail';
```
Add (next to the other `./platform/*` imports near line 33-35):
```typescript
import { TenantsScreen } from './platform/tenants/TenantsScreen';
```
Keep `import { NewTenant } from './components/NewTenant';` (Plan 6).

- [ ] **Step 2: Replace the `PlatformArea` render body.** In `PlatformArea` (currently `src/App.tsx:536-557`), replace the conditional block with:
```typescript
      {route.kind === 'adminOverview' ? (
        <PlatformOverviewScreen state={metricsState} />
      ) : route.kind === 'newTenant' ? (
        <NewTenant
          client={adminClient}
          onCreated={onCreatedTenant}
          onCancel={onCancelNewTenant}
        />
      ) : (
        <TenantsScreen
          client={adminClient}
          selectedTenantId={route.kind === 'tenantDetail' ? route.organizationId : null}
          initialInvite={route.kind === 'tenantDetail' ? route.initialInvite : null}
          onOpenTenant={onOpenTenant}
          onCloseTenant={onBackToTenants}
          onCreateTenant={onCreateTenant}
        />
      )}
```
Everything else in `PlatformArea` (the `AppShell` props, `PLATFORM_SCREEN_TITLE`, `pathForAdminRoute`, `handleNavigate`) stays unchanged. The `PlatformAreaProps` interface, `onOpenTenant`/`onBackToTenants`/`onCreatedTenant`/`onCancelNewTenant` callbacks, and the `AdminRoute` union all remain as-is.

- [ ] **Step 3: Build gate (catches dangling references).** Run: `npm run build` → Expected: exit 0. If `tsc` reports `TenantList`/`TenantDetailView` still referenced anywhere besides the deleted imports, fix those references (there should be none outside the files deleted in Task 12).

- [ ] **Step 4: Update `src/App.test.tsx`.** Read the file. Any test that signs in as admin and navigates to `/admin/tenants` (or asserts legacy `TenantList` text such as a "Tenants" heading / "No tenants yet" / table built by the legacy component) must now assert the new screen. Replace those assertions with new anchors that the `TenantsScreen` renders, e.g.:
  - the search box: `screen.getByLabelText('Поиск по названию или ключу')`, or
  - the "New tenant" button: `screen.getByRole('button', { name: 'Новый тенант' })`, or
  - a seeded tenant name from the mocked `listTenants`.
  Ensure the admin tests render within `I18nProvider` + `ToastProvider` (the `TenantsScreen` uses `useToast`). If the existing admin test helper (`renderWithProviders` referenced in the Plan 1 work) already wraps both providers, reuse it. Do NOT weaken unrelated assertions.

- [ ] **Step 5: Update `src/preview/DemoApp.tsx` if it references the removed imports** so `tsc -b` stays green. This file is untracked dev scratch — update the working tree but do NOT `git add` it.

- [ ] **Step 6: Full gates.** Run: `npm test` → Expected: PASS (all files). Run: `npm run build` → Expected: exit 0.

- [ ] **Step 7: Commit (tracked files only).**
```bash
git add src/App.tsx src/App.test.tsx
git commit -m "feat(platform): render TenantsScreen for /admin/tenants routes"
```
> Verify `git status` shows `src/preview/DemoApp.tsx` as still-untracked (NOT staged) before committing.

---

## Task 12: Delete legacy tenant components + final verification

**Files:**
- Delete: `src/components/TenantList.tsx` (+ `TenantList.test.tsx` if present)
- Delete: `src/components/TenantDetail.tsx` (+ test if present)
- Delete: `src/components/StatusControl.tsx` (+ test if present)
- Delete: `src/components/PlanControl.tsx` (+ test if present)
- Delete: `src/components/LimitsControl.tsx` (+ test if present)

- [ ] **Step 1: Confirm no remaining references.** Run a search for each symbol across `src/` (excluding the files themselves):
```bash
git grep -n "TenantList\|TenantDetailView\|StatusControl\|PlanControl\|LimitsControl" -- "src/" ":!src/components/TenantList.tsx" ":!src/components/TenantDetail.tsx" ":!src/components/StatusControl.tsx" ":!src/components/PlanControl.tsx" ":!src/components/LimitsControl.tsx"
```
Expected: no matches (except possibly in `src/preview/DemoApp.tsx`, which is untracked — fix it but don't commit). If any tracked file still references them, resolve before deleting.

- [ ] **Step 2: Delete the files** (use `git rm` for tracked test files too):
```bash
git rm src/components/TenantList.tsx src/components/TenantDetail.tsx src/components/StatusControl.tsx src/components/PlanControl.tsx src/components/LimitsControl.tsx
```
Then delete any co-located `*.test.tsx` for these that `git rm` reports — list them first with `git status` / `git ls-files src/components | findstr /R "TenantList TenantDetail StatusControl PlanControl LimitsControl"` and `git rm` each. Do NOT delete `OwnerInvitesSection`, `SupportNotesSection`, `HealthSection`, or `NewTenant`.

- [ ] **Step 3: Full gates on the result.**
Run: `npm test` → Expected: PASS (all files; deleted components' tests are gone).
Run: `npm run build` → Expected: exit 0 (`tsc -b` + `vite build`).

- [ ] **Step 4: Commit.**
```bash
git add -A -- src/
git commit -m "refactor(platform): delete legacy tenant list/detail/control components"
```
> Re-check `git status` to ensure `src/preview/DemoApp.tsx` is NOT staged.

- [ ] **Step 5: Final holistic review.** Dispatch a final code reviewer over the whole Plan 2 diff (`git diff main...HEAD` on the feature branch): verify spec compliance (list + drawer with Overview/Status/Plan/Limits; legacy sections embedded; deep-link + initialInvite preserved; legacy components deleted), house patterns (discriminated-union hooks, param-less `t`, money n/a here, no optimistic success on status), and that both gates are green. Then proceed to `superpowers:finishing-a-development-branch`.

---

## Self-review (author checklist — done)

- **Spec coverage:** list (Table + search + filter) ✓ (Tasks 2,5,10); detail drawer in Sheet ✓ (Tasks 9,10); sections status/plan/limits ✓ (Tasks 6-8); status via ConfirmDialog ✓ (Task 6); delete legacy TenantList/TenantDetail/*Control ✓ (Task 12). Owner-invites/support-notes (Plan 5) and subscription/invoice sections (Plan 4) intentionally embedded-legacy / deferred — documented in Scope reconciliation.
- **Placeholder scan:** all code blocks complete; the only "read & confirm" notes are for prop-name/export verification (`Card*` exports, legacy section props, `BadgeVariant` export) and the App.test.tsx assertion swap, which genuinely require reading the live file — each gives concrete targets.
- **Type consistency:** `TenantsState`/`TenantDetailState` discriminated unions match usage; `buildTenantRows(tenants, filter)` signature consistent across Tasks 2/10; section props `{ client, tenant, onUpdated }` consistent across Tasks 6-9; `TenantsScreen` props match the `PlatformArea` call site in Task 11; `apply`/`onChanged` chain consistent (Task 4 → 9 → 10).
- **Build gate:** every task runs `npm run build` (tsc) + targeted `npm test`; Tasks 11-12 run full `npm test`.
