# Operator «Карта» B2-3b — Zone editing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a floor manager create, reshape, recolour, rename and delete *zones* in the «План» editor, and reassign seats between zones — so newly created zones are actually usable.

**Architecture:** Extends the B2-3a editor. The single source of truth stays the in-memory `PlanDraft`; we add zone-level immutable mutations + a seat→zone reassignment mutation, a zones list panel (selection + «add»), a zone inspector (name / X / Y / W / H / colour / delete), and a zone-reassign dropdown on the seat inspector. The canvas only gains a *selected-zone highlight* — zone positioning is numeric (inspector fields), not drag, which keeps `FloorPlan` interaction code minimal and fully unit-testable. The full-replace serializer already round-trips zones and walls; the only serializer change is emitting the server zone id separately from the client id so brand-new zones (no server id yet) are created instead of mismatched.

**Tech Stack:** React + TypeScript (Vite), `bun test` + `@testing-library/react` (happy-dom), `@afk4/i18n` (generated `MessageKey`).

---

## Scope & deliberate exclusions

**In scope (B2-3b):** zone create / move (numeric) / resize (numeric) / rename / colour / delete (with orphan + last-zone guards); seat → zone reassignment; selected-zone highlight on canvas.

**Out of scope — deferred to a future B2-3c:**
- **Wall drawing / deletion.** Walls are purely decorative line segments; the backend keeps the ones we already echo back (`toBulkUpdateRequest` passes `draft.walls` through unchanged, so there is **no regression** — existing walls survive every save). Adding a wall-drawing interaction is a separate, lower-value chunk and would bloat `FloorPlan` with a third pointer mode. Build it after zones land if still wanted.
- **`zoneType` editing.** `zoneType` is currently dead data — nothing in the renderer reads it (only `color` affects drawing). We **preserve** whatever value exists (the serializer already passes `zoneType` through) but do not expose an editor for it. Revisit only if a real consumer appears.
- **Zone drag-to-move on canvas.** Numeric X/Y fields cover positioning with live canvas feedback. Drag-to-move is a possible later polish, not a blocker.

### Backend invariants this plan must respect (from B2-1, already in prod)

`PUT /api/branches/{id}/floor-map` is a **full replace** guarded by `If-Match` ETag:
- Any zone/seat whose id is absent from the request is **deleted**; walls are wiped + recreated.
- The request must contain **≥ 1 zone**.
- Every seat's `zoneClientId` must reference a zone present **in the same request** (by its `clientId`).
- A new entity is created when its server id (`zoneId`/`seatId`) is `null` but `clientId` is set; an existing entity is matched when its server id is present.
- Deleting a seat that still has device/session history → 409. (Not reachable here — we never drop seats from the request; B2-3a keeps unplaced seats in the payload.)

**Consequences for zone deletion:**
1. Removing a zone that still has seats pointing at it would leave those seats with a dangling `zoneClientId` → 400 from the backend. So deletion is **blocked while the zone has any seats** (placed *or* unplaced). The fix path for the operator is: reassign those seats to another zone first.
2. The request needs ≥ 1 zone, so deleting the **last** zone is blocked.

Both guards are enforced in the UI (disabled delete button + explicit reason) so the operator never hits a raw backend error.

---

## File Structure

- **Modify** `src/AFK4.Operator.App.Web/src/floorPlanDraft.ts` — add `DraftZone.serverId`; zone mutations (`addZone`, `setZoneGeometry`, `renameZone`, `setZoneColor`, `removeZone`); seat mutation `setSeatZone`; read helper `zoneSeatCount`; update `createDraft` and `toBulkUpdateRequest`.
- **Modify** `src/AFK4.Operator.App.Web/src/FloorPlan.tsx` — add optional `selectedZoneId` prop → highlight class on the matching zone rect.
- **Create** `src/AFK4.Operator.App.Web/src/FloorZonePanel.tsx` — list of zones (select on click) + «Добавить зону» button.
- **Create** `src/AFK4.Operator.App.Web/src/FloorZoneInspector.tsx` — name / X / Y / W / H / colour / delete for the selected zone.
- **Modify** `src/AFK4.Operator.App.Web/src/FloorInspector.tsx` — add a «Зона» reassignment dropdown.
- **Modify** `src/AFK4.Operator.App.Web/src/FloorPlanEditor.tsx` — selection union (seat XOR zone), wire panel/inspectors/mutations, new-zone placement, delete guards + feedback.
- **Modify** `locales/{ru,en,tg}.json` + regenerate `packages/i18n/src/messages.ts`; extend the Tajik parity allow-list only if genuinely identical.
- **Modify** `src/AFK4.Operator.App.Web/src/styles.css` — zone panel / zone inspector / selected-zone styles.
- **Test files:** extend `floorPlanDraft.test.ts`, `FloorPlan.test.tsx`, `FloorInspector.test.tsx`, `FloorPlanEditor.test.tsx`; create `FloorZonePanel.test.tsx`, `FloorZoneInspector.test.tsx`.

### Environment notes for the implementer (read before starting)

- `bun` is at `~/.bun/bin/bun`. **`bun test` does NOT typecheck** — after touching types you MUST also run `~/.bun/bin/bun run build` (`tsc -b && vite build`) from the web workspace to catch type errors.
- Operator web test command (App.test runs in its own bun invocation by design — keep the split):
  ```bash
  cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun test $(ls src/*.test.ts src/*.test.tsx | grep -v App.test) && ~/.bun/bin/bun test src/App.test.tsx
  ```
- i18n: locale sources are repo-root `locales/{ru,en,tg}.json`. **Never hand-edit `packages/i18n/src/messages.ts`** — regenerate with `cd packages/i18n && ~/.bun/bin/bun run gen`. `MessageKey` is a generated union; a typo'd key fails `tsc`.
- Verify your git base before starting each task: `git -C <repo> log --oneline -1` must show your branch tip, not an old main. (A stale base caused a hallucinated re-implementation last time.)

---

### Task 1: Draft — server id, zone mutations, seat reassignment, serializer

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/floorPlanDraft.ts`
- Test: `src/AFK4.Operator.App.Web/src/floorPlanDraft.test.ts`

**Context:** `DraftZone.zoneId` is today used as BOTH the stable client key AND the server id. To create brand-new zones we must separate them: keep `zoneId` as the always-present **client key** (existing zones = their GUID, new zones = a synthetic id) and add `serverId` (`null` for new zones). `createDraft` sets `serverId = zone.zoneId` for existing zones, so nothing changes for them. The serializer emits `zoneId: serverId` (the create/update discriminator) and `clientId: zoneId` (the reference key seats point at via their `zoneId`).

- [ ] **Step 1: Write the failing tests**

Add these to `floorPlanDraft.test.ts` (keep existing tests):

```typescript
import { describe, it, expect } from 'bun:test';
import {
  createDraft,
  addZone,
  setZoneGeometry,
  renameZone,
  setZoneColor,
  removeZone,
  setSeatZone,
  zoneSeatCount,
  toBulkUpdateRequest
} from './floorPlanDraft';
import type { OperatorFloorMapState } from './floorMapState';

