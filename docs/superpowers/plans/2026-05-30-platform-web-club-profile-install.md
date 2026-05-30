# Club Console 7b — Profile + Install + Delete Legacy Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a read-only `/club/profile` screen, port the `/club/install` screen onto the modern design system, then retire the dead legacy branch-detail routes and delete `src/components/ClubDashboard.tsx`.

**Architecture:** Profile = pure `profileModel` (permission grouping + branch-name resolution) + presentational `ProfileScreen` (data is synchronous from session/props — no hook). Install = `installModel` (owner-code view-model + `getSetupMsiUrl`) + load-only `useOwnerCode` hook + `OwnerCodePanel` (owns generate/rotate mutations) + `InstallScreen` shell. Then `ClubArea` renders both and the legacy fallback + dead routes are removed.

**Tech Stack:** React 19 + TypeScript, Vite, Vitest 4 + jsdom + @testing-library/react, shadcn/ui, i18n RU/EN.

**Grounding facts (verified against repo):**
- `StaffSession` (from `@/auth/staffTokenStore`): `{ staffUserId, organizationId, displayName, branchIds: string[], permissions: string[], … }`.
- Owner-code types (from `@/api/types`): `OwnerCodeSummary { codeSuffix, expiresAtUtc, lastUsedAtUtc: string|null, failedAttemptCount }`, `OwnerCodeIssued { ownerCode, codeSuffix, expiresAtUtc }`.
- `clubApi` already has `getOwnerCode(): Promise<OwnerCodeSummary | null>` (204→null), `generateOwnerCode(): Promise<OwnerCodeIssued>`, `rotateOwnerCode(reason): Promise<OwnerCodeIssued>`.
- Owner-code manage permission string: `identity.owner_code.manage`.
- `useBranchDirectory` returns `Record<string, { name: string; city: string }>`. `ClubArea` already builds `branches = session.branchIds.map(id => ({ branchId: id, name: directory[id]?.name ?? t('branches.unnamed') }))` and has `directory`, `session`, `onSignOut`, `role` (`roleFromPermissions`), `ROLE_LABEL` in scope.
- `getSetupMsiUrl` (currently in `ClubDashboard.tsx`): reads `import.meta.env.VITE_SETUP_MSI_URL`, trims, fallback `/downloads/AFK4-Agent.msi`. We re-home it into `installModel.ts`.
- **Only** `App.tsx:9` imports from `./components/ClubDashboard` (`LegacyClubScreen`). Nothing imports `ClubDashboard`/`DashboardHome` externally. So deleting the file only requires fixing `App.tsx`.
- `LegacyClubScreen` in the current `ClubArea` is the terminal `else`; it is reached for `clubInstall` + the dead branch-detail routes `clubBranchDetail`/`clubBranchFloorMap`/`clubBranchDevices`/`clubBranchPendingDevices`/`clubBranchOperators`. Those branch-detail features are already covered by Venue/Settings/Branches and nothing navigates to those route kinds.
- ui: `Card, CardHeader, CardTitle, CardContent` from `@/components/ui/card`; `Button` from `@/components/ui/button`; `Input` from `@/components/ui/input`; `Badge` from `@/components/ui/badge`; `LoadingCards, ErrorState, EmptyState` from `@/components/ui/states`; `useToast` from `@/components/ui/toast`; `useI18n` from `@/i18n/I18nProvider`.
- Vitest `globals: false` → import `{ it, expect, vi }` from `'vitest'`. Component tests wrap `<I18nProvider><ToastProvider>…</ToastProvider></I18nProvider>`. **vitest does NOT type-check; only `npm run build` (`tsc -b && vite build`) does.** Type mocked fns explicitly (`vi.fn<(a: string) => Promise<X>>()`).

---

### Task 1: i18n keys (profile + install) + parity coverage

**Files:**
- Modify: `src/AFK4.Platform.Web/src/i18n/messages.ts` (ru + en)
- Modify: `src/AFK4.Platform.Web/src/i18n/messages.test.ts`

- [ ] **Step 1: Add the coverage block to `messages.test.ts` (append):**

```ts
it('includes the profile + install keys', () => {
  for (const key of [
    'profile.identity.title', 'profile.field.displayName', 'profile.field.organization',
    'profile.field.staffId', 'profile.field.role', 'profile.branches.title', 'profile.branches.empty',
    'profile.permissions.title', 'profile.permissions.empty', 'profile.editUnavailable',
    'install.title', 'install.subtitle', 'install.download',
    'install.ownerCode.title', 'install.ownerCode.noAccess', 'install.ownerCode.none',
    'install.ownerCode.validUntil', 'install.ownerCode.lastUsed', 'install.ownerCode.failed',
    'install.ownerCode.generate', 'install.ownerCode.rotate', 'install.ownerCode.reason',
    'install.ownerCode.generated', 'install.ownerCode.rotated', 'install.ownerCode.error',
    'install.wizard.title', 'install.wizard.step1', 'install.wizard.step2',
    'install.wizard.step3', 'install.wizard.step4',
    'install.branches.title', 'install.branches.empty'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});
```

- [ ] **Step 2: Run `npm test -- messages` → FAIL.**

