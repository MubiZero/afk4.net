# Operator «Управление» Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebuild the operator `Управление` workspace as a single left-nav settings center with eight destinations on one shared, well-styled screen/form scaffold — removing the overview landing, audit events, stock movements, update publishing, and the raw unstyled forms.

**Architecture:** Introduce one `management` workspace behind the existing rail section «Управление». `ManagementWorkspace` owns a left secondary nav (`managementNav`), loads the settings-domain data once (as `BackendSettingsWorkspace` does today), and renders each destination inside a shared `ManagementScreen` scaffold (header + panels + sticky save bar + states). Five destinations reuse the existing `settings/Settings*Section` components; three (`Оплата`/`Лояльность`/`Новости`) reuse the existing standalone workspaces, restyled onto the scaffold. The old top tab-strip, the internal settings-nav, the readiness/quick-actions side panel, the integrations section, and the logs tab are removed from `Управление`.

**Tech Stack:** React + TypeScript, Vite, `@afk4/i18n` (ICU MessageFormat, ru/en/tg), lucide-react icons, `bun test` + `@testing-library/react` + happy-dom, project CSS custom properties in `src/styles/`.

## Global Constraints

- Base: a **fresh branch off `main`** (e.g. `feat/operator-management-redesign`). Do **not** inherit the codex `management/` module from `feat/operator-reports-workspace-consolidation`.
- Money values render neutral/white, never amber; amber is warnings only. (Source: operator tokens; see `[[operator-redesign-phase0-decisions]]`.)
- Currency formatting keeps the established minor-units → major-on-UI-boundary contract; use the existing `@afk4/money`/`formatMoney` helpers, no local formatters.
- All i18n copy goes through `@afk4/i18n`; every new `MessageKey` must exist in ru **and** en **and** tg (enforced by `packages/i18n/src/messages.test.ts`). Tajik must be real Tajik, never a copy of ru — flag any key you cannot translate confidently rather than duplicating ru.
- Feedback uses the shared `useFeedbackToasts(feedback)` hook; no local success/error banners.
- Never render a raw branch UUID in the UI; show a human branch name.
- Verification gate per slice: `cd src/AFK4.Operator.App.Web && bun test` green **and** `bun run build` green (`tsc -b` typechecks test files and narrowings too — a green `bun test` is not a green build). (Source: `[[frontends-on-bun-test]]`.)
- Design execution note: exact spacing, interaction states, contrast, and final markup for every visual task are produced during that task via the **interface-limb** skill, grounded in the existing `src/styles/15-settings.css` tokens and operator surface-elevation rules (light theme = raised white panel + `--shadow-card`; no shadow on nested card-in-card, inputs, modals). This plan fixes structure, contracts, and behavior; interface-limb fixes pixels.

---

## File Structure

New module `src/AFK4.Operator.App.Web/src/management/`:

- `managementNav.ts` — destination model + permission-derived visibility. One responsibility: what destinations exist and who may see them.
- `ManagementScreen.tsx` — shared scaffold: header (title + subtitle), body slot, optional sticky save bar, loading/error/empty helpers. Pure presentation.
- `useUnsavedGuard.ts` — hook returning a guarded navigation function that intercepts destination switches while dirty.
- `ManagementWorkspace.tsx` — section shell: left nav + settings-domain data load + destination routing. Composition root.
- `destinations/ClubDestination.tsx`, `HallsDevicesDestination.tsx`, `TariffsPackagesDestination.tsx`, `StaffRolesDestination.tsx`, `GoodsDestination.tsx`, `PaymentDestination.tsx`, `LoyaltyDestination.tsx`, `NewsDestination.tsx` — thin wrappers that adapt an existing component to the scaffold. Each has one responsibility: present one destination.
- Tests colocated as `*.test.ts(x)` beside each file.

Reused as-is or adapted (existing files):

- `settings/SettingsProfileSection.tsx`, `SettingsLayoutSection.tsx`, `SettingsTariffsSection.tsx`, `SettingsStaffSection.tsx`, `SettingsGoodsSection.tsx` — reused; `SettingsGoodsSection` loses its stock-movement block.
- `LoyaltySettingsWorkspace.tsx`, `NewsWorkspace.tsx`, `PaymentGatewaysWorkspace.tsx` — reused, restyled onto the scaffold.
- `settings/SettingsIntegrationsSection.tsx` and `BackendSettingsWorkspace.tsx` — deleted in slice 3.
- `BackendLogsWorkspace.tsx` — kept, unhooked from `Управление` (future own rail section, backlog).

Modified shell/wiring:

- `operatorTypes.ts` — `WorkspaceId` union.
- `operatorData.ts` — `navSections` admin section.
- `WorkspaceRouter.tsx` — route `management`, drop absorbed routes.
- `operatorVisibility.ts` + `operatorVisibility.test.ts` — `canOpenWorkspace` for `management`.
- `packages/i18n/src/messages.ts` — new `op.management.*` keys (ru/en/tg).
- `src/styles/15-settings.css` (+ maybe a new `src/styles/1x-management.css`) — scaffold styles.

---

## SLICE 1 — Scaffold + navigation

Delivers a working `Управление` with the eight-item left nav, the shared scaffold, permission visibility, the unsaved guard, and three simplest destinations (`Клуб`, `Лояльность`, `Новости`) migrated as proof.