function stateWith(): OperatorFloorMapState {
  return {
    branchName: 'B', source: 'backend', loadStatus: 'ready', loadedAtMs: 0,
    error: null, isStale: false, etag: 'v1',
    seats: [
      { id: 'seat-1', zoneId: 'zone-1', name: 'PC-1', sortOrder: 0, tone: 'free', stateLabel: 'free', seatType: 'pc', rotation: 0, posX: 0, posY: 0 },
      { id: 'seat-2', zoneId: 'zone-1', name: 'PC-2', sortOrder: 1, tone: 'free', stateLabel: 'free', seatType: 'pc', rotation: 0, posX: null, posY: null }
    ] as OperatorFloorMapState['seats'],
    zones: [
      { zoneId: 'zone-1', name: 'Зал', sortOrder: 0, geoX: 0, geoY: 0, geoWidth: 4, geoHeight: 3, color: null, zoneType: null }
    ] as OperatorFloorMapState['zones'],
    walls: [{ wallId: 'w1', x1: 0, y1: 0, x2: 5, y2: 0 }] as OperatorFloorMapState['walls']
  } as OperatorFloorMapState;
}

describe('zone editing', () => {
  it('createDraft mirrors zoneId into serverId for existing zones', () => {
    const draft = createDraft(stateWith());
    expect(draft.zones[0].zoneId).toBe('zone-1');
    expect(draft.zones[0].serverId).toBe('zone-1');
  });

  it('addZone appends a new zone with serverId null and next sortOrder, dirty', () => {
    const draft = addZone(createDraft(stateWith()), 'new-zone-0', 'Новая зона', 0, 3, 4, 3);
    const added = draft.zones.find((z) => z.zoneId === 'new-zone-0');
    expect(added).toBeTruthy();
    expect(added?.serverId).toBeNull();
    expect(added?.sortOrder).toBe(1);
    expect(added?.geoWidth).toBe(4);
    expect(draft.isDirty).toBe(true);
  });

  it('setZoneGeometry patches only given fields', () => {
    const draft = setZoneGeometry(createDraft(stateWith()), 'zone-1', { geoWidth: 9 });
    expect(draft.zones[0].geoWidth).toBe(9);
    expect(draft.zones[0].geoX).toBe(0);
    expect(draft.isDirty).toBe(true);
  });

  it('renameZone and setZoneColor update the zone', () => {
    let draft = renameZone(createDraft(stateWith()), 'zone-1', 'VIP');
    draft = setZoneColor(draft, 'zone-1', '#3b82f6');
    expect(draft.zones[0].name).toBe('VIP');
    expect(draft.zones[0].color).toBe('#3b82f6');
  });

  it('zoneSeatCount counts placed AND unplaced seats of a zone', () => {
    expect(zoneSeatCount(createDraft(stateWith()), 'zone-1')).toBe(2);
  });

  it('setSeatZone repoints a seat; removeZone drops an empty zone', () => {
    let draft = addZone(createDraft(stateWith()), 'new-zone-0', 'Новая зона', 0, 3, 4, 3);
    // move both seats out of zone-1 onto the new zone, then zone-1 is deletable
    draft = setSeatZone(draft, 'seat-1', 'new-zone-0');
    draft = setSeatZone(draft, 'seat-2', 'new-zone-0');
    expect(zoneSeatCount(draft, 'zone-1')).toBe(0);
    draft = removeZone(draft, 'zone-1');
    expect(draft.zones.find((z) => z.zoneId === 'zone-1')).toBeUndefined();
  });

  it('toBulkUpdateRequest emits serverId as zoneId (null for new zones) and clientId as the ref key', () => {
    let draft = addZone(createDraft(stateWith()), 'new-zone-0', 'Новая зона', 0, 3, 4, 3);
    draft = setSeatZone(draft, 'seat-1', 'new-zone-0');
    const req = toBulkUpdateRequest(draft, 'org-1');

    const existing = req.zones.find((z) => z.clientId === 'zone-1');
    expect(existing?.zoneId).toBe('zone-1'); // existing → server id present

    const created = req.zones.find((z) => z.clientId === 'new-zone-0');
    expect(created?.zoneId).toBeNull(); // new → server creates it

    const moved = req.seats.find((s) => s.clientId === 'seat-1');
    expect(moved?.zoneClientId).toBe('new-zone-0'); // seat references the zone's clientId
  });
});
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun test src/floorPlanDraft.test.ts`
Expected: FAIL — `addZone`/`setZoneGeometry`/`renameZone`/`setZoneColor`/`removeZone`/`setSeatZone`/`zoneSeatCount` are not exported; `serverId` missing.

- [ ] **Step 3: Implement**

In `floorPlanDraft.ts`, add `serverId` to the `DraftZone` interface:

```typescript
export interface DraftZone {
  zoneId: string; serverId: string | null; name: string; sortOrder: number;
  geoX: number | null; geoY: number | null; geoWidth: number | null; geoHeight: number | null;
  color: string | null; zoneType: string | null;
}
```

In `createDraft`, set `serverId` when mapping zones (the GUID doubles as both):

```typescript
    zones: state.zones.map((zone) => ({
      zoneId: zone.zoneId,
      serverId: zone.zoneId,
      name: zone.name,
      sortOrder: zone.sortOrder,
      geoX: zone.geoX ?? null,
      geoY: zone.geoY ?? null,
      geoWidth: zone.geoWidth ?? null,
      geoHeight: zone.geoHeight ?? null,
      color: zone.color ?? null,
      zoneType: zone.zoneType ?? null
    })),
```

Add zone + seat-zone mutations and the read helper (place after the seat mutations, before `toBulkUpdateRequest`):

```typescript
function mutateZone(draft: PlanDraft, zoneId: string, change: Partial<DraftZone>): PlanDraft {
  return { ...draft, isDirty: true, zones: draft.zones.map((z) => (z.zoneId === zoneId ? { ...z, ...change } : z)) };
}

