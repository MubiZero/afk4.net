# Club Settings & Operators Screen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the redesigned **Настройки** club screen — tab **Филиал** (branch profile + manual-device-approval toggle) and tab **Операторы и роли** (staff list, create operator, edit profile/roles, enable/disable, password reset) — owner-only, on the new design system.

**Architecture:** Follows the feature shape proven in sub-project 2 Plan 1: a pure view-model builder (`settingsModel.ts`) + a `useSettings` hook returning a discriminated-union state (`loading | error | ready` + `retry`) + a presentational `SettingsScreen` switching on `state.status`. List+detail uses the Sheet drawer; create uses a Dialog; destructive/credential actions (deactivate, password reset) go through `ConfirmDialog` with server-confirmed-only toasts. Two new vendored primitives (Switch, Checkbox) are added. `clubApi` gains four thin wrappers over staff routes the backend already exposes; no new backend contracts.

**Tech Stack:** React 19, TypeScript, Vite, Vitest 4 + jsdom + @testing-library/react (`globals: false` — every test imports `it`/`expect`/`vi` from `'vitest'`), shadcn/ui-style primitives over the `radix-ui` umbrella package, Tailwind v4, i18n RU/EN via `useI18n()`.

---

## Backend Contracts Consumed (already exist — do NOT change the backend)

All staff routes verified in `src/AFK4.Platform.Api/Program.cs`. All return a `StaffUser` DTO (`{ staffUserId, organizationId, userName, displayName, isActive, roleNames, createdAtUtc }`).

| Method | Route | Request body |
|--------|-------|--------------|
| GET | `/api/branches/{branchId}/staff` | — → `StaffUser[]` |
| POST | `/api/branches/{branchId}/staff` | `{ organizationId, userName, displayName, password, roleNames }` |
| PATCH | `/api/branches/{branchId}/staff/{staffUserId}/roles` | `{ organizationId, roleNames }` |
| PATCH | `/api/branches/{branchId}/staff/{staffUserId}/profile` | `{ organizationId, userName, displayName }` |
| PATCH | `/api/branches/{branchId}/staff/{staffUserId}/state` | `{ organizationId, isActive }` |
| POST | `/api/branches/{branchId}/staff/{staffUserId}/password-reset` | `{ organizationId, newPassword }` |
| GET / PATCH | `/api/branches/{branchId}/profile` | profile: `{ organizationId, name, city }` |
| GET / PUT | `/api/branches/{branchId}/settings` | settings: `{ organizationId, requireManualDeviceApproval }` |

Backend validation rules to mirror in the UI (defensive only — server is source of truth):
- **Password:** at least 8 characters.
- **Roles:** at least one role required; assignable branch-staff roles are exactly `branch_manager`, `shift_supervisor`, `cashier_operator`, `technician`, `accountant_auditor`. `owner` is provisioned via owner-invite and is **not** assignable here.
- **State:** the backend rejects deactivating the currently-authenticated account; the UI disables that action for self.

`organizationId` for create/profile requests comes from `session.organizationId`; for per-operator updates it is also present on the operator row (`StaffUser.organizationId`).

## File Structure

- Create: `src/components/ui/switch.tsx` (+ test) — settings toggle primitive.
- Create: `src/components/ui/checkbox.tsx` (+ test) — role multi-select primitive.
- Modify: `src/api/types.ts` — four new request interfaces.
- Modify: `src/api/clubApi.ts` — four new staff wrapper methods.
- Create: `src/api/clubApi.staff.test.ts` — wrapper request-shape tests.
- Create: `src/club/settings/roles.ts` (+ test) — assignable-role list + role→label-key map.
- Create: `src/club/settings/settingsModel.ts` (+ test) — pure view-model builder.
- Create: `src/club/settings/useSettings.ts` (+ test) — data hook.
- Create: `src/club/settings/BranchProfileForm.tsx` (+ test) — Филиал tab content.
- Create: `src/club/settings/OperatorsTable.tsx` (+ test) — staff table.
- Create: `src/club/settings/OperatorDrawer.tsx` (+ test) — operator detail/edit drawer (canonical safety pattern).
- Create: `src/club/settings/CreateOperatorDialog.tsx` (+ test) — create-operator dialog.
- Create: `src/club/settings/SettingsScreen.tsx` (+ test) — tabbed screen shell.
- Modify: `src/i18n/messages.ts` + `src/i18n/messages.test.ts` — settings/operators/roles keys.
- Modify: `src/club/nav.ts` — `settings` item `soon: false`.
- Modify: `src/App.tsx` — `clubSettings` route, title, path, `isClubRoute`, owner-gated render.

---

### Task 1: Switch primitive

**Files:**
- Create: `src/AFK4.Platform.Web/src/components/ui/switch.tsx`
- Test: `src/AFK4.Platform.Web/src/components/ui/switch.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// src/components/ui/switch.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { Switch } from './switch';

it('toggles via onCheckedChange when clicked', () => {
  const onCheckedChange = vi.fn();
  render(<Switch aria-label="approval" checked={false} onCheckedChange={onCheckedChange} />);
  fireEvent.click(screen.getByRole('switch', { name: 'approval' }));
  expect(onCheckedChange).toHaveBeenCalledWith(true);
});

it('is disabled when disabled prop is set', () => {
  render(<Switch aria-label="approval" checked={false} disabled onCheckedChange={vi.fn()} />);
  expect(screen.getByRole('switch', { name: 'approval' })).toBeDisabled();
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- switch`
Expected: FAIL — cannot resolve `./switch`.

- [ ] **Step 3: Write minimal implementation**

```tsx
// src/components/ui/switch.tsx
import type { ComponentProps } from 'react';
import { Switch as SwitchPrimitive } from 'radix-ui';
import { cn } from '@/lib/utils';

export function Switch({ className, ...props }: ComponentProps<typeof SwitchPrimitive.Root>) {
  return (
    <SwitchPrimitive.Root
      className={cn(
        'peer inline-flex h-5 w-9 shrink-0 cursor-pointer items-center rounded-full border-2 border-transparent transition-colors outline-none',
        'focus-visible:ring-[3px] focus-visible:ring-ring/50 disabled:cursor-not-allowed disabled:opacity-50',
        'data-[state=checked]:bg-primary data-[state=unchecked]:bg-input',
        className
      )}
      {...props}
    >
      <SwitchPrimitive.Thumb className="pointer-events-none block size-4 rounded-full bg-background shadow-sm transition-transform data-[state=checked]:translate-x-4 data-[state=unchecked]:translate-x-0" />
    </SwitchPrimitive.Root>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- switch`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Web/src/components/ui/switch.tsx src/AFK4.Platform.Web/src/components/ui/switch.test.tsx
git commit -m "feat(web): vendor Switch primitive"
```

---

### Task 2: Checkbox primitive

**Files:**
- Create: `src/AFK4.Platform.Web/src/components/ui/checkbox.tsx`
- Test: `src/AFK4.Platform.Web/src/components/ui/checkbox.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// src/components/ui/checkbox.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { Checkbox } from './checkbox';

