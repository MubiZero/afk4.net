# Operator Post-Auth Shift Gate Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Block shift-capable staff at an authoritative open-shift form immediately after sign-in or native session restore, while hiding permanently unauthorized navigation sections and allowing non-shift staff through.

**Architecture:** Add a permission-based `usePostAuthShiftGate` state machine in front of the regular Operator shell and a dedicated non-dismissible `PostAuthShiftGate` presentation component. The hook reads and opens shifts through the existing typed client, handles stale responses and concurrent opens, and exposes a small controller API; `App` passes a gated auth status to operational hooks so no floor-map/realtime data starts before the check succeeds.

**Tech Stack:** React 19, TypeScript 6, Bun 1.3.14, Testing Library, existing AFK4 typed Operator API clients and i18n package.

## Global Constraints

- Eligibility must use `shifts.open`, never hardcoded role names.
- The current-shift API response is authoritative; no cached/dashboard shift projection may release the gate.
- The gate must run after interactive sign-in and native session restore.
- Required staff may only open a shift or sign out; no close, escape, backdrop, palette, hotkey, or navigation bypass.
- Staff without `shifts.open` must skip the gate.
- Existing backend contracts, idempotency behavior, authorization, and database schema remain unchanged.
- Permanently unauthorized rail sections are hidden; backend permission checks remain the security boundary.
- Follow RED-GREEN-REFACTOR and observe each new test fail before production edits.

---

### Task 1: Hide Permanently Unauthorized Rail Sections

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/WorkspaceRail.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/WorkspaceRail.tsx`

**Interfaces:**
- Consumes: `canOpenWorkspace(session, workspaceId): boolean` and `navSections`.
- Produces: `WorkspaceRail` renders only sections containing at least one permitted workspace.

- [ ] **Step 1: Write the failing navigation tests**

Create a localized component test that renders `WorkspaceRail` inside `I18nProvider` with a minimal session. Assert that a floor-map-only employee sees the floor section and does not see Cash, Reports, or Management; assert that a session with one management permission still sees Management.

```tsx
it('hides sections with no permitted workspace', () => {
  renderRail(['floor_map.view']);
  expect(screen.getByTitle('Зал')).toBeInTheDocument();
  expect(screen.queryByTitle('Касса')).not.toBeInTheDocument();
  expect(screen.queryByTitle('Отчёты')).not.toBeInTheDocument();
  expect(screen.queryByTitle('Управление')).not.toBeInTheDocument();
});

it('keeps a section when one nested workspace is permitted', () => {
  renderRail(['diagnostics.view']);
  expect(screen.getByTitle('Управление')).toBeInTheDocument();
});
```

- [ ] **Step 2: Run RED**

Run: `bun test src/WorkspaceRail.test.tsx`

Expected: the first test fails because locked sections are still rendered.

- [ ] **Step 3: Filter the rail before mapping**

Derive visible sections once during render and remove locked-button rendering:

```tsx
const visibleSections = navSections.filter((section) =>
  section.items.some((item) => canOpenWorkspace(session, item.id))
);