// `clientId` is generated by the editor (e.g. `new-zone-3`); serverId stays null so the backend creates it.
export function addZone(
  draft: PlanDraft, clientId: string, name: string,
  geoX: number, geoY: number, geoWidth: number, geoHeight: number
): PlanDraft {
  const sortOrder = draft.zones.reduce((max, z) => Math.max(max, z.sortOrder), -1) + 1;
  const zone: DraftZone = { zoneId: clientId, serverId: null, name, sortOrder, geoX, geoY, geoWidth, geoHeight, color: null, zoneType: null };
  return { ...draft, isDirty: true, zones: [...draft.zones, zone] };
}

export function setZoneGeometry(
  draft: PlanDraft, zoneId: string,
  geo: Partial<Pick<DraftZone, 'geoX' | 'geoY' | 'geoWidth' | 'geoHeight'>>
): PlanDraft {
  return mutateZone(draft, zoneId, geo);
}
export function renameZone(draft: PlanDraft, zoneId: string, name: string): PlanDraft {
  return mutateZone(draft, zoneId, { name });
}
export function setZoneColor(draft: PlanDraft, zoneId: string, color: string | null): PlanDraft {
  return mutateZone(draft, zoneId, { color });
}
export function removeZone(draft: PlanDraft, zoneId: string): PlanDraft {
  return { ...draft, isDirty: true, zones: draft.zones.filter((z) => z.zoneId !== zoneId) };
}

export function setSeatZone(draft: PlanDraft, seatId: string, zoneClientId: string): PlanDraft {
  return mutateSeat(draft, seatId, { zoneId: zoneClientId });
}

// Seats (placed or unplaced) that still reference this zone. Drives the delete guard.
export function zoneSeatCount(draft: PlanDraft, zoneClientId: string): number {
  return draft.seats.filter((s) => s.zoneId === zoneClientId).length;
}
```

Update `toBulkUpdateRequest` so the zone request emits the server id separately from the client key:

```typescript
  const zones: FloorMapBulkZoneRequest[] = draft.zones.map((zone) => ({
    zoneId: zone.serverId, clientId: zone.zoneId, name: zone.name, sortOrder: zone.sortOrder,
    geoX: zone.geoX, geoY: zone.geoY, geoWidth: zone.geoWidth, geoHeight: zone.geoHeight,
    color: zone.color, zoneType: zone.zoneType
  }));
```

Also update the doc comment above `toBulkUpdateRequest` — replace the parenthetical "(zone geometry + walls are edited in B2-3b, not here)" with "(walls are edited in B2-3c, not here)".

- [ ] **Step 4: Run the tests to verify they pass**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun test src/floorPlanDraft.test.ts`
Expected: PASS (new + existing).

- [ ] **Step 5: Typecheck (bun test does NOT typecheck)**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun run build`
Expected: build succeeds. If any existing caller constructs a `DraftZone` literal without `serverId`, add it.

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/floorPlanDraft.ts src/AFK4.Operator.App.Web/src/floorPlanDraft.test.ts
git commit -m "feat(operator-map): draft zone mutations + seat reassignment + serverId serializer (B2-3b)"
```

---

### Task 2: Canvas — selected-zone highlight

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/FloorPlan.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/styles.css`
- Test: `src/AFK4.Operator.App.Web/src/FloorPlan.test.tsx`

**Context:** Zone *selection* lives in the zones panel (Task 3), but the operator needs to see which zone is selected on the canvas. Add an optional `selectedZoneId` prop and a modifier class on the matching `<rect>`. No new interaction — this is purely a visual.

- [ ] **Step 1: Write the failing test**

Add to `FloorPlan.test.tsx`:

```typescript
it('marks the selected zone rect with a highlight class', () => {
  const model = {
    placedSeats: [{ id: 's1', name: 'PC-1', tone: 'free', stateLabel: 'free', seatType: 'pc', rotation: 0, posX: 0, posY: 0 }],
    unplacedSeats: [],
    zones: [
      { id: 'zone-1', name: 'Зал', geoX: 0, geoY: 0, geoWidth: 4, geoHeight: 3, color: null, zoneType: null },
      { id: 'zone-2', name: 'VIP', geoX: 0, geoY: 4, geoWidth: 3, geoHeight: 2, color: null, zoneType: null }
    ],
    walls: [],
    bbox: { minX: 0, minY: 0, maxX: 4, maxY: 6 },
    isEmpty: false
  };
  const { container } = render(
    <FloorPlan model={model} selectedSeatId="" onSelectSeat={() => {}} selectedZoneId="zone-2" />
  );
  const selected = container.querySelectorAll('.floor-plan-zone--selected');
  expect(selected.length).toBe(1);
});
```

(If `FloorPlan.test.tsx` lacks an `I18nProvider` wrapper / `render` import, mirror the existing tests in that file — reuse their render helper rather than introducing a new one.)

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun test src/FloorPlan.test.tsx`
Expected: FAIL — no element matches `.floor-plan-zone--selected`.

- [ ] **Step 3: Implement**

In `FloorPlan.tsx`, add `selectedZoneId` to the prop type and destructure it (default undefined):

```typescript
  mode = 'view',
  onSeatMove,
  selectedZoneId
}: {
  model: PlanModel;
  selectedSeatId: string;
  onSelectSeat: (seatId: string) => void;
  onSeatContextMenu?: (seatId: string, event: ReactMouseEvent) => void;
  mode?: 'view' | 'edit';
  onSeatMove?: (seatId: string, posX: number, posY: number) => void;
  selectedZoneId?: string;
}) {
```

In the zone `<rect>` render, swap the static class for a conditional one:

```tsx
              <rect
                key={zone.id}
                className={zone.id === selectedZoneId ? 'floor-plan-zone floor-plan-zone--selected' : 'floor-plan-zone'}
                rx={8}
```

In `styles.css`, just after the `.floor-plan-zone { … }` block (around line 1139), add:

```css
.floor-plan-zone--selected {
  fill: color-mix(in srgb, var(--accent) 16%, transparent);
  stroke: var(--accent);
  stroke-width: 2.5;
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun test src/FloorPlan.test.tsx`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/FloorPlan.tsx src/AFK4.Operator.App.Web/src/FloorPlan.test.tsx src/AFK4.Operator.App.Web/src/styles.css
git commit -m "feat(operator-map): selected-zone highlight on the plan canvas (B2-3b)"
```

---

### Task 3: Zones panel (list + add)

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/FloorZonePanel.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/styles.css`
- Test: `src/AFK4.Operator.App.Web/src/FloorZonePanel.test.tsx`

