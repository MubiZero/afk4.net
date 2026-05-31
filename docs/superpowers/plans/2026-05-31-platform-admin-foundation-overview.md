# Platform Admin Foundation + Overview (SP3 Plan 1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up the `/admin/*` control-plane shell (a `PlatformArea` wrapping the shared `AppShell` with a platform nav, no branch switcher) and a real Platform **Overview** screen whose KPIs are aggregated client-side from the existing `GET /api/platform/tenants` endpoint — mirroring how club Plan 1 established the club shell + Overview.

**Architecture:** Frontend-only (no backend changes this plan). First we **decouple the shared shell** (`AppShell`/`NavList`/`Topbar`) from the club so it can serve two areas: nav groups become a prop, the branch switcher becomes an optional `sidebarHeader` slot. Then we add a `src/platform/` module (mirror of `src/club/`) with `nav.ts`, and `overview/` = pure `metricsModel` builder + `useTenantMetrics` hook (discriminated-union state + `retry`) + presentational `OverviewScreen`. Finally we add an `adminOverview` route and an inline `PlatformArea` in `App.tsx` (mirror of `ClubArea`) that renders Overview and keeps the legacy Tenants screens inside the new shell until Plan 2 redesigns them.

**Tech Stack:** React 19 + Vite + Tailwind v4 + Radix, TypeScript (build gate `tsc -b`), Vitest (`globals: false` — import `it/expect/vi/describe` from `'vitest'`), `@testing-library/react`. Commands run from `src/AFK4.Platform.Web`.

**Branch:** `sp3-admin-control-plane` (already created; the SP3 spec is committed there).

---

## Key facts the implementer must know (verified 2026-05-31)

- **Vitest globals are OFF.** Every test file imports its helpers: `import { describe, expect, it, vi } from 'vitest';`. Test setup auto-cleans (`src/test/setup.ts`).
- **The build gate is `tsc -b`, not the test run.** `npm test` (vitest/esbuild) skips type-checking. After every task run BOTH `npm test` and `npm run build` (which runs `tsc -b && vite build`). A change can pass tests yet fail the build on a type error.
- **The shared shell is currently club-coupled.** `AppShell` requires `role: ClubRole`, always renders `<BranchSwitcher>`, and passes `role` to `NavList`, which calls `visibleNav(role)` from `@/club/nav`. The only non-test consumer of `AppShell`/`NavList`/`Topbar`/`visibleNav`/`pathForRoute`/`resolvePlatformRoute` is `src/App.tsx`. `NavList` and `Topbar` are consumed only by `AppShell`.
- **`messages.ts` shape:** one object `messages` with `ru:` (lines ~4–379) and `en:` (starts line 380), then `export type MessageKey = keyof (typeof messages)['ru']` (line 758). A test (`messages.test.ts`) asserts `Object.keys(messages.en).sort()` equals `Object.keys(messages.ru).sort()` — **ru and en MUST stay key-identical.**
- **`PlatformApiClient.listTenants(): Promise<TenantSummary[]>`** (`src/api/platformApi.ts:86`). `TenantSummary` (`src/api/types.ts:232`) fields: `organizationId, slug, name, status, planCode, subscriptionStatus, branchCount, createdAtUtc, updatedAtUtc` (all strings except `branchCount: number`).
- **Status / subscription / plan string values:** status ∈ `active | suspended | deletion_pending`; subscriptionStatus ∈ `trial | active | past_due | cancelled`; planCode ∈ `starter | growth | scale`.
- **Harness caveat (this machine):** Read/cat/grep intermittently return scrambled or hallucinated content. Trust `git show HEAD:<path>`, the `tsc -b` exit code, and the vitest run over eyeballed output. When an Edit anchor is described rather than pinned to a line number, it is because the target file is large and reads are unreliable — read the region live immediately before editing.

---

## File structure

**New files (`src/AFK4.Platform.Web/src/`):**
- `components/shell/navModel.ts` — shared `NavItem` / `NavGroup` types (extracted from club so the shell no longer depends on `@/club`).
- `platform/nav.ts` — `platformNav: NavGroup[]` (Control plane + Account groups).
- `platform/nav.test.ts`
- `platform/overview/metricsModel.ts` — pure `buildTenantMetrics(tenants, nowIso)` → view-model.
- `platform/overview/metricsModel.test.ts`
- `platform/overview/useTenantMetrics.ts` — `use*` hook (discriminated union + retry).
- `platform/overview/useTenantMetrics.test.tsx`
- `platform/overview/OverviewScreen.tsx` — presentational platform overview.
- `platform/overview/OverviewScreen.test.tsx`

**Modified files:**
- `components/shell/NavList.tsx` — take `groups: NavGroup[]` instead of `role`.
- `components/shell/Topbar.tsx` — rename `branchName` → `subtitle`.
- `components/shell/AppShell.tsx` — generic: `navGroups` + `sidebarHeader` slot; drop branch/role props.
- `components/shell/AppShell.test.tsx` — updated to new props.
- `club/nav.ts` — import `NavItem`/`NavGroup` from `navModel`; keep `ClubRole`/`clubNav`/`visibleNav`/`roleFromPermissions`.
- `i18n/messages.ts` — add platform keys (ru + en).
- `i18n/messages.test.ts` — assert the new platform keys.
- `App.tsx` — `adminOverview` route + `PlatformArea` + new `AppShell` call site for `ClubArea` + imports.
- `App.routing.test.ts` — add `/admin` and `/admin/tenants` route assertions.

---

## Task 1: Extract shared nav types into the shell

**Files:**
- Create: `src/components/shell/navModel.ts`
- Modify: `src/club/nav.ts`
- Test: existing `src/club/nav.test.ts` (must keep passing — no edit)

- [ ] **Step 1: Create the shared nav types module**

Create `src/components/shell/navModel.ts`:

```typescript
import type { MessageKey } from '@/i18n/messages';

export interface NavItem {
  key: string;
  labelKey: MessageKey;
  path: string;
  ownerOnly: boolean;
  soon: boolean;
}

export interface NavGroup {
  key: string;
  labelKey: MessageKey;
  items: NavItem[];
}
```

- [ ] **Step 2: Repoint `club/nav.ts` at the shared types**

In `src/club/nav.ts`, replace the local type block. The current top of the file is:

```typescript
import type { MessageKey } from '@/i18n/messages';

export type ClubRole = 'owner' | 'manager';
export type NavGroupKey = 'branch' | 'account';

export interface NavItem {
  key: string;
  labelKey: MessageKey;
  path: string;
  ownerOnly: boolean;
  soon: boolean;
}
export interface NavGroup { key: NavGroupKey; labelKey: MessageKey; items: NavItem[]; }
```

Replace that entire block with:

```typescript
import type { NavGroup } from '@/components/shell/navModel';

export type ClubRole = 'owner' | 'manager';
```

The rest of the file is unchanged: `export const clubNav: NavGroup[] = [ ... ]` (the group `key` values `'branch'`/`'account'` are still valid because `NavGroup.key` is now `string`), `roleFromPermissions`, and `visibleNav(role: ClubRole): NavGroup[]`.

- [ ] **Step 3: Run the club nav tests + typecheck**

Run: `npm test -- club/nav` and `npm run build`
Expected: `nav.test.ts` passes (it imports `clubNav`, `roleFromPermissions`, `visibleNav` — all still exported); build succeeds.

- [ ] **Step 4: Commit**

