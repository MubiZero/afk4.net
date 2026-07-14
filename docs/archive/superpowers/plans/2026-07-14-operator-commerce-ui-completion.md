# Operator Commerce UI Completion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Integrate the verified commerce core into the consolidated Operator UI and finish client selection, POS labels/stock health, order disclosure, and arbitrary booking-seat selection.

**Architecture:** Start a new isolated topic branch at the immutable consolidated-UI tip and cherry-pick only the reviewed commerce slice. Keep shared client lookup in `booking/ClientPicker`, keep booking draft ownership in `BackendBookingWorkspace`, and keep visual state local to each component.

**Tech Stack:** Git worktrees, React 19, TypeScript, Bun test, Testing Library, `@afk4/i18n`, CSS.

## Global Constraints

- Base the execution branch on the then-current verified `feat/operator-ui-consolidated` tip; record the immutable commit before changes.
- Import commerce commits `435633af12d04892802466a4ba07deccb639e378` through `96d93c41e1b52a1c934c38851c7b1f39f67b98cc`, excluding the older divergent Operator UI ancestry.
- Do not remove `Журнал кассы`, anti-fraud review, or receipt/refund actions.
- Non-stock products never contribute to POS stock-health warnings.
- Ctrl/meta selection supplements plain click and drag; it does not replace them.
- Use localized strings in `packages/i18n/src/messages.ts` for all new copy.
- Preserve unrelated worktrees and user changes.

---

### Task 1: Create And Prove The Integrated Baseline

**Files:**
- Modify on conflict only: files reported by `git cherry-pick`
- Test: every conflicted source file's focused test

**Interfaces:**
- Consumes: consolidated Operator UI commit recorded as `UI_BASE`; commerce range `435633af^..96d93c41`.
- Produces: branch `feat/operator-commerce-booking-ux` in `.worktrees/operator-commerce-booking-ux`, clean and containing both verified trees.

- [ ] **Step 1: Create the isolated worktree**

```bash
UI_BASE=$(git rev-parse feat/operator-ui-consolidated)
COMMERCE_TIP=$(git rev-parse feat/commerce-financial-integrity-impl)
test "$COMMERCE_TIP" = "96d93c41e1b52a1c934c38851c7b1f39f67b98cc"
git worktree add .worktrees/operator-commerce-booking-ux -b feat/operator-commerce-booking-ux "$UI_BASE"
```

Expected: a clean linked worktree on `feat/operator-commerce-booking-ux`.

- [ ] **Step 2: Apply only the reviewed commerce slice**

```bash
git cherry-pick 435633af12d04892802466a4ba07deccb639e378^..96d93c41e1b52a1c934c38851c7b1f39f67b98cc
```

Expected: either all commits apply or Git stops on a genuine overlap. Resolve overlaps by retaining consolidated UI behavior plus commerce contracts; never choose an entire side without inspecting the semantic diff.

- [ ] **Step 3: Verify conflict resolutions narrowly**

For each resolved frontend file run its test, for example:

```bash
cd src/AFK4.Operator.App.Web
bun test src/BackendPosWorkspace.test.tsx src/PosOrdersTicker.test.tsx
```

For each resolved backend/contract file run the matching project test filter:

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter 'FullyQualifiedName~ShopCommerce|FullyQualifiedName~EfPosService' -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Expected: PASS; fix integration regressions before feature work.

- [ ] **Step 4: Commit conflict-only integration repairs if needed**

```bash
git add -u
git diff --cached --check
git diff --cached --stat
git commit -m "fix(integration): reconcile commerce with consolidated operator UI"
```

Expected: skip this commit when cherry-pick completed without additional repair edits.

