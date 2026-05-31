# Platform New-Tenant Flow + Profile + Legacy Cleanup (SP3 Plan 6) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Finish the `/admin/*` control-plane redesign by (1) adding a real **Platform Profile** screen at `/admin/profile`, (2) redesigning the **New-tenant** flow onto the shared design system, and (3) redesigning the tenant-detail **Health** section onto the design system so the last two legacy admin components (`NewTenant.tsx`, `HealthSection.tsx`) can be deleted.

**Architecture:** Frontend-only (no backend changes). New per-feature folders under `src/platform/` mirror the club architecture: `profile/` = pure `profileModel` (`groupPermissions`) + presentational `ProfileScreen` fed the `PlatformAdminSession` as a prop (no hook — the session is the data, exactly like the club `ProfileScreen`). `tenants/NewTenantScreen.tsx` is a presentational form (design-system `Card`/`Input`/`Select`/`Button`, toast on result) replacing the legacy `NewTenant.tsx`. `tenants/TenantHealthSection.tsx` loads `getHealth` with the same `useEffect`+`tick` pattern as the other redesigned sections, replacing the legacy `HealthSection.tsx`. `App.tsx` gains an `adminProfile` route and wires the three screens into `PlatformArea`; `TenantDrawer` swaps the legacy health card for the new one; the two legacy files are deleted.

**Tech Stack:** React 19 + Vite + Tailwind v4 + Radix, TypeScript (build gate `tsc -b`), Vitest (`globals: false` — import `it/expect/vi/describe/beforeAll` from `'vitest'`), `@testing-library/react`. Commands run from `src/AFK4.Platform.Web`.

---

## Key facts the implementer must know (verified 2026-05-31)

- **Vitest globals are OFF.** Every test file imports its helpers: `import { it, expect, vi } from 'vitest';` (add `describe`/`beforeAll`/`waitFor` as needed). Test setup auto-cleans (`src/test/setup.ts`).
- **The build gate is `tsc -b`, not the test run.** `npm test` (vitest/esbuild) skips type-checking. After every task run BOTH `npm test` and `npm run build` (which runs `tsc -b && vite build`). A change can pass tests yet fail the build on a type error.
- **i18n parity is enforced.** `messages.test.ts` asserts `Object.keys(messages.en).sort()` equals `Object.keys(messages.ru).sort()`. Every new key MUST be added to BOTH `ru` and `en`. Keys are flat strings; **key order does not matter, only the key set.** New keys reference `MessageKey` (a union of existing keys), so any component referencing a not-yet-added key fails `tsc -b` until Task 1 lands — that is why **i18n keys are added first (Task 1)**.
- **Radix `Select` in tests needs pointer/scroll shims.** Any test that *renders* a component containing `@/components/ui/select` (even without interacting) should add the `beforeAll` shim block used by `TenantOwnerInvitesSection.test.tsx` (see Task 4). Tests that only render a `Select` at its default value (no dropdown interaction) work with the shim and do not need to open the dropdown.
- **`groupPermissions` already exists in `src/club/profile/profileModel.ts`** but the platform module must NOT import from `@/club` (Plan 1 deliberately decoupled the areas). We duplicate the tiny pure function into `src/platform/profile/profileModel.ts`.
- **`PlatformAdminSession`** (`src/auth/tokenStore.ts`): `{ platformAdminId, userName, displayName, roles: string[], permissions: string[], accessToken, accessTokenExpiresAtUtc, refreshToken, refreshTokenExpiresAtUtc }`. No branches. The session is read once in `App.tsx` and passed to `PlatformArea` as `session` — no API call needed for the profile.
- **`PlatformApiClient.createTenant(request: CreateTenantRequest): Promise<CreateTenantResponse>`** and **`PlatformApiClient.getHealth(organizationId: string): Promise<TenantHealth>`** already exist (`src/api/platformApi.ts`). `TenantLimits`, `CreateTenantRequest`, `CreateTenantResponse`, `TenantHealth`, `TenantHealthError` are in `src/api/types.ts`. `TenantPlanCode` (`Starter`/`Growth`/`Scale`) and `SubscriptionStatus` (`Trial`/`Active`/`PastDue`/`Cancelled`) are exported `as const` objects from `src/api/types.ts`.
- **Design-system primitives** live under `@/components/ui/*`: `card` (`Card`/`CardContent`/`CardHeader`/`CardTitle`), `button` (`Button`), `input` (`Input`), `badge` (`Badge`), `select` (`Select`/`SelectTrigger`/`SelectValue`/`SelectContent`/`SelectItem`), `table` (`Table`/`TableHeader`/`TableBody`/`TableRow`/`TableHead`/`TableCell`), `states` (`LoadingCards`/`ErrorState`/`EmptyState`), `toast` (`ToastProvider`/`useToast`). `useI18n()` returns `{ t, formatNumber, formatDate, formatCurrency }`.
- **The legacy `src/components/ui.tsx` module stays.** It still exports `Field`/`ErrorBanner` used by `SignIn.tsx`, `AcceptInvite.tsx`, `StaffSignIn.tsx` (all explicitly out of scope per the spec). Do NOT delete `ui.tsx`. After this plan its `Loading`/`StatusBadge`/`EmptyState` exports become unused (only `HealthSection` used them) — leaving unused exports in a kept module is fine and does not fail the build.
- **Harness caveat (this machine):** Read/grep intermittently return scrambled content. Trust `git show HEAD:<path>`, the `tsc -b` exit code, and the vitest run over eyeballed output. Read the region live immediately before editing when an anchor is described rather than line-pinned.

---

## File structure

**New files (`src/AFK4.Platform.Web/src/`):**
- `platform/profile/profileModel.ts` — pure `groupPermissions(permissions)` → `PermissionGroup[]`.
- `platform/profile/profileModel.test.ts`
- `platform/profile/ProfileScreen.tsx` — presentational platform-admin profile (identity / roles / permissions / sign-out).
- `platform/profile/ProfileScreen.test.tsx`
- `platform/tenants/NewTenantScreen.tsx` — redesigned new-tenant form (replaces legacy `components/NewTenant.tsx`).
- `platform/tenants/NewTenantScreen.test.tsx`
- `platform/tenants/TenantHealthSection.tsx` — redesigned tenant health card (replaces legacy `components/HealthSection.tsx`).
- `platform/tenants/TenantHealthSection.test.tsx`

**Modified files:**
- `i18n/messages.ts` — add 47 platform keys (ru + en).
- `i18n/messages.test.ts` — assert the new keys.
- `platform/nav.ts` — flip `profile` `soon: true` → `false`.
- `platform/nav.test.ts` — assert `profile` is now live.
- `platform/tenants/TenantDrawer.tsx` — swap `HealthSection` for `TenantHealthSection`.
- `App.tsx` — `adminProfile` route + `PlatformArea` wiring for profile + new-tenant screen swap + imports.
- `App.routing.test.ts` — add `/admin/profile` route assertion.
- `App.test.tsx` — update the two legacy new-tenant assertions to the redesigned screen.

**Deleted files:**
- `components/NewTenant.tsx`
- `components/HealthSection.tsx`

---

## Task 1: i18n keys (profile + new-tenant + health)