```bash
git add src/components/shell/navModel.ts src/club/nav.ts
git commit -m "refactor(shell): extract shared NavItem/NavGroup types into navModel"
```

---

## Task 2: Generalize the shared shell (decouple from club + branch)

Make `AppShell`/`NavList`/`Topbar` area-agnostic so both `ClubArea` and the new `PlatformArea` can use them. Nav groups become a prop; the branch switcher becomes an optional `sidebarHeader` slot; the topbar's `branchName` becomes a generic `subtitle`. This is one coherent change committed together so the build stays green.

**Files:**
- Modify: `src/components/shell/NavList.tsx`
- Modify: `src/components/shell/Topbar.tsx`
- Modify: `src/components/shell/AppShell.tsx`
- Modify: `src/components/shell/AppShell.test.tsx`
- Modify: `src/App.tsx` (the `ClubArea` `<AppShell>` call site only)

- [ ] **Step 1: Rewrite `NavList.tsx` to take groups**

Replace the entire contents of `src/components/shell/NavList.tsx` with:

```typescript
import { cn } from '@/lib/utils';
import { useI18n } from '@/i18n/I18nProvider';
import { Badge } from '@/components/ui/badge';
import type { NavGroup } from './navModel';

export interface NavListProps {
  groups: NavGroup[];
  activePath: string;
  counts?: Record<string, number>;
  onNavigate: (path: string) => void;
}

export function NavList({ groups, activePath, counts = {}, onNavigate }: NavListProps) {
  const { t } = useI18n();
  return (
    <nav className="flex flex-col gap-1">
      {groups.map(group => (
        <div key={group.key} className="px-2 py-1">
          <div className="px-3 pb-1 pt-3 text-[10px] font-bold uppercase tracking-wide text-muted">
            {t(group.labelKey)}
          </div>
          {group.items.map(item => {
            const active = item.path === activePath;
            const count = counts[item.key];
            return (
              <button
                key={item.key}
                type="button"
                aria-current={active ? 'page' : undefined}
                onClick={() => onNavigate(item.path)}
                className={cn(
                  'flex w-full items-center gap-3 rounded-lg px-3 py-2 text-left text-sm font-medium text-foreground/80 hover:bg-accent',
                  active && 'bg-accent font-semibold text-accent-foreground'
                )}
              >
                <span>{t(item.labelKey)}</span>
                {typeof count === 'number' && count > 0 && (
                  <Badge variant="secondary" className="ml-auto">{count}</Badge>
                )}
                {item.soon && !(typeof count === 'number' && count > 0) && (
                  <span className="ml-auto text-[10px] text-muted">{t('shell.soon')}</span>
                )}
              </button>
            );
          })}
        </div>
      ))}
    </nav>
  );
}
```

(Only change vs. current: drop the `visibleNav`/`ClubRole` import and the `role` prop; iterate `groups` directly.)

- [ ] **Step 2: Rename `Topbar` `branchName` → `subtitle`**

Replace the entire contents of `src/components/shell/Topbar.tsx` with:

```typescript
import type { ReactNode } from 'react';
import { Menu } from 'lucide-react';
import { Button } from '@/components/ui/button';

export interface TopbarProps { subtitle: string; screenTitle: string; onOpenSidebar: () => void; right?: ReactNode; }

export function Topbar({ subtitle, screenTitle, onOpenSidebar, right }: TopbarProps) {
  return (
    <header className="flex items-center justify-between border-b border-border bg-card px-5 py-3">
      <div className="flex items-center gap-3">
        <Button variant="ghost" size="icon" className="md:hidden" aria-label="menu" onClick={onOpenSidebar}>
          <Menu className="size-4" />
        </Button>
        <div className="text-sm text-muted">
          {subtitle && <>{subtitle} · </>}
          <b className="text-base text-foreground">{screenTitle}</b>
        </div>
      </div>
      {right}
    </header>
  );
}
```

- [ ] **Step 3: Rewrite `AppShell.tsx` to be area-agnostic**

Replace the entire contents of `src/components/shell/AppShell.tsx` with:

```typescript
import { useState, type ReactNode } from 'react';
import { cn } from '@/lib/utils';
import { NavList } from './NavList';
import { UserMenu } from './UserMenu';
import { Topbar } from './Topbar';
import type { NavGroup } from './navModel';

export interface AppShellProps {
  navGroups: NavGroup[];
  sidebarHeader: ReactNode;
  activePath: string;
  subtitle: string;
  screenTitle: string;
  userName: string;
  roleLabel: string;
  counts?: Record<string, number>;
  topbarRight?: ReactNode;
  onNavigate: (path: string) => void;
  onSignOut: () => void;
  children: ReactNode;
}

export function AppShell(props: AppShellProps) {
  const [sidebarOpen, setSidebarOpen] = useState(false);
  return (
    <div className="flex min-h-screen bg-background text-foreground">
      <aside
        className={cn(
          'fixed inset-y-0 left-0 z-40 flex w-60 flex-col border-r border-border bg-card transition-transform md:static md:translate-x-0',
          sidebarOpen ? 'translate-x-0' : '-translate-x-full'
        )}
      >
        {props.sidebarHeader}
        <div className="flex-1 overflow-auto">
          <NavList groups={props.navGroups} activePath={props.activePath} counts={props.counts}
            onNavigate={(p) => { setSidebarOpen(false); props.onNavigate(p); }} />
        </div>
        <UserMenu displayName={props.userName} roleLabel={props.roleLabel} onSignOut={props.onSignOut} />
      </aside>

      {sidebarOpen && (
        <div className="fixed inset-0 z-30 bg-black/40 md:hidden" onClick={() => setSidebarOpen(false)} aria-hidden />
      )}

      <div className="flex min-w-0 flex-1 flex-col">
        <Topbar subtitle={props.subtitle}
          screenTitle={props.screenTitle} onOpenSidebar={() => setSidebarOpen(true)} right={props.topbarRight} />
        <main className="flex-1 overflow-auto p-5">{props.children}</main>
      </div>
    </div>
  );
}
```

(Dropped props: `role`, `orgName`, `branches`, `activeBranchId`, `onSelectBranch`. Dropped import of `BranchSwitcher`/`BranchOption`/`ClubRole`. The branch switcher is now passed in by the club via `sidebarHeader`; the topbar subtitle is computed by the caller.)

- [ ] **Step 4: Update the `ClubArea` `<AppShell>` call site in `App.tsx`**

First add two imports to the top import block of `src/App.tsx` (place next to the existing shell/club imports):

```typescript
import { BranchSwitcher } from './components/shell/BranchSwitcher';
import { visibleNav } from './club/nav';
```

(Note: `roleFromPermissions` is already imported from `./club/nav`; extend that line or add this separate import — both compile. If TypeScript complains about a duplicate module import, merge into the existing `import { roleFromPermissions } from './club/nav';` line to read `import { roleFromPermissions, visibleNav } from './club/nav';`.)

Then, in the `ClubArea` function, the current `<AppShell ...>` opening tag reads:

```tsx
    <AppShell
      role={role}
      orgName={session.displayName}
      branches={branches}
      activeBranchId={activeBranchId}
      activePath={pathForRoute(route)}
      screenTitle={CLUB_SCREEN_TITLE[route.kind] ?? ''}
      userName={session.displayName}
      roleLabel={ROLE_LABEL[role]}
      onNavigate={handleNavigate}
      onSelectBranch={select}
      onSignOut={onSignOut}
    >
```