return (
  <nav className="workspace-rail" aria-label={t('op.shell.workspaces')}>
    {visibleSections.map((section) => {
      const Icon = section.icon;
      const label = t(section.labelKey);
      return (
        <button
          key={section.key}
          type="button"
          className={section.key === activeSectionKey ? 'active' : ''}
          title={label}
          onClick={() => onNavigateSection(section)}
        >
          <Icon size={20} />
          <span>{label}</span>
        </button>
      );
    })}
```

- [ ] **Step 4: Run GREEN**

Run: `bun test src/WorkspaceRail.test.tsx`

Expected: 2 passed, 0 failed.

- [ ] **Step 5: Commit the navigation unit**

```bash
git add src/AFK4.Operator.App.Web/src/WorkspaceRail.tsx \
  src/AFK4.Operator.App.Web/src/WorkspaceRail.test.tsx
git commit -m "feat(operator): hide unauthorized rail sections"
```

---

### Task 2: Implement the Permission-Based Shift Gate Controller

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/usePostAuthShiftGate.ts`
- Create: `src/AFK4.Operator.App.Web/src/usePostAuthShiftGate.test.tsx`

**Interfaces:**
- Consumes: `AuthStatus`, `OperatorBackendContext | null`, translation function, existing `OpenShiftRequest`, `createAuthenticatedOperatorClients`, `hasPermission`, and `permissionNames.openShift`.
- Produces:

```ts
export type PostAuthShiftGateStatus =
  | 'not-required' | 'checking' | 'required' | 'opening' | 'failed' | 'ready';

export interface PostAuthShiftGateController {
  status: PostAuthShiftGateStatus;
  error: string | null;
  retry(): void;
  openShift(request: OpenShiftRequest): Promise<void>;
}

export interface PostAuthShiftClient {
  getCurrentShift(branchId: string): Promise<unknown | null>;
  openShift(branchId: string, request: OpenShiftRequest): Promise<unknown>;
}
```

- [ ] **Step 1: Write failing controller tests**

Use `renderHook`, deferred promises, and an injected `PostAuthShiftClient`. Cover these exact behaviors:

```tsx
it('skips staff without shifts.open', async () => {
  const { result } = renderGate({ permissions: ['floor_map.view'] });
  expect(result.current.status).toBe('not-required');
  expect(client.getCurrentShift).not.toHaveBeenCalled();
});

it('exposes checking synchronously and requires an empty shift', async () => {
  client.getCurrentShift.mockResolvedValue(null);
  const { result } = renderGate({ permissions: ['shifts.open', 'shifts.view'] });
  expect(result.current.status).toBe('checking');
  await waitFor(() => expect(result.current.status).toBe('required'));
});

it('releases an existing shift', async () => {
  client.getCurrentShift.mockResolvedValue({ shiftId: 'shift-1' });
  const { result } = renderGate({ permissions: ['shifts.open', 'shifts.view'] });
  await waitFor(() => expect(result.current.status).toBe('ready'));
});

it('retries a failed check and ignores a stale branch response', async () => {
  // First request rejects; retry succeeds. Then rerender to branch B while the
  // branch A promise is pending and prove A cannot overwrite branch B state.
});

it('opens idempotently and reconciles a concurrent open', async () => {
  // Direct success becomes ready. For rejected open, return an existing shift
  // from getCurrentShift and assert ready rather than failed.
});
```

- [ ] **Step 2: Run RED**

Run: `bun test src/usePostAuthShiftGate.test.tsx`

Expected: FAIL because `usePostAuthShiftGate` does not exist.

- [ ] **Step 3: Implement the minimal state machine**

Use a request key based on `staffUserId` and `branchId`; expose `checking`
synchronously whenever the eligible key differs from the last resolved key.
Guard every async completion with an incrementing request generation stored in
a ref. The effect reads `getCurrentShift`; `retry()` increments a nonce;
`openShift()` sets `opening`, calls the existing open endpoint, and on failure
rechecks current shift before projecting the original error.

```ts
const eligible = authStatus === 'signed-in'
  && backend !== null
  && hasPermission(backend.session, permissionNames.openShift);
const gateKey = eligible
  ? `${backend.session.staffUserId}:${backend.branchId}`
  : null;
const exposedStatus = gateKey === null
  ? 'not-required'
  : snapshot.key === gateKey
    ? snapshot.status
    : 'checking';
```

The default client is created only inside active check/open paths; tests inject
the narrow client interface. `retry()` is a stable callback. An invalidated
request must never update status or error.

- [ ] **Step 4: Run GREEN and type-check the unit**

Run:

```bash
bun test src/usePostAuthShiftGate.test.tsx
bunx tsc -b --pretty false
```

Expected: controller tests pass and TypeScript reports no errors.

- [ ] **Step 5: Commit the controller**

```bash
git add src/AFK4.Operator.App.Web/src/usePostAuthShiftGate.ts \
  src/AFK4.Operator.App.Web/src/usePostAuthShiftGate.test.tsx
git commit -m "feat(operator): add post-auth shift gate state"
```

---

### Task 3: Build the Non-Dismissible Shift Gate Screen

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/PostAuthShiftGate.tsx`
- Create: `src/AFK4.Operator.App.Web/src/PostAuthShiftGate.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/styles/03-auth.css`
- Modify: `packages/i18n/src/messages.ts`

**Interfaces:**
- Consumes: `PostAuthShiftGateController`, currency code, organization id, `parseNonNegativeMoneyInputMinorUnits`, `createIdempotencyKey`, `AuthFrame`.
- Produces:

```ts
export function PostAuthShiftGate(props: {
  controller: PostAuthShiftGateController;
  organizationId: string;
  currencyCode: string;
  onSignOut: () => void;
}): JSX.Element;
```

- [ ] **Step 1: Write failing presentation tests**

Cover loading, required form, invalid negative cash, pending duplicate-submit
protection, failed check retry, and sign-out. The required-state assertion is:

```tsx
expect(screen.getByRole('heading', { name: 'Откройте смену' })).toBeInTheDocument();
expect(screen.queryByRole('button', { name: /закрыть/i })).not.toBeInTheDocument();
fireEvent.change(screen.getByLabelText('Старт наличных'), { target: { value: '100.50' } });
fireEvent.click(screen.getByRole('button', { name: 'Открыть смену' }));
expect(openShift).toHaveBeenCalledWith(expect.objectContaining({
  organizationId: ORG_ID,
  startingCash: { currencyCode: 'TJS', minorUnits: 10050 },
  openingNote: 'Утренняя смена'
}));
```

- [ ] **Step 2: Run RED**

Run: `bun test src/PostAuthShiftGate.test.tsx`

Expected: FAIL because the component does not exist.

- [ ] **Step 3: Implement the screen and localized copy**

Render inside `AuthFrame` and reuse existing auth panel/form primitives. Add
Russian, English, and Tajik values for these keys:

```text
op.shiftGate.title = Откройте смену / Open the shift / Навбатро кушоед
op.shiftGate.subtitle = До начала работы укажите наличные в кассе. / Enter the cash in the till before starting work. / Пеш аз оғози кор нақди кассаро ворид кунед.
op.shiftGate.checking = Проверяем текущую смену… / Checking the current shift… / Навбати ҷорӣ санҷида мешавад…
op.shiftGate.retry = Проверить снова / Check again / Аз нав санҷидан
op.shiftGate.signOut = Выйти из аккаунта / Sign out / Баромадан аз аккаунт
```

Use the existing cash field labels/default note/submit translations. Preserve
form values across open failures, disable fields for `opening`, and show
`controller.error` in `role="alert"`. The component has no close handler and no
document-level Escape listener.

- [ ] **Step 4: Run GREEN and the i18n tests**

Run:

```bash
bun test src/PostAuthShiftGate.test.tsx
bun test ../../packages/i18n/src/messages.test.ts
```

Expected: all focused tests pass with no missing-key failures.

- [ ] **Step 5: Commit the screen**

```bash
git add src/AFK4.Operator.App.Web/src/PostAuthShiftGate.tsx \
  src/AFK4.Operator.App.Web/src/PostAuthShiftGate.test.tsx \
  src/AFK4.Operator.App.Web/src/styles/03-auth.css \
  packages/i18n/src/messages.ts
git commit -m "feat(operator): require shift opening after auth"
```

---

### Task 4: Integrate the Gate at the Root App Boundary

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/App.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/App.test.tsx`

**Interfaces:**
- Consumes: `usePostAuthShiftGate`, `PostAuthShiftGate`, `handleSignOut`, `backendContext`.
- Produces: normal Operator shell mounts only for `ready` or `not-required`.

- [ ] **Step 1: Write failing App integration tests**

Add tests for both native restore and interactive sign-in with `/shifts/current`
returning a 404/empty optional response. Assert the required heading is visible,
the workspace navigation and floor map are absent, opening the shift releases
the app, and a floor-map-only technician session skips the call and enters its
workspace. Also assert unauthorized rail sections are absent in the existing
partial-permission test.

```tsx
expect(await screen.findByRole('heading', { name: 'Откройте смену' })).toBeInTheDocument();
expect(screen.queryByRole('navigation', { name: 'Рабочие места' })).not.toBeInTheDocument();
expect(screen.queryByLabelText('ПК зала')).not.toBeInTheDocument();

fireEvent.change(screen.getByLabelText('Старт наличных'), { target: { value: '100' } });
fireEvent.click(screen.getByRole('button', { name: 'Открыть смену' }));

expect(await screen.findByRole('navigation', { name: 'Рабочие места' })).toBeInTheDocument();
expect(await screen.findByLabelText('ПК зала')).toBeInTheDocument();
```

- [ ] **Step 2: Run RED**

Run only the new named tests:

```bash
bun test src/App.test.tsx --test-name-pattern "requires an open shift|skips the shift gate"
```

Expected: the app renders the floor map before the required form exists.

- [ ] **Step 3: Wire the gate before operational data flows**

Call `usePostAuthShiftGate` after `backendContext` is derived. Compute:

```ts
const workspaceAllowed = shiftGate.status === 'ready'
  || shiftGate.status === 'not-required';
const operationalAuthStatus = workspaceAllowed ? authStatus : 'checking';
```

Pass `operationalAuthStatus` to `useFloorMap`, `useOperatorRealtime`,
`useShellData`, and `usePlayersPreload`. After the signed-out return and before
calculating/rendering shell navigation, return:

```tsx
if (!workspaceAllowed) {
  return (
    <PostAuthShiftGate
      controller={shiftGate}
      organizationId={authSession.organizationId}
      currencyCode={config.currencyCode}
      onSignOut={handleSignOut}
    />
  );
}
```

This preserves hook order, prevents operational fetch/realtime startup during
the gate, and avoids a one-frame shell flash because eligible unresolved keys
expose `checking` synchronously.

- [ ] **Step 4: Run GREEN and regression tests**

Run:

```bash
bun test src/App.test.tsx --test-name-pattern "requires an open shift|skips the shift gate"
bun test src/App.test.tsx
```

Expected: new tests pass, then all App tests pass.

- [ ] **Step 5: Commit root integration**

```bash
git add src/AFK4.Operator.App.Web/src/App.tsx \
  src/AFK4.Operator.App.Web/src/App.test.tsx
git commit -m "feat(operator): gate workspaces on shift readiness"
```

---

### Task 5: Full Verification And Durable Status

**Files:**
- Modify: `docs/progress/2026-05-12-vertical-slice-progress.md`
- Modify: `docs/superpowers/plans/2026-07-15-operator-post-auth-shift-gate.md`

**Interfaces:**
- Consumes: all prior task outputs.
- Produces: verified implementation state and an archived/completed plan at publication time.

- [ ] **Step 1: Run the complete Operator Web verification**

Run:

```bash
cd src/AFK4.Operator.App.Web
bun test $(find src -name '*.test.ts' -o -name '*.test.tsx' | grep -v App.test)
bun test src/App.test.tsx
bun run build
```

Expected: all tests pass and Vite production build completes.

- [ ] **Step 2: Run repository hygiene checks**

Run from the repository root:

```bash
git diff --check
git status --short
git diff --stat origin/main...HEAD
```

Expected: no whitespace errors and only intended feature/docs files differ.

- [ ] **Step 3: Update durable progress**

Record the permission-based post-auth gate, hidden unauthorized rail sections,
fresh test count, production build, and any known environment limitation in the
compact current-state section. Do not copy full logs into progress.

- [ ] **Step 4: Mark the plan complete and commit docs**

Check every completed step, move the plan to
`docs/archive/superpowers/plans/2026-07-15-operator-post-auth-shift-gate.md`, then run:

```bash
git add docs/progress/2026-05-12-vertical-slice-progress.md \
  docs/archive/superpowers/plans/2026-07-15-operator-post-auth-shift-gate.md
git diff --cached --check
git commit -m "docs(operator): record post-auth shift gate"
```

- [ ] **Step 5: Self-review and publication gate**

Inspect `git diff origin/main...HEAD`, confirm no role-name authorization was
introduced, confirm all required actions remain backend-confirmed, and use the
repository's PR/CI workflow. Do not merge until latest-head required checks are
green.
