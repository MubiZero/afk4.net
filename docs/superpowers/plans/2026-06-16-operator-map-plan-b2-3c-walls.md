# Operator «Карта» B2-3c — Wall drawing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development or executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Let a floor manager draw and delete walls in the «План» editor — the last tail of B2; after this the «Карта» Stage 1 epic is fully closed.

**Architecture:** Walls are anonymous line segments `{x1,y1,x2,y2}` in grid-NODE coordinates (cell corners). The draft already carries `walls` and `toBulkUpdateRequest` already serializes them; `planModelFromDraft` already renders them. This adds: two draft mutations (`addWall`/`removeWall` by index), a third pointer "tool" in `FloorPlan` (`tool='wall'`: click-click on nodes to draw, with a live preview line; click an existing wall to delete it), and an editor toggle to enter/leave wall mode. Wall positioning reuses the SAME node math as seat drag (ignores pan, like seat drag does — consistency over partial correctness).

**Tech Stack:** React + TS (Vite), bun test + @testing-library/react (happy-dom), @afk4/i18n.

---

## Scope
**In:** draw wall (click start node → click end node, snap to grid nodes, live preview), delete wall (click it in wall mode), wall-mode toggle. **Out:** wall thickness/style editing, curved walls, drag-to-move a wall endpoint (YAGNI).

Backend: `PUT /floor-map` wipes+recreates all walls each save (full replace). Walls have no server id — the editor sends the whole `walls` array; nothing to guard.

## Files
- Modify `src/AFK4.Operator.App.Web/src/floorPlanDraft.ts` — `addWall`, `removeWall`.
- Modify `src/AFK4.Operator.App.Web/src/FloorPlan.tsx` — wall tool.
- Modify `src/AFK4.Operator.App.Web/src/FloorPlanEditor.tsx` — `tool` state + toggle + wiring + hint.
- Modify `locales/{ru,en,tg}.json` + regen `packages/i18n/src/messages.ts`.
- Modify `src/AFK4.Operator.App.Web/src/styles.css` — wall hit/preview/node + wall-mode cursor + active toggle.
- Tests: `floorPlanDraft.test.ts`, `FloorPlan.test.tsx`, `FloorPlanEditor.test.tsx`.

### Env (reminder)
`bun` at `~/.bun/bin/bun`; `bun test` does NOT typecheck → also `~/.bun/bin/bun run build`. i18n sources are repo-root `locales/*.json`; regen via `cd packages/i18n && ~/.bun/bin/bun run gen` (never hand-edit messages.ts). Operator test cmd keeps App.test in its own bun invocation.

---

### Task 1: Draft — addWall / removeWall

```typescript
export function addWall(draft: PlanDraft, x1: number, y1: number, x2: number, y2: number): PlanDraft {
  return { ...draft, isDirty: true, walls: [...draft.walls, { x1, y1, x2, y2 }] };
}
export function removeWall(draft: PlanDraft, index: number): PlanDraft {
  return { ...draft, isDirty: true, walls: draft.walls.filter((_, i) => i !== index) };
}
```
Index matches `planModelFromDraft`'s `draft-wall-${index}` id scheme. Tests: addWall appends + sets dirty; removeWall drops the indexed segment + sets dirty. Run `bun test src/floorPlanDraft.test.ts`, then `bun run build`. Commit.

### Task 2: FloorPlan — wall tool

New props (after `selectedZoneId`): `tool?: 'select' | 'wall'` (default `'select'`), `onAddWall?: (x1,y1,x2,y2) => void`, `onDeleteWall?: (index: number) => void`.

State: `const [pendingNode, setPendingNode] = useState<{x:number;y:number}|null>(null); const [cursorNode, setCursorNode] = useState<{x:number;y:number}|null>(null);`

Reset when leaving wall mode: `useEffect(() => { if (tool !== 'wall') { setPendingNode(null); setCursorNode(null); } }, [tool]);`

Node-from-event helper (render scope; mirrors seat-drag `computeTargetCell`, ignores pan for consistency):
```typescript
const nodeFromEvent = (clientX: number, clientY: number) => {
  const rect = viewportRef.current?.getBoundingClientRect() ?? { left: 0, top: 0 };
  const canvasX = (clientX - rect.left) / scale;
  const canvasY = (clientY - rect.top) / scale;
  return { x: pxToCell(canvasX - CANVAS_PADDING, cell) + originX, y: pxToCell(canvasY - CANVAS_PADDING, cell) + originY };
};
```

`onPointerDown`: branch at the very top —
```typescript
  if (mode === 'edit' && tool === 'wall') {
    const node = nodeFromEvent(event.clientX, event.clientY);
    if (pendingNode === null) {
      setPendingNode(node);
    } else {
      if (node.x !== pendingNode.x || node.y !== pendingNode.y) {
        onAddWall?.(pendingNode.x, pendingNode.y, node.x, node.y);
      }
      setPendingNode(null);
    }
    return;
  }
```
(then the existing pan logic). `onPointerMove`: in wall mode update `setCursorNode(nodeFromEvent(...))`; else existing pan branch.