- [ ] **Step 3: Add to the `ru` object** (after the `journal.*` block):

```ts
    // profile
    'profile.identity.title': 'Учётная запись',
    'profile.field.displayName': 'Имя',
    'profile.field.organization': 'Организация',
    'profile.field.staffId': 'ID сотрудника',
    'profile.field.role': 'Роль',
    'profile.branches.title': 'Доступные филиалы',
    'profile.branches.empty': 'Нет доступных филиалов.',
    'profile.permissions.title': 'Права доступа',
    'profile.permissions.empty': 'Права не назначены.',
    'profile.editUnavailable': 'Редактирование профиля недоступно.',
    // install
    'install.title': 'Установка на ПК',
    'install.subtitle': 'Используйте код владельца в мастере установки Windows.',
    'install.download': 'Скачать установщик MSI',
    'install.ownerCode.title': 'Код владельца',
    'install.ownerCode.noAccess': 'Ваша учётная запись не может генерировать код владельца.',
    'install.ownerCode.none': 'Код не сгенерирован',
    'install.ownerCode.validUntil': 'Действителен до',
    'install.ownerCode.lastUsed': 'Последнее использование',
    'install.ownerCode.failed': 'Неудачные попытки',
    'install.ownerCode.generate': 'Сгенерировать код',
    'install.ownerCode.rotate': 'Перевыпустить',
    'install.ownerCode.reason': 'Причина перевыпуска',
    'install.ownerCode.generated': 'Код сгенерирован',
    'install.ownerCode.rotated': 'Код перевыпущен',
    'install.ownerCode.error': 'Не удалось обновить код владельца',
    'install.wizard.title': 'Шаги мастера установки',
    'install.wizard.step1': 'Запустите MSI на ПК с Windows 10/11.',
    'install.wizard.step2': 'Введите 8-значный код владельца.',
    'install.wizard.step3': 'Выберите филиал и место на карте зала.',
    'install.wizard.step4': 'Выберите тип (игровой ПК или рабочее место менеджера) и завершите привязку.',
    'install.branches.title': 'Филиалы, доступные мастеру',
    'install.branches.empty': 'К этой учётной записи не привязаны филиалы.',
```

- [ ] **Step 4: Add to the `en` object** (after its `journal.*` block):

```ts
    // profile
    'profile.identity.title': 'Account',
    'profile.field.displayName': 'Name',
    'profile.field.organization': 'Organization',
    'profile.field.staffId': 'Staff ID',
    'profile.field.role': 'Role',
    'profile.branches.title': 'Accessible branches',
    'profile.branches.empty': 'No accessible branches.',
    'profile.permissions.title': 'Permissions',
    'profile.permissions.empty': 'No permissions assigned.',
    'profile.editUnavailable': 'Profile editing is unavailable.',
    // install
    'install.title': 'Install on PCs',
    'install.subtitle': 'Use the owner code in the Windows setup wizard.',
    'install.download': 'Download MSI installer',
    'install.ownerCode.title': 'Owner code',
    'install.ownerCode.noAccess': 'Your account cannot generate an owner code.',
    'install.ownerCode.none': 'No code generated',
    'install.ownerCode.validUntil': 'Valid until',
    'install.ownerCode.lastUsed': 'Last used',
    'install.ownerCode.failed': 'Failed attempts',
    'install.ownerCode.generate': 'Generate code',
    'install.ownerCode.rotate': 'Rotate',
    'install.ownerCode.reason': 'Rotation reason',
    'install.ownerCode.generated': 'Code generated',
    'install.ownerCode.rotated': 'Code rotated',
    'install.ownerCode.error': 'Failed to update the owner code',
    'install.wizard.title': 'Setup wizard steps',
    'install.wizard.step1': 'Run the MSI on the Windows 10/11 PC.',
    'install.wizard.step2': 'Enter the 8-digit owner code.',
    'install.wizard.step3': 'Pick the branch and the seat on the floor map.',
    'install.wizard.step4': 'Choose the type (gaming PC or manager workstation) and finish enrollment.',
    'install.branches.title': 'Branches available to the wizard',
    'install.branches.empty': 'No branches are linked to this account.',
```

- [ ] **Step 5: Run `npm test -- messages` → PASS.**
- [ ] **Step 6: Commit.** From `D:\afk4.net`: `git add -A && git commit -m "feat(club): i18n keys for profile + install"`

---

### Task 2: profileModel (pure)

**Files:**
- Create: `src/AFK4.Platform.Web/src/club/profile/profileModel.ts`
- Test: `src/AFK4.Platform.Web/src/club/profile/profileModel.test.ts`

- [ ] **Step 1: Write the failing test.**

```ts
import { it, expect } from 'vitest';
import { groupPermissions, resolveBranchNames } from './profileModel';

it('groups permissions by their prefix, sorted', () => {
  const groups = groupPermissions(['billing.refund', 'players.view', 'billing.wallet.top_up', 'players.create']);
  expect(groups).toEqual([
    { key: 'billing', permissions: ['billing.refund', 'billing.wallet.top_up'] },
    { key: 'players', permissions: ['players.create', 'players.view'] }
  ]);
});

it('uses the whole string as group when there is no dot', () => {
  expect(groupPermissions(['admin'])).toEqual([{ key: 'admin', permissions: ['admin'] }]);
});

it('resolves branch names with a fallback', () => {
  const names = resolveBranchNames(['b1', 'b2'], { b1: { name: 'Центр' } }, 'Без названия');
  expect(names).toEqual([{ branchId: 'b1', name: 'Центр' }, { branchId: 'b2', name: 'Без названия' }]);
});
```