**Files:**
- Modify: `src/i18n/messages.ts`
- Test: `src/i18n/messages.test.ts`

- [ ] **Step 1: Add the failing test blocks**

In `src/i18n/messages.test.ts`, append these three `it(...)` blocks at the end of the file (after the existing `'includes the tenants admin keys'` block, before EOF):

```typescript
it('includes the platform profile keys', () => {
  for (const key of [
    'platform.profile.field.userName', 'platform.profile.field.adminId',
    'platform.profile.roles.title', 'platform.profile.roles.empty'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});

it('includes the new-tenant keys', () => {
  for (const key of [
    'platform.newTenant.section.organization', 'platform.newTenant.section.branch',
    'platform.newTenant.section.plan', 'platform.newTenant.section.limits', 'platform.newTenant.section.owner',
    'platform.newTenant.field.orgSlug', 'platform.newTenant.field.orgSlugHint', 'platform.newTenant.field.orgName',
    'platform.newTenant.field.branchSlug', 'platform.newTenant.field.branchName', 'platform.newTenant.field.branchCity',
    'platform.newTenant.field.planCode', 'platform.newTenant.field.subscriptionStatus',
    'platform.newTenant.field.maxBranches', 'platform.newTenant.field.maxDevices',
    'platform.newTenant.field.maxSessions', 'platform.newTenant.field.maxStaff',
    'platform.newTenant.field.ownerUserName', 'platform.newTenant.field.ownerDisplayName',
    'platform.newTenant.sub.trial', 'platform.newTenant.sub.active', 'platform.newTenant.sub.pastDue', 'platform.newTenant.sub.cancelled',
    'platform.newTenant.submit', 'platform.newTenant.submitting', 'platform.newTenant.cancel',
    'platform.newTenant.created', 'platform.newTenant.error'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});

it('includes the tenant health keys', () => {
  for (const key of [
    'platform.tenant.section.health', 'platform.tenant.health.refresh',
    'platform.tenant.health.status', 'platform.tenant.health.branches', 'platform.tenant.health.devices',
    'platform.tenant.health.activeStaff', 'platform.tenant.health.lastSignIn', 'platform.tenant.health.latestMigration',
    'platform.tenant.health.recentErrors', 'platform.tenant.health.recentErrorsEmpty',
    'platform.tenant.health.col.time', 'platform.tenant.health.col.source', 'platform.tenant.health.col.action',
    'platform.tenant.health.col.outcome', 'platform.tenant.health.col.message', 'platform.tenant.health.error'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `npm test -- i18n/messages`
Expected: FAIL — the three new blocks fail because the keys are undefined (the parity test still passes for now).

- [ ] **Step 3: Add the keys to the `ru` block**

Open `src/i18n/messages.ts`. In the `ru:` object, find the line:

```typescript
    'platform.tenant.section.invites': 'Коды настройки',
```

Immediately AFTER that line, insert:

```typescript
    'platform.profile.field.userName': 'Логин',
    'platform.profile.field.adminId': 'ID администратора',
    'platform.profile.roles.title': 'Роли',
    'platform.profile.roles.empty': 'Роли не назначены.',
    'platform.newTenant.section.organization': 'Организация',
    'platform.newTenant.section.branch': 'Первый филиал',
    'platform.newTenant.section.plan': 'Тариф',
    'platform.newTenant.section.limits': 'Лимиты (необязательно)',
    'platform.newTenant.section.owner': 'Получатель кода настройки (необязательно)',
    'platform.newTenant.field.orgSlug': 'Ключ тенанта',
    'platform.newTenant.field.orgSlugHint': 'URL-безопасный ключ: 3–64 символа, a-z, 0-9, дефисы между сегментами.',
    'platform.newTenant.field.orgName': 'Название',
    'platform.newTenant.field.branchSlug': 'Ключ филиала',
    'platform.newTenant.field.branchName': 'Название филиала',
    'platform.newTenant.field.branchCity': 'Город',
    'platform.newTenant.field.planCode': 'Тариф',
    'platform.newTenant.field.subscriptionStatus': 'Статус подписки',
    'platform.newTenant.field.maxBranches': 'Макс. филиалов',
    'platform.newTenant.field.maxDevices': 'Макс. устройств на филиал',
    'platform.newTenant.field.maxSessions': 'Макс. одновременных сессий',
    'platform.newTenant.field.maxStaff': 'Макс. сотрудников на филиал',
    'platform.newTenant.field.ownerUserName': 'Логин владельца (email)',
    'platform.newTenant.field.ownerDisplayName': 'Имя владельца',
    'platform.newTenant.sub.trial': 'Триал',
    'platform.newTenant.sub.active': 'Активна',
    'platform.newTenant.sub.pastDue': 'Просрочена',
    'platform.newTenant.sub.cancelled': 'Отменена',
    'platform.newTenant.submit': 'Создать тенант',
    'platform.newTenant.submitting': 'Создание…',
    'platform.newTenant.cancel': 'Отмена',
    'platform.newTenant.created': 'Тенант создан',
    'platform.newTenant.error': 'Не удалось создать тенанта.',
    'platform.tenant.section.health': 'Состояние',
    'platform.tenant.health.refresh': 'Обновить',
    'platform.tenant.health.status': 'Статус',
    'platform.tenant.health.branches': 'Филиалы',
    'platform.tenant.health.devices': 'Устройства',
    'platform.tenant.health.activeStaff': 'Активные сотрудники',
    'platform.tenant.health.lastSignIn': 'Последний вход сотрудника',
    'platform.tenant.health.latestMigration': 'Последняя миграция',
    'platform.tenant.health.recentErrors': 'Отклонённые аудиты (7д)',
    'platform.tenant.health.recentErrorsEmpty': 'Нет отклонённых аудитов за 7 дней.',
    'platform.tenant.health.col.time': 'Время',
    'platform.tenant.health.col.source': 'Источник',
    'platform.tenant.health.col.action': 'Действие',
    'platform.tenant.health.col.outcome': 'Результат',
    'platform.tenant.health.col.message': 'Сообщение',
    'platform.tenant.health.error': 'Не удалось загрузить состояние.',
```

- [ ] **Step 4: Add the same keys to the `en` block**

In the same file, find the line in the `en:` object:

```typescript
    'platform.tenant.section.invites': 'Setup codes',