### Task 2: Correct POS Labels And Stock Health

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/BackendPosWorkspace.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/BackendPosWorkspace.lowStock.test.ts`
- Modify: `src/AFK4.Operator.App.Web/src/BackendPosWorkspace.test.tsx`
- Modify: `packages/i18n/src/messages.ts`

**Interfaces:**
- Consumes: `PosProductDto.trackStock`, `Money`, selected `PlayerClientItem`.
- Produces: `isLowStock(item)` that distinguishes tracked zero stock from disabled low-stock thresholds; localized `op.pos.catalog.priceLabel` and `op.pos.cart.balanceLabel`.

- [ ] **Step 1: Write failing stock-rule tests**

Add cases equivalent to:

```ts
expect(isLowStock({ source: 'backend', trackStock: false, stockOnHand: 0, reorderThreshold: 0 })).toBe(false);
expect(isLowStock({ source: 'backend', trackStock: true, stockOnHand: 0, reorderThreshold: 0 })).toBe(true);
expect(isLowStock({ source: 'backend', trackStock: true, stockOnHand: 3, reorderThreshold: 0 })).toBe(false);
```

Update all existing fixtures to include `trackStock`.

- [ ] **Step 2: Write failing rendered-copy tests**

In `BackendPosWorkspace.test.tsx`, render a backend product and linked client, then assert:

```ts
expect(screen.getByText('Цена:')).toBeInTheDocument();
expect(screen.getByText('Баланс:')).toBeInTheDocument();
```

- [ ] **Step 3: Run RED tests**

```bash
cd src/AFK4.Operator.App.Web
bun test src/BackendPosWorkspace.lowStock.test.ts src/BackendPosWorkspace.test.tsx
```

Expected: FAIL because `trackStock` and the two labels are not implemented.

- [ ] **Step 4: Implement the product projection and labels**

Use this shape:

```ts
type PosCatalogItem = {
  // existing fields
  trackStock: boolean;
};

export function isLowStock(
  item: Pick<PosCatalogItem, 'source' | 'trackStock' | 'stockOnHand' | 'reorderThreshold'>
): boolean {
  return item.source === 'backend'
    && item.trackStock
    && (item.stockOnHand === 0 || (item.reorderThreshold > 0 && item.stockOnHand <= item.reorderThreshold));
}
```

Project `trackStock` from `PosProductDto`, set fixture services to false, and render:

```tsx
<b><span>{t('op.pos.catalog.priceLabel')}</span> <Money minorUnits={product.priceMinorUnits} currencyCode={currencyCode} /></b>
<em>{selectedPosPlayer.phoneNumber || t('op.pos.cart.clientNoPhone')} · {t('op.pos.cart.balanceLabel')} <Money minorUnits={selectedPosPlayer.balanceMinorUnits} currencyCode={currencyCode} /></em>
```

Add ru/en/tg catalog values: `Цена:`, `Price:`, `Нарх:` and `Баланс:`, `Balance:`, `Тавозун:`.

- [ ] **Step 5: Run GREEN tests and commit**

```bash
cd src/AFK4.Operator.App.Web
bun test src/BackendPosWorkspace.lowStock.test.ts src/BackendPosWorkspace.test.tsx
cd ../../..
git add src/AFK4.Operator.App.Web/src/BackendPosWorkspace.tsx src/AFK4.Operator.App.Web/src/BackendPosWorkspace.lowStock.test.ts src/AFK4.Operator.App.Web/src/BackendPosWorkspace.test.tsx packages/i18n/src/messages.ts
git diff --cached --check
git commit -m "fix(operator-pos): clarify price balance and stock health"
```

Expected: PASS and one focused commit.

### Task 3: Make Order Disclosure Stable And Accessible

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/PosOrdersTicker.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/PosOrdersTicker.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/styles/11-pos.css`
- Modify: `packages/i18n/src/messages.ts`

**Interfaces:**
- Consumes: existing `popover?.id`, reduced-motion media query.
- Produces: icon-only `.pos-order-chevron` whose state follows `aria-expanded`; no `op.shopOrders.details` rendering.

- [ ] **Step 1: Write the failing disclosure test**

```ts
const disclosure = await screen.findByRole('button', { expanded: false });
expect(disclosure).not.toHaveTextContent(/Подробнее|Details|Тафсилот/);
fireEvent.click(disclosure);
expect(disclosure).toHaveAttribute('aria-expanded', 'true');
expect(disclosure.querySelector('.pos-order-chevron')).toHaveClass('is-expanded');
```

- [ ] **Step 2: Run RED**

```bash
cd src/AFK4.Operator.App.Web
bun test src/PosOrdersTicker.test.tsx
```

Expected: FAIL because detail copy is present and the icon has no expanded class.

- [ ] **Step 3: Implement semantic state and CSS**

Render:

```tsx
<ChevronRight
  className={`pos-order-chevron${popover?.id === order.id ? ' is-expanded' : ''}`}
  size={14}
  aria-hidden="true"
/>
```