- [ ] **Step 2: Run `npm test -- profileModel` → FAIL.**

- [ ] **Step 3: Implement `profileModel.ts`.**

```ts
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

export interface BranchName { branchId: string; name: string; }

export function resolveBranchNames(
  branchIds: readonly string[],
  directory: Record<string, { name: string }>,
  fallback: string
): BranchName[] {
  return branchIds.map(branchId => ({ branchId, name: directory[branchId]?.name ?? fallback }));
}
```

- [ ] **Step 4: Run `npm test -- profileModel` → PASS.**
- [ ] **Step 5: `npm run build` → clean.**
- [ ] **Step 6: Commit.** `git add -A && git commit -m "feat(club): profileModel"`

---

### Task 3: ProfileScreen

**Files:**
- Create: `src/AFK4.Platform.Web/src/club/profile/ProfileScreen.tsx`
- Test: `src/AFK4.Platform.Web/src/club/profile/ProfileScreen.test.tsx`

- [ ] **Step 1: Write the failing test.**

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import type { StaffSession } from '@/auth/staffTokenStore';
import { ProfileScreen } from './ProfileScreen';

const session: StaffSession = {
  staffUserId: 'u1', organizationId: 'org-1', displayName: 'Иван', branchIds: ['b1'],
  permissions: ['players.view', 'billing.refund'],
  accessToken: 'a', accessTokenExpiresAtUtc: '', refreshToken: 'r', refreshTokenExpiresAtUtc: ''
} as StaffSession;

it('shows identity, permissions, and signs out', () => {
  const onSignOut = vi.fn();
  render(
    <I18nProvider>
      <ProfileScreen session={session} branches={[{ branchId: 'b1', name: 'Центр' }]} roleLabel="Владелец" onSignOut={onSignOut} />
    </I18nProvider>
  );
  expect(screen.getByText('Иван')).toBeInTheDocument();
  expect(screen.getByText('Владелец')).toBeInTheDocument();
  expect(screen.getByText('Центр')).toBeInTheDocument();
  expect(screen.getByText('players.view')).toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Выйти' }));
  expect(onSignOut).toHaveBeenCalled();
});
```

- [ ] **Step 2: Run `npm test -- ProfileScreen` → FAIL.**

- [ ] **Step 3: Implement `ProfileScreen.tsx`.**

```tsx
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { useI18n } from '@/i18n/I18nProvider';
import type { StaffSession } from '@/auth/staffTokenStore';
import { groupPermissions } from './profileModel';