```

Immediately AFTER that line, insert:

```typescript
    'platform.profile.field.userName': 'Username',
    'platform.profile.field.adminId': 'Admin ID',
    'platform.profile.roles.title': 'Roles',
    'platform.profile.roles.empty': 'No roles assigned.',
    'platform.newTenant.section.organization': 'Organization',
    'platform.newTenant.section.branch': 'First branch',
    'platform.newTenant.section.plan': 'Plan',
    'platform.newTenant.section.limits': 'Limits (optional)',
    'platform.newTenant.section.owner': 'Setup-code recipient (optional)',
    'platform.newTenant.field.orgSlug': 'Tenant key',
    'platform.newTenant.field.orgSlugHint': 'URL-safe key: 3–64 chars, a-z, 0-9, hyphens between segments.',
    'platform.newTenant.field.orgName': 'Name',
    'platform.newTenant.field.branchSlug': 'Branch key',
    'platform.newTenant.field.branchName': 'Branch name',
    'platform.newTenant.field.branchCity': 'City',
    'platform.newTenant.field.planCode': 'Plan code',
    'platform.newTenant.field.subscriptionStatus': 'Subscription status',
    'platform.newTenant.field.maxBranches': 'Max branches',
    'platform.newTenant.field.maxDevices': 'Max devices per branch',
    'platform.newTenant.field.maxSessions': 'Max concurrent sessions',
    'platform.newTenant.field.maxStaff': 'Max staff users per branch',
    'platform.newTenant.field.ownerUserName': 'Owner username (email)',
    'platform.newTenant.field.ownerDisplayName': 'Owner display name',
    'platform.newTenant.sub.trial': 'Trial',
    'platform.newTenant.sub.active': 'Active',
    'platform.newTenant.sub.pastDue': 'Past due',
    'platform.newTenant.sub.cancelled': 'Cancelled',
    'platform.newTenant.submit': 'Create tenant',
    'platform.newTenant.submitting': 'Creating…',
    'platform.newTenant.cancel': 'Cancel',
    'platform.newTenant.created': 'Tenant created',
    'platform.newTenant.error': 'Failed to create tenant.',
    'platform.tenant.section.health': 'Health',
    'platform.tenant.health.refresh': 'Refresh',
    'platform.tenant.health.status': 'Status',
    'platform.tenant.health.branches': 'Branches',
    'platform.tenant.health.devices': 'Devices',
    'platform.tenant.health.activeStaff': 'Active staff',
    'platform.tenant.health.lastSignIn': 'Latest staff sign-in',
    'platform.tenant.health.latestMigration': 'Latest migration',
    'platform.tenant.health.recentErrors': 'Recent denied audits (7d)',
    'platform.tenant.health.recentErrorsEmpty': 'No denied audit records in the last 7 days.',
    'platform.tenant.health.col.time': 'Time',
    'platform.tenant.health.col.source': 'Source',
    'platform.tenant.health.col.action': 'Action',
    'platform.tenant.health.col.outcome': 'Outcome',
    'platform.tenant.health.col.message': 'Message',
    'platform.tenant.health.error': 'Failed to load health.',
```

(If a scrambled read means the `en` anchor line is not found verbatim, instead insert the `en` keys immediately before the `}` that closes the `en:` object — only ru/en key-set parity matters, not position.)

- [ ] **Step 5: Run the tests + full typecheck**

Run: `npm test -- i18n/messages` then `npm run build`
Expected: all four i18n test blocks pass (parity + 3 new); `npm run build` succeeds. 47 keys added to each locale.

- [ ] **Step 6: Commit**

```bash
git add src/i18n/messages.ts src/i18n/messages.test.ts
git commit -m "feat(platform): i18n keys for profile, new-tenant flow, tenant health"
```

---

## Task 2: Platform profile model + screen

**Files:**
- Create: `src/platform/profile/profileModel.ts`
- Test: `src/platform/profile/profileModel.test.ts`
- Create: `src/platform/profile/ProfileScreen.tsx`
- Test: `src/platform/profile/ProfileScreen.test.tsx`

- [ ] **Step 1: Write the failing model test**

Create `src/platform/profile/profileModel.test.ts`:

```typescript
import { describe, expect, it } from 'vitest';
import { groupPermissions } from './profileModel';