**Context:** Mirrors `FloorPalette` (seats list). Lists every draft zone; clicking selects it; an «Добавить зону» button creates one. i18n keys `op.map.plan.edit.zonesPanelTitle` and `op.map.plan.edit.addZone` are added in Task 7 — they will not exist yet, so this task's typecheck happens at the end of Task 7, not here (referencing an undefined `MessageKey` fails `tsc`). Use the literal keys now; `bun test` (no typecheck) still runs green.

- [ ] **Step 1: Write the failing test**

Create `FloorZonePanel.test.tsx`:

```typescript
import { describe, it, expect, mock } from 'bun:test';
import { render, screen, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { FloorZonePanel } from './FloorZonePanel';

function renderPanel(props: Partial<Parameters<typeof FloorZonePanel>[0]> = {}) {
  const onSelectZone = mock(() => {});
  const onAddZone = mock(() => {});
  render(
    <I18nProvider initialLocale="ru">
      <FloorZonePanel
        zones={[{ id: 'zone-1', name: 'Зал' }, { id: 'zone-2', name: 'VIP' }]}
        selectedZoneId="zone-2"
        onSelectZone={onSelectZone}
        onAddZone={onAddZone}
        {...props}
      />
    </I18nProvider>
  );
  return { onSelectZone, onAddZone };
}

describe('FloorZonePanel', () => {
  it('lists zones and selects on click', () => {
    const { onSelectZone } = renderPanel();
    fireEvent.click(screen.getByText('Зал'));
    expect(onSelectZone).toHaveBeenCalledWith('zone-1');
  });

  it('marks the selected zone', () => {
    renderPanel();
    expect(screen.getByText('VIP').closest('button')?.className).toContain('is-selected');
  });

  it('fires onAddZone', () => {
    const { onAddZone } = renderPanel();
    fireEvent.click(screen.getByRole('button', { name: 'Добавить зону' }));
    expect(onAddZone).toHaveBeenCalled();
  });
});
```

(If sibling tests use a different `I18nProvider` import/prop, copy theirs verbatim — check `FloorPalette.test.tsx` first.)

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun test src/FloorZonePanel.test.tsx`
Expected: FAIL — module `./FloorZonePanel` not found.

- [ ] **Step 3: Implement**

Create `FloorZonePanel.tsx`:

```tsx
import { useI18n } from '@afk4/i18n';
import { Plus } from 'lucide-react';

export interface FloorZonePanelItem {
  id: string;
  name: string;
}

export function FloorZonePanel({
  zones,
  selectedZoneId,
  onSelectZone,
  onAddZone
}: {
  zones: FloorZonePanelItem[];
  selectedZoneId: string;
  onSelectZone: (id: string) => void;
  onAddZone: () => void;
}) {
  const { t } = useI18n();

  return (
    <div className="floor-zone-panel">
      <h3 className="floor-palette-title">{t('op.map.plan.edit.zonesPanelTitle')}</h3>
      <ul className="floor-palette-list">
        {zones.map((zone) => (
          <li key={zone.id}>
            <button
              type="button"
              className={zone.id === selectedZoneId ? 'floor-palette-item is-selected' : 'floor-palette-item'}
              onClick={() => onSelectZone(zone.id)}
            >
              {zone.name}
            </button>
          </li>
        ))}
      </ul>
      <button type="button" className="floor-zone-add" onClick={onAddZone}>
        <Plus size={14} aria-hidden="true" /> {t('op.map.plan.edit.addZone')}
      </button>
    </div>
  );
}
```

In `styles.css`, after the `.floor-palette-empty { … }` block (around line 8334), add:

```css
.floor-zone-panel {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 12px;
  background: var(--surface-elevated);
  border: 1px solid var(--border-default);
  border-radius: 10px;
}

.floor-palette-item.is-selected {
  border-color: var(--accent);
  background: color-mix(in srgb, var(--accent) 14%, var(--surface-canvas));
}

.floor-zone-add {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 6px 10px;
  border: 1px dashed var(--border-strong);
  border-radius: 8px;
  background: transparent;
  color: var(--text-secondary);
  font-size: 13px;
  cursor: pointer;
}

.floor-zone-add:hover {
  background: var(--surface-hover);
  color: var(--text-primary);
}