export function ProfileScreen({ session, branches, roleLabel, onSignOut }: {
  session: StaffSession;
  branches: { branchId: string; name: string }[];
  roleLabel: string;
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
          <Field label={t('profile.field.role')} value={roleLabel} />
          <Field label={t('profile.field.organization')} value={session.organizationId} />
          <Field label={t('profile.field.staffId')} value={session.staffUserId} />
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>{t('profile.branches.title')}</CardTitle></CardHeader>
        <CardContent>
          {branches.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t('profile.branches.empty')}</p>
          ) : (
            <ul className="flex flex-col gap-1">
              {branches.map(b => <li key={b.branchId} className="text-sm">{b.name}</li>)}
            </ul>
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

- [ ] **Step 4: Run `npm test -- ProfileScreen` → PASS.**
- [ ] **Step 5: `npm run build` → clean.**
- [ ] **Step 6: Commit.** `git add -A && git commit -m "feat(club): ProfileScreen"`

---

### Task 4: installModel (owner-code view + getSetupMsiUrl)

**Files:**
- Create: `src/AFK4.Platform.Web/src/club/install/installModel.ts`
- Test: `src/AFK4.Platform.Web/src/club/install/installModel.test.ts`

- [ ] **Step 1: Write the failing test.**

```ts
import { it, expect } from 'vitest';
import { toOwnerCodeView, getSetupMsiUrl } from './installModel';

it('shows the full issued code when freshly issued', () => {
  const view = toOwnerCodeView(null, { ownerCode: '12345678', codeSuffix: '5678', expiresAtUtc: '2026-06-01T00:00:00.000Z' });
  expect(view).toEqual({ code: '12345678', hasCode: true, expiresAtUtc: '2026-06-01T00:00:00.000Z', lastUsedAtUtc: null, failedAttemptCount: 0 });
});

it('masks the code from a summary', () => {
  const view = toOwnerCodeView({ codeSuffix: '5678', expiresAtUtc: '2026-06-01T00:00:00.000Z', lastUsedAtUtc: '2026-05-30T00:00:00.000Z', failedAttemptCount: 2 }, null);
  expect(view.code).toBe('**** 5678');
  expect(view.hasCode).toBe(true);
  expect(view.failedAttemptCount).toBe(2);
});

it('reports no code when both are null', () => {
  const view = toOwnerCodeView(null, null);
  expect(view.hasCode).toBe(false);
  expect(view.code).toBe('—');
});

it('falls back to the default MSI url when env is unset', () => {
  expect(getSetupMsiUrl()).toBe('/downloads/AFK4-Agent.msi');
});
```

- [ ] **Step 2: Run `npm test -- installModel` → FAIL.** (Note: the last test assumes `VITE_SETUP_MSI_URL` is unset in the test env, which it is. If the project ever sets it for tests, this assertion would change — but it does not.)

- [ ] **Step 3: Implement `installModel.ts`.**

```ts
import type { OwnerCodeSummary, OwnerCodeIssued } from '@/api/types';

export interface OwnerCodeView {
  code: string;
  hasCode: boolean;
  expiresAtUtc: string | null;
  lastUsedAtUtc: string | null;
  failedAttemptCount: number;
}

export function toOwnerCodeView(summary: OwnerCodeSummary | null, issued: OwnerCodeIssued | null): OwnerCodeView {
  if (issued !== null) {
    return { code: issued.ownerCode, hasCode: true, expiresAtUtc: issued.expiresAtUtc, lastUsedAtUtc: null, failedAttemptCount: 0 };
  }
  if (summary === null) {
    return { code: '—', hasCode: false, expiresAtUtc: null, lastUsedAtUtc: null, failedAttemptCount: 0 };
  }
  return {
    code: `**** ${summary.codeSuffix}`,
    hasCode: true,
    expiresAtUtc: summary.expiresAtUtc,
    lastUsedAtUtc: summary.lastUsedAtUtc,
    failedAttemptCount: summary.failedAttemptCount
  };
}

export function getSetupMsiUrl(): string {
  const configured = import.meta.env.VITE_SETUP_MSI_URL;
  return typeof configured === 'string' && configured.trim().length > 0
    ? configured.trim()
    : '/downloads/AFK4-Agent.msi';
}
```

- [ ] **Step 4: Run `npm test -- installModel` → PASS.**
- [ ] **Step 5: `npm run build` → clean.**
- [ ] **Step 6: Commit.** `git add -A && git commit -m "feat(club): installModel + getSetupMsiUrl"`

---

### Task 5: useOwnerCode hook (load-only)

**Files:**
- Create: `src/AFK4.Platform.Web/src/club/install/useOwnerCode.ts`
- Test: `src/AFK4.Platform.Web/src/club/install/useOwnerCode.test.ts`

- [ ] **Step 1: Write the failing test.**

```ts
import { it, expect, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import type { OwnerCodeSummary } from '@/api/types';
import { useOwnerCode } from './useOwnerCode';

const summary: OwnerCodeSummary = { codeSuffix: '5678', expiresAtUtc: '2026-06-01T00:00:00.000Z', lastUsedAtUtc: null, failedAttemptCount: 0 };

it('loads the owner-code summary when enabled', async () => {
  const client = { getOwnerCode: vi.fn<() => Promise<OwnerCodeSummary | null>>(async () => summary) };
  const { result } = renderHook(() => useOwnerCode(client as never, true));
  await waitFor(() => expect(result.current.status).toBe('ready'));
  if (result.current.status !== 'ready') throw new Error('not ready');
  expect(result.current.summary).toEqual(summary);
});

it('does not fetch when disabled', async () => {
  const client = { getOwnerCode: vi.fn<() => Promise<OwnerCodeSummary | null>>(async () => summary) };
  const { result } = renderHook(() => useOwnerCode(client as never, false));
  await waitFor(() => expect(result.current.status).toBe('ready'));
  expect(client.getOwnerCode).not.toHaveBeenCalled();
  if (result.current.status !== 'ready') throw new Error('not ready');
  expect(result.current.summary).toBeNull();
});

it('reports an error when the load fails', async () => {
  const client = { getOwnerCode: vi.fn<() => Promise<OwnerCodeSummary | null>>(async () => { throw new Error('boom'); }) };
  const { result } = renderHook(() => useOwnerCode(client as never, true));
  await waitFor(() => expect(result.current.status).toBe('error'));
});
```

- [ ] **Step 2: Run `npm test -- useOwnerCode` → FAIL.**

- [ ] **Step 3: Implement `useOwnerCode.ts`.**

```ts
import { useCallback, useEffect, useRef, useState } from 'react';
import type { ClubApiClient } from '@/api/clubApi';
import type { OwnerCodeSummary } from '@/api/types';

type Loadable = Pick<ClubApiClient, 'getOwnerCode'>;

export type OwnerCodeState =
  | { status: 'loading' }
  | { status: 'error'; retry: () => void }
  | { status: 'ready'; summary: OwnerCodeSummary | null; retry: () => void };

export function useOwnerCode(client: Loadable, enabled: boolean): OwnerCodeState {
  const [tick, setTick] = useState(0);
  const [phase, setPhase] = useState<'loading' | 'error' | 'ready'>('loading');
  const [summary, setSummary] = useState<OwnerCodeSummary | null>(null);
  const clientRef = useRef(client);
  clientRef.current = client;

  const retry = useCallback(() => setTick(t => t + 1), []);

  useEffect(() => {
    let cancelled = false;
    if (!enabled) {
      setSummary(null);
      setPhase('ready');
      return;
    }
    setPhase('loading');
    clientRef.current.getOwnerCode()
      .then(result => { if (!cancelled) { setSummary(result); setPhase('ready'); } })
      .catch(() => { if (!cancelled) setPhase('error'); });
    return () => { cancelled = true; };
  }, [enabled, tick]);

  if (phase === 'error') return { status: 'error', retry };
  if (phase === 'loading') return { status: 'loading' };
  return { status: 'ready', summary, retry };
}
```

- [ ] **Step 4: Run `npm test -- useOwnerCode` → PASS.**
- [ ] **Step 5: `npm run build` → clean.**
- [ ] **Step 6: Commit.** `git add -A && git commit -m "feat(club): useOwnerCode hook"`

---

### Task 6: OwnerCodePanel

**Files:**
- Create: `src/AFK4.Platform.Web/src/club/install/OwnerCodePanel.tsx`
- Test: `src/AFK4.Platform.Web/src/club/install/OwnerCodePanel.test.tsx`

- [ ] **Step 1: Write the failing test.**

```tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import type { OwnerCodeSummary, OwnerCodeIssued } from '@/api/types';
import { OwnerCodePanel } from './OwnerCodePanel';

const summary: OwnerCodeSummary = { codeSuffix: '5678', expiresAtUtc: '2026-06-01T00:00:00.000Z', lastUsedAtUtc: null, failedAttemptCount: 0 };

function fakeClient() {
  return {
    getOwnerCode: vi.fn<() => Promise<OwnerCodeSummary | null>>(async () => summary),
    generateOwnerCode: vi.fn<() => Promise<OwnerCodeIssued>>(async () => ({ ownerCode: '99998888', codeSuffix: '8888', expiresAtUtc: '2026-07-01T00:00:00.000Z' })),
    rotateOwnerCode: vi.fn<(reason: string) => Promise<OwnerCodeIssued>>(async () => ({ ownerCode: '77776666', codeSuffix: '6666', expiresAtUtc: '2026-07-01T00:00:00.000Z' }))
  };
}

it('shows the masked code then the full code after generating', async () => {
  const client = fakeClient();
  render(
    <I18nProvider><ToastProvider>
      <OwnerCodePanel client={client as never} canManage />
    </ToastProvider></I18nProvider>
  );
  expect(await screen.findByText('**** 5678')).toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Сгенерировать код' }));
  await waitFor(() => expect(client.generateOwnerCode).toHaveBeenCalled());
  expect(await screen.findByText('99998888')).toBeInTheDocument();
});

it('shows a no-access note when management is not allowed', () => {
  const client = fakeClient();
  render(
    <I18nProvider><ToastProvider>
      <OwnerCodePanel client={client as never} canManage={false} />
    </ToastProvider></I18nProvider>
  );
  expect(screen.getByText('Ваша учётная запись не может генерировать код владельца.')).toBeInTheDocument();
  expect(client.getOwnerCode).not.toHaveBeenCalled();
});
```

- [ ] **Step 2: Run `npm test -- OwnerCodePanel` → FAIL.**

- [ ] **Step 3: Implement `OwnerCodePanel.tsx`.**

```tsx
import { useState } from 'react';
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { LoadingCards, ErrorState } from '@/components/ui/states';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import type { OwnerCodeSummary, OwnerCodeIssued } from '@/api/types';
import { useOwnerCode } from './useOwnerCode';
import { toOwnerCodeView } from './installModel';

type Client = Pick<ClubApiClient, 'getOwnerCode' | 'generateOwnerCode' | 'rotateOwnerCode'>;

export function OwnerCodePanel({ client, canManage }: { client: Client; canManage: boolean }) {
  const { t, formatDate } = useI18n();
  const { toast } = useToast();
  const state = useOwnerCode(client, canManage);
  const [issued, setIssued] = useState<OwnerCodeIssued | null>(null);
  const [override, setOverride] = useState<OwnerCodeSummary | null>(null);
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);

  async function run(kind: 'generate' | 'rotate') {
    setBusy(true);
    try {
      const next = kind === 'generate'
        ? await client.generateOwnerCode()
        : await client.rotateOwnerCode(reason.trim().length > 0 ? reason.trim() : 'dashboard rotation');
      setIssued(next);
      setOverride({ codeSuffix: next.codeSuffix, expiresAtUtc: next.expiresAtUtc, lastUsedAtUtc: null, failedAttemptCount: 0 });
      toast({ title: kind === 'generate' ? t('install.ownerCode.generated') : t('install.ownerCode.rotated'), variant: 'success' });
    } catch {
      toast({ title: t('install.ownerCode.error'), variant: 'error' });
    } finally {
      setBusy(false);
    }
  }

  return (
    <Card>
      <CardHeader><CardTitle>{t('install.ownerCode.title')}</CardTitle></CardHeader>
      <CardContent>
        {!canManage ? (
          <p className="text-sm text-muted-foreground">{t('install.ownerCode.noAccess')}</p>
        ) : state.status === 'loading' ? (
          <LoadingCards count={1} />
        ) : state.status === 'error' ? (
          <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />
        ) : (
          <OwnerCodeBody
            view={toOwnerCodeView(override ?? state.summary, issued)}
            reason={reason} setReason={setReason} busy={busy}
            onGenerate={() => void run('generate')} onRotate={() => void run('rotate')}
            formatDate={formatDate}
          />
        )}
      </CardContent>
    </Card>
  );
}

function OwnerCodeBody({ view, reason, setReason, busy, onGenerate, onRotate, formatDate }: {
  view: ReturnType<typeof toOwnerCodeView>;
  reason: string;
  setReason: (v: string) => void;
  busy: boolean;
  onGenerate: () => void;
  onRotate: () => void;
  formatDate: (iso: string) => string;
}) {
  const { t } = useI18n();
  return (
    <div className="flex flex-col gap-4">
      <div className="font-mono text-2xl font-semibold tracking-widest" aria-label={t('install.ownerCode.title')}>
        {view.hasCode ? view.code : t('install.ownerCode.none')}
      </div>
      <dl className="grid grid-cols-1 gap-2 text-sm sm:grid-cols-3">
        <div><dt className="text-xs text-muted-foreground">{t('install.ownerCode.validUntil')}</dt>
          <dd>{view.expiresAtUtc === null ? '—' : formatDate(view.expiresAtUtc)}</dd></div>
        <div><dt className="text-xs text-muted-foreground">{t('install.ownerCode.lastUsed')}</dt>
          <dd>{view.lastUsedAtUtc === null ? '—' : formatDate(view.lastUsedAtUtc)}</dd></div>
        <div><dt className="text-xs text-muted-foreground">{t('install.ownerCode.failed')}</dt>
          <dd className="tabular-nums">{view.failedAttemptCount}</dd></div>
      </dl>
      <div className="flex flex-wrap items-end gap-3">
        <Button disabled={busy} onClick={onGenerate}>{t('install.ownerCode.generate')}</Button>
        <label className="flex flex-col gap-1 text-xs text-muted-foreground">
          {t('install.ownerCode.reason')}
          <Input aria-label={t('install.ownerCode.reason')} value={reason} onChange={e => setReason(e.target.value)} disabled={busy} />
        </label>
        <Button variant="outline" disabled={busy || !view.hasCode} onClick={onRotate}>{t('install.ownerCode.rotate')}</Button>
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Run `npm test -- OwnerCodePanel` → PASS (both tests).**
- [ ] **Step 5: `npm run build` → clean.**
- [ ] **Step 6: Commit.** `git add -A && git commit -m "feat(club): OwnerCodePanel"`

---

### Task 7: InstallScreen

**Files:**
- Create: `src/AFK4.Platform.Web/src/club/install/InstallScreen.tsx`
- Test: `src/AFK4.Platform.Web/src/club/install/InstallScreen.test.tsx`

- [ ] **Step 1: Write the failing test.**

```tsx
import { render, screen } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import type { OwnerCodeSummary } from '@/api/types';
import { InstallScreen } from './InstallScreen';

function fakeClient() {
  return {
    getOwnerCode: vi.fn<() => Promise<OwnerCodeSummary | null>>(async () => null),
    generateOwnerCode: vi.fn(),
    rotateOwnerCode: vi.fn()
  };
}

it('renders the install header, a download link, and the branch list', async () => {
  render(
    <I18nProvider><ToastProvider>
      <InstallScreen client={fakeClient() as never} canManage branches={[{ branchId: 'b1', name: 'Центр', city: 'Москва' }]} />
    </ToastProvider></I18nProvider>
  );
  expect(screen.getByText('Установка на ПК')).toBeInTheDocument();
  expect(screen.getByRole('link', { name: 'Скачать установщик MSI' })).toBeInTheDocument();
  expect(await screen.findByText('Центр')).toBeInTheDocument();
});
```

- [ ] **Step 2: Run `npm test -- InstallScreen` → FAIL.**

- [ ] **Step 3: Implement `InstallScreen.tsx`.**

```tsx
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import { OwnerCodePanel } from './OwnerCodePanel';
import { getSetupMsiUrl } from './installModel';

type Client = Pick<ClubApiClient, 'getOwnerCode' | 'generateOwnerCode' | 'rotateOwnerCode'>;

export function InstallScreen({ client, canManage, branches }: {
  client: Client;
  canManage: boolean;
  branches: { branchId: string; name: string; city?: string }[];
}) {
  const { t } = useI18n();
  const msiUrl = getSetupMsiUrl();

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="text-lg font-semibold">{t('install.title')}</h2>
          <p className="text-sm text-muted-foreground">{t('install.subtitle')}</p>
        </div>
        <Button asChild>
          <a href={msiUrl} download>{t('install.download')}</a>
        </Button>
      </div>

      <OwnerCodePanel client={client} canManage={canManage} />

      <Card>
        <CardHeader><CardTitle>{t('install.wizard.title')}</CardTitle></CardHeader>
        <CardContent className="flex flex-col gap-3">
          <ol className="list-decimal space-y-1 pl-5 text-sm">
            <li>{t('install.wizard.step1')}</li>
            <li>{t('install.wizard.step2')}</li>
            <li>{t('install.wizard.step3')}</li>
            <li>{t('install.wizard.step4')}</li>
          </ol>
          <pre className="rounded-md bg-muted px-3 py-2 font-mono text-xs">msiexec /i AFK4-Agent.msi</pre>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>{t('install.branches.title')}</CardTitle></CardHeader>
        <CardContent>
          {branches.length === 0 ? (
            <EmptyState message={t('install.branches.empty')} />
          ) : (
            <ul className="grid grid-cols-1 gap-2 sm:grid-cols-2">
              {branches.map(b => (
                <li key={b.branchId} className="rounded-md border px-3 py-2">
                  <div className="text-sm font-medium">{b.name}</div>
                  {b.city !== undefined && b.city.length > 0 && <div className="text-xs text-muted-foreground">{b.city}</div>}
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
```

> If `Button` does not support `asChild`, render a plain anchor styled with the button classes instead — but the shadcn `Button` in this repo is built on Radix Slot and supports `asChild`. Confirm by reading `src/components/ui/button.tsx`; if `asChild` is absent, use `<a className="<button classes>" href={msiUrl} download>` or import `buttonVariants` and apply `className={buttonVariants()}`.

- [ ] **Step 4: Run `npm test -- InstallScreen` → PASS.**
- [ ] **Step 5: `npm run build` → clean.**
- [ ] **Step 6: Commit.** `git add -A && git commit -m "feat(club): InstallScreen"`

---

### Task 8: Wire profile + new install into ClubArea

**Files:**
- Modify: `src/AFK4.Platform.Web/src/club/nav.ts`
- Modify: `src/AFK4.Platform.Web/src/App.tsx`

- [ ] **Step 1: Enable the profile nav item.** In `nav.ts`, change the `profile` item — both flags:
```ts
      { key: 'profile', labelKey: 'nav.profile', path: '/club/profile', ownerOnly: false, soon: false },
```

- [ ] **Step 2: Add the `clubProfile` route kind** to the `ClubRoute` union (after `clubInstall`):
```ts
  | { kind: 'clubProfile' }
```

- [ ] **Step 3: Screen title.** In `CLUB_SCREEN_TITLE`, add (after `clubInstall: 'Установка',`):
```ts
  clubProfile: 'Профиль',
```

- [ ] **Step 4: pathForRoute.** Add a case:
```ts
    case 'clubProfile':
      return '/club/profile';
```

- [ ] **Step 5: resolvePlatformRoute.** After the `/club/install` check add:
```ts
    if (path === '/club/profile') {
      return { route: { kind: 'clubProfile' } };
    }
```

- [ ] **Step 6: isClubRoute.** Add:
```ts
    || route.kind === 'clubProfile'
```

- [ ] **Step 7: Imports.** Near the other club screen imports, add:
```ts
import { InstallScreen } from './club/install/InstallScreen';
import { ProfileScreen } from './club/profile/ProfileScreen';
```

- [ ] **Step 8: Render branches in `ClubArea`.** First READ `ClubArea` to find the ternary chain and the final `) : ( <LegacyClubScreen … /> )`. Add TWO new branches BEFORE that final `LegacyClubScreen` else (e.g. right after the `clubJournal` branch):

```tsx
      ) : route.kind === 'clubInstall' ? (
        <InstallScreen
          client={clubClient}
          canManage={session.permissions.includes('identity.owner_code.manage')}
          branches={session.branchIds.map(id => ({ branchId: id, name: directory[id]?.name ?? t('branches.unnamed'), city: directory[id]?.city }))}
        />
      ) : route.kind === 'clubProfile' ? (
        <ProfileScreen
          session={session}
          branches={branches}
          roleLabel={ROLE_LABEL[role]}
          onSignOut={onSignOut}
        />
```

Keep the existing final `) : ( <LegacyClubScreen … /> )` as-is for now — after these edits it is only reached by the dead branch-detail routes (`clubInstall` is now handled above it). `directory`, `branches`, `session`, `role`, `ROLE_LABEL`, `onSignOut`, `t` are all already in scope in `ClubArea`.

- [ ] **Step 9: Build + tests.** From the frontend dir: `npm run build` → clean; `npm test` → all green.
- [ ] **Step 10: Commit.** `git add -A && git commit -m "feat(club): wire profile + redesigned install screens"`

---

### Task 9: Retire dead routes + delete ClubDashboard

**Files:**
- Modify: `src/AFK4.Platform.Web/src/App.tsx`
- Delete: `src/AFK4.Platform.Web/src/components/ClubDashboard.tsx`

- [ ] **Step 1: Remove the dead branch-detail route kinds from the `ClubRoute` union.** Delete these five lines:
```ts
  | { kind: 'clubBranchDetail'; branchId: string }
  | { kind: 'clubBranchFloorMap'; branchId: string }
  | { kind: 'clubBranchDevices'; branchId: string }
  | { kind: 'clubBranchPendingDevices'; branchId: string }
  | { kind: 'clubBranchOperators'; branchId: string }
```

- [ ] **Step 2: Remove their `CLUB_SCREEN_TITLE` entries:**
```ts
  clubBranchDetail: 'Филиал',
  clubBranchFloorMap: 'Зал и ПК',
  clubBranchDevices: 'Устройства',
  clubBranchPendingDevices: 'Устройства',
  clubBranchOperators: 'Операторы'
```
(Leave the rest of the object intact; mind the trailing comma on the line now preceding the closing brace.)

- [ ] **Step 3: Remove their `pathForRoute` cases.** Delete the block:
```ts
    case 'clubBranchDetail':
    case 'clubBranchFloorMap':
    case 'clubBranchDevices':
    case 'clubBranchPendingDevices':
    case 'clubBranchOperators':
      return '/club/branches';
```
(The `default: return '/club';` already covers any fallthrough.)

- [ ] **Step 4: Remove their `isClubRoute` clauses.** Delete:
```ts
    || route.kind === 'clubBranchDetail'
    || route.kind === 'clubBranchFloorMap'
    || route.kind === 'clubBranchDevices'
    || route.kind === 'clubBranchPendingDevices'
    || route.kind === 'clubBranchOperators'
```
Ensure the remaining chain ends correctly (the last surviving clause ends with `;`).

- [ ] **Step 5: Replace the terminal `LegacyClubScreen` render.** In `ClubArea`, the JSX ternary chain currently ends with:
```tsx
      ) : (
        <LegacyClubScreen
          client={clubClient}
          route={route}
          session={session}
          onNavigate={onNavigate}
        />
      )}
```
Replace that terminal `else` with the `clubInstall` branch becoming the final case. Concretely: change the `clubInstall` branch you added in Task 8 from `) : route.kind === 'clubInstall' ? ( <InstallScreen … /> )` so that `InstallScreen` becomes the terminal `) : ( <InstallScreen … /> )`, OR keep `clubInstall` explicit and make the terminal else a safety `notFound`. RECOMMENDED simplest valid form: keep `clubProfile` as the last explicit `? (`-branch and make `InstallScreen` the terminal `: ( … )`:

```tsx
      ) : route.kind === 'clubProfile' ? (
        <ProfileScreen
          session={session}
          branches={branches}
          roleLabel={ROLE_LABEL[role]}
          onSignOut={onSignOut}
        />
      ) : (
        <InstallScreen
          client={clubClient}
          canManage={session.permissions.includes('identity.owner_code.manage')}
          branches={session.branchIds.map(id => ({ branchId: id, name: directory[id]?.name ?? t('branches.unnamed'), city: directory[id]?.city }))}
        />
      )}
```
This removes the explicit `route.kind === 'clubInstall' ?` test and lets `clubInstall` fall through to the terminal `InstallScreen` (the union is now exhaustive; `clubInstall` is the only remaining kind not matched by an earlier branch). READ the chain first and make this restructure carefully so every `) : cond ? (` / `) : ( … )}` stays balanced.

- [ ] **Step 6: Remove the import.** Delete `App.tsx` line 9:
```ts
import { LegacyClubScreen } from './components/ClubDashboard';
```

- [ ] **Step 7: Delete the legacy file.** `git rm src/AFK4.Platform.Web/src/components/ClubDashboard.tsx` (run from `D:\afk4.net`).

- [ ] **Step 8: Grep for stragglers.** Search the whole `src/AFK4.Platform.Web/src` tree for `ClubDashboard`, `LegacyClubScreen`, `DashboardHome`, and any of the removed route kinds (`clubBranchDetail`, `clubBranchFloorMap`, `clubBranchDevices`, `clubBranchPendingDevices`, `clubBranchOperators`). Expected: ZERO matches (other than possibly the design/plan docs under `docs/`, which are fine). If any `.ts`/`.tsx` source references remain, fix them.

- [ ] **Step 9: FINAL GATE.** From the frontend dir: `npm run build` (`tsc -b && vite build`) → clean (this proves the union is exhaustive and no dangling imports). Then `npm test` → ALL green. Report the totals.

- [ ] **Step 10: Commit.** From `D:\afk4.net`: `git add -A && git commit -m "refactor(club): retire dead branch-detail routes + delete legacy ClubDashboard"`

---

## Self-Review notes (for the controller)
- **Sequencing:** Task 8 keeps `LegacyClubScreen` as the (now dead-route-only) terminal else so the build stays green; Task 9 removes the dead routes AND the legacy file together, restructuring the terminal else to `InstallScreen`. Don't reorder.
- **`asChild` on Button:** the repo's Button is Radix-Slot based; `asChild` lets the MSI download render as an `<a download>`. Task 7 has a fallback note if it isn't.
- **Profile is synchronous:** no hook — data comes from `session` + the `branches` list + `roleLabel` already built in `ClubArea`. The `directory` provides `city` for the install branch list.
- **Owner-code panel** owns its mutations (generate/rotate) with local `issued`/`override` state layered over the load-only `useOwnerCode`; it does not fetch when `canManage` is false (avoids a guaranteed 403).
- **No backend changes.** Profile editing is intentionally absent (honest note). After this plan, `ClubDashboard.tsx` is gone and sub-project 2 (full `/club/*` console) is complete.