describe('platform groupPermissions', () => {
  it('groups permissions by prefix and sorts within and across groups', () => {
    const groups = groupPermissions(['tenants.write', 'billing.refund', 'tenants.read']);
    expect(groups).toEqual([
      { key: 'billing', permissions: ['billing.refund'] },
      { key: 'tenants', permissions: ['tenants.read', 'tenants.write'] }
    ]);
  });

  it('uses the whole string as the key when there is no dot', () => {
    expect(groupPermissions(['superuser'])).toEqual([{ key: 'superuser', permissions: ['superuser'] }]);
  });
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `npm test -- platform/profile/profileModel`
Expected: FAIL — cannot resolve `./profileModel`.

- [ ] **Step 3: Write `profileModel.ts`**

Create `src/platform/profile/profileModel.ts`:

```typescript
export interface PermissionGroup { key: string; permissions: string[]; }

export function groupPermissions(permissions: readonly string[]): PermissionGroup[] {
  const map = new Map<string, string[]>();
  for (const permission of [...permissions].sort()) {
    const dot = permission.indexOf('.');
    const key = dot === -1 ? permission : permission.slice(0, dot);
    const list = map.get(key) ?? [];
    list.push(permission);
    map.set(key, list);
  }
  return [...map.entries()].map(([key, perms]) => ({ key, permissions: perms }));
}
```

- [ ] **Step 4: Run the model test to verify it passes**

Run: `npm test -- platform/profile/profileModel`
Expected: PASS (2 tests).

- [ ] **Step 5: Write the failing screen test**

Create `src/platform/profile/ProfileScreen.test.tsx`:

```typescript
import { render, screen, fireEvent } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import type { PlatformAdminSession } from '@/auth/tokenStore';
import { ProfileScreen } from './ProfileScreen';

const session = {
  platformAdminId: 'pa-1', userName: 'admin@afk4.io', displayName: 'Админ',
  roles: ['platform_admin'], permissions: ['tenants.read', 'billing.invoice.void'],
  accessToken: 'a', accessTokenExpiresAtUtc: '', refreshToken: 'r', refreshTokenExpiresAtUtc: ''
} as PlatformAdminSession;

it('shows identity, roles, permissions, and signs out', () => {
  const onSignOut = vi.fn();
  render(
    <I18nProvider>
      <ProfileScreen session={session} onSignOut={onSignOut} />
    </I18nProvider>
  );
  expect(screen.getByText('Админ')).toBeInTheDocument();
  expect(screen.getByText('admin@afk4.io')).toBeInTheDocument();
  expect(screen.getByText('platform_admin')).toBeInTheDocument();
  expect(screen.getByText('tenants.read')).toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Выйти' }));
  expect(onSignOut).toHaveBeenCalled();
});
```

- [ ] **Step 6: Run it to verify it fails**

Run: `npm test -- platform/profile/ProfileScreen`
Expected: FAIL — cannot resolve `./ProfileScreen`.

- [ ] **Step 7: Write `ProfileScreen.tsx`**

Create `src/platform/profile/ProfileScreen.tsx`:

```typescript
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { useI18n } from '@/i18n/I18nProvider';
import type { PlatformAdminSession } from '@/auth/tokenStore';
import { groupPermissions } from './profileModel';

export function ProfileScreen({ session, onSignOut }: {
  session: PlatformAdminSession;
  onSignOut: () => void;
}) {
  const { t } = useI18n();
  const groups = groupPermissions(session.permissions);

  return (
    <div className="flex max-w-3xl flex-col gap-4">
      <Card>
        <CardHeader><CardTitle>{t('profile.identity.title')}</CardTitle></CardHeader>
        <CardContent className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <Field label={t('profile.field.displayName')} value={session.displayName} />
          <Field label={t('platform.profile.field.userName')} value={session.userName} />
          <Field label={t('platform.profile.field.adminId')} value={session.platformAdminId} />
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>{t('platform.profile.roles.title')}</CardTitle></CardHeader>
        <CardContent>
          {session.roles.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t('platform.profile.roles.empty')}</p>
          ) : (
            <div className="flex flex-wrap gap-1">
              {session.roles.map(r => <Badge key={r} variant="secondary">{r}</Badge>)}
            </div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>{t('profile.permissions.title')}</CardTitle></CardHeader>
        <CardContent className="flex flex-col gap-3">
          {groups.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t('profile.permissions.empty')}</p>
          ) : (
            groups.map(group => (
              <div key={group.key} className="flex flex-col gap-1">
                <div className="text-xs font-medium uppercase text-muted-foreground">{group.key}</div>
                <div className="flex flex-wrap gap-1">
                  {group.permissions.map(p => <Badge key={p} variant="secondary">{p}</Badge>)}
                </div>
              </div>
            ))
          )}
        </CardContent>
      </Card>

      <p className="text-xs text-muted-foreground">{t('profile.editUnavailable')}</p>
      <div><Button variant="outline" onClick={onSignOut}>{t('shell.signOut')}</Button></div>
    </div>
  );
}

function Field({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex flex-col gap-0.5">
      <span className="text-xs text-muted-foreground">{label}</span>
      <span className="text-sm font-medium break-all">{value}</span>
    </div>
  );
}
```

(Reuses existing keys `profile.identity.title`, `profile.field.displayName`, `profile.permissions.title`, `profile.permissions.empty`, `profile.editUnavailable`, `shell.signOut` — all present today; `shell.signOut` renders "Выйти".)

- [ ] **Step 8: Run the screen test + full typecheck**

Run: `npm test -- platform/profile/ProfileScreen` then `npm run build`
Expected: screen test passes; `npm run build` succeeds.

- [ ] **Step 9: Commit**

```bash
git add src/platform/profile/profileModel.ts src/platform/profile/profileModel.test.ts src/platform/profile/ProfileScreen.tsx src/platform/profile/ProfileScreen.test.tsx
git commit -m "feat(platform): platform admin Profile screen + permissions model"
```

---

## Task 3: Wire the `adminProfile` route + flip the nav flag

Adds the `/admin/profile` route, renders the new `ProfileScreen` inside `PlatformArea`, and flips the platform nav `profile` item from `soon: true` to a live link.

**Files:**
- Modify: `src/platform/nav.ts`
- Modify: `src/platform/nav.test.ts`
- Modify: `src/App.tsx`
- Modify: `src/App.routing.test.ts`

- [ ] **Step 1: Update the nav test (failing assertion first)**

In `src/platform/nav.test.ts`, the second test currently reads:

```typescript
  it('marks overview, tenants and billing live, profile soon', () => {
    const items = platformNav.flatMap(g => g.items);
    expect(items.find(i => i.key === 'overview')?.soon).toBe(false);
    expect(items.find(i => i.key === 'tenants')?.soon).toBe(false);
    expect(items.find(i => i.key === 'billing')?.soon).toBe(false);
    expect(items.find(i => i.key === 'profile')?.soon).toBe(true);
  });
```

Replace that whole test with:

```typescript
  it('marks every platform nav item live', () => {
    const items = platformNav.flatMap(g => g.items);
    expect(items.find(i => i.key === 'overview')?.soon).toBe(false);
    expect(items.find(i => i.key === 'tenants')?.soon).toBe(false);
    expect(items.find(i => i.key === 'billing')?.soon).toBe(false);
    expect(items.find(i => i.key === 'profile')?.soon).toBe(false);
  });
```

- [ ] **Step 2: Run it to verify it fails**

Run: `npm test -- platform/nav`
Expected: FAIL — `profile` is still `soon: true`.

- [ ] **Step 3: Flip the nav flag**

In `src/platform/nav.ts`, change the `profile` item line:

```typescript
      { key: 'profile', labelKey: 'nav.platform.profile', path: '/admin/profile', ownerOnly: false, soon: true }
```

to:

```typescript
      { key: 'profile', labelKey: 'nav.platform.profile', path: '/admin/profile', ownerOnly: false, soon: false }
```

- [ ] **Step 4: Run the nav test to verify it passes**

Run: `npm test -- platform/nav`
Expected: PASS (3 tests).

- [ ] **Step 5: Add the failing routing assertion**

Append to `src/App.routing.test.ts`:

```typescript
it('resolves /admin/profile to adminProfile', () => {
  expect(resolvePlatformRoute('/admin/profile', null, '', 'admin').route).toEqual({ kind: 'adminProfile' });
});
```

- [ ] **Step 6: Run to verify failure**

Run: `npm test -- App.routing`
Expected: FAIL — `/admin/profile` currently resolves to `notFound`.

- [ ] **Step 7: Add `adminProfile` to the `AdminRoute` union**

In `src/App.tsx`, the current union is:

```typescript
export type AdminRoute =
  | { kind: 'adminOverview' }
  | { kind: 'adminBilling' }
  | { kind: 'tenantList' }
  | { kind: 'newTenant' }
  | { kind: 'tenantDetail'; organizationId: string; initialInvite: OwnerInvite | null };
```

Replace with:

```typescript
export type AdminRoute =
  | { kind: 'adminOverview' }
  | { kind: 'adminBilling' }
  | { kind: 'adminProfile' }
  | { kind: 'tenantList' }
  | { kind: 'newTenant' }
  | { kind: 'tenantDetail'; organizationId: string; initialInvite: OwnerInvite | null };
```

- [ ] **Step 8: Teach `isAdminRoute` and `resolvePlatformRoute` about `/admin/profile`**

In `isAdminRoute` (currently lists `adminOverview`/`adminBilling`/`tenantList`/`newTenant`/`tenantDetail`), add the profile kind:

```typescript
function isAdminRoute(route: AppRoute): route is AdminRoute {
  return route.kind === 'adminOverview'
    || route.kind === 'adminBilling'
    || route.kind === 'adminProfile'
    || route.kind === 'tenantList'
    || route.kind === 'newTenant'
    || route.kind === 'tenantDetail';
}
```

In `resolvePlatformRoute`, the current admin block has:

```typescript
    if (path === '/admin/billing') {
      return { route: { kind: 'adminBilling' } };
    }
```

Immediately after that `if`, add:

```typescript
    if (path === '/admin/profile') {
      return { route: { kind: 'adminProfile' } };
    }
```

- [ ] **Step 9: Add the profile screen title + path mapping**

In `PLATFORM_SCREEN_TITLE` (currently `adminOverview`/`adminBilling`/`tenantList`/`newTenant`/`tenantDetail`), add the profile entry:

```typescript
const PLATFORM_SCREEN_TITLE: Record<AdminRoute['kind'], string> = {
  adminOverview: 'Обзор',
  adminBilling: 'Биллинг',
  adminProfile: 'Профиль',
  tenantList: 'Тенанты',
  newTenant: 'Новый тенант',
  tenantDetail: 'Тенант'
};
```

In `pathForAdminRoute`, add a case for the profile route. The current switch has `case 'adminBilling': return '/admin/billing';` — add directly after it:

```typescript
    case 'adminProfile':
      return '/admin/profile';
```

- [ ] **Step 10: Import the platform `ProfileScreen` and render it in `PlatformArea`**

In the top import block of `src/App.tsx`, next to the other `./platform/*` imports (e.g. after the `TenantsScreen` import), add:

```typescript
import { ProfileScreen as PlatformProfileScreen } from './platform/profile/ProfileScreen';
```

(`ProfileScreen` is aliased because the club `ProfileScreen` is already imported under that name.)

In `PlatformArea`'s render ternary, the current chain begins:

```tsx
      {route.kind === 'adminOverview' ? (
        <PlatformOverviewScreen state={metricsState} billing={billingMetricsState} />
      ) : route.kind === 'adminBilling' ? (
        <PlatformBillingScreen client={adminClient} />
      ) : route.kind === 'newTenant' ? (
```

Insert an `adminProfile` branch between the `adminBilling` and `newTenant` branches so it reads:

```tsx
      {route.kind === 'adminOverview' ? (
        <PlatformOverviewScreen state={metricsState} billing={billingMetricsState} />
      ) : route.kind === 'adminBilling' ? (
        <PlatformBillingScreen client={adminClient} />
      ) : route.kind === 'adminProfile' ? (
        <PlatformProfileScreen session={session} onSignOut={onSignOut} />
      ) : route.kind === 'newTenant' ? (
```

Also update the now-stale comment inside `PlatformArea`'s `handleNavigate`:

```typescript
    // Soon/unbuilt nav targets (profile) resolve to notFound and are ignored; live targets
    // (overview, billing, tenants) navigate.
```

Replace those two comment lines with:

```typescript
    // All platform nav targets (overview, tenants, billing, profile) resolve to admin routes.
```

- [ ] **Step 11: Run routing test + full typecheck**

Run: `npm test -- App.routing` then `npm run build`
Expected: routing tests pass (club/venue + the three admin assertions); `npm run build` succeeds (`PlatformProfileScreen` props match; the `Record<AdminRoute['kind'], string>` is exhaustive with `adminProfile`).

- [ ] **Step 12: Commit**

```bash
git add src/platform/nav.ts src/platform/nav.test.ts src/App.tsx src/App.routing.test.ts
git commit -m "feat(platform): adminProfile route + live profile nav link"
```

---

## Task 4: Redesigned new-tenant flow

Replaces the legacy `components/NewTenant.tsx` with a design-system `platform/tenants/NewTenantScreen.tsx`, wires it into `PlatformArea`, and deletes the legacy file.

**Files:**
- Create: `src/platform/tenants/NewTenantScreen.tsx`
- Test: `src/platform/tenants/NewTenantScreen.test.tsx`
- Modify: `src/App.tsx`
- Modify: `src/App.test.tsx`
- Delete: `src/components/NewTenant.tsx`

- [ ] **Step 1: Write the failing screen test**

Create `src/platform/tenants/NewTenantScreen.test.tsx`:

```typescript
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi, beforeAll } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { NewTenantScreen } from './NewTenantScreen';

beforeAll(() => {
  window.HTMLElement.prototype.hasPointerCapture = () => false;
  window.HTMLElement.prototype.scrollIntoView = () => {};
  window.HTMLElement.prototype.releasePointerCapture = () => {};
});

const response = {
  tenant: { organizationId: 'org-9' },
  ownerInvite: { ownerInviteId: 'i1', code: 'X' }
} as never;

function renderScreen(client: any, onCreated = vi.fn(), onCancel = vi.fn()) {
  render(
    <I18nProvider><ToastProvider>
      <NewTenantScreen client={client} onCreated={onCreated} onCancel={onCancel} />
    </ToastProvider></I18nProvider>
  );
  return { onCreated, onCancel };
}

function fillRequired() {
  fireEvent.change(screen.getByLabelText('Ключ тенанта'), { target: { value: '  victory  ' } });
  fireEvent.change(screen.getByLabelText('Название'), { target: { value: 'Victory' } });
  fireEvent.change(screen.getByLabelText('Ключ филиала'), { target: { value: 'main' } });
  fireEvent.change(screen.getByLabelText('Название филиала'), { target: { value: 'Main' } });
  fireEvent.change(screen.getByLabelText('Город'), { target: { value: 'Moscow' } });
}

it('submits trimmed values with the default plan/status and calls onCreated', async () => {
  const client = { createTenant: vi.fn().mockResolvedValue(response) };
  const { onCreated } = renderScreen(client);

  fillRequired();
  fireEvent.click(screen.getByRole('button', { name: 'Создать тенант' }));

  await waitFor(() => expect(client.createTenant).toHaveBeenCalled());
  const payload = client.createTenant.mock.calls[0][0];
  expect(payload.organizationSlug).toBe('victory');
  expect(payload.organizationName).toBe('Victory');
  expect(payload.planCode).toBe('starter');
  expect(payload.subscriptionStatus).toBe('trial');
  expect(payload.limits).toBeNull();
  await waitFor(() => expect(onCreated).toHaveBeenCalledWith(response));
});

it('shows an inline error when creation fails', async () => {
  const client = { createTenant: vi.fn().mockRejectedValue(new Error('slug taken')) };
  renderScreen(client);
  fillRequired();
  fireEvent.click(screen.getByRole('button', { name: 'Создать тенант' }));
  expect(await screen.findByText('slug taken')).toBeInTheDocument();
});

it('cancels without submitting', () => {
  const client = { createTenant: vi.fn() };
  const { onCancel } = renderScreen(client);
  fireEvent.click(screen.getByRole('button', { name: 'Отмена' }));
  expect(onCancel).toHaveBeenCalled();
  expect(client.createTenant).not.toHaveBeenCalled();
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `npm test -- platform/tenants/NewTenantScreen`
Expected: FAIL — cannot resolve `./NewTenantScreen`.

- [ ] **Step 3: Write `NewTenantScreen.tsx`**

Create `src/platform/tenants/NewTenantScreen.tsx`:

```typescript
import { useState, type FormEvent } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { PlatformApiClient } from '@/api/platformApi';
import { TenantPlanCode, SubscriptionStatus, type CreateTenantResponse, type TenantLimits } from '@/api/types';

type Client = Pick<PlatformApiClient, 'createTenant'>;

export interface NewTenantScreenProps {
  client: Client;
  onCreated: (response: CreateTenantResponse) => void;
  onCancel: () => void;
}

interface FormState {
  organizationSlug: string;
  organizationName: string;
  branchSlug: string;
  branchName: string;
  branchCity: string;
  planCode: string;
  subscriptionStatus: string;
  ownerUserName: string;
  ownerDisplayName: string;
  maxBranches: string;
  maxDevicesPerBranch: string;
  maxConcurrentSessions: string;
  maxStaffUsersPerBranch: string;
}

const defaultState: FormState = {
  organizationSlug: '',
  organizationName: '',
  branchSlug: 'main',
  branchName: 'Main Branch',
  branchCity: '',
  planCode: TenantPlanCode.Starter,
  subscriptionStatus: SubscriptionStatus.Trial,
  ownerUserName: '',
  ownerDisplayName: '',
  maxBranches: '',
  maxDevicesPerBranch: '',
  maxConcurrentSessions: '',
  maxStaffUsersPerBranch: ''
};

export function NewTenantScreen({ client, onCreated, onCancel }: NewTenantScreenProps) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [form, setForm] = useState<FormState>(defaultState);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  function update(field: keyof FormState, value: string) {
    setForm(current => ({ ...current, [field]: value }));
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      const response = await client.createTenant({
        organizationSlug: form.organizationSlug.trim(),
        organizationName: form.organizationName.trim(),
        branchSlug: form.branchSlug.trim(),
        branchName: form.branchName.trim(),
        branchCity: form.branchCity.trim(),
        planCode: form.planCode,
        subscriptionStatus: form.subscriptionStatus,
        limits: buildLimits(form),
        ownerUserName: form.ownerUserName.trim() === '' ? null : form.ownerUserName.trim(),
        ownerDisplayName: form.ownerDisplayName.trim() === '' ? null : form.ownerDisplayName.trim(),
        ownerInviteLifetime: null
      });
      toast({ title: t('platform.newTenant.created'), variant: 'success' });
      onCreated(response);
    } catch (cause) {
      const message = cause instanceof Error ? cause.message : t('platform.newTenant.error');
      setError(message);
      toast({ title: t('platform.newTenant.error'), variant: 'error' });
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form className="flex max-w-3xl flex-col gap-4" onSubmit={handleSubmit}>
      {error !== null && (
        <Card><CardContent className="py-3 text-sm text-destructive">{error}</CardContent></Card>
      )}

      <Card>
        <CardHeader><CardTitle>{t('platform.newTenant.section.organization')}</CardTitle></CardHeader>
        <CardContent className="flex flex-col gap-3">
          <LabeledInput label={t('platform.newTenant.field.orgSlug')} hint={t('platform.newTenant.field.orgSlugHint')}
            value={form.organizationSlug} onChange={v => update('organizationSlug', v)} required />
          <LabeledInput label={t('platform.newTenant.field.orgName')}
            value={form.organizationName} onChange={v => update('organizationName', v)} required />
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>{t('platform.newTenant.section.branch')}</CardTitle></CardHeader>
        <CardContent className="flex flex-col gap-3">
          <LabeledInput label={t('platform.newTenant.field.branchSlug')} value={form.branchSlug} onChange={v => update('branchSlug', v)} required />
          <LabeledInput label={t('platform.newTenant.field.branchName')} value={form.branchName} onChange={v => update('branchName', v)} required />
          <LabeledInput label={t('platform.newTenant.field.branchCity')} value={form.branchCity} onChange={v => update('branchCity', v)} required />
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>{t('platform.newTenant.section.plan')}</CardTitle></CardHeader>
        <CardContent className="flex flex-col gap-3">
          <label className="block">
            <span className="mb-1 block text-sm text-muted-foreground">{t('platform.newTenant.field.planCode')}</span>
            <Select value={form.planCode} onValueChange={v => update('planCode', v)}>
              <SelectTrigger aria-label={t('platform.newTenant.field.planCode')}><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value={TenantPlanCode.Starter}>{t('platform.plan.starter')}</SelectItem>
                <SelectItem value={TenantPlanCode.Growth}>{t('platform.plan.growth')}</SelectItem>
                <SelectItem value={TenantPlanCode.Scale}>{t('platform.plan.scale')}</SelectItem>
              </SelectContent>
            </Select>
          </label>
          <label className="block">
            <span className="mb-1 block text-sm text-muted-foreground">{t('platform.newTenant.field.subscriptionStatus')}</span>
            <Select value={form.subscriptionStatus} onValueChange={v => update('subscriptionStatus', v)}>
              <SelectTrigger aria-label={t('platform.newTenant.field.subscriptionStatus')}><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value={SubscriptionStatus.Trial}>{t('platform.newTenant.sub.trial')}</SelectItem>
                <SelectItem value={SubscriptionStatus.Active}>{t('platform.newTenant.sub.active')}</SelectItem>
                <SelectItem value={SubscriptionStatus.PastDue}>{t('platform.newTenant.sub.pastDue')}</SelectItem>
                <SelectItem value={SubscriptionStatus.Cancelled}>{t('platform.newTenant.sub.cancelled')}</SelectItem>
              </SelectContent>
            </Select>
          </label>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>{t('platform.newTenant.section.limits')}</CardTitle></CardHeader>
        <CardContent className="flex flex-col gap-3">
          <LabeledInput label={t('platform.newTenant.field.maxBranches')} type="number" value={form.maxBranches} onChange={v => update('maxBranches', v)} />
          <LabeledInput label={t('platform.newTenant.field.maxDevices')} type="number" value={form.maxDevicesPerBranch} onChange={v => update('maxDevicesPerBranch', v)} />
          <LabeledInput label={t('platform.newTenant.field.maxSessions')} type="number" value={form.maxConcurrentSessions} onChange={v => update('maxConcurrentSessions', v)} />
          <LabeledInput label={t('platform.newTenant.field.maxStaff')} type="number" value={form.maxStaffUsersPerBranch} onChange={v => update('maxStaffUsersPerBranch', v)} />
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>{t('platform.newTenant.section.owner')}</CardTitle></CardHeader>
        <CardContent className="flex flex-col gap-3">
          <LabeledInput label={t('platform.newTenant.field.ownerUserName')} value={form.ownerUserName} onChange={v => update('ownerUserName', v)} />
          <LabeledInput label={t('platform.newTenant.field.ownerDisplayName')} value={form.ownerDisplayName} onChange={v => update('ownerDisplayName', v)} />
        </CardContent>
      </Card>

      <div className="flex gap-2">
        <Button type="submit" disabled={submitting}>
          {submitting ? t('platform.newTenant.submitting') : t('platform.newTenant.submit')}
        </Button>
        <Button type="button" variant="outline" onClick={onCancel} disabled={submitting}>
          {t('platform.newTenant.cancel')}
        </Button>
      </div>
    </form>
  );
}

function LabeledInput({ label, hint, value, onChange, type, required }: {
  label: string;
  hint?: string;
  value: string;
  onChange: (value: string) => void;
  type?: string;
  required?: boolean;
}) {
  return (
    <label className="block">
      <span className="mb-1 block text-sm text-muted-foreground">{label}</span>
      <Input aria-label={label} type={type} value={value} required={required} onChange={e => onChange(e.target.value)} />
      {hint !== undefined && <span className="mt-1 block text-xs text-muted-foreground">{hint}</span>}
    </label>
  );
}

function buildLimits(form: FormState): TenantLimits | null {
  const parsed: TenantLimits = {
    maxBranches: parseOptional(form.maxBranches),
    maxDevicesPerBranch: parseOptional(form.maxDevicesPerBranch),
    maxConcurrentSessions: parseOptional(form.maxConcurrentSessions),
    maxStaffUsersPerBranch: parseOptional(form.maxStaffUsersPerBranch)
  };
  if (Object.values(parsed).every(value => value === null)) {
    return null;
  }
  return parsed;
}

function parseOptional(value: string): number | null {
  if (value.trim() === '') {
    return null;
  }
  const parsed = Number.parseInt(value, 10);
  return Number.isNaN(parsed) ? null : parsed;
}
```

- [ ] **Step 4: Run the screen test to verify it passes**

Run: `npm test -- platform/tenants/NewTenantScreen`
Expected: PASS (3 tests).

- [ ] **Step 5: Swap `NewTenant` → `NewTenantScreen` in `App.tsx`**

In `src/App.tsx`, replace the import line:

```typescript
import { NewTenant } from './components/NewTenant';
```

with:

```typescript
import { NewTenantScreen } from './platform/tenants/NewTenantScreen';
```

Then in `PlatformArea`'s render ternary, the `newTenant` branch currently reads:

```tsx
      ) : route.kind === 'newTenant' ? (
        <NewTenant
          client={adminClient}
          onCreated={onCreatedTenant}
          onCancel={onCancelNewTenant}
        />
      ) : (
```

Replace it with:

```tsx
      ) : route.kind === 'newTenant' ? (
        <NewTenantScreen
          client={adminClient}
          onCreated={onCreatedTenant}
          onCancel={onCancelNewTenant}
        />
      ) : (
```

- [ ] **Step 6: Update the two legacy new-tenant assertions in `App.test.tsx`**

The redesigned screen has no `New tenant` heading (the topbar supplies the screen title) and its buttons are RU-labelled. Two tests assert the old heading/Cancel label.

**Edit 1** — in the test `redirects a legacy new-tenant bookmark to the admin-prefixed screen`, change:

```typescript
    expect(screen.getByRole('heading', { name: 'New tenant' })).toBeInTheDocument();
```

to:

```typescript
    expect(await screen.findByRole('button', { name: 'Создать тенант' })).toBeInTheDocument();
```

**Edit 2** — in the test `pushes admin-prefixed URLs for tenant list navigation`, change:

```typescript
    expect(screen.getByRole('heading', { name: 'New tenant' })).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
```

to:

```typescript
    expect(await screen.findByRole('button', { name: 'Создать тенант' })).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Отмена' }));
```

Leave every other line in those two tests unchanged (the path-redirect assertions and the tenant-list search assertion still hold). Do NOT touch the `resolvePlatformRoute('/tenants/new')` pure-routing test — its `{ kind: 'newTenant' }` assertion is unchanged.

- [ ] **Step 7: Delete the legacy component**

```bash
git rm src/AFK4.Platform.Web/src/components/NewTenant.tsx
```

(Run from the repo root. `NewTenant.tsx` had no test file. Its only importer was `App.tsx`, now repointed.)

- [ ] **Step 8: Run the affected suites + full typecheck**

Run: `npm test -- platform/tenants/NewTenantScreen` then `npm test -- App` then `npm run build`
Expected: NewTenantScreen tests pass; `App.test.tsx` passes with the two edits; `npm run build` succeeds with no dangling import of `./components/NewTenant`.

- [ ] **Step 9: Commit**

```bash
git add src/platform/tenants/NewTenantScreen.tsx src/platform/tenants/NewTenantScreen.test.tsx src/App.tsx src/App.test.tsx src/components/NewTenant.tsx
git commit -m "feat(platform): redesigned new-tenant screen; drop legacy NewTenant"
```

---

## Task 5: Redesigned tenant Health section

Replaces the legacy `components/HealthSection.tsx` with a design-system `platform/tenants/TenantHealthSection.tsx`, swaps it into `TenantDrawer`, and deletes the legacy file.

**Files:**
- Create: `src/platform/tenants/TenantHealthSection.tsx`
- Test: `src/platform/tenants/TenantHealthSection.test.tsx`
- Modify: `src/platform/tenants/TenantDrawer.tsx`
- Delete: `src/components/HealthSection.tsx`

- [ ] **Step 1: Write the failing test**

Create `src/platform/tenants/TenantHealthSection.test.tsx`:

```typescript
import { render, screen } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { TenantHealthSection } from './TenantHealthSection';
import type { TenantHealth } from '@/api/types';

const health: TenantHealth = {
  organizationId: 'o1', status: 'active', branchCount: 3, deviceCount: 12,
  activeStaffUserCount: 5, latestStaffSignInAtUtc: '2026-05-01T00:00:00Z',
  latestMigration: '20260501_Init', recentErrorCount: 1,
  recentErrors: [{ createdAtUtc: '2026-05-02T00:00:00Z', source: 'auth', action: 'sign_in', outcome: 'denied', message: 'bad creds' }]
};

it('renders health metrics and the recent-errors row', async () => {
  const client = { getHealth: vi.fn().mockResolvedValue(health) };
  render(<I18nProvider><TenantHealthSection client={client} organizationId="o1" /></I18nProvider>);
  expect(await screen.findByText('active')).toBeInTheDocument();
  expect(screen.getByText('bad creds')).toBeInTheDocument();
});

it('shows an error state with a retry button', async () => {
  const client = { getHealth: vi.fn().mockRejectedValue(new Error('boom')) };
  render(<I18nProvider><TenantHealthSection client={client} organizationId="o1" /></I18nProvider>);
  expect(await screen.findByText('Повторить')).toBeInTheDocument();
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `npm test -- platform/tenants/TenantHealthSection`
Expected: FAIL — cannot resolve `./TenantHealthSection`.

- [ ] **Step 3: Write `TenantHealthSection.tsx`**

Create `src/platform/tenants/TenantHealthSection.tsx`:

```typescript
import { useEffect, useState, type ReactNode } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { PlatformApiClient } from '@/api/platformApi';
import type { TenantHealth } from '@/api/types';

type Client = Pick<PlatformApiClient, 'getHealth'>;

interface Props {
  client: Client;
  organizationId: string;
}

export function TenantHealthSection({ client, organizationId }: Props) {
  const { t, formatNumber, formatDate } = useI18n();
  const [tick, setTick] = useState(0);
  const [health, setHealth] = useState<TenantHealth | null>(null);
  const [error, setError] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setHealth(null); setError(false);
    client.getHealth(organizationId)
      .then(data => { if (!cancelled) setHealth(data); })
      .catch(() => { if (!cancelled) setError(true); });
    return () => { cancelled = true; };
  }, [client, organizationId, tick]);

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between">
        <CardTitle>{t('platform.tenant.section.health')}</CardTitle>
        <Button variant="ghost" size="sm" onClick={() => setTick(n => n + 1)}>{t('platform.tenant.health.refresh')}</Button>
      </CardHeader>
      <CardContent className="flex flex-col gap-4 text-sm">
        {error ? (
          <ErrorState message={t('platform.tenant.health.error')} retryLabel={t('state.retry')} onRetry={() => setTick(n => n + 1)} />
        ) : health === null ? (
          <LoadingCards count={1} />
        ) : (
          <>
            <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
              <Row label={t('platform.tenant.health.status')}><Badge variant="secondary">{health.status}</Badge></Row>
              <Row label={t('platform.tenant.health.branches')}>{formatNumber(health.branchCount)}</Row>
              <Row label={t('platform.tenant.health.devices')}>{formatNumber(health.deviceCount)}</Row>
              <Row label={t('platform.tenant.health.activeStaff')}>{formatNumber(health.activeStaffUserCount)}</Row>
              <Row label={t('platform.tenant.health.lastSignIn')}>{health.latestStaffSignInAtUtc !== null ? formatDate(health.latestStaffSignInAtUtc) : '—'}</Row>
              <Row label={t('platform.tenant.health.latestMigration')}>{health.latestMigration ?? '—'}</Row>
              <Row label={t('platform.tenant.health.recentErrors')}>{formatNumber(health.recentErrorCount)}</Row>
            </div>

            {health.recentErrors.length === 0 ? (
              <EmptyState message={t('platform.tenant.health.recentErrorsEmpty')} />
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>{t('platform.tenant.health.col.time')}</TableHead>
                    <TableHead>{t('platform.tenant.health.col.source')}</TableHead>
                    <TableHead>{t('platform.tenant.health.col.action')}</TableHead>
                    <TableHead>{t('platform.tenant.health.col.outcome')}</TableHead>
                    <TableHead>{t('platform.tenant.health.col.message')}</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {health.recentErrors.map((entry, index) => (
                    <TableRow key={`${entry.createdAtUtc}-${index}`}>
                      <TableCell className="tabular-nums">{formatDate(entry.createdAtUtc)}</TableCell>
                      <TableCell>{entry.source}</TableCell>
                      <TableCell>{entry.action}</TableCell>
                      <TableCell>{entry.outcome}</TableCell>
                      <TableCell><code className="font-mono text-xs">{entry.message ?? ''}</code></TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}

function Row({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex justify-between gap-3">
      <span className="text-muted-foreground">{label}</span>
      <span className="text-right">{children}</span>
    </div>
  );
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `npm test -- platform/tenants/TenantHealthSection`
Expected: PASS (2 tests). (`state.retry` renders "Повторить".)

- [ ] **Step 5: Swap the section into `TenantDrawer.tsx`**

In `src/platform/tenants/TenantDrawer.tsx`, replace the import:

```typescript
import { HealthSection } from '@/components/HealthSection';
```

with:

```typescript
import { TenantHealthSection } from './TenantHealthSection';
```

Then replace the interim render block at the bottom of the drawer:

```tsx
      {/* Interim: legacy Health section embedded unchanged until later plans redesign it. */}
      <HealthSection client={client} organizationId={tenant.organizationId} />
```

with:

```tsx
      <TenantHealthSection client={client} organizationId={tenant.organizationId} />
```

- [ ] **Step 6: Delete the legacy component**

```bash
git rm src/AFK4.Platform.Web/src/components/HealthSection.tsx
```

(Run from the repo root. `HealthSection.tsx` had no test file; its only importer was `TenantDrawer.tsx`, now repointed.)

- [ ] **Step 7: Run the affected suites + full typecheck**

Run: `npm test -- platform/tenants` then `npm run build`
Expected: the new health-section tests plus the existing tenants tests pass; `npm run build` succeeds with no dangling import of `@/components/HealthSection`.

- [ ] **Step 8: Commit**

```bash
git add src/platform/tenants/TenantHealthSection.tsx src/platform/tenants/TenantHealthSection.test.tsx src/platform/tenants/TenantDrawer.tsx src/components/HealthSection.tsx
git commit -m "feat(platform): redesigned tenant Health section; drop legacy HealthSection"
```

---

## Task 6: Final verification

**Files:** none (verification only)

- [ ] **Step 1: Confirm the legacy admin components are gone and unreferenced**

Run: `git status --short src/AFK4.Platform.Web/src/components/`
Expected: `NewTenant.tsx` and `HealthSection.tsx` are deleted (staged in their respective commits).

Run a reference sweep (PowerShell): `Select-String -Path src/AFK4.Platform.Web/src/**/*.tsx,src/AFK4.Platform.Web/src/**/*.ts -Pattern "components/NewTenant|components/HealthSection|HealthSection"` (or `rg "components/NewTenant|components/HealthSection|\bHealthSection\b" src/AFK4.Platform.Web/src`).
Expected: ZERO matches (no import or usage of either legacy component remains). `src/components/ui.tsx` is NOT deleted — `SignIn`/`AcceptInvite`/`StaffSignIn` still import `Field`/`ErrorBanner` from it; that is intentional.

- [ ] **Step 2: Run the complete test suite**

Run: `npm test`
Expected: all suites pass — new `platform/profile/*`, `platform/tenants/NewTenantScreen`, `platform/tenants/TenantHealthSection`, updated `platform/nav`, `App.routing`, `App.test`, `i18n/messages`; no club regressions.

- [ ] **Step 3: Run the production build (the real type gate)**

Run: `npm run build`
Expected: `tsc -b` reports no errors and `vite build` completes. The `AdminRoute` union, `PLATFORM_SCREEN_TITLE` exhaustiveness, and all `MessageKey` references resolve.

- [ ] **Step 4: Manual smoke (optional)**

Run: `npm run dev` and open `http://127.0.0.1:5175/admin` with the admin audience. After signing in: confirm the sidebar now shows **Профиль** as a live link (no "скоро"); clicking it opens the profile (identity / roles / permissions / Выйти); from **Тенанты** click **Новый тенант** and confirm the redesigned card-based form creates a tenant; open any tenant and confirm the **Состояние** card renders on the design system. (Optional — build + tests are the authoritative gate.)

---

## Self-review notes (addressed)

- **Spec coverage (Plan 6 slice):** New-tenant flow redesigned ✔ (Task 4, `NewTenantScreen` on the design system, legacy `NewTenant.tsx` deleted); Platform Profile ✔ (Task 2 screen + Task 3 route/nav wiring, mirror of club `ProfileScreen`); delete remaining legacy admin components ✔ (Task 4 deletes `NewTenant.tsx`; Task 5 redesigns Health into `TenantHealthSection` so `HealthSection.tsx` can be deleted). The legacy `components/ui.tsx` is intentionally retained because the out-of-scope auth screens (`SignIn`/`AcceptInvite`/`StaffSignIn`) still depend on it — Task 6 Step 1 documents this. Club-side billing is Plan 7, out of scope here.
- **Placeholder scan:** every code step contains complete, runnable code and exact Edit anchors; no TODOs.
- **Type consistency:** `groupPermissions`/`PermissionGroup` (Task 2) match the screen usage; `NewTenantScreenProps`/`FormState`/`buildLimits`/`parseOptional` (Task 4) match the legacy contract (`createTenant(CreateTenantRequest)` → `CreateTenantResponse`); `TenantHealthSection` props (`client: Pick<…,'getHealth'>`, `organizationId`) match the `TenantDrawer` call site; the `adminProfile` route kind is added consistently to `AdminRoute`, `isAdminRoute`, `resolvePlatformRoute`, `PLATFORM_SCREEN_TITLE`, `pathForAdminRoute`, and the `PlatformArea` render in Task 3.
- **i18n parity:** Task 1 adds the same 47 keys to `ru` and `en`; the existing parity test guards it; all later tasks only reference keys added in Task 1 or pre-existing keys (`profile.*`, `shell.signOut`, `platform.plan.*`, `state.retry`).
- **Test-render hazards:** `NewTenantScreen.test` and any App test rendering it rely on the Radix pointer shims (added in `NewTenantScreen.test`; `App.test.tsx` only renders the form at default Select values, no dropdown interaction); all required text inputs are filled so jsdom does not block submit.