Remove `.pos-order-more` copy and remove the unused translation key in all locales. Replace chip lift with non-geometric feedback:

```css
.pos-order-chip { transition: background var(--duration-fast) var(--ease-out), border-color var(--duration-fast) var(--ease-out), box-shadow var(--duration-fast) var(--ease-out); }
.pos-order-chip:hover { transform: none; box-shadow: var(--shadow-sm); }
.pos-order-chevron { transition: transform var(--duration-fast) var(--ease-out); }
.pos-order-chevron.is-expanded { transform: rotate(90deg); }
@media (prefers-reduced-motion: reduce) { .pos-order-chevron { transition: none; } }
```

- [ ] **Step 4: Run GREEN and commit**

```bash
cd src/AFK4.Operator.App.Web
bun test src/PosOrdersTicker.test.tsx
cd ../../..
git add src/AFK4.Operator.App.Web/src/PosOrdersTicker.tsx src/AFK4.Operator.App.Web/src/PosOrdersTicker.test.tsx src/AFK4.Operator.App.Web/src/styles/11-pos.css packages/i18n/src/messages.ts
git diff --cached --check
git commit -m "fix(operator-pos): stabilize order disclosure motion"
```

### Task 4: Reuse ClientPicker In New Session

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/booking/ClientPicker.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/booking/ClientPicker.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/MapSidePanel.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/MapSidePanel.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/styles/06-map-grid.css`

**Interfaces:**
- Consumes: `ClientPicker`, `PlayerClientItem`, `billingPlayers` search callback.
- Produces: one shared picker for booking and session start; selected session client sets `selectedPlayerId`, query, balance, and packages.

- [ ] **Step 1: Extend ClientPicker test coverage before reuse**

Add a test that clicks a result and one that presses ArrowDown/Enter, asserting the same `ClientPick`. Add a stale-link test:

```ts
fireEvent.change(screen.getByRole('combobox'), { target: { value: 'New name' } });
expect(onQueryChange).toHaveBeenCalledWith('New name');
```

- [ ] **Step 2: Write a failing MapSidePanel integration test**

Render the start modal with a backend client search stub, enter two characters, click the result, and assert the result button is selected and its balance/package state is shown. Assert switching to guest clears the linked client.

- [ ] **Step 3: Run RED**

```bash
cd src/AFK4.Operator.App.Web
bun test src/booking/ClientPicker.test.tsx src/MapSidePanel.test.tsx
```

Expected: Map test fails because it still owns a separate inline result list.

- [ ] **Step 4: Replace the inline list with ClientPicker**

Use controlled props:

```tsx
<ClientPicker
  value={playerSearch}
  linked={selectedPlayerId !== ''}
  disabled={!actionsEnabled || isBusy}
  search={searchBillingPlayers}
  onQueryChange={(value) => {
    setPlayerSearch(value);
    setSelectedPlayerId('');
    setSelectedPlayerPackageId('');
  }}
  onPick={(pick) => {
    setPlayerSearch(pick.name);
    setSelectedPlayerId(pick.playerAccountId);
  }}
  onClear={() => {
    setPlayerSearch('');
    setSelectedPlayerId('');
    setSelectedPlayerPackageId('');
  }}
/>
```

Extract `searchBillingPlayers(query): Promise<PlayerClientItem[]>` from the existing effect/API call so ClientPicker remains the debounce owner. Switching to guest calls the same clear helper.

- [ ] **Step 5: Run GREEN and commit**

```bash
cd src/AFK4.Operator.App.Web
bun test src/booking/ClientPicker.test.tsx src/MapSidePanel.test.tsx
cd ../../..
git add src/AFK4.Operator.App.Web/src/booking/ClientPicker.tsx src/AFK4.Operator.App.Web/src/booking/ClientPicker.test.tsx src/AFK4.Operator.App.Web/src/MapSidePanel.tsx src/AFK4.Operator.App.Web/src/MapSidePanel.test.tsx src/AFK4.Operator.App.Web/src/styles/06-map-grid.css
git diff --cached --check
git commit -m "feat(operator-map): reuse linked client picker for sessions"
```

### Task 5: Add Arbitrary Ctrl/Meta Booking Selection

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/booking/BookingTimeline.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/booking/BookingTimeline.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/BackendBookingWorkspace.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/booking/BookingDrawer.tsx`
- Create: `src/AFK4.Operator.App.Web/src/booking/BookingDrawer.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/styles/10-booking.css`