### Task 1.1: Destination model (`managementNav`)

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/management/managementNav.ts`
- Test: `src/AFK4.Operator.App.Web/src/management/managementNav.test.ts`

**Interfaces:**
- Consumes: `permissionNames`, `hasPermission` from `../operatorPermissions`; `OperatorAuthSession` from `../authClient`; `MessageKey` from `@afk4/i18n`; `LucideIcon` from `lucide-react`.
- Produces:
  ```ts
  export type ManagementDestinationId =
    | 'club' | 'halls' | 'tariffs' | 'staff' | 'goods'
    | 'payment' | 'loyalty' | 'news';
  export interface ManagementDestination {
    id: ManagementDestinationId;
    labelKey: MessageKey;
    subtitleKey: MessageKey;
    Icon: LucideIcon;
    permissions: readonly string[]; // visible if the session has ANY of these
  }
  export const managementDestinations: readonly ManagementDestination[];
  export function allowedManagementDestinations(
    session: OperatorAuthSession | null
  ): ManagementDestination[];
  ```

- [ ] **Step 1: Write the failing test**

```ts
// managementNav.test.ts
import { describe, it, expect } from 'bun:test';
import { allowedManagementDestinations, managementDestinations } from './managementNav';
import { permissionNames } from '../operatorPermissions';

const sessionWith = (perms: string[]) => ({ permissions: perms }) as never;