it('emits onCheckedChange(true) when an unchecked box is clicked', () => {
  const onCheckedChange = vi.fn();
  render(<Checkbox aria-label="role" checked={false} onCheckedChange={onCheckedChange} />);
  fireEvent.click(screen.getByRole('checkbox', { name: 'role' }));
  expect(onCheckedChange).toHaveBeenCalledWith(true);
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- checkbox`
Expected: FAIL — cannot resolve `./checkbox`.

- [ ] **Step 3: Write minimal implementation**

```tsx
// src/components/ui/checkbox.tsx
import type { ComponentProps } from 'react';
import { Checkbox as CheckboxPrimitive } from 'radix-ui';
import { Check } from 'lucide-react';
import { cn } from '@/lib/utils';

export function Checkbox({ className, ...props }: ComponentProps<typeof CheckboxPrimitive.Root>) {
  return (
    <CheckboxPrimitive.Root
      className={cn(
        'peer size-4 shrink-0 rounded-[4px] border border-input shadow-xs outline-none transition-shadow',
        'focus-visible:ring-[3px] focus-visible:ring-ring/50 disabled:cursor-not-allowed disabled:opacity-50',
        'data-[state=checked]:border-primary data-[state=checked]:bg-primary data-[state=checked]:text-primary-foreground',
        className
      )}
      {...props}
    >
      <CheckboxPrimitive.Indicator className="flex items-center justify-center text-current">
        <Check className="size-3.5" />
      </CheckboxPrimitive.Indicator>
    </CheckboxPrimitive.Root>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- checkbox`
Expected: PASS (1 test).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Web/src/components/ui/checkbox.tsx src/AFK4.Platform.Web/src/components/ui/checkbox.test.tsx
git commit -m "feat(web): vendor Checkbox primitive"
```

---

### Task 3: Staff request types + clubApi wrappers

**Files:**
- Modify: `src/AFK4.Platform.Web/src/api/types.ts` (append after `CreateStaffUserRequest`, ~line 209)
- Modify: `src/AFK4.Platform.Web/src/api/clubApi.ts` (import block ~lines 8-23; new methods after `createStaff`, ~line 181)
- Test: `src/AFK4.Platform.Web/src/api/clubApi.staff.test.ts`

- [ ] **Step 1: Write the failing test**

```ts
// src/api/clubApi.staff.test.ts
import { it, expect, vi } from 'vitest';
import { ClubApiClient } from './clubApi';

function okResponse(body: unknown) {
  return { ok: true, status: 200, headers: new Map(), text: async () => JSON.stringify(body) } as unknown as Response;
}

function makeClient(fetchImpl: typeof fetch) {
  return new ClubApiClient({
    baseUrl: 'https://api.test',
    fetchImpl,
    session: {
      staffUserId: 'u1', organizationId: 'org1', displayName: 'D', branchIds: ['b1'], permissions: [],
      accessToken: 'tok', accessTokenExpiresAtUtc: '', refreshToken: 'r', refreshTokenExpiresAtUtc: ''
    },
    onSessionChanged: () => {}
  });
}

it('updateStaffRoles PATCHes the roles route with the role names', async () => {
  const fetchImpl = vi.fn().mockResolvedValue(okResponse({ staffUserId: 's1' }));
  const client = makeClient(fetchImpl as unknown as typeof fetch);
  await client.updateStaffRoles('b1', 's1', { organizationId: 'org1', roleNames: ['branch_manager'] });
  const [url, init] = fetchImpl.mock.calls[0];
  expect(url).toBe('https://api.test/api/branches/b1/staff/s1/roles');
  expect(init.method).toBe('PATCH');
  expect(JSON.parse(init.body)).toEqual({ organizationId: 'org1', roleNames: ['branch_manager'] });
});

it('updateStaffState PATCHes the state route', async () => {
  const fetchImpl = vi.fn().mockResolvedValue(okResponse({ staffUserId: 's1' }));
  const client = makeClient(fetchImpl as unknown as typeof fetch);
  await client.updateStaffState('b1', 's1', { organizationId: 'org1', isActive: false });
  const [url, init] = fetchImpl.mock.calls[0];
  expect(url).toBe('https://api.test/api/branches/b1/staff/s1/state');
  expect(init.method).toBe('PATCH');
  expect(JSON.parse(init.body)).toEqual({ organizationId: 'org1', isActive: false });
});

it('resetStaffPassword POSTs the password-reset route', async () => {
  const fetchImpl = vi.fn().mockResolvedValue(okResponse({ staffUserId: 's1' }));
  const client = makeClient(fetchImpl as unknown as typeof fetch);
  await client.resetStaffPassword('b1', 's1', { organizationId: 'org1', newPassword: 'longenough' });
  const [url, init] = fetchImpl.mock.calls[0];
  expect(url).toBe('https://api.test/api/branches/b1/staff/s1/password-reset');
  expect(init.method).toBe('POST');
  expect(JSON.parse(init.body)).toEqual({ organizationId: 'org1', newPassword: 'longenough' });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- clubApi.staff`
Expected: FAIL — `updateStaffRoles` is not a function / type errors.

- [ ] **Step 3: Add the request types**

In `src/api/types.ts`, immediately after the `CreateStaffUserRequest` interface (ends ~line 209), add:

```ts
export interface UpdateStaffUserRolesRequest {
  organizationId: string;
  roleNames: string[];
}

export interface UpdateStaffUserProfileRequest {
  organizationId: string;
  userName: string;
  displayName: string;
}

export interface UpdateStaffUserStateRequest {
  organizationId: string;
  isActive: boolean;
}

export interface ResetStaffUserPasswordRequest {
  organizationId: string;
  newPassword: string;
}
```

- [ ] **Step 4: Add the imports + methods in clubApi.ts**

In the `import type { ... } from './types';` block, add these four names (keep alphabetical-ish ordering consistent with the existing block):

```ts
  ResetStaffUserPasswordRequest,
  UpdateStaffUserProfileRequest,
  UpdateStaffUserRolesRequest,
  UpdateStaffUserStateRequest,
```

Then, immediately after the `createStaff` method (ends ~line 181), add:

```ts
  public updateStaffRoles(
    branchId: string,
    staffUserId: string,
    request: UpdateStaffUserRolesRequest
  ): Promise<StaffUser> {
    return this.send<StaffUser>(
      'PATCH',
      `/api/branches/${encodeURIComponent(branchId)}/staff/${encodeURIComponent(staffUserId)}/roles`,
      request
    );
  }

  public updateStaffProfile(
    branchId: string,
    staffUserId: string,
    request: UpdateStaffUserProfileRequest
  ): Promise<StaffUser> {
    return this.send<StaffUser>(
      'PATCH',
      `/api/branches/${encodeURIComponent(branchId)}/staff/${encodeURIComponent(staffUserId)}/profile`,
      request
    );
  }

  public updateStaffState(
    branchId: string,
    staffUserId: string,
    request: UpdateStaffUserStateRequest
  ): Promise<StaffUser> {
    return this.send<StaffUser>(
      'PATCH',
      `/api/branches/${encodeURIComponent(branchId)}/staff/${encodeURIComponent(staffUserId)}/state`,
      request
    );
  }

  public resetStaffPassword(
    branchId: string,
    staffUserId: string,
    request: ResetStaffUserPasswordRequest
  ): Promise<StaffUser> {
    return this.send<StaffUser>(
      'POST',
      `/api/branches/${encodeURIComponent(branchId)}/staff/${encodeURIComponent(staffUserId)}/password-reset`,
      request
    );
  }
```

- [ ] **Step 5: Run test to verify it passes**

Run: `npm test -- clubApi.staff`
Expected: PASS (3 tests).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Platform.Web/src/api/types.ts src/AFK4.Platform.Web/src/api/clubApi.ts src/AFK4.Platform.Web/src/api/clubApi.staff.test.ts
git commit -m "feat(web): add staff role/profile/state/password-reset client methods"
```

---

### Task 4: Roles model (assignable roles + label keys)

**Files:**
- Create: `src/AFK4.Platform.Web/src/club/settings/roles.ts`
- Test: `src/AFK4.Platform.Web/src/club/settings/roles.test.ts`

- [ ] **Step 1: Write the failing test**

```ts
// src/club/settings/roles.test.ts
import { it, expect } from 'vitest';
import { ASSIGNABLE_ROLES, roleLabelKey } from './roles';

it('exposes the five assignable branch-staff roles and excludes owner', () => {
  expect(ASSIGNABLE_ROLES).toEqual([
    'branch_manager', 'shift_supervisor', 'cashier_operator', 'technician', 'accountant_auditor'
  ]);
  expect(ASSIGNABLE_ROLES).not.toContain('owner');
});

it('maps known roles to label keys and falls back to roles.unknown', () => {
  expect(roleLabelKey('branch_manager')).toBe('roles.branch_manager');
  expect(roleLabelKey('owner')).toBe('roles.owner');
  expect(roleLabelKey('something_else')).toBe('roles.unknown');
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- settings/roles`
Expected: FAIL — cannot resolve `./roles`.

- [ ] **Step 3: Write minimal implementation**

```ts
// src/club/settings/roles.ts
import type { MessageKey } from '@/i18n/messages';

/**
 * Branch-assignable staff roles. `owner` is provisioned via the owner-invite flow
 * and is intentionally NOT assignable from this screen (mirrors the backend's
 * IsAssignableBranchStaffRole allow-list).
 */
export const ASSIGNABLE_ROLES = [
  'branch_manager',
  'shift_supervisor',
  'cashier_operator',
  'technician',
  'accountant_auditor'
] as const;

export type AssignableRole = (typeof ASSIGNABLE_ROLES)[number];

const ROLE_LABEL_KEY: Record<string, MessageKey> = {
  owner: 'roles.owner',
  branch_manager: 'roles.branch_manager',
  shift_supervisor: 'roles.shift_supervisor',
  cashier_operator: 'roles.cashier_operator',
  technician: 'roles.technician',
  accountant_auditor: 'roles.accountant_auditor'
};

export function roleLabelKey(roleName: string): MessageKey {
  return ROLE_LABEL_KEY[roleName] ?? 'roles.unknown';
}
```

> NOTE: This file references message keys (`roles.*`) added in Task 12. TypeScript will not error because `MessageKey` is a string-literal union and these keys are added before the build gate runs; if you run the build before Task 12, expect type errors on the `roles.*` keys — that is expected and resolved by Task 12.

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- settings/roles`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Web/src/club/settings/roles.ts src/AFK4.Platform.Web/src/club/settings/roles.test.ts
git commit -m "feat(web): add branch-staff role model"
```

---

### Task 5: Settings view-model builder

**Files:**
- Create: `src/AFK4.Platform.Web/src/club/settings/settingsModel.ts`
- Test: `src/AFK4.Platform.Web/src/club/settings/settingsModel.test.ts`

- [ ] **Step 1: Write the failing test**

```ts
// src/club/settings/settingsModel.test.ts
import { it, expect } from 'vitest';
import { buildSettings } from './settingsModel';
import type { BranchProfile, BranchSettings, StaffUser } from '@/api/types';

const profile: BranchProfile = {
  organizationId: 'org', branchId: 'b1', name: 'Центр', city: 'Москва', createdAtUtc: '2026-01-01T00:00:00Z'
};
const settings: BranchSettings = { organizationId: 'org', branchId: 'b1', requireManualDeviceApproval: true };
const staff: StaffUser[] = [
  { staffUserId: 's2', organizationId: 'org', userName: 'BOB', displayName: 'Борис', isActive: true, roleNames: ['cashier_operator'], createdAtUtc: '2026-01-02T00:00:00Z' },
  { staffUserId: 's1', organizationId: 'org', userName: 'ANN', displayName: 'Анна', isActive: false, roleNames: ['branch_manager'], createdAtUtc: '2026-01-01T00:00:00Z' }
];

it('maps profile, settings flag, and sorts operators by display name', () => {
  const vm = buildSettings(profile, settings, staff);
  expect(vm.profile).toEqual({ branchId: 'b1', organizationId: 'org', name: 'Центр', city: 'Москва' });
  expect(vm.requireManualDeviceApproval).toBe(true);
  expect(vm.operators.map(o => o.displayName)).toEqual(['Анна', 'Борис']);
  expect(vm.operators[0]).toEqual({
    staffUserId: 's1', organizationId: 'org', userName: 'ANN', displayName: 'Анна', isActive: false, roleNames: ['branch_manager']
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- settingsModel`
Expected: FAIL — cannot resolve `./settingsModel`.

- [ ] **Step 3: Write minimal implementation**

```ts
// src/club/settings/settingsModel.ts
import type { BranchProfile, BranchSettings, StaffUser } from '@/api/types';

export interface OperatorRow {
  staffUserId: string;
  organizationId: string;
  userName: string;
  displayName: string;
  isActive: boolean;
  roleNames: string[];
}

export interface BranchProfileView {
  branchId: string;
  organizationId: string;
  name: string;
  city: string;
}

export interface SettingsViewModel {
  profile: BranchProfileView;
  requireManualDeviceApproval: boolean;
  operators: OperatorRow[];
}

export function buildSettings(
  profile: BranchProfile,
  settings: BranchSettings,
  staff: StaffUser[]
): SettingsViewModel {
  return {
    profile: {
      branchId: profile.branchId,
      organizationId: profile.organizationId,
      name: profile.name,
      city: profile.city
    },
    requireManualDeviceApproval: settings.requireManualDeviceApproval,
    operators: staff
      .map(u => ({
        staffUserId: u.staffUserId,
        organizationId: u.organizationId,
        userName: u.userName,
        displayName: u.displayName,
        isActive: u.isActive,
        roleNames: u.roleNames
      }))
      .sort((a, b) => a.displayName.localeCompare(b.displayName))
  };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- settingsModel`
Expected: PASS (1 test).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Web/src/club/settings/settingsModel.ts src/AFK4.Platform.Web/src/club/settings/settingsModel.test.ts
git commit -m "feat(web): add settings view-model builder"
```

---

### Task 6: useSettings hook

**Files:**
- Create: `src/AFK4.Platform.Web/src/club/settings/useSettings.ts`
- Test: `src/AFK4.Platform.Web/src/club/settings/useSettings.test.tsx`

This mirrors `useDevices`/`useOverview` exactly (deps `[branchId, tick]`, `clientRef` to avoid effect churn, discriminated-union return).

- [ ] **Step 1: Write the failing test**

```tsx
// src/club/settings/useSettings.test.tsx
import { renderHook, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { useSettings } from './useSettings';
import type { BranchProfile, BranchSettings, StaffUser } from '@/api/types';

const profile: BranchProfile = { organizationId: 'org', branchId: 'b1', name: 'Центр', city: 'Москва', createdAtUtc: '' };
const settings: BranchSettings = { organizationId: 'org', branchId: 'b1', requireManualDeviceApproval: false };
const staff: StaffUser[] = [];

it('loads profile, settings, and staff into a ready state', async () => {
  const client = {
    getBranchProfile: vi.fn().mockResolvedValue(profile),
    getBranchSettings: vi.fn().mockResolvedValue(settings),
    listStaff: vi.fn().mockResolvedValue(staff)
  };
  const { result } = renderHook(() => useSettings(client as never, 'b1'));
  expect(result.current.status).toBe('loading');
  await waitFor(() => expect(result.current.status).toBe('ready'));
  if (result.current.status === 'ready') {
    expect(result.current.data.profile.name).toBe('Центр');
  }
});

it('surfaces an error state when a call rejects', async () => {
  const client = {
    getBranchProfile: vi.fn().mockRejectedValue(new Error('boom')),
    getBranchSettings: vi.fn().mockResolvedValue(settings),
    listStaff: vi.fn().mockResolvedValue(staff)
  };
  const { result } = renderHook(() => useSettings(client as never, 'b1'));
  await waitFor(() => expect(result.current.status).toBe('error'));
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- useSettings`
Expected: FAIL — cannot resolve `./useSettings`.

- [ ] **Step 3: Write minimal implementation**

```ts
// src/club/settings/useSettings.ts
import { useCallback, useEffect, useRef, useState } from 'react';
import type { ClubApiClient } from '@/api/clubApi';
import { buildSettings, type SettingsViewModel } from './settingsModel';

export type SettingsState =
  | { status: 'loading'; retry: () => void }
  | { status: 'error'; message: string; retry: () => void }
  | { status: 'ready'; data: SettingsViewModel; retry: () => void };

type Loadable = Pick<ClubApiClient, 'getBranchProfile' | 'getBranchSettings' | 'listStaff'>;

export function useSettings(client: Loadable, branchId: string): SettingsState {
  const [tick, setTick] = useState(0);
  const [state, setState] = useState<{ status: 'loading' | 'error' | 'ready'; data?: SettingsViewModel; message?: string }>({ status: 'loading' });
  const retry = useCallback(() => setTick(t => t + 1), []);
  const clientRef = useRef(client);
  clientRef.current = client;

  useEffect(() => {
    let cancelled = false;
    setState({ status: 'loading' });
    const c = clientRef.current;
    Promise.all([c.getBranchProfile(branchId), c.getBranchSettings(branchId), c.listStaff(branchId)])
      .then(([profile, settings, staff]) => {
        if (cancelled) return;
        setState({ status: 'ready', data: buildSettings(profile, settings, staff) });
      })
      .catch((err: unknown) => {
        if (cancelled) return;
        setState({ status: 'error', message: err instanceof Error ? err.message : 'error' });
      });
    return () => { cancelled = true; };
  }, [branchId, tick]);

  if (state.status === 'ready') return { status: 'ready', data: state.data!, retry };
  if (state.status === 'error') return { status: 'error', message: state.message ?? 'error', retry };
  return { status: 'loading', retry };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- useSettings`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Web/src/club/settings/useSettings.ts src/AFK4.Platform.Web/src/club/settings/useSettings.test.tsx
git commit -m "feat(web): add useSettings data hook"
```

---

### Task 7: BranchProfileForm (Филиал tab)

**Files:**
- Create: `src/AFK4.Platform.Web/src/club/settings/BranchProfileForm.tsx`
- Test: `src/AFK4.Platform.Web/src/club/settings/BranchProfileForm.test.tsx`

Profile fields (name, city) save via one button; the approval toggle persists immediately on change and reverts its local state if the server call fails (server-confirmed only).

- [ ] **Step 1: Write the failing test**

```tsx
// src/club/settings/BranchProfileForm.test.tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { BranchProfileForm } from './BranchProfileForm';
import type { BranchProfileView } from './settingsModel';

const profile: BranchProfileView = { branchId: 'b1', organizationId: 'org', name: 'Центр', city: 'Москва' };

function setup(client: { updateBranchProfile: ReturnType<typeof vi.fn>; updateBranchSettings: ReturnType<typeof vi.fn> }, onDone = vi.fn()) {
  render(
    <I18nProvider><ToastProvider>
      <BranchProfileForm profile={profile} requireManualDeviceApproval={false} branchId="b1" client={client as never} onDone={onDone} />
    </ToastProvider></I18nProvider>
  );
  return { client, onDone };
}

it('saves the branch profile with trimmed values', async () => {
  const client = { updateBranchProfile: vi.fn().mockResolvedValue({}), updateBranchSettings: vi.fn() };
  const { onDone } = setup(client);
  fireEvent.change(screen.getByLabelText('Название филиала'), { target: { value: 'Север ' } });
  fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));
  await waitFor(() => expect(client.updateBranchProfile).toHaveBeenCalledWith('b1', { organizationId: 'org', name: 'Север', city: 'Москва' }));
  await waitFor(() => expect(onDone).toHaveBeenCalled());
});

it('persists the approval toggle when switched on', async () => {
  const client = { updateBranchProfile: vi.fn(), updateBranchSettings: vi.fn().mockResolvedValue({}) };
  setup(client);
  fireEvent.click(screen.getByRole('switch', { name: 'Ручное подтверждение устройств' }));
  await waitFor(() => expect(client.updateBranchSettings).toHaveBeenCalledWith('b1', { organizationId: 'org', requireManualDeviceApproval: true }));
});

it('reverts the toggle and shows an error toast when the settings call fails', async () => {
  const client = { updateBranchProfile: vi.fn(), updateBranchSettings: vi.fn().mockRejectedValue(new Error('boom')) };
  setup(client);
  const toggle = screen.getByRole('switch', { name: 'Ручное подтверждение устройств' });
  fireEvent.click(toggle);
  await waitFor(() => expect(screen.getByText('Не удалось выполнить действие')).toBeInTheDocument());
  expect(toggle).toHaveAttribute('aria-checked', 'false');
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- BranchProfileForm`
Expected: FAIL — cannot resolve `./BranchProfileForm`.

- [ ] **Step 3: Write minimal implementation**

```tsx
// src/club/settings/BranchProfileForm.tsx
import { useState } from 'react';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Switch } from '@/components/ui/switch';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import type { BranchProfileView } from './settingsModel';

type Actions = Pick<ClubApiClient, 'updateBranchProfile' | 'updateBranchSettings'>;

export function BranchProfileForm({ profile, requireManualDeviceApproval, branchId, client, onDone }: {
  profile: BranchProfileView;
  requireManualDeviceApproval: boolean;
  branchId: string;
  client: Actions;
  onDone: () => void;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [name, setName] = useState(profile.name);
  const [city, setCity] = useState(profile.city);
  const [approval, setApproval] = useState(requireManualDeviceApproval);
  const [pending, setPending] = useState(false);

  async function saveProfile() {
    setPending(true);
    try {
      await client.updateBranchProfile(branchId, { organizationId: profile.organizationId, name: name.trim(), city: city.trim() });
      toast({ title: t('toast.saved'), variant: 'success' });
      onDone();
    } catch {
      toast({ title: t('toast.failed'), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  function toggleApproval(next: boolean) {
    setApproval(next);
    setPending(true);
    void (async () => {
      try {
        await client.updateBranchSettings(branchId, { organizationId: profile.organizationId, requireManualDeviceApproval: next });
        toast({ title: t('toast.saved'), variant: 'success' });
        onDone();
      } catch {
        setApproval(!next);
        toast({ title: t('toast.failed'), variant: 'error' });
      } finally {
        setPending(false);
      }
    })();
  }

  return (
    <div className="flex max-w-md flex-col gap-5">
      <label className="block text-sm">
        <span className="mb-1 block text-muted-foreground">{t('settings.branch.name')}</span>
        <Input aria-label={t('settings.branch.name')} value={name} onChange={e => setName(e.target.value)} />
      </label>
      <label className="block text-sm">
        <span className="mb-1 block text-muted-foreground">{t('settings.branch.city')}</span>
        <Input aria-label={t('settings.branch.city')} value={city} onChange={e => setCity(e.target.value)} />
      </label>
      <Button disabled={pending || name.trim() === '' || city.trim() === ''} onClick={() => void saveProfile()}>
        {t('common.save')}
      </Button>

      <div className="flex items-center justify-between border-t border-border pt-4">
        <div>
          <div className="text-sm font-medium">{t('settings.branch.approval')}</div>
          <div className="text-xs text-muted-foreground">{t('settings.branch.approval.hint')}</div>
        </div>
        <Switch
          aria-label={t('settings.branch.approval')}
          checked={approval}
          disabled={pending}
          onCheckedChange={toggleApproval}
        />
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- BranchProfileForm`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Web/src/club/settings/BranchProfileForm.tsx src/AFK4.Platform.Web/src/club/settings/BranchProfileForm.test.tsx
git commit -m "feat(web): add branch profile form (settings)"
```

---

### Task 8: OperatorsTable

**Files:**
- Create: `src/AFK4.Platform.Web/src/club/settings/OperatorsTable.tsx`
- Test: `src/AFK4.Platform.Web/src/club/settings/OperatorsTable.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// src/club/settings/OperatorsTable.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { OperatorsTable } from './OperatorsTable';
import type { OperatorRow } from './settingsModel';

const rows: OperatorRow[] = [
  { staffUserId: 's1', organizationId: 'org', userName: 'ANN', displayName: 'Анна', isActive: true, roleNames: ['branch_manager'] }
];

function setup(onSelect = vi.fn(), data = rows) {
  render(<I18nProvider><OperatorsTable rows={data} emptyMessage="Пусто" onSelect={onSelect} /></I18nProvider>);
  return { onSelect };
}

it('renders an operator row with localized role and active badge', () => {
  setup();
  expect(screen.getByText('Анна')).toBeInTheDocument();
  expect(screen.getByText('Управляющий')).toBeInTheDocument();
  expect(screen.getByText('Активен')).toBeInTheDocument();
});

it('calls onSelect when a row is clicked', () => {
  const { onSelect } = setup();
  fireEvent.click(screen.getByText('Анна'));
  expect(onSelect).toHaveBeenCalledWith(rows[0]);
});

it('shows the empty message when there are no operators', () => {
  setup(vi.fn(), []);
  expect(screen.getByText('Пусто')).toBeInTheDocument();
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- OperatorsTable`
Expected: FAIL — cannot resolve `./OperatorsTable`.

- [ ] **Step 3: Write minimal implementation**

```tsx
// src/club/settings/OperatorsTable.tsx
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { EmptyState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import { roleLabelKey } from './roles';
import type { OperatorRow } from './settingsModel';

export function OperatorsTable({ rows, emptyMessage, onSelect }: {
  rows: OperatorRow[];
  emptyMessage: string;
  onSelect: (row: OperatorRow) => void;
}) {
  const { t } = useI18n();
  if (rows.length === 0) return <EmptyState message={emptyMessage} />;
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t('operators.col.name')}</TableHead>
          <TableHead>{t('operators.col.roles')}</TableHead>
          <TableHead>{t('operators.col.status')}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {rows.map(row => (
          <TableRow key={row.staffUserId} data-clickable="true" onClick={() => onSelect(row)}>
            <TableCell>
              <div className="font-medium">{row.displayName}</div>
              <div className="text-xs text-muted-foreground">{row.userName}</div>
            </TableCell>
            <TableCell className="text-sm">{row.roleNames.map(r => t(roleLabelKey(r))).join(', ')}</TableCell>
            <TableCell>
              <Badge variant={row.isActive ? 'default' : 'secondary'}>
                {row.isActive ? t('operators.status.active') : t('operators.status.inactive')}
              </Badge>
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- OperatorsTable`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Web/src/club/settings/OperatorsTable.tsx src/AFK4.Platform.Web/src/club/settings/OperatorsTable.test.tsx
git commit -m "feat(web): add operators table (settings)"
```

---

### Task 9: OperatorDrawer (canonical edit/safety pattern)

**Files:**
- Create: `src/AFK4.Platform.Web/src/club/settings/OperatorDrawer.tsx`
- Test: `src/AFK4.Platform.Web/src/club/settings/OperatorDrawer.test.tsx`

This is the canonical safety component for this screen: profile edit (save), role multi-select (save, ≥1 required), enable/disable (confirm-gated deactivate, disabled for self; direct activate), and password reset (ConfirmDialog with a new-password field, client-side ≥8 guard plus server validation). All actions are server-confirmed only — toast on result, `onDone()` only on success.

- [ ] **Step 1: Write the failing test**

```tsx
// src/club/settings/OperatorDrawer.test.tsx
import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { OperatorDrawer } from './OperatorDrawer';
import type { OperatorRow } from './settingsModel';

const active: OperatorRow = {
  staffUserId: 's1', organizationId: 'org', userName: 'ANN', displayName: 'Анна', isActive: true, roleNames: ['branch_manager']
};

function fakeClient() {
  return {
    updateStaffProfile: vi.fn().mockResolvedValue({}),
    updateStaffRoles: vi.fn().mockResolvedValue({}),
    updateStaffState: vi.fn().mockResolvedValue({}),
    resetStaffPassword: vi.fn().mockResolvedValue({})
  };
}

function setup(row: OperatorRow, currentStaffUserId = 'me', client = fakeClient(), onDone = vi.fn()) {
  render(
    <I18nProvider><ToastProvider>
      <OperatorDrawer operator={row} branchId="b1" currentStaffUserId={currentStaffUserId} client={client as never} onDone={onDone} />
    </ToastProvider></I18nProvider>
  );
  return { client, onDone };
}

it('saves the operator profile', async () => {
  const { client } = setup(active);
  fireEvent.change(screen.getByLabelText('Отображаемое имя'), { target: { value: 'Анна Б.' } });
  fireEvent.click(screen.getByRole('button', { name: 'Сохранить профиль' }));
  await waitFor(() => expect(client.updateStaffProfile).toHaveBeenCalledWith('b1', 's1', { organizationId: 'org', userName: 'ANN', displayName: 'Анна Б.' }));
});

it('adds a role and saves the role set', async () => {
  const { client } = setup(active);
  fireEvent.click(screen.getByRole('checkbox', { name: 'Техник' }));
  fireEvent.click(screen.getByRole('button', { name: 'Сохранить роли' }));
  await waitFor(() => expect(client.updateStaffRoles).toHaveBeenCalledWith('b1', 's1', { organizationId: 'org', roleNames: ['branch_manager', 'technician'] }));
});

it('deactivates an active operator through the confirm dialog', async () => {
  const { client } = setup(active);
  fireEvent.click(screen.getByRole('button', { name: 'Деактивировать' }));
  const dialog = await waitFor(() => screen.getByRole('dialog'));
  fireEvent.click(within(dialog).getByRole('button', { name: 'Деактивировать' }));
  await waitFor(() => expect(client.updateStaffState).toHaveBeenCalledWith('b1', 's1', { organizationId: 'org', isActive: false }));
});

it('disables deactivation for the current account (self)', () => {
  setup(active, 's1');
  expect(screen.getByRole('button', { name: 'Деактивировать' })).toBeDisabled();
});

it('resets the password when the new password meets the length requirement', async () => {
  const { client } = setup(active);
  fireEvent.click(screen.getByRole('button', { name: 'Сбросить пароль' }));
  const dialog = await waitFor(() => screen.getByRole('dialog'));
  fireEvent.change(within(dialog).getByLabelText('Новый пароль'), { target: { value: 'longenough' } });
  fireEvent.click(within(dialog).getByRole('button', { name: 'Сбросить пароль' }));
  await waitFor(() => expect(client.resetStaffPassword).toHaveBeenCalledWith('b1', 's1', { organizationId: 'org', newPassword: 'longenough' }));
});

it('rejects a too-short password without calling the API', async () => {
  const { client } = setup(active);
  fireEvent.click(screen.getByRole('button', { name: 'Сбросить пароль' }));
  const dialog = await waitFor(() => screen.getByRole('dialog'));
  fireEvent.change(within(dialog).getByLabelText('Новый пароль'), { target: { value: 'short' } });
  fireEvent.click(within(dialog).getByRole('button', { name: 'Сбросить пароль' }));
  await waitFor(() => expect(screen.getByText('Пароль должен содержать не менее 8 символов')).toBeInTheDocument());
  expect(client.resetStaffPassword).not.toHaveBeenCalled();
});

it('shows an error toast and does not call onDone when a save fails', async () => {
  const client = { ...fakeClient(), updateStaffProfile: vi.fn().mockRejectedValue(new Error('boom')) };
  const { onDone } = setup(active, 'me', client as never);
  fireEvent.click(screen.getByRole('button', { name: 'Сохранить профиль' }));
  await waitFor(() => expect(screen.getByText('Не удалось выполнить действие')).toBeInTheDocument());
  expect(onDone).not.toHaveBeenCalled();
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- OperatorDrawer`
Expected: FAIL — cannot resolve `./OperatorDrawer`.

- [ ] **Step 3: Write minimal implementation**

```tsx
// src/club/settings/OperatorDrawer.tsx
import { useState } from 'react';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { ConfirmDialog } from '@/components/shared/ConfirmDialog';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import { ASSIGNABLE_ROLES, roleLabelKey } from './roles';
import type { OperatorRow } from './settingsModel';

type Actions = Pick<ClubApiClient, 'updateStaffProfile' | 'updateStaffRoles' | 'updateStaffState' | 'resetStaffPassword'>;

export function OperatorDrawer({ operator, branchId, currentStaffUserId, client, onDone }: {
  operator: OperatorRow;
  branchId: string;
  currentStaffUserId: string;
  client: Actions;
  onDone: () => void;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [userName, setUserName] = useState(operator.userName);
  const [displayName, setDisplayName] = useState(operator.displayName);
  const [roles, setRoles] = useState<string[]>(operator.roleNames);
  const [pending, setPending] = useState(false);
  const [confirm, setConfirm] = useState<null | 'deactivate' | 'password'>(null);
  const isSelf = operator.staffUserId === currentStaffUserId;
  const org = operator.organizationId;

  async function run(action: () => Promise<unknown>) {
    setPending(true);
    try {
      await action();
      toast({ title: t('toast.saved'), variant: 'success' });
      onDone();
    } catch {
      toast({ title: t('toast.failed'), variant: 'error' });
    } finally {
      setPending(false);
      setConfirm(null);
    }
  }

  function toggleRole(role: string, checked: boolean) {
    setRoles(prev => (checked ? [...new Set([...prev, role])] : prev.filter(r => r !== role)));
  }

  return (
    <div className="flex flex-col gap-5">
      <label className="block text-sm">
        <span className="mb-1 block text-muted-foreground">{t('operators.field.userName')}</span>
        <Input aria-label={t('operators.field.userName')} value={userName} onChange={e => setUserName(e.target.value)} />
      </label>
      <label className="block text-sm">
        <span className="mb-1 block text-muted-foreground">{t('operators.field.displayName')}</span>
        <Input aria-label={t('operators.field.displayName')} value={displayName} onChange={e => setDisplayName(e.target.value)} />
      </label>
      <Button disabled={pending || userName.trim() === '' || displayName.trim() === ''}
        onClick={() => void run(() => client.updateStaffProfile(branchId, operator.staffUserId, { organizationId: org, userName: userName.trim(), displayName: displayName.trim() }))}>
        {t('operators.save.profile')}
      </Button>

      <fieldset className="flex flex-col gap-2 border-t border-border pt-4">
        <legend className="mb-1 text-sm font-medium">{t('operators.section.roles')}</legend>
        {ASSIGNABLE_ROLES.map(role => (
          <label key={role} className="flex items-center gap-2 text-sm">
            <Checkbox checked={roles.includes(role)} aria-label={t(roleLabelKey(role))}
              onCheckedChange={c => toggleRole(role, c === true)} />
            {t(roleLabelKey(role))}
          </label>
        ))}
        <Button className="mt-1" disabled={pending || roles.length === 0}
          onClick={() => void run(() => client.updateStaffRoles(branchId, operator.staffUserId, { organizationId: org, roleNames: roles }))}>
          {t('operators.save.roles')}
        </Button>
      </fieldset>

      <div className="flex flex-col gap-3 border-t border-border pt-4">
        {operator.isActive ? (
          <Button variant="destructive" disabled={pending || isSelf} onClick={() => setConfirm('deactivate')}>
            {t('operators.action.deactivate')}
          </Button>
        ) : (
          <Button variant="outline" disabled={pending}
            onClick={() => void run(() => client.updateStaffState(branchId, operator.staffUserId, { organizationId: org, isActive: true }))}>
            {t('operators.action.activate')}
          </Button>
        )}
        <Button variant="outline" disabled={pending} onClick={() => setConfirm('password')}>
          {t('operators.action.resetPassword')}
        </Button>
      </div>

      <ConfirmDialog
        open={confirm === 'deactivate'} title={t('operators.deactivate.confirm')}
        confirmLabel={t('operators.action.deactivate')} cancelLabel={t('common.cancel')}
        destructive pending={pending}
        onConfirm={() => void run(() => client.updateStaffState(branchId, operator.staffUserId, { organizationId: org, isActive: false }))}
        onOpenChange={open => { if (!open) setConfirm(null); }}
      />
      <ConfirmDialog
        open={confirm === 'password'} title={t('operators.resetPassword.confirm')}
        confirmLabel={t('operators.action.resetPassword')} cancelLabel={t('common.cancel')}
        reasonLabel={t('operators.field.newPassword')} pending={pending}
        onConfirm={value => {
          if (value.trim().length < 8) { toast({ title: t('operators.password.tooShort'), variant: 'error' }); return; }
          void run(() => client.resetStaffPassword(branchId, operator.staffUserId, { organizationId: org, newPassword: value.trim() }));
        }}
        onOpenChange={open => { if (!open) setConfirm(null); }}
      />
    </div>
  );
}
```

> NOTE: The deactivate trigger button and its confirm button share the label "Деактивировать" — the test scopes the confirm click with `within(dialog)`. Do not rename either label.

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- OperatorDrawer`
Expected: PASS (7 tests).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Web/src/club/settings/OperatorDrawer.tsx src/AFK4.Platform.Web/src/club/settings/OperatorDrawer.test.tsx
git commit -m "feat(web): add operator edit drawer (settings)"
```

---

### Task 10: CreateOperatorDialog

**Files:**
- Create: `src/AFK4.Platform.Web/src/club/settings/CreateOperatorDialog.tsx`
- Test: `src/AFK4.Platform.Web/src/club/settings/CreateOperatorDialog.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// src/club/settings/CreateOperatorDialog.test.tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { CreateOperatorDialog } from './CreateOperatorDialog';

function setup(client: { createStaff: ReturnType<typeof vi.fn> }, onDone = vi.fn(), onOpenChange = vi.fn()) {
  render(
    <I18nProvider><ToastProvider>
      <CreateOperatorDialog open branchId="b1" organizationId="org" client={client as never} onOpenChange={onOpenChange} onDone={onDone} />
    </ToastProvider></I18nProvider>
  );
  return { client, onDone, onOpenChange };
}

it('keeps submit disabled until all fields and a role are valid', () => {
  setup({ createStaff: vi.fn() });
  const submit = screen.getByRole('button', { name: 'Создать' });
  expect(submit).toBeDisabled();
  fireEvent.change(screen.getByLabelText('Логин'), { target: { value: 'newop' } });
  fireEvent.change(screen.getByLabelText('Отображаемое имя'), { target: { value: 'Новый' } });
  fireEvent.change(screen.getByLabelText('Пароль'), { target: { value: 'longenough' } });
  fireEvent.click(screen.getByRole('checkbox', { name: 'Кассир-оператор' }));
  expect(submit).toBeEnabled();
});

it('creates the operator with trimmed values and selected roles', async () => {
  const client = { createStaff: vi.fn().mockResolvedValue({}) };
  const { onDone, onOpenChange } = setup(client);
  fireEvent.change(screen.getByLabelText('Логин'), { target: { value: ' newop ' } });
  fireEvent.change(screen.getByLabelText('Отображаемое имя'), { target: { value: 'Новый' } });
  fireEvent.change(screen.getByLabelText('Пароль'), { target: { value: 'longenough' } });
  fireEvent.click(screen.getByRole('checkbox', { name: 'Кассир-оператор' }));
  fireEvent.click(screen.getByRole('button', { name: 'Создать' }));
  await waitFor(() => expect(client.createStaff).toHaveBeenCalledWith('b1', {
    organizationId: 'org', userName: 'newop', displayName: 'Новый', password: 'longenough', roleNames: ['cashier_operator']
  }));
  await waitFor(() => expect(onDone).toHaveBeenCalled());
  await waitFor(() => expect(onOpenChange).toHaveBeenCalledWith(false));
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- CreateOperatorDialog`
Expected: FAIL — cannot resolve `./CreateOperatorDialog`.

- [ ] **Step 3: Write minimal implementation**

```tsx
// src/club/settings/CreateOperatorDialog.tsx
import { useState } from 'react';
import { Dialog, DialogContent, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import { ASSIGNABLE_ROLES, roleLabelKey } from './roles';

type Actions = Pick<ClubApiClient, 'createStaff'>;

export function CreateOperatorDialog({ open, branchId, organizationId, client, onOpenChange, onDone }: {
  open: boolean;
  branchId: string;
  organizationId: string;
  client: Actions;
  onOpenChange: (open: boolean) => void;
  onDone: () => void;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [userName, setUserName] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [password, setPassword] = useState('');
  const [roles, setRoles] = useState<string[]>([]);
  const [pending, setPending] = useState(false);

  const valid = userName.trim() !== '' && displayName.trim() !== '' && password.trim().length >= 8 && roles.length > 0;

  async function submit() {
    setPending(true);
    try {
      await client.createStaff(branchId, {
        organizationId,
        userName: userName.trim(),
        displayName: displayName.trim(),
        password: password.trim(),
        roleNames: roles
      });
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
        <DialogTitle>{t('operators.create.title')}</DialogTitle>
        <div className="flex flex-col gap-3">
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('operators.field.userName')}</span>
            <Input aria-label={t('operators.field.userName')} value={userName} onChange={e => setUserName(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('operators.field.displayName')}</span>
            <Input aria-label={t('operators.field.displayName')} value={displayName} onChange={e => setDisplayName(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('operators.field.password')}</span>
            <Input type="password" aria-label={t('operators.field.password')} value={password} onChange={e => setPassword(e.target.value)} />
          </label>
          <fieldset className="flex flex-col gap-2">
            <legend className="mb-1 text-sm font-medium">{t('operators.section.roles')}</legend>
            {ASSIGNABLE_ROLES.map(role => (
              <label key={role} className="flex items-center gap-2 text-sm">
                <Checkbox checked={roles.includes(role)} aria-label={t(roleLabelKey(role))}
                  onCheckedChange={c => setRoles(prev => (c === true ? [...new Set([...prev, role])] : prev.filter(r => r !== role)))} />
                {t(roleLabelKey(role))}
              </label>
            ))}
          </fieldset>
        </div>
        <DialogFooter>
          <Button variant="outline" disabled={pending} onClick={() => onOpenChange(false)}>{t('common.cancel')}</Button>
          <Button disabled={pending || !valid} onClick={() => void submit()}>{t('operators.create.submit')}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- CreateOperatorDialog`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Web/src/club/settings/CreateOperatorDialog.tsx src/AFK4.Platform.Web/src/club/settings/CreateOperatorDialog.test.tsx
git commit -m "feat(web): add create-operator dialog (settings)"
```

---

### Task 11: SettingsScreen (tabbed shell)

**Files:**
- Create: `src/AFK4.Platform.Web/src/club/settings/SettingsScreen.tsx`
- Test: `src/AFK4.Platform.Web/src/club/settings/SettingsScreen.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// src/club/settings/SettingsScreen.test.tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { SettingsScreen } from './SettingsScreen';
import type { BranchProfile, BranchSettings, StaffUser } from '@/api/types';

const profile: BranchProfile = { organizationId: 'org', branchId: 'b1', name: 'Центр', city: 'Москва', createdAtUtc: '' };
const settings: BranchSettings = { organizationId: 'org', branchId: 'b1', requireManualDeviceApproval: false };
const staff: StaffUser[] = [
  { staffUserId: 's1', organizationId: 'org', userName: 'ANN', displayName: 'Анна', isActive: true, roleNames: ['branch_manager'], createdAtUtc: '' }
];

function fakeClient() {
  return {
    getBranchProfile: vi.fn().mockResolvedValue(profile),
    getBranchSettings: vi.fn().mockResolvedValue(settings),
    listStaff: vi.fn().mockResolvedValue(staff),
    updateBranchProfile: vi.fn(), updateBranchSettings: vi.fn(),
    updateStaffProfile: vi.fn(), updateStaffRoles: vi.fn(), updateStaffState: vi.fn(), resetStaffPassword: vi.fn(), createStaff: vi.fn()
  };
}

function setup(client = fakeClient()) {
  render(
    <I18nProvider><ToastProvider>
      <SettingsScreen client={client as never} branchId="b1" organizationId="org" currentStaffUserId="me" />
    </ToastProvider></I18nProvider>
  );
  return { client };
}

it('renders both tabs and shows the branch form by default', async () => {
  setup();
  expect(await screen.findByRole('tab', { name: 'Филиал' })).toBeInTheDocument();
  expect(screen.getByRole('tab', { name: 'Операторы и роли' })).toBeInTheDocument();
  expect(screen.getByLabelText('Название филиала')).toBeInTheDocument();
});

it('switches to the operators tab and opens the operator drawer on row click', async () => {
  setup();
  fireEvent.click(await screen.findByRole('tab', { name: 'Операторы и роли' }));
  fireEvent.click(await screen.findByText('Анна'));
  expect(await screen.findByRole('button', { name: 'Сохранить профиль' })).toBeInTheDocument();
});

it('shows the error state with retry when loading fails', async () => {
  const client = { ...fakeClient(), getBranchProfile: vi.fn().mockRejectedValue(new Error('boom')) };
  setup(client as never);
  expect(await screen.findByText('Не удалось загрузить данные.')).toBeInTheDocument();
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- SettingsScreen`
Expected: FAIL — cannot resolve `./SettingsScreen`.

- [ ] **Step 3: Write minimal implementation**

```tsx
// src/club/settings/SettingsScreen.tsx
import { useState } from 'react';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import { Sheet, SheetContent, SheetTitle } from '@/components/ui/sheet';
import { Button } from '@/components/ui/button';
import { LoadingCards, ErrorState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import { useSettings } from './useSettings';
import { BranchProfileForm } from './BranchProfileForm';
import { OperatorsTable } from './OperatorsTable';
import { OperatorDrawer } from './OperatorDrawer';
import { CreateOperatorDialog } from './CreateOperatorDialog';
import type { OperatorRow } from './settingsModel';

export function SettingsScreen({ client, branchId, organizationId, currentStaffUserId }: {
  client: ClubApiClient;
  branchId: string;
  organizationId: string;
  currentStaffUserId: string;
}) {
  const { t } = useI18n();
  const state = useSettings(client, branchId);
  const [selected, setSelected] = useState<OperatorRow | null>(null);
  const [creating, setCreating] = useState(false);

  if (state.status === 'loading') return <LoadingCards count={2} />;
  if (state.status === 'error') return <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />;

  const { profile, requireManualDeviceApproval, operators } = state.data;
  return (
    <>
      <Tabs defaultValue="branch">
        <TabsList>
          <TabsTrigger value="branch">{t('settings.tab.branch')}</TabsTrigger>
          <TabsTrigger value="operators">{t('settings.tab.operators')}</TabsTrigger>
        </TabsList>
        <TabsContent value="branch">
          <BranchProfileForm
            profile={profile}
            requireManualDeviceApproval={requireManualDeviceApproval}
            branchId={branchId}
            client={client}
            onDone={state.retry}
          />
        </TabsContent>
        <TabsContent value="operators">
          <div className="mb-3 flex justify-end">
            <Button onClick={() => setCreating(true)}>{t('operators.create.title')}</Button>
          </div>
          <OperatorsTable rows={operators} emptyMessage={t('operators.empty')} onSelect={setSelected} />
        </TabsContent>
      </Tabs>

      <Sheet open={selected !== null} onOpenChange={open => { if (!open) setSelected(null); }}>
        <SheetContent closeLabel={t('common.close')}>
          {selected && (
            <>
              <SheetTitle>{selected.displayName}</SheetTitle>
              <OperatorDrawer
                operator={selected}
                branchId={branchId}
                currentStaffUserId={currentStaffUserId}
                client={client}
                onDone={() => { setSelected(null); state.retry(); }}
              />
            </>
          )}
        </SheetContent>
      </Sheet>

      <CreateOperatorDialog
        open={creating}
        branchId={branchId}
        organizationId={organizationId}
        client={client}
        onOpenChange={setCreating}
        onDone={state.retry}
      />
    </>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- SettingsScreen`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Web/src/club/settings/SettingsScreen.tsx src/AFK4.Platform.Web/src/club/settings/SettingsScreen.test.tsx
git commit -m "feat(web): add settings screen shell"
```

---

### Task 12: i18n keys (settings / operators / roles)

**Files:**
- Modify: `src/AFK4.Platform.Web/src/i18n/messages.ts` (add keys to BOTH `ru` and `en` — parity is enforced)
- Modify: `src/AFK4.Platform.Web/src/i18n/messages.test.ts`

- [ ] **Step 1: Add a failing parity assertion**

Append a new test to `messages.test.ts`:

```ts
it('includes the new settings/operators/roles keys', () => {
  for (const key of [
    'settings.tab.branch', 'settings.tab.operators', 'settings.branch.name', 'settings.branch.city',
    'settings.branch.approval', 'settings.ownerOnly',
    'operators.col.name', 'operators.status.active', 'operators.save.profile',
    'operators.action.deactivate', 'operators.action.resetPassword', 'operators.password.tooShort',
    'operators.create.title', 'operators.create.submit',
    'roles.branch_manager', 'roles.technician', 'roles.unknown'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- messages`
Expected: FAIL — the new keys are missing (and the parity test will also catch any imbalance).

- [ ] **Step 3: Add the keys**

In `messages.ts`, inside the `ru` object (before the closing `},` of `ru`), add:

```ts
    'settings.tab.branch': 'Филиал',
    'settings.tab.operators': 'Операторы и роли',
    'settings.branch.name': 'Название филиала',
    'settings.branch.city': 'Город',
    'settings.branch.approval': 'Ручное подтверждение устройств',
    'settings.branch.approval.hint': 'Новые устройства требуют подтверждения перед подключением.',
    'settings.ownerOnly': 'Раздел доступен только владельцу.',
    'operators.col.name': 'Сотрудник',
    'operators.col.roles': 'Роли',
    'operators.col.status': 'Статус',
    'operators.status.active': 'Активен',
    'operators.status.inactive': 'Отключён',
    'operators.empty': 'Сотрудники не добавлены.',
    'operators.field.userName': 'Логин',
    'operators.field.displayName': 'Отображаемое имя',
    'operators.field.password': 'Пароль',
    'operators.field.newPassword': 'Новый пароль',
    'operators.section.roles': 'Роли',
    'operators.save.profile': 'Сохранить профиль',
    'operators.save.roles': 'Сохранить роли',
    'operators.action.deactivate': 'Деактивировать',
    'operators.action.activate': 'Активировать',
    'operators.action.resetPassword': 'Сбросить пароль',
    'operators.deactivate.confirm': 'Деактивировать сотрудника? Сессии будут завершены.',
    'operators.resetPassword.confirm': 'Сбросить пароль сотрудника?',
    'operators.password.tooShort': 'Пароль должен содержать не менее 8 символов',
    'operators.create.title': 'Добавить сотрудника',
    'operators.create.submit': 'Создать',
    'roles.owner': 'Владелец',
    'roles.branch_manager': 'Управляющий',
    'roles.shift_supervisor': 'Старший смены',
    'roles.cashier_operator': 'Кассир-оператор',
    'roles.technician': 'Техник',
    'roles.accountant_auditor': 'Бухгалтер',
    'roles.unknown': 'Роль',
```

In the `en` object (before its closing `}`), add the matching keys:

```ts
    'settings.tab.branch': 'Branch',
    'settings.tab.operators': 'Operators & roles',
    'settings.branch.name': 'Branch name',
    'settings.branch.city': 'City',
    'settings.branch.approval': 'Manual device approval',
    'settings.branch.approval.hint': 'New devices require approval before they can connect.',
    'settings.ownerOnly': 'This section is available to owners only.',
    'operators.col.name': 'Staff member',
    'operators.col.roles': 'Roles',
    'operators.col.status': 'Status',
    'operators.status.active': 'Active',
    'operators.status.inactive': 'Disabled',
    'operators.empty': 'No staff members yet.',
    'operators.field.userName': 'Username',
    'operators.field.displayName': 'Display name',
    'operators.field.password': 'Password',
    'operators.field.newPassword': 'New password',
    'operators.section.roles': 'Roles',
    'operators.save.profile': 'Save profile',
    'operators.save.roles': 'Save roles',
    'operators.action.deactivate': 'Deactivate',
    'operators.action.activate': 'Activate',
    'operators.action.resetPassword': 'Reset password',
    'operators.deactivate.confirm': 'Deactivate this staff member? Their sessions will end.',
    'operators.resetPassword.confirm': 'Reset this staff member\'s password?',
    'operators.password.tooShort': 'Password must be at least 8 characters',
    'operators.create.title': 'Add staff member',
    'operators.create.submit': 'Create',
    'roles.owner': 'Owner',
    'roles.branch_manager': 'Branch manager',
    'roles.shift_supervisor': 'Shift supervisor',
    'roles.cashier_operator': 'Cashier / operator',
    'roles.technician': 'Technician',
    'roles.accountant_auditor': 'Accountant',
    'roles.unknown': 'Role',
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- messages`
Expected: PASS (parity + new-keys assertions green).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Web/src/i18n/messages.ts src/AFK4.Platform.Web/src/i18n/messages.test.ts
git commit -m "feat(web): add settings/operators/roles i18n keys"
```

---

### Task 13: Enable the nav item

**Files:**
- Modify: `src/AFK4.Platform.Web/src/club/nav.ts` (the `settings` item, ~line 20)
- Test: `src/AFK4.Platform.Web/src/club/nav.test.ts`

- [ ] **Step 1: Add a failing test**

Append to `nav.test.ts`:

```ts
it('exposes settings as a live owner-only branch item', () => {
  const settings = clubNav[0].items.find(i => i.key === 'settings');
  expect(settings).toBeDefined();
  expect(settings?.soon).toBe(false);
  expect(settings?.ownerOnly).toBe(true);
  expect(settings?.path).toBe('/club/settings');
});
```

> NOTE: `clubNav` is already imported in `nav.test.ts`. If it is not, add `import { clubNav } from './nav';` at the top.

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- nav`
Expected: FAIL — `soon` is still `true`.

- [ ] **Step 3: Make the change**

In `nav.ts`, change the `settings` item to:

```ts
      { key: 'settings', labelKey: 'nav.settings', path: '/club/settings', ownerOnly: true, soon: false }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- nav`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Web/src/club/nav.ts src/AFK4.Platform.Web/src/club/nav.test.ts
git commit -m "feat(web): enable settings nav item"
```

---

### Task 14: Route + owner-gated render in App.tsx

**Files:**
- Modify: `src/AFK4.Platform.Web/src/App.tsx`
- Test: `src/AFK4.Platform.Web/src/App.settings.test.tsx`

This wires `/club/settings` to a new `clubSettings` route and renders `SettingsScreen` only for owners (managers get a localized owner-only notice). Routing has no role gating — gating is render-side (consistent with the spec's "enforced in routing and render": routing keeps the route reachable, render enforces the role).

- [ ] **Step 1: Write the failing test**

```tsx
// src/App.settings.test.tsx
import { it, expect } from 'vitest';
import { resolvePlatformRoute, pathForRoute } from './App';

it('resolves /club/settings to the clubSettings route', () => {
  const { route } = resolvePlatformRoute('/club/settings', null, '', 'club');
  expect(route).toEqual({ kind: 'clubSettings' });
});

it('maps the clubSettings route back to /club/settings', () => {
  expect(pathForRoute({ kind: 'clubSettings' })).toBe('/club/settings');
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- App.settings`
Expected: FAIL — route resolves to `notFound`; `pathForRoute` returns `/club` for the unknown kind / type error.

- [ ] **Step 3: Make the edits in App.tsx**

(a) Add the import (with the other club-screen imports, ~line 13):

```ts
import { SettingsScreen } from './club/settings/SettingsScreen';
```

(b) Add to the `ClubRoute` union (the block starting ~line 34) — add `clubSettings` after `clubVenue`:

```ts
  | { kind: 'clubVenue' }
  | { kind: 'clubSettings' }
```

(c) Add to `CLUB_SCREEN_TITLE` (~line 294):

```ts
  clubSettings: 'Настройки',
```

(d) Add to `pathForRoute` (~line 311), after the `clubVenue` case:

```ts
    case 'clubSettings':
      return '/club/settings';
```

(e) Add to `isClubRoute` (~line 577), in the `||` chain:

```ts
    || route.kind === 'clubSettings'
```

(f) Add to `resolvePlatformRoute` inside the `allowsClubRoutes(audience)` block (after the `/club/venue` check, ~line 448):

```ts
    if (path === '/club/settings') {
      return { route: { kind: 'clubSettings' } };
    }
```

(g) In `ClubArea`, add the i18n hook and EmptyState import, then the render branch. At the top of the component body (~line 333) add:

```ts
  const { t } = useI18n();
```

Add these imports near the other UI imports (top of file):

```ts
import { EmptyState } from './components/ui/states';
import { useI18n } from './i18n/I18nProvider';
```

Then in the render chain, add a branch for `clubSettings` between the `clubVenue` and the `LegacyClubScreen` fallback:

```tsx
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

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- App.settings`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Web/src/App.tsx src/AFK4.Platform.Web/src/App.settings.test.tsx
git commit -m "feat(web): route and owner-gate the settings screen"
```

---

### Task 15: Full suite + build gate

**Files:** none (verification only).

- [ ] **Step 1: Run the full test suite**

Run (from `src/AFK4.Platform.Web`): `npm test`
Expected: all test files pass (the ~31 existing files plus the new ones from this plan).

- [ ] **Step 2: Run the build gate**

Run: `npm run build`
Expected: `tsc -b` and `vite build` both succeed with no type errors. (If `roles.*` key type errors surfaced earlier, Task 12 has resolved them.)

- [ ] **Step 3: Commit any incidental fixes**

If the build surfaced a type issue, fix it minimally and commit:

```bash
git add -A
git commit -m "fix(web): resolve build issues in settings screen"
```

If nothing needed fixing, there is no commit for this task.

---

## Self-Review (completed during planning)

**Spec coverage:** Настройки → Филиал (profile + approval toggle) = Tasks 5–7, 11; Настройки → Операторы и роли (list, create, role/profile/state/password-reset) = Tasks 3, 4, 8–11; owner-only gating in routing+render = Tasks 13, 14; money/destructive safety pattern (confirm + server-confirmed toast) = Tasks 7, 9; data-region states (loading/empty/error-retry) = Tasks 6, 8, 11; i18n RU/EN parity = Task 12; primitives added per-need (Switch, Checkbox) = Tasks 1, 2. No-new-backend-contracts respected (Task 3 wraps existing routes).

**Placeholder scan:** none — every code step contains full content.

**Type consistency:** `OperatorRow`/`BranchProfileView`/`SettingsViewModel` defined in Task 5 and consumed unchanged in Tasks 6–11; client method names (`updateStaffRoles`, `updateStaffProfile`, `updateStaffState`, `resetStaffPassword`) defined in Task 3 and used verbatim in Task 9; `roleLabelKey`/`ASSIGNABLE_ROLES` defined in Task 4 and used in Tasks 8–10; message keys added in Task 12 match every `t('…')` call across Tasks 4, 7–11, 14.

## Out of Scope (subsequent plans, per the design spec)

- **Real branch switching + "Все филиалы" aggregated dashboard + branch CRUD** — the next plan. They are coherent together (enumerate branches, name them, switch context, navigate in); branch switching is deferred here because the pilot is single-branch and switching between unnamed branches in isolation would be a half-feature.
- **Карта зала floor-map editor** (ETag optimistic concurrency) — its own plan.
- **Deleting the orphaned `ClubDashboard`** — only once the floor-map and branches legacy screens are also redesigned (the `LegacyClubScreen` fallback is still used by `clubBranch*` routes after this plan).
- Монетизация, Клиенты/CRM, Отчёты, Профиль — later plans.
