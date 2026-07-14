# Operator App QA Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the Operator App’s session-start CTA accessible and its browser preview reliable for local QA.

**Architecture:** Production transport and SignalR remain intact. Browser preview selects a memory-only realtime client and stateful fixtures through its existing config; `devMockFetch` stays the single API interception point. CSS corrections remain component-scoped.

**Tech Stack:** React 19, TypeScript, Bun test, Vite, SignalR client, CSS custom properties, Playwright + axe.

## Global Constraints

- Do not modify Platform API endpoints, shared contracts, idempotency, money rules, or native WebView2 bridge contracts.
- Preview-only code is selected by `shellMode === 'vite-dev-preview'`; live preview and packaged runtime retain real SignalR.
- Add a failing test before each behaviour change.
- Do not commit browser binaries, screenshots, temporary scripts, or `node_modules`.

---

### Task 1: Restore primary CTA priority and audited contrast

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/styles/06-map-grid.css:827-864`
- Modify: `src/AFK4.Operator.App.Web/src/styles/11-pos.css:610-615`
- Modify: `src/AFK4.Operator.App.Web/src/styles/12-players.css:308-311` and the `.client-row-detail` declaration
- Modify: `src/AFK4.Operator.App.Web/src/styles/02-shell.css`, `04-rail.css`, `06-map-grid.css`, `07-map-sidepanel.css`
- Modify: `src/AFK4.Operator.App.Web/src/MapSidePanel.test.tsx`
- Create: `src/AFK4.Operator.App.Web/src/styles/qaContrast.test.ts`

**Interfaces:**
- Consumes: existing `.cta-primary`, `.critical-confirmation-actions`, and `@afk4/tokens` values.
- Produces: AA-safe audited selectors and a visibly primary, enabled launch control.

- [ ] **Step 1: Write the failing tests**

Add a ready-seat test that opens the start dialog and proves the launch control is enabled and has its explicit primary class:

```tsx
const start = screen.getByRole('button', { name: 'Старт · открытый счёт' });
expect(start).toBeEnabled();
expect(start).toHaveClass('cta-primary');
```

Create `qaContrast.test.ts`, based on `packages/tokens/tokens.test.ts`, to read the relevant CSS files and assert the selector and component-specific color pairs:

```ts
expect(mapCss).toContain('.critical-confirmation-actions .cta-primary:not(:disabled)');
expect(contrast('#ffffff', '#0b9e74')).toBeGreaterThanOrEqual(4.5);
```

- [ ] **Step 2: Verify RED**

Run:

```bash
cd src/AFK4.Operator.App.Web
bun test src/MapSidePanel.test.tsx src/styles/qaContrast.test.ts
```

Expected: the CSS test fails because the explicit, more-specific primary selector and audited AA values are absent.

- [ ] **Step 3: Implement the minimum CSS change**

After the generic footer rule, add:

```css
.critical-confirmation-actions .cta-primary:not(:disabled) {
  border-color: var(--accent);
  background: var(--accent);
  color: var(--text-on-accent);
}

.critical-confirmation-actions .cta-primary:hover:not(:disabled),
.critical-confirmation-actions .cta-primary:focus-visible:not(:disabled) {
  border-color: var(--accent-hover);
  background: var(--accent-hover);
}
```

Replace only audited low-contrast presentation uses: POS SKU, client secondary text/inactive badge, and light-theme command-search, active rail text, avatar, shift text, map labels, open-tab, success chip, and primary CTA. Use existing AA-safe tokens or theme-local values; do not alter global warning/danger semantics.

- [ ] **Step 4: Verify GREEN and commit**

Run the Step 2 command. Expected: PASS and all test pairs are at least 4.5:1.

```bash
git add src/AFK4.Operator.App.Web/src/styles src/AFK4.Operator.App.Web/src/MapSidePanel.test.tsx
git commit -m "fix(operator): restore CTA priority and AA contrast"
```

### Task 2: Make session-start preview state and Settings readiness coherent

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/devMockBackend.ts:30-120, 340-365, 620-700`
- Modify: `src/AFK4.Operator.App.Web/src/devMockBackend.test.ts`

**Interfaces:**
- Consumes: `POST /api/branches/{branchId}/sessions/start` with `seatId`, `durationMode`, `idempotencyKey`, and billing fields.
- Produces: `resetDevMockState(): void`; stateful responses for `/floor-map`, `/layout/zones`, and `/staff`.

- [ ] **Step 1: Write failing preview-state tests**

Reset state before each new test. Test a `POST .../sessions/start` for `a2`, then a second `GET .../floor-map`:

```ts
resetDevMockState();
await devMockFetch('https://x/api/branches/branch/sessions/start', {
  method: 'POST',
  body: JSON.stringify({ seatId: 'a2', durationMode: 'open', idempotencyKey: 'start-1' })
});
const body = await (await devMockFetch('https://x/api/branches/branch/floor-map')).json();
const seat = body.seats.find((item: { seatId: string }) => item.seatId === 'a2');
expect(seat).toMatchObject({ state: 'Active', activeSessionId: expect.any(String) });
```

Also call `/layout/zones` and `/staff`; assert zone seats total to the seeded map-seat count and staff has at least one entry.

- [ ] **Step 2: Verify RED**

Run:

```bash
cd src/AFK4.Operator.App.Web
bun test src/devMockBackend.test.ts
```