Render (inside the `<svg>`, after the existing `model.walls.map(...)`): when `mode==='edit' && tool==='wall'`, render per wall a transparent wide hit line that deletes on pointerdown (stopPropagation so it doesn't place a node):
```tsx
{mode === 'edit' && tool === 'wall' && model.walls.map((wall, index) => (
  <line
    key={`hit-${wall.id}`}
    className="floor-plan-wall-hit"
    x1={px(wall.x1)} y1={py(wall.y1)} x2={px(wall.x2)} y2={py(wall.y2)}
    onPointerDown={(event) => { event.stopPropagation(); onDeleteWall?.(index); }}
  />
))}
{pendingNode && cursorNode && (
  <line className="floor-plan-wall-preview" x1={px(pendingNode.x)} y1={py(pendingNode.y)} x2={px(cursorNode.x)} y2={py(cursorNode.y)} />
)}
{pendingNode && <circle className="floor-plan-wall-node" cx={px(pendingNode.x)} cy={py(pendingNode.y)} r={5} />}
```
Add `tool === 'wall' ? 'floor-plan-viewport floor-plan-viewport--wall' : 'floor-plan-viewport'` to the viewport className (crosshair cursor).

CSS (styles.css, near `.floor-plan-wall`):
```css
.floor-plan-wall-hit { stroke: transparent; stroke-width: 14; cursor: pointer; pointer-events: stroke; }
.floor-plan-wall-preview { stroke: var(--accent); stroke-width: 3; stroke-dasharray: 6 4; stroke-linecap: round; pointer-events: none; }
.floor-plan-wall-node { fill: var(--accent); pointer-events: none; }
.floor-plan-viewport--wall { cursor: crosshair; }
```

Tests (FloorPlan.test.tsx, reuse the file's render + an edit-mode model with bbox minX0/minY0):
- Two `fireEvent.pointerDown` on the viewport at clientX/Y mapping to nodes (scale 1, padding 32, cell 56): (32,32)→node(0,0), (144,32)→node(2,0) ⇒ `onAddWall` called with `(0,0,2,0)`.
- Render a model with one wall + `tool='wall'`; `fireEvent.pointerDown` on `container.querySelector('.floor-plan-wall-hit')` ⇒ `onDeleteWall` called with `0`.

Run `bun test src/FloorPlan.test.tsx`, then `bun run build`. Commit.

### Task 3: Editor — wall-mode toggle + wiring

In `FloorPlanEditor.tsx`: `const [tool, setTool] = useState<'select' | 'wall'>('select');`
- Editor bar: a toggle button `{t('op.map.plan.edit.wallTool')}` with `className={tool === 'wall' ? 'active' : undefined}`, onClick toggles tool; entering wall mode clears selection (`setSelectedSeatId(''); setSelectedZoneId('');`).
- When `tool==='wall'`, show a hint `<p className="floor-plan-editor-hint">{t('op.map.plan.edit.wallHint')}</p>` and HIDE the palette/zone-panel/inspectors (drawing, not arranging) — wrap the existing `floor-plan-editor-body` children so that in wall mode only the `<FloorPlan>` shows (rail + inspector omitted), keeping the canvas full-width is optional; simplest: keep layout, just don't render inspectors while drawing. Minimal acceptable: keep the rail, always render FloorPlan, and pass the tool through.
- Pass to FloorPlan: `tool={tool}`, `onAddWall={(x1,y1,x2,y2) => setDraft((c) => addWall(c, x1, y1, x2, y2))}`, `onDeleteWall={(i) => setDraft((c) => removeWall(c, i))}`. Import `addWall, removeWall`.

Test (FloorPlanEditor.test.tsx): click the wall-tool button → the hint text appears; clicking it again → hint gone. (Drawing itself is covered at the FloorPlan unit level.)

Run `bun test src/FloorPlanEditor.test.tsx`, then `bun run build`. Commit.

### Task 4: i18n
Add to ru/en/tg after `op.map.plan.edit.seatZoneLabel`:
- `op.map.plan.edit.wallTool` — ru "Стены" / en "Walls" / tg "Деворҳо"
- `op.map.plan.edit.wallHint` — ru "Кликайте по узлам сетки, чтобы провести стену. Клик по стене — удалить." / en "Click grid nodes to draw a wall. Click a wall to delete it." / tg "Барои кашидани девор ба гиреҳҳои тӯр клик кунед. Клик ба девор — нест кардан."
Regen, run `cd packages/i18n && ~/.bun/bin/bun test` (parity). Commit.

### Task 5: Verify
`cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun run build` (green) → full operator suite (App.test isolated) → `cd packages/i18n && ~/.bun/bin/bun test`. Diff review: no dead keys, walls still pass through `toBulkUpdateRequest`, no console.log.

## Self-Review
- Coverage: draw (Task 1 addWall + Task 2 click-click), delete (Task 1 removeWall + Task 2 hit line), toggle (Task 3). ✔
- Type consistency: `addWall`/`removeWall` reused verbatim in Task 3; `tool`/`onAddWall`/`onDeleteWall` prop names consistent FloorPlan↔editor. ✔
- Node math reuses seat-drag formula (pan-agnostic) for behavioural consistency. ✔