Replace it with:

```tsx
    <AppShell
      navGroups={visibleNav(role)}
      sidebarHeader={
        <BranchSwitcher
          orgName={session.displayName}
          branches={branches}
          activeBranchId={activeBranchId}
          onSelect={select}
        />
      }
      activePath={pathForRoute(route)}
      subtitle={branches.find(b => b.branchId === activeBranchId)?.name ?? ''}
      screenTitle={CLUB_SCREEN_TITLE[route.kind] ?? ''}
      userName={session.displayName}
      roleLabel={ROLE_LABEL[role]}
      onNavigate={handleNavigate}
      onSignOut={onSignOut}
    >
```

- [ ] **Step 5: Rewrite `AppShell.test.tsx` for the new props**

Replace the entire contents of `src/components/shell/AppShell.test.tsx` with:

```typescript
import { describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ThemeProvider } from '@/theme/ThemeProvider';
import { visibleNav } from '@/club/nav';
import { BranchSwitcher } from './BranchSwitcher';
import { AppShell } from './AppShell';

function renderShell(role: 'owner' | 'manager', onNavigate = vi.fn()) {
  return render(
    <ThemeProvider><I18nProvider>
      <AppShell
        navGroups={visibleNav(role)}
        sidebarHeader={
          <BranchSwitcher orgName="Победа" branches={[{ branchId: 'b1', name: 'Центральный' }]}
            activeBranchId="b1" onSelect={vi.fn()} />
        }
        activePath="/club"
        subtitle="Центральный"
        screenTitle="Обзор"
        userName="Алишер"
        roleLabel="Владелец"
        counts={{ venue: 2 }}
        onNavigate={onNavigate}
        onSignOut={vi.fn()}
      >
        <div>screen-body</div>
      </AppShell>
    </I18nProvider></ThemeProvider>
  );
}

describe('AppShell', () => {
  it('renders branch + account groups and the body for an owner', () => {
    renderShell('owner');
    expect(screen.getByText('Филиал')).toBeInTheDocument();
    expect(screen.getByText('Аккаунт')).toBeInTheDocument();
    expect(screen.getByText('Настройки')).toBeInTheDocument();
    expect(screen.getByText('screen-body')).toBeInTheDocument();
  });

  it('hides owner-only items for a manager', () => {
    renderShell('manager');
    expect(screen.queryByText('Настройки')).not.toBeInTheDocument();
    expect(screen.queryByText('Биллинг')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Обзор' })).toBeInTheDocument();
  });

  it('fires navigation on item click', () => {
    const onNavigate = vi.fn();
    renderShell('owner', onNavigate);
    fireEvent.click(screen.getByRole('button', { name: 'Обзор' }));
    expect(onNavigate).toHaveBeenCalledWith('/club');
  });
});
```

- [ ] **Step 6: Run shell tests + full typecheck**

Run: `npm test -- shell` then `npm run build`
Expected: `AppShell.test.tsx` passes; build succeeds (this proves the `App.tsx` `ClubArea` call site type-checks against the new `AppShellProps`).

- [ ] **Step 7: Commit**

```bash
git add src/components/shell/NavList.tsx src/components/shell/Topbar.tsx src/components/shell/AppShell.tsx src/components/shell/AppShell.test.tsx src/App.tsx
git commit -m "refactor(shell): make AppShell area-agnostic (nav groups + sidebar slot)"
```

---

## Task 3: Platform navigation config

**Files:**
- Create: `src/platform/nav.ts`
- Test: `src/platform/nav.test.ts`

- [ ] **Step 1: Write the failing test**

Create `src/platform/nav.test.ts`:

```typescript
import { describe, expect, it } from 'vitest';
import { platformNav } from './nav';

describe('platform nav', () => {
  it('exposes overview, tenants, billing and profile', () => {
    const keys = platformNav.flatMap(g => g.items.map(i => i.key));
    expect(keys).toContain('overview');
    expect(keys).toContain('tenants');
    expect(keys).toContain('billing');
    expect(keys).toContain('profile');
  });

  it('marks overview and tenants live, billing and profile soon', () => {
    const items = platformNav.flatMap(g => g.items);
    expect(items.find(i => i.key === 'overview')?.soon).toBe(false);
    expect(items.find(i => i.key === 'tenants')?.soon).toBe(false);
    expect(items.find(i => i.key === 'billing')?.soon).toBe(true);
    expect(items.find(i => i.key === 'profile')?.soon).toBe(true);
  });

  it('every item has an /admin path and a nav. label key', () => {
    for (const g of platformNav) for (const i of g.items) {
      expect(i.path.startsWith('/admin')).toBe(true);
      expect(i.labelKey.startsWith('nav.')).toBe(true);
    }
  });
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `npm test -- platform/nav`
Expected: FAIL — cannot resolve `./nav`.

- [ ] **Step 3: Write `platform/nav.ts`**

Create `src/platform/nav.ts`:

```typescript
import type { NavGroup } from '@/components/shell/navModel';