**Interfaces:**
- Consumes: `SeatSummary`, timeline click timestamp, `BookingDraft.seatIds`.
- Produces: `onSeatToggle(seat, startMs)` callback; toggled selections share one draft start/duration and remain subject to `seatHasClash`.

- [ ] **Step 1: Write failing Ctrl/meta tests**

Use two zones and non-free tones. Fire Ctrl-click on one row and meta-click on another. Assert:

```ts
expect(onSeatToggle).toHaveBeenNthCalledWith(1, expect.objectContaining({ id: 'a1' }), expectedStart);
expect(onSeatToggle).toHaveBeenNthCalledWith(2, expect.objectContaining({ id: 'b4' }), expectedStart);
```

Also assert plain click still invokes `onCellCreate` and drag still invokes `onSeatsCreate`.

- [ ] **Step 2: Run RED**

```bash
cd src/AFK4.Operator.App.Web
bun test src/booking/BookingTimeline.test.tsx
```

Expected: FAIL because `onSeatToggle` does not exist.

- [ ] **Step 3: Implement the timeline callback**

Add the prop:

```ts
onSeatToggle: (seat: SeatSummary, startMs: number) => void;
```

At the beginning of `handleTrackClick`, after suppressing a completed drag:

```ts
const startMs = snap(msFromClientX(event.currentTarget, event.clientX));
if (event.ctrlKey || event.metaKey) {
  event.preventDefault();
  onSeatToggle(seat, startMs);
  return;
}
```

- [ ] **Step 4: Implement draft toggling in the workspace**

Add:

```ts
const toggleDraftSeat = (seat: SeatSummary, startMs: number) => {
  setDrawerMode('create');
  setDraft((current) => {
    const basis = current.seatIds.length > 0 ? current.seatIds : current.seatId ? [current.seatId] : [];
    const seatIds = basis.includes(seat.id) ? basis.filter((id) => id !== seat.id) : [...basis, seat.id];
    return {
      ...current,
      seatId: '',
      seatIds,
      startsAt: basis.length === 0 ? toDateTimeInputValue(new Date(startMs)) : current.startsAt
    };
  });
};
```

Pass `onSeatToggle={toggleDraftSeat}`. Keep conflicted IDs visible as warning chips and keep `hasGroupConflict` as the submit blocker.

- [ ] **Step 5: Run GREEN and commit**

```bash
cd src/AFK4.Operator.App.Web
bun test src/booking/BookingTimeline.test.tsx src/booking/BookingDrawer.test.tsx src/booking/ClientPicker.test.tsx
cd ../../..
git add src/AFK4.Operator.App.Web/src/booking/BookingTimeline.tsx src/AFK4.Operator.App.Web/src/booking/BookingTimeline.test.tsx src/AFK4.Operator.App.Web/src/BackendBookingWorkspace.tsx src/AFK4.Operator.App.Web/src/booking/BookingDrawer.tsx src/AFK4.Operator.App.Web/src/booking/BookingDrawer.test.tsx src/AFK4.Operator.App.Web/src/styles/10-booking.css
git diff --cached --check
git commit -m "feat(operator-booking): select arbitrary seats with ctrl click"
```

### Task 6: Verify The UI Slice

**Files:**
- Modify only if durable state changed: `docs/progress/2026-05-12-vertical-slice-progress.md`

**Interfaces:**
- Consumes: Tasks 1-5.
- Produces: green integrated UI baseline ready for the money and reservation plans.

- [ ] **Step 1: Run full Operator Web tests and build**

```bash
cd src/AFK4.Operator.App.Web
bun test $(find src -name '*.test.ts' -o -name '*.test.tsx' | grep -v App.test)
bun test src/App.test.tsx
bun run build
```

Expected: all tests PASS and Vite production build succeeds.

- [ ] **Step 2: Run integration-sensitive contract/build checks**

```bash
cd ../../..
dotnet test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
dotnet build AFK4.sln -p:EnableWindowsTargeting=true -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
git diff --check
git status --short --branch
```

Expected: green checks; only intentional later-plan work may remain uncommitted.