Expected: FAIL because start is currently an empty write and settings data falls through to `[]`.

- [ ] **Step 3: Implement one lazy fixture store**

Rename the static floor-map function to `seedFloorMap`. Add a lazy mutable state initialized from `structuredClone(seedFloorMap())` and export `resetDevMockState` to restore it for tests. Route `GET /floor-map` to this state.

Handle only `POST /sessions/start`: parse its JSON, find a free seat, set `state: 'Active'`, assign `activeSessionId: 'preview-session-{n}'`, set `sessionStartedAtUtc`, and set `remainingSeconds` for fixed mode or `accruedCostMinorUnits: 0` for open mode. Return `{ idempotencyKey, session, deviceCommands: [] }`.

Build `GET /layout/zones` by grouping the same state seats by `zoneId`; return one seeded `StaffUserDto` from `GET /staff`. No unrelated preview mutations are added.

- [ ] **Step 4: Verify GREEN and commit**

Run the Step 2 command. Expected: PASS and later reads reflect the successful start.

```bash
git add src/AFK4.Operator.App.Web/src/devMockBackend.ts src/AFK4.Operator.App.Web/src/devMockBackend.test.ts
git commit -m "fix(operator): make preview session state coherent"
```

### Task 3: Prevent browser preview from opening a real SignalR connection

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/devHostBridge.ts:45-70`
- Modify: `src/AFK4.Operator.App.Web/src/operatorRealtime.ts`
- Modify: `src/AFK4.Operator.App.Web/src/useOperatorRealtime.ts:115-185`
- Modify: `src/AFK4.Operator.App.Web/src/operatorRealtime.test.ts`

**Interfaces:**
- Consumes: `OperatorConfig.shellMode`, `OperatorRealtimeOptions`, `OperatorRealtimeClient`.
- Produces: `createPreviewOperatorRealtimeClient(options: OperatorRealtimeOptions): OperatorRealtimeClient`.

- [ ] **Step 1: Write the failing client test**

```ts
const states: string[] = [];
const client = createPreviewOperatorRealtimeClient({
  baseUrl: 'http://127.0.0.1:5174/', getAccessToken: () => 'token',
  onDeviceStatusChanged: () => {}, onConnectionStateChanged: (state) => states.push(state)
});
await client.start();
await client.stop();
expect(states).toEqual(['connecting', 'connected', 'disconnected']);
```

Add a selection test using `{ shellMode: 'vite-dev-preview' }` and a throwing production connection factory; the preview path must not invoke it.

- [ ] **Step 2: Verify RED**

Run:

```bash
cd src/AFK4.Operator.App.Web
bun test src/operatorRealtime.test.ts
```

Expected: FAIL because the preview client and mode do not exist.

- [ ] **Step 3: Implement narrow preview selection**

Set `shellMode: 'vite-dev-preview'` only in `PREVIEW_MOCK`; keep `vite-dev` for `?live`. Implement a client that reports `connecting`, then `connected`, resolves immediately, and reports `disconnected` on stop. In `useOperatorRealtime`, select it only for `vite-dev-preview`; leave the current SignalR branch unchanged for all other runtime modes.

- [ ] **Step 4: Verify GREEN and commit**

Run the Step 2 command. Expected: PASS, including existing production hub URL tests.

```bash
git add src/AFK4.Operator.App.Web/src/devHostBridge.ts src/AFK4.Operator.App.Web/src/operatorRealtime.ts src/AFK4.Operator.App.Web/src/useOperatorRealtime.ts src/AFK4.Operator.App.Web/src/operatorRealtime.test.ts
git commit -m "fix(operator): isolate preview realtime from SignalR"
```

### Task 4: Verify the integrated browser-preview flow

**Files:**
- Modify: `docs/progress/2026-05-12-vertical-slice-progress.md` only when a durable QA known-gap statement changes.

**Interfaces:**
- Consumes: completed CSS, fixture-state, and preview-realtime changes.
- Produces: fresh verification evidence outside the repository.

- [ ] **Step 1: Run focused and full web verification**

```bash
cd src/AFK4.Operator.App.Web
bun test src/MapSidePanel.test.tsx src/devMockBackend.test.ts src/operatorRealtime.test.ts src/styles/qaContrast.test.ts
bun run test
bun run build
```

Expected: all commands exit 0.

- [ ] **Step 2: Run local browser and axe QA**

Start Vite at `127.0.0.1:5174`. Keep temporary Playwright and axe scripts in `/tmp`; verify sign-in, floor map in dark/light themes, POS, Clients, session start followed by active-seat rendering, Settings readiness, no browser-console errors, and no `/hubs/devices` request.

Expected: no WCAG 2 A/AA violations in the specified states, no real hub request, and the post-start seat is active.

- [ ] **Step 3: Record only durable project state and commit if changed**

If preview is no longer a known QA limitation, update the compact progress snapshot to retain only Windows-native test limits. Then:

```bash
git diff --check
git status --short
git add docs/progress/2026-05-12-vertical-slice-progress.md
git commit -m "docs(progress): record operator QA hardening verification"
```

Do not commit when no durable progress text changes. Do not push.

## Plan Self-Review

- Task 1 covers the CTA and all audited contrast families.
- Task 2 covers mutable floor-map state and Settings data consistency.
- Task 3 covers deterministic preview realtime without touching production SignalR.
- Task 4 covers focused tests, full test/build, browser/axe validation, and docs hygiene.