export const platformNav: NavGroup[] = [
  {
    key: 'controlPlane',
    labelKey: 'nav.group.controlPlane',
    items: [
      { key: 'overview', labelKey: 'nav.platform.overview', path: '/admin', ownerOnly: false, soon: false },
      { key: 'tenants', labelKey: 'nav.platform.tenants', path: '/admin/tenants', ownerOnly: false, soon: false },
      { key: 'billing', labelKey: 'nav.platform.billing', path: '/admin/billing', ownerOnly: false, soon: true }
    ]
  },
  {
    key: 'platformAccount',
    labelKey: 'nav.group.platformAccount',
    items: [
      { key: 'profile', labelKey: 'nav.platform.profile', path: '/admin/profile', ownerOnly: false, soon: true }
    ]
  }
];
```

Note: `labelKey` values reference message keys added in Task 4. TypeScript will error here until Task 4 adds them to `messages.ts` (because `MessageKey` is a union of existing keys). That is expected — `npm test` (esbuild) still runs because it skips types; the `tsc -b` build will only go green after Task 4. Do Task 3 and Task 4 back-to-back; run the full build at the end of Task 4.

- [ ] **Step 4: Run the test to verify it passes**

Run: `npm test -- platform/nav`
Expected: PASS (vitest skips type-checking, so the not-yet-added label keys don't block the test).

- [ ] **Step 5: Commit**

```bash
git add src/platform/nav.ts src/platform/nav.test.ts
git commit -m "feat(platform): platform nav config (overview/tenants/billing/profile)"
```

---

## Task 4: i18n keys for the platform area

**Files:**
- Modify: `src/i18n/messages.ts`
- Test: `src/i18n/messages.test.ts`

- [ ] **Step 1: Add the failing test block**

In `src/i18n/messages.test.ts`, add this test at the end of the file (after the last existing `it(...)` block, before EOF):

```typescript
it('includes the platform admin keys', () => {
  for (const key of [
    'nav.group.controlPlane', 'nav.group.platformAccount',
    'nav.platform.overview', 'nav.platform.tenants', 'nav.platform.billing', 'nav.platform.profile',
    'platform.overview.kpi.tenants', 'platform.overview.kpi.active', 'platform.overview.kpi.suspended',
    'platform.overview.kpi.trial', 'platform.overview.kpi.branches', 'platform.overview.kpi.new30d',
    'platform.overview.byPlan.title', 'platform.overview.attention.title', 'platform.overview.attention.empty',
    'platform.overview.attention.suspended', 'platform.overview.attention.pastDue',
    'platform.plan.starter', 'platform.plan.growth', 'platform.plan.scale'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `npm test -- i18n/messages`
Expected: FAIL — `messages.ru[key]` undefined for the new keys (and the parity test still passes for now).

- [ ] **Step 3: Add the keys to the `ru` block**

Open `src/i18n/messages.ts`. In the `ru:` object, find the nav keys block (it contains `'nav.profile': 'Профиль и доступ',`). Immediately after the `'nav.profile'` line, insert:

```typescript
    'nav.group.controlPlane': 'Контроль',
    'nav.group.platformAccount': 'Аккаунт',
    'nav.platform.overview': 'Обзор',
    'nav.platform.tenants': 'Тенанты',
    'nav.platform.billing': 'Биллинг',
    'nav.platform.profile': 'Профиль',
    'platform.overview.kpi.tenants': 'Всего тенантов',
    'platform.overview.kpi.active': 'Активные',
    'platform.overview.kpi.suspended': 'Приостановлены',
    'platform.overview.kpi.trial': 'На триале',
    'platform.overview.kpi.branches': 'Филиалов всего',
    'platform.overview.kpi.new30d': 'Новые за 30 дней',
    'platform.overview.byPlan.title': 'Тенанты по тарифам',
    'platform.overview.attention.title': 'Требуют внимания',
    'platform.overview.attention.empty': 'Все тенанты в норме.',
    'platform.overview.attention.suspended': 'приостановлен',
    'platform.overview.attention.pastDue': 'просрочен платёж',
    'platform.plan.starter': 'Starter',
    'platform.plan.growth': 'Growth',
    'platform.plan.scale': 'Scale',
```

- [ ] **Step 4: Add the same keys to the `en` block**

In the same file, find the `en:` object (starts at the line `  en: {`). Find the `en` nav block (it contains the English `'nav.profile': ...` line). Immediately after that `'nav.profile'` line, insert:

```typescript
    'nav.group.controlPlane': 'Control plane',
    'nav.group.platformAccount': 'Account',
    'nav.platform.overview': 'Overview',
    'nav.platform.tenants': 'Tenants',
    'nav.platform.billing': 'Billing',
    'nav.platform.profile': 'Profile',
    'platform.overview.kpi.tenants': 'Total tenants',
    'platform.overview.kpi.active': 'Active',
    'platform.overview.kpi.suspended': 'Suspended',
    'platform.overview.kpi.trial': 'On trial',
    'platform.overview.kpi.branches': 'Total branches',
    'platform.overview.kpi.new30d': 'New in 30 days',
    'platform.overview.byPlan.title': 'Tenants by plan',
    'platform.overview.attention.title': 'Needs attention',
    'platform.overview.attention.empty': 'All tenants are healthy.',
    'platform.overview.attention.suspended': 'suspended',
    'platform.overview.attention.pastDue': 'past due',
    'platform.plan.starter': 'Starter',
    'platform.plan.growth': 'Growth',
    'platform.plan.scale': 'Scale',
```

(If the file's `en` block does not contain a `'nav.profile'` line because a previous read was scrambled, instead insert the `en` keys immediately before the closing `}` of the `en:` object — i.e. after the last `en` key and before the `};` that closes `messages`. Key order does not matter; only ru/en key-set parity matters.)

- [ ] **Step 5: Run the tests + full typecheck**

Run: `npm test -- i18n/messages` then `npm test -- platform/nav` then `npm run build`
Expected: the new platform keys test passes; the ru/en parity test passes (20 keys added to both); `platform/nav.ts` now type-checks (its `labelKey` values are valid `MessageKey`s); `npm run build` succeeds.

- [ ] **Step 6: Commit**

```bash
git add src/i18n/messages.ts src/i18n/messages.test.ts
git commit -m "feat(platform): i18n keys for platform nav + overview"
```

---

## Task 5: Platform metrics view-model (pure builder)

**Files:**
- Create: `src/platform/overview/metricsModel.ts`
- Test: `src/platform/overview/metricsModel.test.ts`

- [ ] **Step 1: Write the failing test**

Create `src/platform/overview/metricsModel.test.ts`:

```typescript
import { describe, expect, it } from 'vitest';
import { buildTenantMetrics } from './metricsModel';
import type { TenantSummary } from '@/api/types';

function tenant(p: Partial<TenantSummary>): TenantSummary {
  return {
    organizationId: 'o', slug: 's', name: 'Club', status: 'active', planCode: 'starter',
    subscriptionStatus: 'active', branchCount: 1, createdAtUtc: '2026-01-01T00:00:00Z',
    updatedAtUtc: '2026-01-01T00:00:00Z', ...p
  };
}

const NOW = '2026-05-31T00:00:00Z';

describe('buildTenantMetrics', () => {
  it('counts tenants by status, subscription and sums branches', () => {
    const vm = buildTenantMetrics([
      tenant({ organizationId: 'a', status: 'active', subscriptionStatus: 'active', branchCount: 2 }),
      tenant({ organizationId: 'b', status: 'suspended', subscriptionStatus: 'past_due', branchCount: 3 }),
      tenant({ organizationId: 'c', status: 'active', subscriptionStatus: 'trial', branchCount: 1 })
    ], NOW);
    expect(vm.kpis.totalTenants).toBe(3);
    expect(vm.kpis.activeTenants).toBe(2);
    expect(vm.kpis.suspendedTenants).toBe(1);
    expect(vm.kpis.trialTenants).toBe(1);
    expect(vm.kpis.totalBranches).toBe(6);
  });

  it('counts tenants created within the last 30 days', () => {
    const vm = buildTenantMetrics([
      tenant({ organizationId: 'old', createdAtUtc: '2026-01-01T00:00:00Z' }),
      tenant({ organizationId: 'new', createdAtUtc: '2026-05-20T00:00:00Z' })
    ], NOW);
    expect(vm.kpis.newTenants30d).toBe(1);
  });

  it('groups counts by plan in catalog order', () => {
    const vm = buildTenantMetrics([
      tenant({ organizationId: 'a', planCode: 'scale' }),
      tenant({ organizationId: 'b', planCode: 'starter' }),
      tenant({ organizationId: 'c', planCode: 'starter' })
    ], NOW);
    expect(vm.byPlan).toEqual([
      { planCode: 'starter', count: 2 },
      { planCode: 'growth', count: 0 },
      { planCode: 'scale', count: 1 }
    ]);
  });

  it('lists suspended and past-due tenants in the attention feed', () => {
    const vm = buildTenantMetrics([
      tenant({ organizationId: 'a', name: 'Alpha', status: 'active', subscriptionStatus: 'active' }),
      tenant({ organizationId: 'b', name: 'Beta', status: 'suspended', subscriptionStatus: 'active' }),
      tenant({ organizationId: 'c', name: 'Gamma', status: 'active', subscriptionStatus: 'past_due' })
    ], NOW);
    const ids = vm.attention.map(a => a.organizationId);
    expect(ids).toEqual(expect.arrayContaining(['b', 'c']));
    expect(ids).not.toContain('a');
    expect(vm.attention.find(a => a.organizationId === 'b')?.reason).toBe('suspended');
    expect(vm.attention.find(a => a.organizationId === 'c')?.reason).toBe('past_due');
  });
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `npm test -- platform/overview/metricsModel`
Expected: FAIL — cannot resolve `./metricsModel`.

- [ ] **Step 3: Write `metricsModel.ts`**

Create `src/platform/overview/metricsModel.ts`:

```typescript
import type { TenantSummary } from '@/api/types';

export type AttentionReason = 'suspended' | 'past_due';
export interface AttentionRow { organizationId: string; name: string; reason: AttentionReason; }
export interface PlanCount { planCode: string; count: number; }

export interface PlatformMetricsViewModel {
  kpis: {
    totalTenants: number;
    activeTenants: number;
    suspendedTenants: number;
    trialTenants: number;
    totalBranches: number;
    newTenants30d: number;
  };
  byPlan: PlanCount[];
  attention: AttentionRow[];
}

const PLAN_ORDER = ['starter', 'growth', 'scale'] as const;
const THIRTY_DAYS_MS = 30 * 24 * 60 * 60 * 1000;

export function buildTenantMetrics(tenants: TenantSummary[], nowIso: string): PlatformMetricsViewModel {
  const nowMs = Date.parse(nowIso);

  let activeTenants = 0;
  let suspendedTenants = 0;
  let trialTenants = 0;
  let totalBranches = 0;
  let newTenants30d = 0;
  const planCounts = new Map<string, number>();
  const attention: AttentionRow[] = [];

  for (const t of tenants) {
    if (t.status === 'active') activeTenants += 1;
    if (t.status === 'suspended') suspendedTenants += 1;
    if (t.subscriptionStatus === 'trial') trialTenants += 1;
    totalBranches += t.branchCount;

    const createdMs = Date.parse(t.createdAtUtc);
    if (!Number.isNaN(createdMs) && nowMs - createdMs <= THIRTY_DAYS_MS) newTenants30d += 1;

    planCounts.set(t.planCode, (planCounts.get(t.planCode) ?? 0) + 1);

    if (t.status === 'suspended') {
      attention.push({ organizationId: t.organizationId, name: t.name, reason: 'suspended' });
    } else if (t.subscriptionStatus === 'past_due') {
      attention.push({ organizationId: t.organizationId, name: t.name, reason: 'past_due' });
    }
  }

  const byPlan: PlanCount[] = PLAN_ORDER.map(planCode => ({ planCode, count: planCounts.get(planCode) ?? 0 }));
  for (const [planCode, count] of planCounts) {
    if (!PLAN_ORDER.includes(planCode as (typeof PLAN_ORDER)[number])) byPlan.push({ planCode, count });
  }

  return {
    kpis: { totalTenants: tenants.length, activeTenants, suspendedTenants, trialTenants, totalBranches, newTenants30d },
    byPlan,
    attention
  };
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `npm test -- platform/overview/metricsModel`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add src/platform/overview/metricsModel.ts src/platform/overview/metricsModel.test.ts
git commit -m "feat(platform): tenant metrics view-model builder"
```

---

## Task 6: `useTenantMetrics` hook

**Files:**
- Create: `src/platform/overview/useTenantMetrics.ts`
- Test: `src/platform/overview/useTenantMetrics.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `src/platform/overview/useTenantMetrics.test.tsx`:

```typescript
import { describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { useTenantMetrics } from './useTenantMetrics';

const okTenants = [
  { organizationId: 'a', slug: 'a', name: 'Alpha', status: 'active', planCode: 'starter',
    subscriptionStatus: 'active', branchCount: 2, createdAtUtc: '2026-01-01T00:00:00Z', updatedAtUtc: '2026-01-01T00:00:00Z' }
];

function fakeClient(over: Partial<Record<'listTenants', unknown>> = {}) {
  return {
    listTenants: vi.fn().mockResolvedValue(okTenants),
    ...over
  } as never;
}

describe('useTenantMetrics', () => {
  it('reaches ready with a view-model', async () => {
    const { result } = renderHook(() => useTenantMetrics(fakeClient()));
    expect(result.current.status).toBe('loading');
    await waitFor(() => expect(result.current.status).toBe('ready'));
    if (result.current.status === 'ready') {
      expect(result.current.data.kpis.totalTenants).toBe(1);
      expect(result.current.data.kpis.totalBranches).toBe(2);
    }
  });

  it('surfaces an error state and supports retry', async () => {
    const failing = fakeClient({ listTenants: vi.fn().mockRejectedValue(new Error('boom')) });
    const { result } = renderHook(() => useTenantMetrics(failing));
    await waitFor(() => expect(result.current.status).toBe('error'));
    (failing as { listTenants: ReturnType<typeof vi.fn> }).listTenants.mockResolvedValue(okTenants);
    result.current.retry();
    await waitFor(() => expect(result.current.status).toBe('ready'));
  });
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `npm test -- platform/overview/useTenantMetrics`
Expected: FAIL — cannot resolve `./useTenantMetrics`.

- [ ] **Step 3: Write `useTenantMetrics.ts`**

Create `src/platform/overview/useTenantMetrics.ts`:

```typescript
import { useCallback, useEffect, useRef, useState } from 'react';
import type { PlatformApiClient } from '@/api/platformApi';
import { buildTenantMetrics, type PlatformMetricsViewModel } from './metricsModel';

export type TenantMetricsState =
  | { status: 'loading'; retry: () => void }
  | { status: 'error'; message: string; retry: () => void }
  | { status: 'ready'; data: PlatformMetricsViewModel; retry: () => void };

type Loadable = Pick<PlatformApiClient, 'listTenants'>;

export function useTenantMetrics(client: Loadable): TenantMetricsState {
  const [tick, setTick] = useState(0);
  const [state, setState] = useState<{ status: 'loading' | 'error' | 'ready'; data?: PlatformMetricsViewModel; message?: string }>({ status: 'loading' });
  const retry = useCallback(() => setTick(t => t + 1), []);
  const clientRef = useRef(client);
  clientRef.current = client;

  useEffect(() => {
    let cancelled = false;
    setState({ status: 'loading' });
    clientRef.current.listTenants()
      .then(tenants => {
        if (cancelled) return;
        setState({ status: 'ready', data: buildTenantMetrics(tenants, new Date().toISOString()) });
      })
      .catch((err: unknown) => {
        if (cancelled) return;
        setState({ status: 'error', message: err instanceof Error ? err.message : 'error' });
      });
    return () => { cancelled = true; };
  }, [tick]);

  if (state.status === 'ready') return { status: 'ready', data: state.data!, retry };
  if (state.status === 'error') return { status: 'error', message: state.message ?? 'error', retry };
  return { status: 'loading', retry };
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `npm test -- platform/overview/useTenantMetrics`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/platform/overview/useTenantMetrics.ts src/platform/overview/useTenantMetrics.test.tsx
git commit -m "feat(platform): useTenantMetrics hook"
```

---

## Task 7: Platform `OverviewScreen`

**Files:**
- Create: `src/platform/overview/OverviewScreen.tsx`
- Test: `src/platform/overview/OverviewScreen.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `src/platform/overview/OverviewScreen.test.tsx`:

```typescript
import { describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { OverviewScreen } from './OverviewScreen';
import type { TenantMetricsState } from './useTenantMetrics';

function wrap(state: TenantMetricsState) {
  return render(<I18nProvider><OverviewScreen state={state} /></I18nProvider>);
}

const ready: TenantMetricsState = {
  status: 'ready', retry: vi.fn(),
  data: {
    kpis: { totalTenants: 5, activeTenants: 3, suspendedTenants: 1, trialTenants: 1, totalBranches: 9, newTenants30d: 2 },
    byPlan: [{ planCode: 'starter', count: 3 }, { planCode: 'growth', count: 1 }, { planCode: 'scale', count: 1 }],
    attention: [{ organizationId: 'b', name: 'Beta', reason: 'suspended' }]
  }
};

describe('platform OverviewScreen', () => {
  it('renders KPI values when ready', () => {
    wrap(ready);
    expect(screen.getByText('Всего тенантов')).toBeInTheDocument();
    expect(screen.getByText('5')).toBeInTheDocument();
    expect(screen.getByText('Beta')).toBeInTheDocument();
  });

  it('shows a loading skeleton', () => {
    wrap({ status: 'loading', retry: vi.fn() });
    expect(screen.getByTestId('platform-overview-loading')).toBeInTheDocument();
  });

  it('shows an error with a working retry', () => {
    const retry = vi.fn();
    wrap({ status: 'error', message: 'x', retry });
    fireEvent.click(screen.getByText('Повторить'));
    expect(retry).toHaveBeenCalled();
  });

  it('shows the empty attention message when nothing needs attention', () => {
    wrap({ ...ready, data: { ...ready.data!, attention: [] } } as TenantMetricsState);
    expect(screen.getByText('Все тенанты в норме.')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `npm test -- platform/overview/OverviewScreen`
Expected: FAIL — cannot resolve `./OverviewScreen`.

- [ ] **Step 3: Write `OverviewScreen.tsx`**

Create `src/platform/overview/OverviewScreen.tsx`:

```typescript
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { useI18n } from '@/i18n/I18nProvider';
import type { MessageKey } from '@/i18n/messages';
import type { AttentionReason } from './metricsModel';
import type { TenantMetricsState } from './useTenantMetrics';

const ATTENTION_LABEL: Record<AttentionReason, MessageKey> = {
  suspended: 'platform.overview.attention.suspended',
  past_due: 'platform.overview.attention.pastDue'
};

const PLAN_LABEL: Record<string, MessageKey> = {
  starter: 'platform.plan.starter',
  growth: 'platform.plan.growth',
  scale: 'platform.plan.scale'
};

export function OverviewScreen({ state }: { state: TenantMetricsState }) {
  const { t, formatNumber } = useI18n();

  if (state.status === 'loading') {
    return (
      <div data-testid="platform-overview-loading" className="grid grid-cols-1 gap-4 md:grid-cols-3 lg:grid-cols-6">
        {[0, 1, 2, 3, 4, 5].map(i => <Skeleton key={i} className="h-24 w-full rounded-lg" />)}
      </div>
    );
  }

  if (state.status === 'error') {
    return (
      <Card><CardContent className="flex flex-col items-center gap-3 py-10">
        <p className="text-muted">{t('state.error')}</p>
        <Button onClick={state.retry}>{t('state.retry')}</Button>
      </CardContent></Card>
    );
  }

  const { kpis, byPlan, attention } = state.data;
  return (
    <div className="flex flex-col gap-4">
      <div className="grid grid-cols-1 gap-4 md:grid-cols-3 lg:grid-cols-6">
        <Kpi label={t('platform.overview.kpi.tenants')} value={formatNumber(kpis.totalTenants)} />
        <Kpi label={t('platform.overview.kpi.active')} value={formatNumber(kpis.activeTenants)} />
        <Kpi label={t('platform.overview.kpi.suspended')} value={formatNumber(kpis.suspendedTenants)} />
        <Kpi label={t('platform.overview.kpi.trial')} value={formatNumber(kpis.trialTenants)} />
        <Kpi label={t('platform.overview.kpi.branches')} value={formatNumber(kpis.totalBranches)} />
        <Kpi label={t('platform.overview.kpi.new30d')} value={formatNumber(kpis.newTenants30d)} />
      </div>

      <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
        <Card className="md:col-span-2">
          <CardHeader><CardTitle>{t('platform.overview.byPlan.title')}</CardTitle></CardHeader>
          <CardContent className="flex flex-col gap-2">
            {byPlan.map(p => (
              <div key={p.planCode} className="flex items-center justify-between border-b border-border py-2 last:border-0">
                <span className="text-sm font-medium">{PLAN_LABEL[p.planCode] ? t(PLAN_LABEL[p.planCode]) : p.planCode}</span>
                <span className="text-sm tabular-nums">{formatNumber(p.count)}</span>
              </div>
            ))}
          </CardContent>
        </Card>

        <Card>
          <CardHeader><CardTitle>{t('platform.overview.attention.title')}</CardTitle></CardHeader>
          <CardContent className="flex flex-col gap-2">
            {attention.length === 0 && <p className="text-sm text-muted">{t('platform.overview.attention.empty')}</p>}
            {attention.map(row => (
              <div key={row.organizationId} className="flex items-center justify-between border-b border-border py-2 last:border-0">
                <span className="text-sm font-medium">{row.name}</span>
                <Badge variant={row.reason === 'suspended' ? 'destructive' : 'secondary'}>{t(ATTENTION_LABEL[row.reason])}</Badge>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

function Kpi({ label, value }: { label: string; value: string }) {
  return (
    <Card><CardContent className="py-4">
      <div className="text-xs font-medium text-muted">{label}</div>
      <div className="mt-2 text-2xl font-bold tabular-nums">{value}</div>
    </CardContent></Card>
  );
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `npm test -- platform/overview/OverviewScreen`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add src/platform/overview/OverviewScreen.tsx src/platform/overview/OverviewScreen.test.tsx
git commit -m "feat(platform): platform Overview screen"
```

---

## Task 8: Wire the `adminOverview` route + `PlatformArea` into `App.tsx`

This adds the `adminOverview` route at `/admin`, an inline `PlatformArea` (mirror of `ClubArea`) that wraps the generic `AppShell` with `platformNav` and renders Overview (plus the legacy Tenants screens inside the shell until Plan 2), and points the admin audience home at the new overview.

**Files:**
- Modify: `src/App.tsx`
- Test: `src/App.routing.test.ts`
- Test: `src/App.test.tsx` (existing admin routing/render assertions must be updated for the new home + shell)

- [ ] **Step 1: Add failing routing assertions**

Append to `src/App.routing.test.ts`:

```typescript
it('resolves /admin to adminOverview', () => {
  expect(resolvePlatformRoute('/admin', null, '', 'admin').route).toEqual({ kind: 'adminOverview' });
});

it('resolves /admin/tenants to tenantList', () => {
  expect(resolvePlatformRoute('/admin/tenants', null, '', 'admin').route).toEqual({ kind: 'tenantList' });
});
```

- [ ] **Step 2: Run to verify failure**

Run: `npm test -- App.routing`
Expected: FAIL — `/admin` currently resolves to `{ kind: 'tenantList' }`.

- [ ] **Step 3: Add `adminOverview` to the `AdminRoute` union**

In `src/App.tsx`, the current `AdminRoute` type is:

```typescript
export type AdminRoute =
  | { kind: 'tenantList' }
  | { kind: 'newTenant' }
  | { kind: 'tenantDetail'; organizationId: string; initialInvite: OwnerInvite | null };
```

Replace with:

```typescript
export type AdminRoute =
  | { kind: 'adminOverview' }
  | { kind: 'tenantList' }
  | { kind: 'newTenant' }
  | { kind: 'tenantDetail'; organizationId: string; initialInvite: OwnerInvite | null };
```

- [ ] **Step 4: Teach `isAdminRoute` and `resolvePlatformRoute` about `/admin`**

In `isAdminRoute` (currently returns `route.kind === 'tenantList' || route.kind === 'newTenant' || route.kind === 'tenantDetail'`), add the overview kind:

```typescript
function isAdminRoute(route: AppRoute): route is AdminRoute {
  return route.kind === 'adminOverview'
    || route.kind === 'tenantList'
    || route.kind === 'newTenant'
    || route.kind === 'tenantDetail';
}
```

In `resolvePlatformRoute`, the current admin block has:

```typescript
    if (path === '/admin' || path === '/admin/tenants') {
      return { route: { kind: 'tenantList' } };
    }
```

Replace those lines with:

```typescript
    if (path === '/admin') {
      return { route: { kind: 'adminOverview' } };
    }
    if (path === '/admin/tenants') {
      return { route: { kind: 'tenantList' } };
    }
```

Also update the legacy `/tenants` redirect target's route kind to keep `/admin` pointing at overview — find:

```typescript
    if (path === '/tenants') {
      return { route: { kind: 'tenantList' }, redirectTo: '/admin/tenants' };
    }
```

Leave it as-is (legacy `/tenants` still maps to the tenant list at `/admin/tenants`; that is correct).

- [ ] **Step 5: Point the admin audience home at overview**

In `getAudienceHome`, the current admin branch returns:

```typescript
  return { route: { kind: 'tenantList' }, path: '/admin', label: 'Open admin tenants' };
```

Replace with:

```typescript
  return { route: { kind: 'adminOverview' }, path: '/admin', label: 'Open admin overview' };
```

- [ ] **Step 6: Run the routing test**

Run: `npm test -- App.routing`
Expected: PASS (the original club/venue test + the two new admin tests).

- [ ] **Step 7: Add platform imports to `App.tsx`**

In the top import block of `src/App.tsx`, add (next to the other `./platform`-adjacent or `./club` imports):

```typescript
import { platformNav } from './platform/nav';
import { OverviewScreen as PlatformOverviewScreen } from './platform/overview/OverviewScreen';
import { useTenantMetrics } from './platform/overview/useTenantMetrics';
```

(`OverviewScreen` is aliased to `PlatformOverviewScreen` because the club `OverviewScreen` is already imported under that name.)

- [ ] **Step 8: Add admin navigation helper + render `PlatformArea`**

In the `App` component, add a navigation helper next to the other `navigateTo*` callbacks (e.g. after `navigateToTenantDetail`):

```typescript
  const navigateToAdminRoute = useCallback(
    (nextRoute: AdminRoute, path: string) => navigate(nextRoute, path),
    [navigate]
  );
```

Then find the current admin render block (the `if (adminSession === null)` guard followed by the `return ( <> <header className="app-header"> ... </main> </> );`). Replace **from** `if (adminSession === null) {` **through** the closing `);` of that admin `return` (the block that renders `app-header` + `TenantList`/`NewTenant`/`TenantDetailView`) with:

```typescript
  if (adminSession === null) {
    return <SignIn client={adminClient} onSignedIn={() => setAdminSession(adminClient.getSession())} />;
  }

  return (
    <PlatformArea
      adminClient={adminClient}
      route={route}
      session={adminSession}
      onNavigate={navigateToAdminRoute}
      onCreateTenant={navigateToNewTenant}
      onOpenTenant={navigateToTenantDetail}
      onCreatedTenant={(response) => navigateToTenantDetail(response.tenant.organizationId, response.ownerInvite)}
      onCancelNewTenant={navigateToTenantList}
      onBackToTenants={navigateToTenantList}
      onSignOut={() => void adminClient.signOut()}
    />
  );
}
```

- [ ] **Step 9: Define the inline `PlatformArea` component**

Add the following immediately after the `App` component's closing brace (and before, or adjacent to, the `ClubArea` definition — placement among the module-level functions does not matter). It mirrors `ClubArea`:

```typescript
interface PlatformAreaProps {
  adminClient: PlatformApiClient;
  route: AdminRoute;
  session: PlatformAdminSession;
  onNavigate: (route: AdminRoute, path: string) => void;
  onCreateTenant: () => void;
  onOpenTenant: (organizationId: string) => void;
  onCreatedTenant: (response: CreateTenantResponse) => void;
  onCancelNewTenant: () => void;
  onBackToTenants: () => void;
  onSignOut: () => void;
}

const PLATFORM_ROLE_LABEL = 'Администратор';

const PLATFORM_SCREEN_TITLE: Record<AdminRoute['kind'], string> = {
  adminOverview: 'Обзор',
  tenantList: 'Тенанты',
  newTenant: 'Новый тенант',
  tenantDetail: 'Тенант'
};

function pathForAdminRoute(route: AdminRoute): string {
  switch (route.kind) {
    case 'adminOverview':
      return '/admin';
    case 'tenantList':
    case 'newTenant':
    case 'tenantDetail':
      return '/admin/tenants';
    default:
      return '/admin';
  }
}

function PlatformArea({
  adminClient, route, session, onNavigate, onCreateTenant, onOpenTenant,
  onCreatedTenant, onCancelNewTenant, onBackToTenants, onSignOut
}: PlatformAreaProps) {
  const metricsState = useTenantMetrics(adminClient);

  const handleNavigate = (path: string) => {
    const resolution = resolvePlatformRoute(path, null, '');
    if (isAdminRoute(resolution.route)) {
      onNavigate(resolution.route, resolution.redirectTo ?? path);
    }
    // Soon/unbuilt nav targets (billing, profile) resolve to notFound and are ignored.
  };

  return (
    <AppShell
      navGroups={platformNav}
      sidebarHeader={
        <div className="m-3 flex items-center gap-3 rounded-lg border border-border bg-card px-3 py-2 text-left">
          <span className="flex size-7 items-center justify-center rounded-md bg-primary text-xs font-bold text-primary-foreground">
            A
          </span>
          <span className="min-w-0">
            <span className="block truncate text-sm font-bold">AFK4 Control Plane</span>
            <span className="block truncate text-[11px] text-muted">{session.userName}</span>
          </span>
        </div>
      }
      activePath={pathForAdminRoute(route)}
      subtitle=""
      screenTitle={PLATFORM_SCREEN_TITLE[route.kind] ?? ''}
      userName={session.displayName}
      roleLabel={PLATFORM_ROLE_LABEL}
      onNavigate={handleNavigate}
      onSignOut={onSignOut}
    >
      {route.kind === 'adminOverview' ? (
        <PlatformOverviewScreen state={metricsState} />
      ) : route.kind === 'newTenant' ? (
        <NewTenant
          client={adminClient}
          onCreated={onCreatedTenant}
          onCancel={onCancelNewTenant}
        />
      ) : route.kind === 'tenantDetail' ? (
        <TenantDetailView
          client={adminClient}
          organizationId={route.organizationId}
          initialInvite={route.initialInvite}
          onBack={onBackToTenants}
        />
      ) : (
        <TenantList
          client={adminClient}
          onOpenTenant={onOpenTenant}
          onCreateTenant={onCreateTenant}
        />
      )}
    </AppShell>
  );
}
```

Note: `CreateTenantResponse` must be importable. Check the existing `import type { OwnerInvite } from './api/types';` line — extend it to `import type { CreateTenantResponse, OwnerInvite } from './api/types';` (the `NewTenant` `onCreated` callback already receives this shape today, so the type exists in `./api/types`). If `CreateTenantResponse` is not exported from `./api/types`, instead type `onCreatedTenant` against the actual `NewTenant` `onCreated` prop type — read `src/components/NewTenant.tsx` to confirm the exact exported type name and import that.

- [ ] **Step 10: Update `App.test.tsx` for the new admin home + shell**

Two breaking changes ripple into `src/App.test.tsx`: (a) `/` (and `/`-in-admin-audience) now resolves to `adminOverview`, not `tenantList`, and the audience-home label is now `'Open admin overview'`; (b) signed-in admin screens now render inside `AppShell`, which consumes `ThemeProvider`/`I18nProvider`/`ToastProvider` — so the bare `render(<App .../>)` calls for signed-in admins must switch to the file's existing `renderWithProviders(...)` helper (the same migration the club tests already went through; see the comment at the top of the file). Apply these five edits:

**Edit 1** — in the test `resolves root and legacy tenant URLs to admin routes`, the `/` case:

```typescript
    expect(resolvePlatformRoute('/')).toMatchObject({
      redirectTo: '/admin',
      route: { kind: 'tenantList' }
    });
```

becomes:

```typescript
    expect(resolvePlatformRoute('/')).toMatchObject({
      redirectTo: '/admin',
      route: { kind: 'adminOverview' }
    });
```

**Edit 2** — in the test `gates routes by the audience build flag`, the `/`-in-admin case:

```typescript
    expect(resolvePlatformRoute('/', null, '', 'admin')).toMatchObject({
      redirectTo: '/admin',
      route: { kind: 'tenantList' }
    });
```

becomes:

```typescript
    expect(resolvePlatformRoute('/', null, '', 'admin')).toMatchObject({
      redirectTo: '/admin',
      route: { kind: 'adminOverview' }
    });
```

**Edit 3** — in the test `does not render customer screens in an admin audience build` (this one stays a bare `render(...)` because the route is `notFound`, which renders no shell), update the home-button label:

```typescript
    expect(screen.getByRole('button', { name: 'Open admin tenants' })).toBeInTheDocument();
```

becomes:

```typescript
    expect(screen.getByRole('button', { name: 'Open admin overview' })).toBeInTheDocument();
```

**Edit 4** — replace the entire `redirects the old root bookmark to /admin for signed-in platform admins` test (it must use providers, and `/admin` now shows Overview, not the Tenants heading):

```typescript
  it('redirects the old root bookmark to /admin for signed-in platform admins', async () => {
    writeSession(buildSession());
    render(<App apiBaseUrl="http://localhost" />);

    await waitFor(() => expect(window.location.pathname).toBe('/admin'));
    expect(screen.getByRole('heading', { name: 'Tenants' })).toBeInTheDocument();
  });
```

becomes:

```typescript
  it('redirects the old root bookmark to /admin for signed-in platform admins', async () => {
    writeSession(buildSession());
    renderWithProviders(<App apiBaseUrl="http://localhost" />);

    await waitFor(() => expect(window.location.pathname).toBe('/admin'));
    expect(await screen.findByText('Всего тенантов')).toBeInTheDocument();
  });
```

(The `beforeEach` fetch stub returns `200 []`, so `listTenants()` resolves to an empty list and the Overview reaches `ready` with all-zero KPIs; `'Всего тенантов'` is the first KPI label.)

**Edit 5** — the two remaining signed-in-admin tests (`redirects a legacy new-tenant bookmark to the admin-prefixed screen` and `pushes admin-prefixed URLs for tenant list navigation`) each call `render(<App apiBaseUrl="http://localhost" />);`. Change BOTH of those specific calls to `renderWithProviders(<App apiBaseUrl="http://localhost" />);`. Leave every assertion in those two tests unchanged — the legacy `NewTenant` ("New tenant" heading) and `TenantList` ("Tenants" heading, "New tenant"/"Cancel" buttons) components still render their same text, now inside the shell.

Do NOT touch the club tests in this file (`renders the new AppShell with the Overview at /club`, the accept-invite/sign-in tests, etc.) — they already use `renderWithProviders` and are unaffected. Do NOT change the bare `render(...)` in `does not render customer screens in an admin audience build` or `does not render platform-admin screens in a club audience build` — both hit `notFound` and render no shell.

- [ ] **Step 11: Run the full suite + build**

Run: `npm test` then `npm run build`
Expected: all suites pass and `tsc -b && vite build` succeeds. Specifically: `App.test.tsx` passes with the five edits above; `App.branches.test.tsx` / `App.settings.test.tsx` (pure `resolvePlatformRoute`/`pathForRoute` assertions on club routes) are unaffected by Task 2/8 and still pass; the unauthenticated-admin path still renders `<SignIn>` (no shell, no providers needed). If any admin `App.test.tsx` test throws a "must be used within a Provider" error, a signed-in-admin `render(...)` was missed in Edit 5 — switch it to `renderWithProviders`.

- [ ] **Step 12: Commit**

```bash
git add src/App.tsx src/App.routing.test.ts src/App.test.tsx
git commit -m "feat(platform): adminOverview route + PlatformArea shell wiring"
```

---

## Task 9: Final verification

**Files:** none (verification only)

- [ ] **Step 1: Run the complete test suite**

Run: `npm test`
Expected: all suites pass (no regressions in club tests; new platform tests green).

- [ ] **Step 2: Run the production build (the real type gate)**

Run: `npm run build`
Expected: `tsc -b` reports no errors and `vite build` completes. If `tsc -b` errors, fix the type issue and re-run before considering the plan done — vitest does not catch type errors.

- [ ] **Step 3: Manual smoke (optional, if a dev server is wanted)**

Run: `npm run dev` and open `http://127.0.0.1:5175/admin` with the admin audience. After signing in as a platform admin, confirm the new sidebar (Обзор / Тенанты / Биллинг(скоро) / Профиль(скоро)) renders, Обзор shows the KPI cards, and clicking Тенанты shows the (still-legacy) tenant list inside the shell. (This is optional — the build + tests are the authoritative gate.)

---

## Self-review notes (addressed)

- **Spec coverage (Plan 1 slice):** `src/platform/` module ✔ (Task 3/5/6/7); `PlatformArea` wrapping `AppShell` with no branch switcher ✔ (Task 8, `sidebarHeader` is a brand block, not `BranchSwitcher`); platform nav (Обзор/Тенанты/Биллинг/Профиль) ✔ (Task 3, billing+profile `soon`); Overview with client-side KPIs from `listTenants` ✔ (Task 5–7); legacy Tenants screens kept until Plan 2 ✔ (Task 8 renders them in-shell). Backend `/metrics`, tenant redesign, billing, profile screen are later plans per the spec — intentionally out of this plan.
- **Type consistency:** hook state type `TenantMetricsState`, builder `buildTenantMetrics`, view-model `PlatformMetricsViewModel`, `AttentionReason`/`AttentionRow`/`PlanCount` are referenced identically across Tasks 5–8. `NavGroup` from `@/components/shell/navModel` is used by club nav, platform nav, NavList, and AppShell.
- **Decoupling direction:** after Task 1–2 the shell depends on no `@/club` symbol; club depends on the shell's `navModel` (correct direction). The `AppShell.test` deliberately re-imports `visibleNav`/`BranchSwitcher` to exercise the real club composition — acceptable in a test.
- **ru/en parity:** Task 4 adds the same 20 keys to both locales; the existing parity test guards it.