describe('managementNav', () => {
  it('lists exactly the eight destinations in order', () => {
    expect(managementDestinations.map((d) => d.id)).toEqual([
      'club', 'halls', 'tariffs', 'staff', 'goods', 'payment', 'loyalty', 'news'
    ]);
  });

  it('hides destinations the session has no permission for', () => {
    const only = allowedManagementDestinations(sessionWith([permissionNames.manageLoyaltySettings]));
    expect(only.map((d) => d.id)).toEqual(['loyalty']);
  });

  it('returns nothing for a null session', () => {
    expect(allowedManagementDestinations(null)).toEqual([]);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd src/AFK4.Operator.App.Web && bun test src/management/managementNav.test.ts`
Expected: FAIL — module not found.

- [ ] **Step 3: Write minimal implementation**

VERIFIED against `operatorPermissions.ts` (do not re-guess these):
- `permissionNames` currently has NO `manageBranchSettings`. The backend permission string is `branches.settings.manage` (`StaffPermissionNames.ManageBranchSettings`, granted to `branch_manager`/`technician`), and it gates `updateBranchProfile`. **Add** `manageBranchSettings: 'branches.settings.manage'` to `permissionNames` in this task (it belongs with the club profile).
- `hasAnyPermission(session, perms)` and `hasPermission` already exist in `operatorPermissions.ts` — reuse them; do NOT write a local copy.

Map each destination (all names exist in `permissionNames` after adding the one above):
- `club` → `[permissionNames.manageBranchSettings]`
- `halls` → `[permissionNames.manageLayout, permissionNames.viewDeviceDetail, permissionNames.viewDeviceCommandStatus]` (visible if any; matches what `SettingsLayoutSection` needs)
- `tariffs` → `[permissionNames.manageTariffs, permissionNames.managePackages]`
- `staff` → `[permissionNames.manageBranchStaff, permissionNames.manageRoles]`
- `goods` → `[permissionNames.managePosCatalog, permissionNames.manageInventoryStock]`
- `payment` → `[permissionNames.managePaymentGateways]`
- `loyalty` → `[permissionNames.manageLoyaltySettings]`
- `news` → `[permissionNames.manageNews]`
Icons (lucide-react): `Building2, MonitorCog, BadgeDollarSign, UsersRound, Boxes, CreditCard, Gift, Newspaper`. `allowedManagementDestinations` filters with the existing `hasAnyPermission`.

Use these `labelKey`/`subtitleKey` values (added in Task 1.2): `op.management.dest.<id>` and `op.management.dest.<id>.subtitle`.

- [ ] **Step 4: Run test to verify it passes**

Run: `cd src/AFK4.Operator.App.Web && bun test src/management/managementNav.test.ts`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/management/managementNav.ts src/AFK4.Operator.App.Web/src/management/managementNav.test.ts
git commit -m "feat(operator): management destination model + permission visibility"
```

### Task 1.2: i18n keys `op.management.*`

**Files:**
- Modify: `packages/i18n/src/messages.ts` (ru, en, tg blocks)
- Test: `packages/i18n/src/messages.test.ts` (existing parity test must stay green)

**Interfaces:**
- Produces: `MessageKey`s used by every management file: eight `op.management.dest.<id>` labels, eight `op.management.dest.<id>.subtitle`, and:
  - `op.management.save.clean` ("изменений нет"), `op.management.save.saved` ("сохранено")
  - `op.management.unsaved.title`, `op.management.unsaved.body`, `op.management.unsaved.confirm`, `op.management.unsaved.cancel`
  - `op.management.noAccess` (section-level "нет доступных разделов")

- [ ] **Step 1: Add the keys to all three locales**

Add to the ru, en, and tg objects. ru values (labels): Клуб / Залы и ПК / Тарифы и пакеты / Сотрудники и роли / Товары / Оплата / Лояльность / Новости. Subtitles (ru): «Профиль филиала», «Залы, зоны и рабочие места», «Тарифы и пакеты времени», «Сотрудники, роли и доступ», «Каталог товаров», «Приём онлайн-оплаты», «Правила кэшбэка», «Публикации для клиентов». Provide real English and real Tajik for each — do not copy ru into tg.

- [ ] **Step 2: Run the parity test**

Run: `cd packages/i18n && bun test src/messages.test.ts`
Expected: PASS (all keys present in ru/en/tg; no `tg === ru` violation).

- [ ] **Step 3: Commit**

```bash
git add packages/i18n/src/messages.ts
git commit -m "i18n(operator): add op.management.* keys (ru/en/tg)"
```

### Task 1.3: Shared scaffold `ManagementScreen`

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/management/ManagementScreen.tsx`
- Test: `src/AFK4.Operator.App.Web/src/management/ManagementScreen.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/styles/15-settings.css` (add `.management-*` scaffold rules; reuse existing `--surface-*`/`--shadow-card`/`--text-*` tokens)

**Interfaces:**
- Produces:
  ```ts
  export type SaveState = 'clean' | 'dirty' | 'saving' | 'saved';
  export interface ManagementScreenProps {
    title: string;
    subtitle: string;
    children: React.ReactNode;   // destination body (panels/forms)
    save?: {                     // omit for read-only destinations
      state: SaveState;
      onSave: () => void;
      disabled?: boolean;        // e.g. no backend / no permission
    };
  }
  export function ManagementScreen(props: ManagementScreenProps): JSX.Element;
  ```
  Scaffold structure: `<section className="workspace-screen management-screen">` → header (`management-screen-head`: subtitle-eyebrow + `<h1>`) → `<div className="management-screen-body">{children}</div>` → when `save` present, a sticky `<div className="management-save-bar">` with a status span (`clean`→`op.management.save.clean`, `saved`→`op.management.save.saved`) and a `<button>` (`common.save`) that is `disabled` when `state==='clean' || state==='saving' || save.disabled`.

- [ ] **Step 1: Write the failing test**

```tsx
// ManagementScreen.test.tsx
import { describe, it, expect } from 'bun:test';
import { render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ManagementScreen } from './ManagementScreen';

const renderScreen = (ui: React.ReactNode) =>
  render(<I18nProvider initialLocale="ru">{ui}</I18nProvider>);

describe('ManagementScreen', () => {
  it('renders title, subtitle and body', () => {
    renderScreen(
      <ManagementScreen title="Клуб" subtitle="Профиль филиала"><p>тело</p></ManagementScreen>
    );
    expect(screen.getByRole('heading', { name: 'Клуб' })).toBeTruthy();
    expect(screen.getByText('Профиль филиала')).toBeTruthy();
    expect(screen.getByText('тело')).toBeTruthy();
  });

  it('disables save while clean and enables it while dirty', () => {
    const { rerender } = renderScreen(
      <ManagementScreen title="t" subtitle="s" save={{ state: 'clean', onSave: () => {} }}><i/></ManagementScreen>
    );
    expect(screen.getByRole('button', { name: 'Сохранить' }).hasAttribute('disabled')).toBe(true);
    rerender(
      <I18nProvider initialLocale="ru">
        <ManagementScreen title="t" subtitle="s" save={{ state: 'dirty', onSave: () => {} }}><i/></ManagementScreen>
      </I18nProvider>
    );
    expect(screen.getByRole('button', { name: 'Сохранить' }).hasAttribute('disabled')).toBe(false);
  });

  it('omits the save bar when no save prop is given', () => {
    renderScreen(<ManagementScreen title="t" subtitle="s"><i/></ManagementScreen>);
    expect(screen.queryByRole('button', { name: 'Сохранить' })).toBeNull();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd src/AFK4.Operator.App.Web && bun test src/management/ManagementScreen.test.tsx`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement `ManagementScreen.tsx` and its CSS**

Build the component per the structure above (use `useI18n` for `common.save`, `op.management.save.*`). Add scaffold CSS to `15-settings.css`: `.management-screen` (column layout filling the workspace height like `.settings-screen`), `.management-screen-head`, `.management-screen-body` (scroll region), `.management-save-bar` (sticky bottom, right-aligned, status span + button). Use interface-limb here for spacing/contrast/sticky behavior.

- [ ] **Step 4: Run test to verify it passes**

Run: `cd src/AFK4.Operator.App.Web && bun test src/management/ManagementScreen.test.tsx`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/management/ManagementScreen.tsx src/AFK4.Operator.App.Web/src/management/ManagementScreen.test.tsx src/AFK4.Operator.App.Web/src/styles/15-settings.css
git commit -m "feat(operator): shared ManagementScreen scaffold + save bar"
```

### Task 1.4: Unsaved-changes guard hook

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/management/useUnsavedGuard.ts`
- Test: `src/AFK4.Operator.App.Web/src/management/useUnsavedGuard.test.ts`

**Interfaces:**
- Produces:
  ```ts
  export interface UnsavedGuard {
    pendingTarget: ManagementDestinationId | null; // set when a switch is blocked
    requestNavigate: (target: ManagementDestinationId) => void;
    confirm: () => void;   // proceed to pendingTarget, clear dirty
    cancel: () => void;    // stay
  }
  export function useUnsavedGuard(args: {
    isDirty: boolean;
    onNavigate: (target: ManagementDestinationId) => void;
    onDiscard: () => void; // clear the dirty state on the leaving destination
  }): UnsavedGuard;
  ```
  Behavior: `requestNavigate(target)` — if `!isDirty`, calls `onNavigate(target)` immediately; if dirty, stores `pendingTarget` (does not navigate). `confirm()` calls `onDiscard()` then `onNavigate(pendingTarget)` and clears it. `cancel()` clears `pendingTarget`.

- [ ] **Step 1: Write the failing test** (render a tiny probe component with `renderHook` from `@testing-library/react`, or a wrapper component; assert the three flows: clean → immediate navigate; dirty → blocked + pendingTarget set; confirm → navigate + discard; cancel → cleared).

```ts
// useUnsavedGuard.test.ts
import { describe, it, expect, mock } from 'bun:test';
import { renderHook, act } from '@testing-library/react';
import { useUnsavedGuard } from './useUnsavedGuard';

describe('useUnsavedGuard', () => {
  it('navigates immediately when not dirty', () => {
    const onNavigate = mock(() => {});
    const { result } = renderHook(() => useUnsavedGuard({ isDirty: false, onNavigate, onDiscard: () => {} }));
    act(() => result.current.requestNavigate('news'));
    expect(onNavigate).toHaveBeenCalledWith('news');
    expect(result.current.pendingTarget).toBeNull();
  });

  it('blocks and stores the target when dirty, then confirm proceeds', () => {
    const onNavigate = mock(() => {});
    const onDiscard = mock(() => {});
    const { result } = renderHook(() => useUnsavedGuard({ isDirty: true, onNavigate, onDiscard }));
    act(() => result.current.requestNavigate('loyalty'));
    expect(onNavigate).not.toHaveBeenCalled();
    expect(result.current.pendingTarget).toBe('loyalty');
    act(() => result.current.confirm());
    expect(onDiscard).toHaveBeenCalledTimes(1);
    expect(onNavigate).toHaveBeenCalledWith('loyalty');
    expect(result.current.pendingTarget).toBeNull();
  });

  it('cancel clears the pending target without navigating', () => {
    const onNavigate = mock(() => {});
    const { result } = renderHook(() => useUnsavedGuard({ isDirty: true, onNavigate, onDiscard: () => {} }));
    act(() => result.current.requestNavigate('club'));
    act(() => result.current.cancel());
    expect(result.current.pendingTarget).toBeNull();
    expect(onNavigate).not.toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd src/AFK4.Operator.App.Web && bun test src/management/useUnsavedGuard.test.ts`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement `useUnsavedGuard.ts`** (a `useState<ManagementDestinationId | null>` plus the three callbacks as described).

- [ ] **Step 4: Run test to verify it passes**

Run: `cd src/AFK4.Operator.App.Web && bun test src/management/useUnsavedGuard.test.ts`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/management/useUnsavedGuard.ts src/AFK4.Operator.App.Web/src/management/useUnsavedGuard.test.ts
git commit -m "feat(operator): management unsaved-changes guard hook"
```

### Task 1.5: `management` workspace id + rail wiring

**VERIFIED locations (the earlier `operatorVisibility.ts` reference was wrong):**
`WorkspaceId` is in `operatorTypes.ts`. The array `workspaceIds`, the total record `workspacePermissionRules: Record<WorkspaceId, readonly string[]>`, `canOpenWorkspace`, `firstAllowedWorkspace`, `hasAnyPermission` all live in `operatorPermissions.ts`. The role→sections/workspaces contract test is `operatorVisibility.test.ts` (it maps each staff role to `expectedSections` (rail keys, includes `'admin'`) and to expected workspaces).

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/operatorTypes.ts` — `WorkspaceId`: add `'management'`; remove `'settings'`, `'payment_cards'`, `'loyalty'`, `'news'` (keep `'logs'`, `'dashboard'`).
- Modify: `src/AFK4.Operator.App.Web/src/operatorPermissions.ts` — `workspaceIds` array (same add/remove); `workspacePermissionRules` (add `management`, remove the four); the record is total over `WorkspaceId`, so it will not typecheck until updated.
- Modify: `src/AFK4.Operator.App.Web/src/operatorData.ts` — admin `NavSection` → single `management` item.
- Test: `src/AFK4.Operator.App.Web/src/operatorVisibility.test.ts` — update the contract: replace the four removed workspaces with `management` in each role's expected workspace set; `expectedSections` keeps `'admin'` (rail key unchanged).

**Interfaces:**
- Produces: `management` rule in `workspacePermissionRules` = the union of the eight destinations' permission arrays from `managementNav` (import `managementDestinations` and flatten, or inline the same union). `canOpenWorkspace(session, 'management')` is then `hasAnyPermission(session, rule)`, which is equivalent to `allowedManagementDestinations(session).length > 0`.

- [ ] **Step 1: Write the failing test** — extend `operatorVisibility.test.ts` (use its existing session helper; check the file for the exact helper name — do not invent `sessionWith`):

```ts
it('opens management for a role with any management permission', () => {
  expect(canOpenWorkspace(sessionForRole('branch_manager'), 'management')).toBe(true);
});
it('hides management for a role with no management permission', () => {
  expect(canOpenWorkspace(sessionForRole('cashier_operator'), 'management')).toBe(false);
});
```
Also update the role→workspaces expectation table so `branch_manager` (and any other management-capable role) expects `management` instead of `settings`/`payment_cards`/`loyalty`/`news`.

- [ ] **Step 2: Run test to verify it fails**

Run: `cd src/AFK4.Operator.App.Web && bun test src/operatorVisibility.test.ts`
Expected: FAIL — `'management'` not assignable / rule missing / contract mismatch.

- [ ] **Step 3: Implement** the four file changes above. In `operatorData.ts` replace the admin section's five items with `items: [{ id: 'management', labelKey: 'op.shell.navGroup.management' }]` (keep `key:'admin'`, `icon: Settings`). Then grep the whole operator src for the removed ids — `grep -rn "'settings'\|'payment_cards'\|'loyalty'\|'news'" src` — and fix every remaining reference (e.g. `App.test.tsx` navigates using `loyalty.settings.manage`; it must target `'management'` now). WorkspaceRouter routing for these is handled in Task 1.6.

- [ ] **Step 4: Run test to verify it passes**

Run: `cd src/AFK4.Operator.App.Web && bun test src/operatorVisibility.test.ts && bun run build`
Expected: PASS + build green (the total-record and union types must line up).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/operatorTypes.ts src/AFK4.Operator.App.Web/src/operatorPermissions.ts src/AFK4.Operator.App.Web/src/operatorData.ts src/AFK4.Operator.App.Web/src/operatorVisibility.test.ts
git commit -m "feat(operator): add management workspace id + rail visibility"
```

### Task 1.6: `ManagementWorkspace` shell (left nav + data + routing) with three destinations

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/management/ManagementWorkspace.tsx`
- Create: `src/AFK4.Operator.App.Web/src/management/destinations/ClubDestination.tsx`
- Create: `src/AFK4.Operator.App.Web/src/management/destinations/LoyaltyDestination.tsx`
- Create: `src/AFK4.Operator.App.Web/src/management/destinations/NewsDestination.tsx`
- Test: `src/AFK4.Operator.App.Web/src/management/ManagementWorkspace.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/WorkspaceRouter.tsx` (add `management`, remove `settings`/`payment_cards`/`loyalty`/`news` cases — the standalone components are now rendered by destinations)
- Modify: `src/AFK4.Operator.App.Web/src/styles/15-settings.css` (`.management-layout`, `.management-nav` rules)

**Interfaces:**
- Consumes: `managementDestinations`, `allowedManagementDestinations` (1.1); `ManagementScreen` (1.3); `useUnsavedGuard` (1.4). Props: `{ backend: OperatorBackendContext | null; session: OperatorAuthSession | null; currencyCode: string }`.
- Produces: `export function ManagementWorkspace(props)`. Renders `<main className="workspace-screen management-screen">`? No — the screen wrapper is per-destination via `ManagementScreen`. `ManagementWorkspace` renders `<div className="management-layout">` = `<nav className="management-nav">` (buttons from `allowedManagementDestinations`, active state, `onClick=requestNavigate`) + the active destination component. Persist/restore the active destination in component state, defaulting to the first allowed. Render the unsaved-guard inline confirm (a small dialog/section using `op.management.unsaved.*`) when `pendingTarget !== null`.

- Destination wrappers for slice 1:
  - `ClubDestination` — renders `ManagementScreen` (title `op.management.dest.club`, subtitle `op.management.dest.club.subtitle`, `save`) wrapping `SettingsProfileSection`. Owns `clubName`/`city`/dirty/save exactly as `BackendSettingsWorkspace.saveSettings` does today (lift that logic here). Reports dirtiness up to `ManagementWorkspace` via an `onDirtyChange` callback prop so the guard sees it.
  - `LoyaltyDestination` — renders `ManagementScreen` (no `save` bar of its own if `LoyaltySettingsWorkspace` keeps its own save; otherwise lift save into the bar) wrapping `LoyaltySettingsWorkspace`. Slice 1 keeps its existing internal save; the restyle to the scaffold save-bar happens in Task 2.x if needed. Minimum here: title/subtitle + body.
  - `NewsDestination` — renders `ManagementScreen` (title/subtitle) wrapping `NewsWorkspace`.
  - Each wrapper exposes: `{ backend, session, currencyCode, onDirtyChange?: (dirty: boolean) => void }`.

  Define a shared wrapper prop type in `managementNav.ts` or a new `destinations/types.ts`:
  ```ts
  export interface DestinationProps {
    backend: OperatorBackendContext | null;
    session: OperatorAuthSession | null;
    currencyCode: string;
    onDirtyChange?: (dirty: boolean) => void;
  }
  ```

- [ ] **Step 1: Write the failing test**

```tsx
// ManagementWorkspace.test.tsx
import { describe, it, expect } from 'bun:test';
import { render, screen, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../operatorToast';
import { ManagementWorkspace } from './ManagementWorkspace';
import { permissionNames } from '../operatorPermissions';

const wrap = (ui: React.ReactNode) =>
  render(<I18nProvider initialLocale="ru"><ToastProvider>{ui}</ToastProvider></I18nProvider>);

const session = (perms: string[]) => ({ permissions: perms, organizationId: 'o1', displayName: 'x' }) as never;

describe('ManagementWorkspace', () => {
  it('renders only the destinations the session may see', () => {
    wrap(<ManagementWorkspace backend={null} session={session([permissionNames.manageNews, permissionNames.manageLoyaltySettings])} currencyCode="TJS" />);
    expect(screen.getByRole('button', { name: 'Новости' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Лояльность' })).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Клуб' })).toBeNull();
  });

  it('shows a no-access message when nothing is permitted', () => {
    wrap(<ManagementWorkspace backend={null} session={session([])} currencyCode="TJS" />);
    expect(screen.getByText('Нет доступных разделов')).toBeTruthy(); // op.management.noAccess ru value
  });

  it('switches the active destination on nav click', () => {
    wrap(<ManagementWorkspace backend={null} session={session([permissionNames.manageBranchSettings, permissionNames.manageNews])} currencyCode="TJS" />);
    fireEvent.click(screen.getByRole('button', { name: 'Новости' }));
    // News screen head renders its subtitle
    expect(screen.getByRole('heading', { name: 'Новости' })).toBeTruthy();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd src/AFK4.Operator.App.Web && bun test src/management/ManagementWorkspace.test.tsx`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement** `ManagementWorkspace.tsx`, the three destination wrappers, and `destinations/types.ts`. Wire `management` in `WorkspaceRouter.tsx`:
  ```tsx
  {workspace === 'management' && (
    <ManagementWorkspace backend={backend} session={session} currencyCode={currencyCode} />
  )}
  ```
  Remove the `settings`, `payment_cards`, `loyalty`, `news` cases and their now-unused imports (`BackendSettingsWorkspace`, `PaymentGatewaysWorkspace`, `LoyaltySettingsWorkspace`, `NewsWorkspace` move to being imported by destinations instead). Keep `logs`/`dashboard` cases. Add `.management-layout`/`.management-nav` CSS (left nav column + content) via interface-limb.

- [ ] **Step 4: Run tests + build**

Run: `cd src/AFK4.Operator.App.Web && bun test src/management/ && bun run build`
Expected: PASS + build green.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/management src/AFK4.Operator.App.Web/src/WorkspaceRouter.tsx src/AFK4.Operator.App.Web/src/styles/15-settings.css
git commit -m "feat(operator): ManagementWorkspace shell + Клуб/Лояльность/Новости destinations"
```

### Task 1.7: Restyle `NewsWorkspace` and `LoyaltySettingsWorkspace` onto the scaffold form grid

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/NewsWorkspace.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/LoyaltySettingsWorkspace.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/styles/15-settings.css` (reuse `.settings-form-grid`/`.management-*`)
- Test: existing `NewsWorkspace.test.tsx` / `LoyaltySettingsWorkspace.test.tsx` if present must stay green; else add a minimal render test asserting labels are associated with inputs.

**Rationale:** These two screens are the raw glued forms from the screenshots (no dedicated CSS today). This task removes the ad-hoc/native layout and puts fields in the shared `.settings-form-grid` with labels above inputs, normal widths, date fields styled, and an honest empty state.

- [ ] **Step 1:** Add/confirm a render test that fails on the current raw layout in a checkable way — e.g. assert each input has an associated `<label>` via accessible name:

```tsx
it('associates every field with a label', () => {
  // render NewsWorkspace with a fake client returning [] items and [] branches
  expect(screen.getByLabelText('Заголовок')).toBeTruthy();
  expect(screen.getByLabelText('Текст')).toBeTruthy();
});
```
Run it; if labels are not associated (current markup glues text next to inputs without `htmlFor`/wrapping), it FAILS.

- [ ] **Step 2:** Restyle both screens: wrap the body in `.settings-form-grid` (or a management form grid), put each control in a `<label>` with the caption above the input, give `datetime-local`/number inputs normal widths, keep the existing state/handlers and client calls unchanged. Use interface-limb for spacing and the empty-state treatment. Money/percent stays neutral.

- [ ] **Step 3:** Run the file's tests + build.

Run: `cd src/AFK4.Operator.App.Web && bun test src/NewsWorkspace.test.tsx src/LoyaltySettingsWorkspace.test.tsx && bun run build`
Expected: PASS + green build.

- [ ] **Step 4: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/NewsWorkspace.tsx src/AFK4.Operator.App.Web/src/LoyaltySettingsWorkspace.tsx src/AFK4.Operator.App.Web/src/styles/15-settings.css
git commit -m "fix(operator): restyle Новости and Лояльность onto the management form grid"
```

### Task 1.8: Slice 1 manual QA gate

- [ ] **Step 1:** Launch the operator preview per `[[setup-wizard-preview-launch]]` / `operator-wpf-preview` (or `bun run dev` → open the URL from `[[afk4-preview-means-give-link]]`). Sign in as a management-capable role.
- [ ] **Step 2:** Verify: rail «Управление» opens a left nav of the permitted destinations, lands on the first; no top tab-strip; no «Обзор»; no «События»; `Клуб`/`Лояльность`/`Новости` render on the scaffold with labels above fields and no glued rows; switching with unsaved edits on `Клуб` triggers the inline guard; save disabled until dirty; no console errors; check dark and light.
- [ ] **Step 3:** Record the result in the PR description; if anything fails, fix before proceeding.

---

## SLICE 2 — Remaining destinations on the scaffold

Delivers `Залы и ПК`, `Тарифы и пакеты`, `Сотрудники и роли`, `Товары`, `Оплата` on the shared scaffold, with correct lists, money color, and states. Each task adds one destination wrapper + routes it in `ManagementWorkspace` + adapts its underlying component. `ManagementWorkspace` still owns the shared settings-domain data load (lift `loadSettings` from `BackendSettingsWorkspace`, minus integrations/diagnostics-for-readiness) and passes slices to the settings destinations.

### Task 2.1: Lift settings-domain data load into `ManagementWorkspace`

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/management/ManagementWorkspace.tsx`
- Test: `src/AFK4.Operator.App.Web/src/management/ManagementWorkspace.test.tsx`

**Interfaces:**
- Produces: `ManagementWorkspace` loads (via `createAuthenticatedOperatorClients`) exactly the data the retained settings destinations need: `getBranchProfile`, `getStaffUsers`, `getLayoutZones`, `pos.getCatalog`, `getTariffOptions`, `getPackageOptions`, and the device lists (perm-gated) used by `SettingsLayoutSection`. Do **not** load `diagnostics`/`rollouts`/`updatePackages` (integrations/readiness removed). Exposes the loaded slices + `loadStatus` + `reload` to destination wrappers via props.

- [ ] **Step 1:** Extend the test with a module-mock of `../operatorHelpers.createAuthenticatedOperatorClients` (per the catalogue test pattern) returning stub settings methods; assert that mounting with a non-null backend calls `getBranchProfile`/`getLayoutZones`/`getTariffOptions`/`getStaffUsers`/`pos.getCatalog` once each and does **not** call `diagnostics.getDiagnostics`/`updates.getRolloutStatuses`.
- [ ] **Step 2:** Run — FAIL (no load yet).
- [ ] **Step 3:** Implement the load (copy the relevant half of `BackendSettingsWorkspace.loadSettings`, drop integrations/diagnostics). Keep `loadStatus: LoadStatus` and `feedback` + `useFeedbackToasts` here.
- [ ] **Step 4:** Run tests + build — PASS/green.
- [ ] **Step 5:** Commit `feat(operator): management data load (settings slices, no integrations)`.

### Task 2.2: `HallsDevicesDestination` (Залы и ПК)

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/management/destinations/HallsDevicesDestination.tsx`
- Modify: `ManagementWorkspace.tsx` (route `halls`)
- Test: `HallsDevicesDestination.test.tsx`

- [ ] **Step 1:** Test: render inside `ManagementScreen`, assert title «Залы и ПК», subtitle present, and that `SettingsLayoutSection` receives the `zones`/device props (mock the section to a probe, or assert a known zone name renders). Assert money-bearing rows (if any) carry no amber class.
- [ ] **Step 2:** Run — FAIL.
- [ ] **Step 3:** Implement the wrapper: `ManagementScreen` (title/subtitle `op.management.dest.halls*`) wrapping `SettingsLayoutSection` with the props it needs from `ManagementWorkspace` state (`zones, deviceInventory, branchDeviceCommandHistory, backend, can*` flags, `onDeviceInventoryChange`, `onBranchDeviceCommandHistoryChange`, `onReload`, `onFeedback`). No save bar (section saves per-action). Route `halls` in `ManagementWorkspace`.
- [ ] **Step 4:** Run tests + build — PASS/green.
- [ ] **Step 5:** Commit `feat(operator): Залы и ПК destination`.

### Task 2.3: `TariffsPackagesDestination` (Тарифы и пакеты) — money color fix

**Files:**
- Create: `destinations/TariffsPackagesDestination.tsx`
- Modify: `ManagementWorkspace.tsx` (route `tariffs`); `settings/SettingsTariffsSection.tsx` (money color); `styles/15-settings.css` if an amber money class exists on tariff/package rows
- Test: `TariffsPackagesDestination.test.tsx`

- [ ] **Step 1:** Test: assert the tariff/package price text renders and its element does **not** carry the warning/amber class (e.g. `expect(priceEl.className).not.toContain('amber')` / whatever the current class is — inspect `SettingsTariffsSection` + CSS first and assert against the real class name). Assert title «Тарифы и пакеты».
- [ ] **Step 2:** Run — FAIL.
- [ ] **Step 3:** Implement wrapper (reuse `SettingsTariffsSection`, no save bar). Change the tariff/package price rendering to a neutral money class. Route `tariffs`.
- [ ] **Step 4:** Run tests + build — PASS/green.
- [ ] **Step 5:** Commit `feat(operator): Тарифы и пакеты destination; money neutral not amber`.

### Task 2.4: `StaffRolesDestination` (Сотрудники и роли)

**Files:**
- Create: `destinations/StaffRolesDestination.tsx`
- Modify: `ManagementWorkspace.tsx` (route `staff`)
- Test: `StaffRolesDestination.test.tsx`

- [ ] **Step 1:** Test: title «Сотрудники и роли», subtitle present, `SettingsStaffSection` receives `staffUsers` + perms; assert a staff row renders.
- [ ] **Step 2:** Run — FAIL.
- [ ] **Step 3:** Implement wrapper reusing `SettingsStaffSection` (props from `ManagementWorkspace`: `staffUsers, backend, canManageBranchStaff, canManageRoles, onStaffUsersChange, onFeedback`). No save bar. Route `staff`.
- [ ] **Step 4:** Run tests + build — PASS/green.
- [ ] **Step 5:** Commit `feat(operator): Сотрудники и роли destination`.

### Task 2.5: `GoodsDestination` (Товары) — drop stock movement

**Files:**
- Create: `destinations/GoodsDestination.tsx`
- Modify: `ManagementWorkspace.tsx` (route `goods`); `settings/SettingsGoodsSection.tsx` (remove the stock-movement block + its `inventory.createStockMovement` call + `settings-stock-form` markup and now-unused state/props)
- Test: `GoodsDestination.test.tsx`; update `settings/SettingsGoodsSection` tests if they cover stock movement

- [ ] **Step 1:** Test: render `GoodsDestination`, assert title «Товары», the catalog renders (a product name), and there is **no** «Записать движение» control (`expect(screen.queryByRole('button', { name: 'Записать движение' })).toBeNull()`).
- [ ] **Step 2:** Run — FAIL (control still present).
- [ ] **Step 3:** Implement wrapper (reuse `SettingsGoodsSection`). Remove the stock-movement section from `SettingsGoodsSection` (state: `stockReason`, movement type/qty/cost; the `inventory.createStockMovement` handler; the `settings-stock-form` markup and the `recordMovement` action). Keep catalog + barcodes. Update `canManageInventoryStock` usage: it now only gates barcode management (verify) — leave that path intact. Route `goods`.
- [ ] **Step 4:** Run tests + build — PASS/green. Confirm the stock-movement capability still exists under `Склад` (it already does — receiving/adjustment); this only removes the duplicate.
- [ ] **Step 5:** Commit `feat(operator): Товары destination; move stock movement out to Склад`.

### Task 2.6: `PaymentDestination` (Оплата)

**Files:**
- Create: `destinations/PaymentDestination.tsx`
- Modify: `ManagementWorkspace.tsx` (route `payment`); `PaymentGatewaysWorkspace.tsx` (drop its own `<main className="workspace-screen">`/`screen-head` wrapper so it nests cleanly inside `ManagementScreen`; keep all logic/CSS classes for the card rows)
- Test: `PaymentDestination.test.tsx`

- [ ] **Step 1:** Test: render `PaymentDestination` with a fake gateway backend (or mock the client), assert title «Оплата», subtitle present, and the provision form + empty state render; assert the detailed provider/session info is shown (not a generic label) — i.e. concrete `payment-card-*` content when a gateway exists.
- [ ] **Step 2:** Run — FAIL.
- [ ] **Step 3:** Implement wrapper: `ManagementScreen` (title/subtitle `op.management.dest.payment*`) wrapping `PaymentGatewaysWorkspace`. Remove `PaymentGatewaysWorkspace`'s own screen wrapper/head so the scaffold owns the header; the body becomes the provision + list sections. `PaymentGatewaysWorkspace` builds its own client from `backend`; pass `backend` through. Route `payment`.
- [ ] **Step 4:** Run tests + build — PASS/green.
- [ ] **Step 5:** Commit `feat(operator): Оплата destination on scaffold`.

### Task 2.7: States pass (loading skeleton / concrete error / empty) across destinations

**Files:**
- Modify: destination wrappers + `ManagementScreen.tsx` (add optional `state?: 'loading' | 'error' | 'ready'` + `errorDetail?: string` handling, or expose skeleton/error/empty helpers)
- Test: `ManagementScreen.test.tsx` (loading shows skeleton region, error shows the concrete detail with a retry affordance)

- [ ] **Step 1:** Test: `ManagementScreen` with `state='loading'` renders a skeleton container (assert by role/testid, layout preserved); with `state='error'` and `errorDetail='boom'` renders the concrete text `boom` and a retry button that calls `onRetry`.
- [ ] **Step 2:** Run — FAIL.
- [ ] **Step 3:** Implement loading/error/empty in `ManagementScreen` (deferred spinner 150–300ms via CSS/`prefers-reduced-motion`, skeleton preserves geometry, error uses `projectOperatorError` detail already threaded via `feedback`; empty states use honest copy). Wire `ManagementWorkspace.loadStatus` into destination `state`. Use interface-limb for skeleton geometry.
- [ ] **Step 4:** Run tests + build — PASS/green.
- [ ] **Step 5:** Commit `feat(operator): management loading/error/empty states`.

### Task 2.8: Slice 2 manual QA gate

- [ ] Preview all eight destinations at 1920/1440/1280 + narrow, dark and light: forms in grid with labels above, money neutral, no glued rows, no `Настройки загружены` pill, no stock-movement in Товары, concrete errors, skeletons on load, honest empty states, branch shown by name never UUID. Record in PR. Fix regressions before slice 3.

---

## SLICE 3 — Cleanup + follow-ups

Removes the dead old structure and files the backlog items. No behavior regressions.

### Task 3.1: Delete `BackendSettingsWorkspace` and `SettingsIntegrationsSection`

**Files:**
- Delete: `src/AFK4.Operator.App.Web/src/BackendSettingsWorkspace.tsx`
- Delete: `src/AFK4.Operator.App.Web/src/settings/SettingsIntegrationsSection.tsx` (+ its test if any)
- Modify: any remaining imports/tests referencing them
- Grep gate: `grep -rn "BackendSettingsWorkspace\|SettingsIntegrationsSection\|settings-nav-panel\|settings-side-panel\|settings-readiness\|settings-action-card\|op.settings.readiness\|op.settings.quickActions" src/AFK4.Operator.App.Web/src` returns nothing (except deletions).

- [ ] **Step 1:** Delete the two files. Remove the now-orphaned readiness/quick-actions/integrations CSS from `15-settings.css` and the orphaned `op.settings.readiness.*`/`op.settings.quickActions.*`/`op.settings.section.integrations*`/update-package i18n keys from all three locales (only those no longer referenced — grep first).
- [ ] **Step 2:** Run: the grep gate above (empty), then `cd src/AFK4.Operator.App.Web && bun test && bun run build`. Also `cd packages/i18n && bun test`.
Expected: all green; no dangling references.
- [ ] **Step 3:** Commit `refactor(operator): remove old settings workspace, integrations, readiness/quick-actions`.

### Task 3.2: Unhook `logs`/«События» from Управление

**Files:**
- Modify: `WorkspaceRouter.tsx` if the `logs` route was only reachable via admin — keep the `BackendLogsWorkspace` component and its route intact, but confirm nothing in `navSections` points to `logs` anymore (it was removed in Task 1.5).
- No deletion of `BackendLogsWorkspace`.

- [ ] **Step 1:** Confirm via grep that `logs` is not referenced by `navSections`/rail. Leave the component + router case for the future dedicated rail section.
- [ ] **Step 2:** Run `bun test && bun run build` — green.
- [ ] **Step 3:** Commit `refactor(operator): unhook audit logs from Управление (own section pending)`.

### Task 3.3: File backlog items

**Files:**
- Modify: memory index + a memory file (per the repo memory convention) OR the team backlog doc — record two follow-ups so nothing is silently dropped:
  1. **«События» as its own rail section** — promote `BackendLogsWorkspace` to a top-level rail entry (audit ≠ configuration). Include the four category filters from the reports spec if adopted.
  2. **Operator update-publishing → owner web** — the removed `SettingsIntegrationsSection` update-package/rollout publishing must be rebuilt in the platform/owner web surface, not the club operator. Note the API clients involved (`updates.*`).
- Also verify whether the deleted integrations panel carried a still-needed live toggle (e.g. a payment-confirmation mode); if so, open a specific follow-up to surface it under `Оплата`.

- [ ] **Step 1:** Write the backlog entries.
- [ ] **Step 2:** Commit `docs(operator): backlog — События rail section, update-publishing to owner web`.

### Task 3.4: Slice 3 full-suite gate + PR

- [ ] Run the complete operator suite and build plus i18n parity: `cd src/AFK4.Operator.App.Web && bun test && bun run build && cd ../../packages/i18n && bun test`. All green.
- [ ] Final preview pass (dark + light) confirming the acceptance criteria from the spec. Open the PR with the QA notes from all three slices. Auto-merge after green CI per `[[afk4-auto-merge-authorized]]`.

---

## Self-Review

**Spec coverage:**
- Left nav, 8 items, no third level, lands on first → Tasks 1.1, 1.5, 1.6. ✅
- Remove Обзор → nothing adds it (fresh off main; note in 1.6 QA). ✅
- Remove События from Управление → Tasks 1.5, 3.2. ✅
- Movements → Склад → Task 2.5. ✅
- Remove update-publishing → Tasks 3.1, 3.3. ✅
- Shared scaffold / labels above / grid / one save bar / kill "Настройки загружены" pill → Tasks 1.3, 1.7, 2.x, 3.1. ✅
- Money neutral not amber → Task 2.3 (+ QA gates). ✅
- Feedback via toasts → carried in ManagementWorkspace (2.1). ✅
- Loading/error/empty → Task 2.7. ✅
- Permissions/visibility, no disabled promises → Tasks 1.1, 1.5, 1.6. ✅
- Branch by name never UUID → 1.6/1.7 (Club) + QA gates. ✅
- Testing/acceptance → per-task tests + QA gates 1.8/2.8/3.4. ✅
- Delivery slices 1/2/3 → mirrored. ✅

**Placeholder scan:** No "TBD"/"handle edge cases"/"write tests for the above". Visual-only specifics are explicitly delegated to interface-limb per the Global Constraints design note, with concrete structural assertions in tests instead of fabricated final markup — this is deliberate, not a placeholder.

**Type consistency:** `ManagementDestinationId` (1.1) used by `useUnsavedGuard` (1.4), `DestinationProps` (1.6) used by all wrappers; `SaveState`/`ManagementScreenProps` (1.3) referenced by wrappers; `WorkspaceId` gains `'management'` (1.5) used by `WorkspaceRouter`/`operatorData`/`operatorVisibility`. Permission names must be confirmed against `operatorPermissions.ts` in Task 1.1 (e.g. `manageBranchSettings`, `manageNews`, `manageLoyaltySettings`, `managePaymentGateways`) — the plan flags this verification explicitly.

## Execution Handoff

(filled in after save)