.floor-zone-add:focus-visible {
  outline: none;
  box-shadow: var(--focus-ring);
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun test src/FloorZonePanel.test.tsx`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/FloorZonePanel.tsx src/AFK4.Operator.App.Web/src/FloorZonePanel.test.tsx src/AFK4.Operator.App.Web/src/styles.css
git commit -m "feat(operator-map): zones panel — list + add (B2-3b)"
```

---

### Task 4: Zone inspector (name / geometry / colour / delete)

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/FloorZoneInspector.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/styles.css`
- Test: `src/AFK4.Operator.App.Web/src/FloorZoneInspector.test.tsx`

**Context:** Edits the selected zone. Name is a text input; X / Y / W / H are number inputs (X/Y min 0, W/H min 1) — editing them updates the draft, which re-renders the canvas live (immediate feedback, no drag needed). Colour is a row of preset swatch buttons plus a «Без цвета» reset. Delete is a danger button; the parent decides whether it is allowed and passes `canDelete` + an optional `deleteBlockedReason` string (already translated) so the inspector shows *why* it is disabled (no silent dead button — see design rule #32/#34).

- [ ] **Step 1: Write the failing test**

Create `FloorZoneInspector.test.tsx`:

```typescript
import { describe, it, expect, mock } from 'bun:test';
import { render, screen, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { FloorZoneInspector } from './FloorZoneInspector';

function renderInspector(props: Partial<Parameters<typeof FloorZoneInspector>[0]> = {}) {
  const handlers = {
    onRename: mock(() => {}),
    onGeometry: mock(() => {}),
    onColor: mock(() => {}),
    onDelete: mock(() => {})
  };
  render(
    <I18nProvider initialLocale="ru">
      <FloorZoneInspector
        zone={{ id: 'zone-1', name: 'Зал', geoX: 0, geoY: 0, geoWidth: 4, geoHeight: 3, color: null }}
        canDelete
        deleteBlockedReason={null}
        {...handlers}
        {...props}
      />
    </I18nProvider>
  );
  return handlers;
}

describe('FloorZoneInspector', () => {
  it('renames the zone', () => {
    const h = renderInspector();
    fireEvent.change(screen.getByLabelText('Название'), { target: { value: 'VIP' } });
    expect(h.onRename).toHaveBeenCalledWith('VIP');
  });

  it('patches geometry as a partial with a numeric value', () => {
    const h = renderInspector();
    fireEvent.change(screen.getByLabelText('Ширина'), { target: { value: '7' } });
    expect(h.onGeometry).toHaveBeenCalledWith({ geoWidth: 7 });
  });

  it('sets a preset colour and clears it', () => {
    const h = renderInspector();
    fireEvent.click(screen.getByRole('button', { name: 'Без цвета' }));
    expect(h.onColor).toHaveBeenCalledWith(null);
  });

  it('deletes when allowed', () => {
    const h = renderInspector();
    fireEvent.click(screen.getByRole('button', { name: 'Удалить зону' }));
    expect(h.onDelete).toHaveBeenCalledWith('zone-1');
  });

  it('disables delete and shows the reason when blocked', () => {
    const h = renderInspector({ canDelete: false, deleteBlockedReason: 'Сначала перенесите места из этой зоны.' });
    const button = screen.getByRole('button', { name: 'Удалить зону' });
    expect(button).toBeDisabled();
    fireEvent.click(button);
    expect(h.onDelete).not.toHaveBeenCalled();
    expect(screen.getByText('Сначала перенесите места из этой зоны.')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun test src/FloorZoneInspector.test.tsx`
Expected: FAIL — module `./FloorZoneInspector` not found.

- [ ] **Step 3: Implement**

Create `FloorZoneInspector.tsx`:

```tsx
import { useI18n } from '@afk4/i18n';

export interface FloorInspectorZone {
  id: string;
  name: string;
  geoX: number;
  geoY: number;
  geoWidth: number;
  geoHeight: number;
  color: string | null;
}

// Preset swatches — any CSS colour works on the canvas (rendered via color-mix), but a fixed
// palette keeps the look consistent and avoids shipping a full colour picker (YAGNI).
const COLOR_PRESETS = ['#3b82f6', '#22c55e', '#f59e0b', '#ef4444', '#a855f7', '#14b8a6'];

export function FloorZoneInspector({
  zone,
  canDelete,
  deleteBlockedReason,
  onRename,
  onGeometry,
  onColor,
  onDelete
}: {
  zone: FloorInspectorZone;
  canDelete: boolean;
  deleteBlockedReason: string | null;
  onRename: (name: string) => void;
  onGeometry: (geo: Partial<{ geoX: number; geoY: number; geoWidth: number; geoHeight: number }>) => void;
  onColor: (color: string | null) => void;
  onDelete: (id: string) => void;
}) {
  const { t } = useI18n();

  // Number inputs clamp at the field's floor; ignore non-numeric/empty input so the draft never goes NaN.
  const numberField = (
    label: string,
    value: number,
    floor: number,
    apply: (n: number) => void
  ) => (
    <label className="floor-inspector-field">
      <span className="floor-inspector-label">{label}</span>
      <input
        type="number"
        min={floor}
        value={value}
        aria-label={label}
        onChange={(event) => {
          const next = Number(event.target.value);
          if (Number.isFinite(next)) {
            apply(Math.max(floor, Math.round(next)));
          }
        }}
      />
    </label>
  );

  return (
    <div className="floor-inspector">
      <h3 className="floor-inspector-title">{t('op.map.plan.edit.zoneInspectorTitle')}</h3>

      <label className="floor-inspector-field">
        <span className="floor-inspector-label">{t('op.map.plan.edit.zoneNameLabel')}</span>
        <input
          type="text"
          value={zone.name}
          aria-label={t('op.map.plan.edit.zoneNameLabel')}
          onChange={(event) => onRename(event.target.value)}
        />
      </label>

      <div className="floor-zone-geo-grid">
        {numberField(t('op.map.plan.edit.zoneXLabel'), zone.geoX, 0, (n) => onGeometry({ geoX: n }))}
        {numberField(t('op.map.plan.edit.zoneYLabel'), zone.geoY, 0, (n) => onGeometry({ geoY: n }))}
        {numberField(t('op.map.plan.edit.zoneWidthLabel'), zone.geoWidth, 1, (n) => onGeometry({ geoWidth: n }))}
        {numberField(t('op.map.plan.edit.zoneHeightLabel'), zone.geoHeight, 1, (n) => onGeometry({ geoHeight: n }))}
      </div>

      <div className="floor-inspector-field">
        <span className="floor-inspector-label">{t('op.map.plan.edit.zoneColorLabel')}</span>
        <div className="floor-zone-swatches">
          {COLOR_PRESETS.map((preset) => (
            <button
              key={preset}
              type="button"
              className={zone.color === preset ? 'floor-zone-swatch is-selected' : 'floor-zone-swatch'}
              style={{ background: preset }}
              aria-label={preset}
              aria-pressed={zone.color === preset}
              onClick={() => onColor(preset)}
            />
          ))}
          <button type="button" className="floor-zone-swatch-none" onClick={() => onColor(null)}>
            {t('op.map.plan.edit.zoneColorNone')}
          </button>
        </div>
      </div>

      <div className="floor-inspector-actions">
        <button type="button" className="danger" disabled={!canDelete} onClick={() => onDelete(zone.id)}>
          {t('op.map.plan.edit.deleteZone')}
        </button>
        {!canDelete && deleteBlockedReason && (
          <p className="floor-zone-delete-reason">{deleteBlockedReason}</p>
        )}
      </div>
    </div>
  );
}
```

In `styles.css`, after the `.floor-inspector-actions button.danger { … }` block (around line 8406), add:

```css
.floor-inspector-field input {
  width: 100%;
  padding: 6px 8px;
  border: 1px solid var(--border-strong);
  border-radius: 8px;
  background: var(--surface-canvas);
  color: var(--text-primary);
  font-size: 13px;
}

.floor-zone-geo-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 8px;
}

.floor-zone-swatches {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  align-items: center;
}

.floor-zone-swatch {
  width: 22px;
  height: 22px;
  border-radius: 6px;
  border: 2px solid transparent;
  cursor: pointer;
}

.floor-zone-swatch.is-selected {
  border-color: var(--text-primary);
}

.floor-zone-swatch:focus-visible {
  outline: none;
  box-shadow: var(--focus-ring);
}

.floor-zone-swatch-none {
  padding: 3px 8px;
  border: 1px solid var(--border-strong);
  border-radius: 6px;
  background: var(--surface-canvas);
  color: var(--text-secondary);
  font-size: 12px;
  cursor: pointer;
}

.floor-zone-delete-reason {
  margin: 0;
  font-size: 12px;
  color: var(--text-tertiary);
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun test src/FloorZoneInspector.test.tsx`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/FloorZoneInspector.tsx src/AFK4.Operator.App.Web/src/FloorZoneInspector.test.tsx src/AFK4.Operator.App.Web/src/styles.css
git commit -m "feat(operator-map): zone inspector — name/geometry/colour/delete (B2-3b)"
```

---

### Task 5: Seat inspector — zone reassignment

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/FloorInspector.tsx`
- Test: `src/AFK4.Operator.App.Web/src/FloorInspector.test.tsx`

**Context:** New zones start empty. To make them useful (and to let a manager empty a zone before deleting it), the seat inspector gets a «Зона» dropdown listing all zones. The parent passes `zones` (id + name) and the seat's current `zoneId`, plus `onSetZone`. Reuse `PanelSelect` (the app dropdown) — same pattern as the seat-type select already in this file.

- [ ] **Step 1: Write the failing test**

Add to `FloorInspector.test.tsx` (reuse the existing render helper / provider in that file). The current `FloorInspectorSeat` has no `zoneId`; the new prop set adds `zoneId`, `zones`, and `onSetZone`:

```typescript
it('reassigns the seat to another zone', () => {
  const onSetZone = mock(() => {});
  renderInspector({
    seat: { id: 'seat-1', name: 'PC-1', seatType: 'pc', rotation: 0, posX: 0, posY: 0, zoneId: 'zone-1' },
    zones: [{ id: 'zone-1', name: 'Зал' }, { id: 'zone-2', name: 'VIP' }],
    onSetZone
  });
  // PanelSelect renders a combobox; open it and pick VIP.
  fireEvent.click(screen.getByRole('combobox', { name: 'Зона' }));
  fireEvent.click(screen.getByRole('option', { name: 'VIP' }));
  expect(onSetZone).toHaveBeenCalledWith('zone-2');
});
```

Adapt `renderInspector` in the test file to thread the new props with sensible defaults (`zoneId: 'zone-1'`, `zones: [{ id: 'zone-1', name: 'Зал' }]`, `onSetZone: mock(() => {})`) so existing tests keep compiling.

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun test src/FloorInspector.test.tsx`
Expected: FAIL — `onSetZone` / `zones` not accepted; no «Зона» combobox.

- [ ] **Step 3: Implement**

In `FloorInspector.tsx`, extend the seat shape and props, and render a zone `PanelSelect` above the seat-type one. Add `zoneId` to `FloorInspectorSeat`:

```typescript
export interface FloorInspectorSeat {
  id: string;
  name: string;
  seatType: string;
  rotation: number;
  posX: number;
  posY: number;
  zoneId: string;
}
```

Extend the component signature:

```typescript
export function FloorInspector({
  seat,
  zones,
  onSetZone,
  onRotate,
  onSetType,
  onRemove
}: {
  seat: FloorInspectorSeat;
  zones: { id: string; name: string }[];
  onSetZone: (zoneId: string) => void;
  onRotate: (next: number) => void;
  onSetType: (type: string) => void;
  onRemove: (id: string) => void;
}) {
```

Inside, before the seat-type field, add the zone field:

```tsx
      <div className="floor-inspector-field">
        <span className="floor-inspector-label">{t('op.map.plan.edit.seatZoneLabel')}</span>
        <PanelSelect
          value={seat.zoneId}
          options={zones.map((z) => ({ value: z.id, label: z.name }))}
          onChange={onSetZone}
          ariaLabel={t('op.map.plan.edit.seatZoneLabel')}
        />
      </div>
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun test src/FloorInspector.test.tsx`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/FloorInspector.tsx src/AFK4.Operator.App.Web/src/FloorInspector.test.tsx
git commit -m "feat(operator-map): seat inspector — zone reassignment (B2-3b)"
```

---

### Task 6: Editor integration

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/FloorPlanEditor.tsx`
- Test: `src/AFK4.Operator.App.Web/src/FloorPlanEditor.test.tsx`

**Context:** Wire everything together. Selection becomes seat XOR zone: keep `selectedSeatId` and add `selectedZoneId`; selecting one clears the other. The right rail shows the seat inspector when a seat is selected, else the zone inspector when a zone is selected. The left rail stacks the seats palette + the zones panel. Add-zone places a default 4×3 rectangle below existing content and selects it. Delete guards: blocked if the zone still has seats, or if it is the last zone — pass `canDelete` + the translated reason to the inspector.

- [ ] **Step 1: Write the failing tests**

Add to `FloorPlanEditor.test.tsx` (reuse the file's existing render helper + `floorMap` fixture; the fixture must have ≥1 zone and seats with `zoneId`). These three behaviours:

```typescript
it('adds a zone and shows it selected in the inspector', () => {
  renderEditor();
  fireEvent.click(screen.getByRole('button', { name: 'Добавить зону' }));
  // The zone inspector appears (its title) for the freshly added, auto-selected zone.
  expect(screen.getByText('Свойства зоны')).toBeInTheDocument();
});

it('blocks deleting a zone that still has seats and explains why', () => {
  renderEditor();
  // Select the seeded zone that owns the seats (via the zones panel).
  fireEvent.click(screen.getByRole('button', { name: 'Зал' }));
  const del = screen.getByRole('button', { name: 'Удалить зону' });
  expect(del).toBeDisabled();
  expect(screen.getByText('Сначала перенесите места из этой зоны.')).toBeInTheDocument();
});

it('serializes new zones into the save request', async () => {
  const onSave = mock(() => Promise.resolve());
  renderEditor({ onSave });
  fireEvent.click(screen.getByRole('button', { name: 'Добавить зону' }));
  fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));
  await screen.findByRole('button', { name: 'Сохранить' }); // let the async save settle
  const request = (onSave.mock.calls[0] as unknown[])[0] as { zones: { clientId: string; zoneId: string | null }[] };
  expect(request.zones.some((z) => z.zoneId === null)).toBe(true); // a new zone is in the payload
});
```

(Match `renderEditor`'s existing signature in the file; if it doesn't accept overrides, extend it minimally. Use the same zone display name your fixture seeds instead of `'Зал'` if it differs.)

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun test src/FloorPlanEditor.test.tsx`
Expected: FAIL — no «Добавить зону» / zone inspector wired.

- [ ] **Step 3: Implement**

In `FloorPlanEditor.tsx`:

Add imports for the new pieces and mutations:

```typescript
import {
  createDraft,
  moveSeat,
  placeSeat,
  removeSeatFromPlan,
  rotateSeat,
  setSeatType,
  setSeatZone,
  addZone,
  setZoneGeometry,
  renameZone,
  setZoneColor,
  removeZone,
  zoneSeatCount,
  toBulkUpdateRequest,
  type PlanDraft
} from './floorPlanDraft';
import { FloorZonePanel } from './FloorZonePanel';
import { FloorZoneInspector } from './FloorZoneInspector';
```

Add selection + a zone-id counter alongside the existing state:

```typescript
  const [selectedZoneId, setSelectedZoneId] = useState('');
  const zoneSeq = useRef(0);
```

(Add `useRef` to the React import at the top.)

Selecting a seat clears the zone selection and vice-versa. Replace the bare `onSelectSeat={setSelectedSeatId}` wiring with handlers:

```typescript
  const selectSeat = (id: string) => { setSelectedSeatId(id); setSelectedZoneId(''); };
  const selectZone = (id: string) => { setSelectedZoneId(id); setSelectedSeatId(''); };
```

Derive the zone lists + selected zone:

```typescript
  const zoneItems = draft.zones.map((z) => ({ id: z.zoneId, name: z.name }));
  const selectedZone = draft.zones.find((z) => z.zoneId === selectedZoneId) ?? null;
  const inspectorZone = selectedZone && selectedZone.geoX != null && selectedZone.geoY != null
    && selectedZone.geoWidth != null && selectedZone.geoHeight != null
    ? { id: selectedZone.zoneId, name: selectedZone.name, geoX: selectedZone.geoX, geoY: selectedZone.geoY, geoWidth: selectedZone.geoWidth, geoHeight: selectedZone.geoHeight, color: selectedZone.color }
    : selectedZone
      ? { id: selectedZone.zoneId, name: selectedZone.name, geoX: 0, geoY: 0, geoWidth: 1, geoHeight: 1, color: selectedZone.color }
      : null;
```

Add-zone handler (default 4×3 below the lowest existing content):

```typescript
  const handleAddZone = () => {
    let bottom = 0;
    for (const z of draft.zones) {
      if (z.geoY != null && z.geoHeight != null) bottom = Math.max(bottom, z.geoY + z.geoHeight);
    }
    for (const s of draft.seats) {
      if (s.posY != null) bottom = Math.max(bottom, s.posY + 1);
    }
    const clientId = `new-zone-${zoneSeq.current++}`;
    setDraft((current) => addZone(current, clientId, t('op.map.plan.edit.newZoneName'), 0, bottom, 4, 3));
    selectZone(clientId);
  };
```

Delete guard reason (computed for the selected zone):

```typescript
  const deleteBlockedReason = selectedZone
    ? zoneSeatCount(draft, selectedZone.zoneId) > 0
      ? t('op.map.plan.edit.deleteZoneHasSeats')
      : draft.zones.length <= 1
        ? t('op.map.plan.edit.deleteZoneLast')
        : null
    : null;
  const canDeleteZone = selectedZone !== null && deleteBlockedReason === null;
```

In the body, swap the seat-only wiring. The seats palette + zones panel both go in the left rail; the canvas takes `onSelectSeat={selectSeat}` and `selectedZoneId`; the right rail shows seat XOR zone inspector. Replace the existing `<div className="floor-plan-editor-body"> … </div>` with:

```tsx
      <div className="floor-plan-editor-body">
        <div className="floor-plan-editor-rail">
          <FloorPalette unplaced={unplaced} onPlaceSeat={handlePlaceSeat} />
          <FloorZonePanel
            zones={zoneItems}
            selectedZoneId={selectedZoneId}
            onSelectZone={selectZone}
            onAddZone={handleAddZone}
          />
        </div>
        <FloorPlan
          model={planModel}
          mode="edit"
          selectedSeatId={selectedSeatId}
          selectedZoneId={selectedZoneId}
          onSelectSeat={selectSeat}
          onSeatMove={(seatId, x, y) => setDraft((current) => moveSeat(current, seatId, x, y))}
        />
        {inspectorSeat ? (
          <FloorInspector
            seat={{ ...inspectorSeat, zoneId: selectedSeat?.zoneId ?? '' }}
            zones={zoneItems}
            onSetZone={(zoneId) => setDraft((current) => setSeatZone(current, inspectorSeat.id, zoneId))}
            onRotate={(next) => setDraft((current) => rotateSeat(current, inspectorSeat.id, next))}
            onSetType={(type) => setDraft((current) => setSeatType(current, inspectorSeat.id, type))}
            onRemove={(id) => { setDraft((current) => removeSeatFromPlan(current, id)); setSelectedSeatId(''); }}
          />
        ) : inspectorZone ? (
          <FloorZoneInspector
            zone={inspectorZone}
            canDelete={canDeleteZone}
            deleteBlockedReason={deleteBlockedReason}
            onRename={(name) => setDraft((current) => renameZone(current, inspectorZone.id, name))}
            onGeometry={(geo) => setDraft((current) => setZoneGeometry(current, inspectorZone.id, geo))}
            onColor={(color) => setDraft((current) => setZoneColor(current, inspectorZone.id, color))}
            onDelete={(id) => { setDraft((current) => removeZone(current, id)); setSelectedZoneId(''); }}
          />
        ) : null}
      </div>
```

Note: `inspectorSeat` is built from `selectedSeat` (already in the file). `selectedSeat?.zoneId` supplies the seat's current zone for the dropdown.

In `styles.css`, after the `.floor-plan-editor-body { … }` block (around line 8426), add a rail wrapper so palette + zones panel stack in the first grid column:

```css
.floor-plan-editor-rail {
  display: flex;
  flex-direction: column;
  gap: 12px;
  min-height: 0;
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun test src/FloorPlanEditor.test.tsx`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/FloorPlanEditor.tsx src/AFK4.Operator.App.Web/src/FloorPlanEditor.test.tsx src/AFK4.Operator.App.Web/src/styles.css
git commit -m "feat(operator-map): wire zone panel + inspector + reassignment into the editor (B2-3b)"
```

---

### Task 7: i18n keys + regenerate + parity

**Files:**
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json`
- Regenerate: `packages/i18n/src/messages.ts` (via `gen`, never by hand)
- Possibly modify: `packages/i18n/src/messages.test.ts` (Tajik parity allow-list)
- Test: `packages/i18n/src/messages.test.ts`

**Context:** All `op.map.plan.edit.*` keys used in Tasks 3–6 must exist in all three locales. The parity test fails if any `tg` value equals its `ru` value unless allow-listed — so Tajik must be a real translation, not a copy (design rule #37). The keys below are genuinely distinct in Tajik, so **no allow-list additions are expected**.

- [ ] **Step 1: Add keys to `locales/ru.json`**

Insert after the existing `"op.map.plan.edit.noManagePermission"` line:

```json
  "op.map.plan.edit.zonesPanelTitle": "Зоны",
  "op.map.plan.edit.addZone": "Добавить зону",
  "op.map.plan.edit.newZoneName": "Новая зона",
  "op.map.plan.edit.zoneInspectorTitle": "Свойства зоны",
  "op.map.plan.edit.zoneNameLabel": "Название",
  "op.map.plan.edit.zoneXLabel": "X (столбец)",
  "op.map.plan.edit.zoneYLabel": "Y (строка)",
  "op.map.plan.edit.zoneWidthLabel": "Ширина",
  "op.map.plan.edit.zoneHeightLabel": "Высота",
  "op.map.plan.edit.zoneColorLabel": "Цвет",
  "op.map.plan.edit.zoneColorNone": "Без цвета",
  "op.map.plan.edit.deleteZone": "Удалить зону",
  "op.map.plan.edit.deleteZoneHasSeats": "Сначала перенесите места из этой зоны.",
  "op.map.plan.edit.deleteZoneLast": "Должна остаться хотя бы одна зона.",
  "op.map.plan.edit.seatZoneLabel": "Зона",
```

- [ ] **Step 2: Add keys to `locales/en.json`** (same position, English):

```json
  "op.map.plan.edit.zonesPanelTitle": "Zones",
  "op.map.plan.edit.addZone": "Add zone",
  "op.map.plan.edit.newZoneName": "New zone",
  "op.map.plan.edit.zoneInspectorTitle": "Zone properties",
  "op.map.plan.edit.zoneNameLabel": "Name",
  "op.map.plan.edit.zoneXLabel": "X (column)",
  "op.map.plan.edit.zoneYLabel": "Y (row)",
  "op.map.plan.edit.zoneWidthLabel": "Width",
  "op.map.plan.edit.zoneHeightLabel": "Height",
  "op.map.plan.edit.zoneColorLabel": "Color",
  "op.map.plan.edit.zoneColorNone": "No color",
  "op.map.plan.edit.deleteZone": "Delete zone",
  "op.map.plan.edit.deleteZoneHasSeats": "Move the seats out of this zone first.",
  "op.map.plan.edit.deleteZoneLast": "At least one zone must remain.",
  "op.map.plan.edit.seatZoneLabel": "Zone",
```

- [ ] **Step 3: Add keys to `locales/tg.json`** (same position, Tajik — real translation):

```json
  "op.map.plan.edit.zonesPanelTitle": "Минтақаҳо",
  "op.map.plan.edit.addZone": "Илова кардани минтақа",
  "op.map.plan.edit.newZoneName": "Минтақаи нав",
  "op.map.plan.edit.zoneInspectorTitle": "Хосиятҳои минтақа",
  "op.map.plan.edit.zoneNameLabel": "Ном",
  "op.map.plan.edit.zoneXLabel": "X (сутун)",
  "op.map.plan.edit.zoneYLabel": "Y (сатр)",
  "op.map.plan.edit.zoneWidthLabel": "Паҳноӣ",
  "op.map.plan.edit.zoneHeightLabel": "Баландӣ",
  "op.map.plan.edit.zoneColorLabel": "Ранг",
  "op.map.plan.edit.zoneColorNone": "Бе ранг",
  "op.map.plan.edit.deleteZone": "Нест кардани минтақа",
  "op.map.plan.edit.deleteZoneHasSeats": "Аввал ҷойҳоро аз ин минтақа кӯчонед.",
  "op.map.plan.edit.deleteZoneLast": "Камаш як минтақа бояд бимонад.",
  "op.map.plan.edit.seatZoneLabel": "Минтақа",
```

(`X`/`Y` letters are intentionally identical to ru — but the full strings `"X (сутун)"` vs `"X (столбец)"` differ, so parity passes.)

- [ ] **Step 4: Regenerate the message bundle**

Run: `cd packages/i18n && ~/.bun/bin/bun run gen`
Expected: `packages/i18n/src/messages.ts` updated with the new keys (do not edit it by hand).

- [ ] **Step 5: Run the i18n parity test**

Run: `cd packages/i18n && ~/.bun/bin/bun test`
Expected: PASS — key-set parity across locales, and no `tg === ru` violations.

- [ ] **Step 6: Commit**

```bash
git add locales/ru.json locales/en.json locales/tg.json packages/i18n/src/messages.ts
git commit -m "i18n(operator-map): zone-editor strings ru/en/tg (B2-3b)"
```

---

### Task 8: Full verification

**Files:** none (verification only).

- [ ] **Step 1: Typecheck + build the operator web workspace**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun run build`
Expected: `tsc -b && vite build` succeed with no errors. (Catches anything `bun test` missed — e.g. a `DraftZone` literal missing `serverId`, or a `MessageKey` typo.)

- [ ] **Step 2: Run the full operator test suite (App.test isolated)**

Run:
```bash
cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun test $(ls src/*.test.ts src/*.test.tsx | grep -v App.test) && ~/.bun/bin/bun test src/App.test.tsx
```
Expected: all green. App.test must still render the branch heading (the zone wiring touches the editor, not the app boot).

- [ ] **Step 3: Run the i18n workspace tests**

Run: `cd packages/i18n && ~/.bun/bin/bun test`
Expected: green (parity 35 or whatever the new count is).

- [ ] **Step 4: Final review pass**

Re-read the diff for: dead i18n keys (every added key referenced in code?), any `console.log`, any hard-coded user-facing string that should be a `MessageKey`, and confirm `toBulkUpdateRequest` still passes `walls` through unchanged (no wall regression).

---

## Self-Review (author)

- **Spec coverage:** create (Task 1 `addZone` + Task 6 `handleAddZone`), move/resize (Task 1 `setZoneGeometry` + Task 4 numeric fields), rename (Task 1/4), colour (Task 1/4), delete + guards (Task 1 `removeZone`/`zoneSeatCount` + Task 6 guard logic + Task 4 disabled-with-reason), seat reassignment (Task 1 `setSeatZone` + Task 5 dropdown), selected highlight (Task 2), new-zone serialization (Task 1 serializer + Task 6 test). Walls + zoneType + drag explicitly out of scope with rationale. ✔
- **Type consistency:** `DraftZone` gains `serverId` (Task 1) and every literal/`createDraft` updated; `FloorInspectorSeat` gains `zoneId` (Task 5) and the editor supplies it (Task 6); `selectedZoneId` prop added to `FloorPlan` (Task 2) and passed by the editor (Task 6). Mutation names are reused verbatim across Tasks 1 and 6. ✔
- **Placeholder scan:** every code step shows full code; commands have expected output. ✔
- **Backend safety:** zone deletion guarded against orphaned seats and last-zone removal; new zones carry `zoneId: null`; walls passed through unchanged. ✔
