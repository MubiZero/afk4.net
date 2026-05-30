# Club Floor-Map Editor (Карта зала) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the "Карта зала" placeholder tab in the redesigned Venue screen with a real zone/seat editor (add/rename/reorder/remove zones and seats) built on the new shadcn/ui primitives, saving via the existing `getFloorMap`/`updateFloorMap` contracts with ETag optimistic-concurrency and a conflict-reload path.

**Architecture:** A pure model module (`floorMapModel.ts`) maps the read model to editable zones/seats and builds the bulk-update request (porting the proven legacy `toEditorZones`/`compactZones` logic). A `useFloorMap` hook owns the editable state + ETag + save/reload, returning a discriminated-union state; `save()` returns `'ok' | 'conflict' | 'error'` so the screen toasts on success/failure and shows an inline banner on a 412/428 ETag conflict (preserving the user's edits). A presentational `FloorMapEditor` renders the editor and plugs into the Venue screen's Карта зала tab. Edit actions are gated by the `layout.manage` permission threaded from the session.

**Tech Stack:** React 19 + TypeScript, Vite, Vitest 4 + jsdom + @testing-library/react (`globals: false` → import `it`/`expect`/`vi`/`beforeEach` from `'vitest'` per test file), shadcn/ui primitives under `src/components/ui/`, Tailwind v4, i18n RU primary / EN secondary. npm cwd: `D:\afk4.net\src\AFK4.Platform.Web`. Path alias `@/` → `src/`. `App.tsx` uses RELATIVE imports.

---

## Scope

**In scope** (all on existing contracts — `GET`/`PUT /api/branches/{branchId}/floor-map`, no backend change):
- Zone editing: add, rename, reorder (move up/down), remove.
- Seat editing within a zone: add, rename, reorder (move up/down), remove.
- ETag optimistic-concurrency save: `If-Match` on PUT; on `412`/`428` show a conflict banner with a Reload action that re-fetches a fresh ETag (the user's staged edits are preserved until they choose to reload).
- Edit gating: read-only when the session lacks `layout.manage` (inputs disabled, mutate/save hidden, read-only note shown).
- Data-region states: loading skeleton, error+retry, authoritative empty (no zones yet).

**Out of scope / deferred:**
- Drag-and-drop reordering (use up/down buttons — accessible and testable; DnD is a future enhancement).
- Visual 2D canvas layout (coordinates/positions) — the contract is name/sortOrder only; a spatial editor is a separate effort.
- Live seat status (device online/session) — that's the live operator view, not the structural editor.
- Deleting `ClubDashboard`/`LegacyClubScreen` (still serves `clubInstall`; gated on Установка redesign).

**Conflict-UX note:** on a 412/428 the staged edits are intentionally NOT auto-discarded; the banner directs the user to Reload (which re-fetches and resets to server truth). This matches the proven legacy behavior and avoids silently losing work.

---

## File Structure

- `src/club/venue/floorMapModel.ts` — pure: `EditorZone`/`EditorSeat`, `toEditorZones`, `buildBulkRequest`, `moveByIndex`, `makeClientId`. (Task 2)
- `src/club/venue/useFloorMap.ts` — load + editable state + ETag + `save()`/`reload`. (Task 3)
- `src/club/venue/FloorMapEditor.tsx` — the editor UI. (Task 4)
- `src/club/venue/VenueScreen.tsx` — render the editor in the Карта зала tab; new `organizationId` + `canManageLayout` props. (Task 5)
- `src/App.tsx` — thread `organizationId` + `canManageLayout` into `VenueScreen`. (Task 6)
- `src/i18n/messages.ts` — `floor.*` keys (RU+EN); remove the now-unused `venue.map.soon`. (Task 1)

Colocated `*.test.ts(x)` for the model, hook, editor, and VenueScreen.

---

## Task 1: i18n keys for the floor-map editor

**Files:**
- Modify: `src/i18n/messages.ts` (both `ru` and `en`)
- Test: `src/i18n/messages.test.ts`

- [ ] **Step 1: Add the failing test**

In `src/i18n/messages.test.ts`, append:

```ts
it('includes the floor-map editor keys', () => {
  for (const key of [
    'floor.reload', 'floor.save', 'floor.addZone', 'floor.addSeat',
    'floor.zoneName', 'floor.seatName', 'floor.removeZone', 'floor.removeSeat',
    'floor.moveUp', 'floor.moveDown', 'floor.empty', 'floor.conflict',
    'floor.readonly', 'floor.zoneDefault', 'floor.seatDefault'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});
```

- [ ] **Step 2: Run it to confirm it fails**

Run: `npm test -- messages`
Expected: FAIL (keys missing).

- [ ] **Step 3: Edit the messages**

In `src/i18n/messages.ts`, in the `ru` object: find and DELETE the line `'venue.map.soon': 'Редактор карты зала появится в следующем обновлении.',`. Then add the floor keys right after the `'venue.tab.map': 'Карта зала',` line:

```ts
    'venue.tab.map': 'Карта зала',
    'floor.reload': 'Перезагрузить',
    'floor.save': 'Сохранить карту',
    'floor.addZone': 'Добавить зону',
    'floor.addSeat': 'Добавить место',
    'floor.zoneName': 'Название зоны',
    'floor.seatName': 'Название места',
    'floor.removeZone': 'Удалить зону',
    'floor.removeSeat': 'Удалить место',
    'floor.moveUp': 'Вверх',
    'floor.moveDown': 'Вниз',
    'floor.empty': 'Зоны и места ещё не созданы.',
    'floor.conflict': 'Карта изменилась в другом сеансе. Перезагрузите перед сохранением.',
    'floor.readonly': 'Недостаточно прав для редактирования карты зала.',
    'floor.zoneDefault': 'Зона',
    'floor.seatDefault': 'ПК',
```

In the `en` object: DELETE `'venue.map.soon': 'The floor-map editor arrives in a later update.',` and after `'venue.tab.map': 'Floor map',` add:

```ts
    'venue.tab.map': 'Floor map',
    'floor.reload': 'Reload',
    'floor.save': 'Save map',
    'floor.addZone': 'Add zone',
    'floor.addSeat': 'Add seat',
    'floor.zoneName': 'Zone name',
    'floor.seatName': 'Seat name',
    'floor.removeZone': 'Remove zone',
    'floor.removeSeat': 'Remove seat',
    'floor.moveUp': 'Move up',
    'floor.moveDown': 'Move down',
    'floor.empty': 'No zones or seats yet.',
    'floor.conflict': 'The floor map changed in another session. Reload before saving again.',
    'floor.readonly': 'You do not have permission to edit the floor map.',
    'floor.zoneDefault': 'Zone',
    'floor.seatDefault': 'PC',
```

- [ ] **Step 4: Run the tests**

Run: `npm test -- messages`
Expected: PASS (new test + ru/en parity).

- [ ] **Step 5: Commit**

```bash
git add src/i18n/messages.ts src/i18n/messages.test.ts
git commit -m "feat(club): add i18n keys for floor-map editor"
```

---

## Task 2: floorMapModel (pure)

**Files:**
- Create: `src/club/venue/floorMapModel.ts`
- Test: `src/club/venue/floorMapModel.test.ts`

- [ ] **Step 1: Write the failing test**

Create `src/club/venue/floorMapModel.test.ts`:

```ts
import { it, expect } from 'vitest';
import type { FloorMap } from '@/api/types';
import { toEditorZones, buildBulkRequest, moveByIndex, type EditorZone } from './floorMapModel';

const floorMap: FloorMap = {
  branchId: 'b1',
  branchName: 'Центр',
  zones: [
    { zoneId: 'z2', name: 'Zone B', sortOrder: 2 },
    { zoneId: 'z1', name: 'Zone A', sortOrder: 1 }
  ],
  seats: [
    { seatId: 's2', seatName: 'PC-2', zoneId: 'z1', zoneName: 'Zone A', sortOrder: 2, state: 'free', deviceId: null, deviceName: null, isDeviceOnline: null, isDeviceLocked: null, lastHeartbeatAtUtc: null, agentVersion: null, shellVersion: null, activeSessionId: null, remainingSeconds: null },
    { seatId: 's1', seatName: 'PC-1', zoneId: 'z1', zoneName: 'Zone A', sortOrder: 1, state: 'free', deviceId: null, deviceName: null, isDeviceOnline: null, isDeviceLocked: null, lastHeartbeatAtUtc: null, agentVersion: null, shellVersion: null, activeSessionId: null, remainingSeconds: null }
  ]
};

it('maps the read model to zones and seats ordered by sortOrder', () => {
  const zones = toEditorZones(floorMap);
  expect(zones.map(z => z.name)).toEqual(['Zone A', 'Zone B']);
  expect(zones[0].zoneId).toBe('z1');
  expect(zones[0].seats.map(s => s.name)).toEqual(['PC-1', 'PC-2']);
  expect(zones[1].seats).toEqual([]);
});

it('builds a bulk request: drops empty names, indexes sortOrder, links seats to their zone clientId', () => {
  const zones: EditorZone[] = [
    { clientId: 'cz1', zoneId: 'z1', name: ' Hall ', seats: [
      { clientId: 'cs1', seatId: 's1', name: 'PC-1' },
      { clientId: 'cs2', seatId: null, name: '  ' } // empty -> dropped
    ] },
    { clientId: 'cz2', zoneId: null, name: '  ' } as EditorZone // empty zone -> dropped
  ];
  const req = buildBulkRequest('org', zones);
  expect(req.organizationId).toBe('org');
  expect(req.zones).toEqual([{ zoneId: 'z1', clientId: 'cz1', name: 'Hall', sortOrder: 1 }]);
  expect(req.seats).toEqual([{ seatId: 's1', clientId: 'cs1', zoneClientId: 'cz1', name: 'PC-1', sortOrder: 1 }]);
});

it('moveByIndex swaps adjacent items and is a no-op at the edges', () => {
  expect(moveByIndex(['a', 'b', 'c'], 0, 1)).toEqual(['b', 'a', 'c']);
  expect(moveByIndex(['a', 'b', 'c'], 2, 1)).toEqual(['a', 'b', 'c']);
  expect(moveByIndex(['a', 'b', 'c'], 0, -1)).toEqual(['a', 'b', 'c']);
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `npm test -- floorMapModel`
Expected: FAIL (import not resolved).

- [ ] **Step 3: Write the implementation**

Create `src/club/venue/floorMapModel.ts`:

```ts
import type {
  FloorMap,
  FloorMapBulkSeatRequest,
  FloorMapBulkUpdateRequest,
  FloorMapBulkZoneRequest
} from '@/api/types';

export interface EditorSeat {
  clientId: string;
  seatId: string | null;
  name: string;
}

export interface EditorZone {
  clientId: string;
  zoneId: string | null;
  name: string;
  seats: EditorSeat[];
}

export function makeClientId(prefix: string): string {
  return `${prefix}-${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;
}

export function moveByIndex<T>(items: T[], index: number, direction: -1 | 1): T[] {
  const target = index + direction;
  if (target < 0 || target >= items.length) return items;
  const next = [...items];
  [next[index], next[target]] = [next[target], next[index]];
  return next;
}

export function toEditorZones(floorMap: FloorMap): EditorZone[] {
  const seatOrder = new Map<string, number>();
  const entries = new Map<string, { zone: EditorZone; sortOrder: number }>();

  for (const zone of floorMap.zones ?? []) {
    entries.set(zone.zoneId, {
      zone: { clientId: zone.zoneId, zoneId: zone.zoneId, name: zone.name, seats: [] },
      sortOrder: zone.sortOrder
    });
  }
  for (const seat of floorMap.seats) {
    let entry = entries.get(seat.zoneId);
    if (entry === undefined) {
      entry = {
        zone: { clientId: seat.zoneId, zoneId: seat.zoneId, name: seat.zoneName, seats: [] },
        sortOrder: entries.size + 1
      };
      entries.set(seat.zoneId, entry);
    }
    seatOrder.set(seat.seatId, seat.sortOrder);
    entry.zone.seats.push({ clientId: seat.seatId, seatId: seat.seatId, name: seat.seatName });
  }

  const ordered = Array.from(entries.values()).sort((a, b) => a.sortOrder - b.sortOrder);
  for (const e of ordered) {
    e.zone.seats.sort((s1, s2) => (seatOrder.get(s1.seatId ?? '') ?? 0) - (seatOrder.get(s2.seatId ?? '') ?? 0));
  }
  return ordered.map(e => e.zone);
}

export function buildBulkRequest(organizationId: string, zones: EditorZone[]): FloorMapBulkUpdateRequest {
  const compacted = zones.filter(zone => zone.name.trim().length > 0);
  return {
    organizationId,
    zones: compacted.map<FloorMapBulkZoneRequest>((zone, i) => ({
      zoneId: zone.zoneId,
      clientId: zone.clientId,
      name: zone.name.trim(),
      sortOrder: i + 1
    })),
    seats: compacted.flatMap<FloorMapBulkSeatRequest>(zone =>
      zone.seats
        .filter(seat => seat.name.trim().length > 0)
        .map((seat, j) => ({
          seatId: seat.seatId,
          clientId: seat.clientId,
          zoneClientId: zone.clientId,
          name: seat.name.trim(),
          sortOrder: j + 1
        }))
    )
  };
}
```

- [ ] **Step 4: Run the tests**

Run: `npm test -- floorMapModel`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/venue/floorMapModel.ts src/club/venue/floorMapModel.test.ts
git commit -m "feat(club): add floor-map editor model (zones/seats/bulk request)"
```

---

## Task 3: useFloorMap hook

**Files:**
- Create: `src/club/venue/useFloorMap.ts`
- Test: `src/club/venue/useFloorMap.test.ts`

`save()` returns `'ok' | 'conflict' | 'error'`. On success it triggers a reload (so server-assigned ids + fresh ETag replace the staged state). On a 412/428 it sets `conflict` (keeping the staged zones) and returns `'conflict'`. Any other failure returns `'error'`.

- [ ] **Step 1: Write the failing test**

Create `src/club/venue/useFloorMap.test.ts`:

```ts
import { it, expect, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import { PlatformApiError } from '@/api/platformApi';
import type { FloorMap } from '@/api/types';
import { useFloorMap } from './useFloorMap';

function floorMap(): FloorMap {
  return {
    branchId: 'b1', branchName: 'Центр',
    zones: [{ zoneId: 'z1', name: 'Zone A', sortOrder: 1 }],
    seats: [{ seatId: 's1', seatName: 'PC-1', zoneId: 'z1', zoneName: 'Zone A', sortOrder: 1, state: 'free', deviceId: null, deviceName: null, isDeviceOnline: null, isDeviceLocked: null, lastHeartbeatAtUtc: null, agentVersion: null, shellVersion: null, activeSessionId: null, remainingSeconds: null }]
  };
}

function client(overrides: Record<string, unknown> = {}) {
  return {
    getFloorMap: vi.fn(async () => ({ etag: 'etag-1', floorMap: floorMap() })),
    updateFloorMap: vi.fn(async () => ({ eTag: 'etag-2', zones: [], seats: [] })),
    ...overrides
  };
}

it('loads the floor map into editable zones with the branch name', async () => {
  const { result } = renderHook(() => useFloorMap(client() as never, 'b1', 'org'));
  await waitFor(() => expect(result.current.status).toBe('ready'));
  if (result.current.status !== 'ready') throw new Error('not ready');
  expect(result.current.branchName).toBe('Центр');
  expect(result.current.zones.map(z => z.name)).toEqual(['Zone A']);
});

it('save sends the bulk request with the current ETag and returns ok', async () => {
  const c = client();
  const { result } = renderHook(() => useFloorMap(c as never, 'b1', 'org'));
  await waitFor(() => expect(result.current.status).toBe('ready'));
  let outcome: string | undefined;
  await act(async () => { if (result.current.status === 'ready') outcome = await result.current.save(); });
  expect(outcome).toBe('ok');
  expect(c.updateFloorMap).toHaveBeenCalledWith('b1', expect.objectContaining({ organizationId: 'org' }), 'etag-1');
});

it('save surfaces a 412 as a conflict and keeps the staged zones', async () => {
  const c = client({ updateFloorMap: vi.fn(async () => { throw new PlatformApiError(412, 'stale'); }) });
  const { result } = renderHook(() => useFloorMap(c as never, 'b1', 'org'));
  await waitFor(() => expect(result.current.status).toBe('ready'));
  let outcome: string | undefined;
  await act(async () => { if (result.current.status === 'ready') outcome = await result.current.save(); });
  expect(outcome).toBe('conflict');
  await waitFor(() => expect(result.current.status === 'ready' && result.current.conflict).toBe(true));
});

it('save surfaces a non-precondition failure as an error', async () => {
  const c = client({ updateFloorMap: vi.fn(async () => { throw new PlatformApiError(409, 'busy'); }) });
  const { result } = renderHook(() => useFloorMap(c as never, 'b1', 'org'));
  await waitFor(() => expect(result.current.status).toBe('ready'));
  let outcome: string | undefined;
  await act(async () => { if (result.current.status === 'ready') outcome = await result.current.save(); });
  expect(outcome).toBe('error');
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `npm test -- useFloorMap`
Expected: FAIL (import not resolved).

- [ ] **Step 3: Write the implementation**

Create `src/club/venue/useFloorMap.ts`:

```ts
import { useCallback, useEffect, useRef, useState } from 'react';
import type { ClubApiClient } from '@/api/clubApi';
import { PlatformApiError } from '@/api/platformApi';
import { buildBulkRequest, toEditorZones, type EditorZone } from './floorMapModel';

type Loadable = Pick<ClubApiClient, 'getFloorMap' | 'updateFloorMap'>;

export type SaveOutcome = 'ok' | 'conflict' | 'error';

export type FloorMapState =
  | { status: 'loading'; retry: () => void }
  | { status: 'error'; retry: () => void }
  | {
      status: 'ready';
      branchName: string;
      zones: EditorZone[];
      setZones: (updater: (prev: EditorZone[]) => EditorZone[]) => void;
      saving: boolean;
      conflict: boolean;
      save: () => Promise<SaveOutcome>;
      reload: () => void;
    };

export function useFloorMap(client: Loadable, branchId: string, organizationId: string): FloorMapState {
  const [tick, setTick] = useState(0);
  const [phase, setPhase] = useState<'loading' | 'error' | 'ready'>('loading');
  const [branchName, setBranchName] = useState('');
  const [zones, setZones] = useState<EditorZone[]>([]);
  const [etag, setEtag] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [conflict, setConflict] = useState(false);

  const clientRef = useRef(client);
  clientRef.current = client;
  const zonesRef = useRef(zones);
  zonesRef.current = zones;
  const etagRef = useRef(etag);
  etagRef.current = etag;

  const reload = useCallback(() => setTick(t => t + 1), []);

  useEffect(() => {
    let cancelled = false;
    setPhase('loading');
    setConflict(false);
    clientRef.current.getFloorMap(branchId)
      .then(result => {
        if (cancelled) return;
        setBranchName(result.floorMap.branchName);
        setZones(toEditorZones(result.floorMap));
        setEtag(result.etag);
        setPhase('ready');
      })
      .catch(() => { if (!cancelled) setPhase('error'); });
    return () => { cancelled = true; };
  }, [branchId, tick]);

  const updateZones = useCallback((updater: (prev: EditorZone[]) => EditorZone[]) => {
    setZones(prev => updater(prev));
  }, []);

  const save = useCallback(async (): Promise<SaveOutcome> => {
    setSaving(true);
    setConflict(false);
    try {
      await clientRef.current.updateFloorMap(branchId, buildBulkRequest(organizationId, zonesRef.current), etagRef.current);
      setTick(t => t + 1); // reload server truth (new ids + fresh ETag)
      return 'ok';
    } catch (err) {
      if (err instanceof PlatformApiError && (err.status === 412 || err.status === 428)) {
        setConflict(true);
        return 'conflict';
      }
      return 'error';
    } finally {
      setSaving(false);
    }
  }, [branchId, organizationId]);

  if (phase === 'loading') return { status: 'loading', retry: reload };
  if (phase === 'error') return { status: 'error', retry: reload };
  return { status: 'ready', branchName, zones, setZones: updateZones, saving, conflict, save, reload };
}
```

- [ ] **Step 4: Run the tests**

Run: `npm test -- useFloorMap`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/venue/useFloorMap.ts src/club/venue/useFloorMap.test.ts
git commit -m "feat(club): add useFloorMap hook with ETag save and conflict state"
```

---

## Task 4: FloorMapEditor component

**Files:**
- Create: `src/club/venue/FloorMapEditor.tsx`
- Test: `src/club/venue/FloorMapEditor.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `src/club/venue/FloorMapEditor.test.tsx`:

```tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { PlatformApiError } from '@/api/platformApi';
import type { FloorMap } from '@/api/types';
import { FloorMapEditor } from './FloorMapEditor';

function floorMap(): FloorMap {
  return {
    branchId: 'b1', branchName: 'Центр',
    zones: [{ zoneId: 'z1', name: 'Зона A', sortOrder: 1 }],
    seats: [{ seatId: 's1', seatName: 'ПК-1', zoneId: 'z1', zoneName: 'Зона A', sortOrder: 1, state: 'free', deviceId: null, deviceName: null, isDeviceOnline: null, isDeviceLocked: null, lastHeartbeatAtUtc: null, agentVersion: null, shellVersion: null, activeSessionId: null, remainingSeconds: null }]
  };
}

function fakeClient(overrides: Record<string, unknown> = {}) {
  return {
    getFloorMap: vi.fn(async () => ({ etag: 'etag-1', floorMap: floorMap() })),
    updateFloorMap: vi.fn(async () => ({ eTag: 'etag-2', zones: [], seats: [] })),
    ...overrides
  };
}

function setup(client = fakeClient(), canEdit = true) {
  render(
    <I18nProvider><ToastProvider>
      <FloorMapEditor client={client as never} branchId="b1" organizationId="org" canEdit={canEdit} />
    </ToastProvider></I18nProvider>
  );
  return { client };
}

it('renders zones and seats from the loaded map', async () => {
  setup();
  expect(await screen.findByDisplayValue('Зона A')).toBeInTheDocument();
  expect(screen.getByDisplayValue('ПК-1')).toBeInTheDocument();
});

it('adds a zone', async () => {
  setup();
  await screen.findByDisplayValue('Зона A');
  fireEvent.click(screen.getByRole('button', { name: 'Добавить зону' }));
  // a second zone-name input appears (default name starts with "Зона")
  await waitFor(() => expect(screen.getAllByLabelText('Название зоны').length).toBe(2));
});

it('saves via updateFloorMap and toasts success', async () => {
  const { client } = setup();
  await screen.findByDisplayValue('Зона A');
  fireEvent.click(screen.getByRole('button', { name: 'Сохранить карту' }));
  await waitFor(() => expect(client.updateFloorMap).toHaveBeenCalledWith('b1', expect.objectContaining({ organizationId: 'org' }), 'etag-1'));
});

it('shows the conflict banner on a 412', async () => {
  const client = fakeClient({ updateFloorMap: vi.fn(async () => { throw new PlatformApiError(412, 'stale'); }) });
  setup(client);
  await screen.findByDisplayValue('Зона A');
  fireEvent.click(screen.getByRole('button', { name: 'Сохранить карту' }));
  expect(await screen.findByText('Карта изменилась в другом сеансе. Перезагрузите перед сохранением.')).toBeInTheDocument();
});

it('is read-only without the edit permission', async () => {
  setup(fakeClient(), false);
  await screen.findByDisplayValue('Зона A');
  expect(screen.queryByRole('button', { name: 'Сохранить карту' })).not.toBeInTheDocument();
  expect(screen.queryByRole('button', { name: 'Добавить зону' })).not.toBeInTheDocument();
  expect(screen.getByDisplayValue('Зона A')).toBeDisabled();
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `npm test -- FloorMapEditor`
Expected: FAIL (import not resolved).

- [ ] **Step 3: Write the implementation**

Create `src/club/venue/FloorMapEditor.tsx`:

```tsx
import { ArrowDown, ArrowUp, Trash2 } from 'lucide-react';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import { useFloorMap } from './useFloorMap';
import { makeClientId, moveByIndex, type EditorSeat, type EditorZone } from './floorMapModel';

type Client = Pick<ClubApiClient, 'getFloorMap' | 'updateFloorMap'>;

export function FloorMapEditor({ client, branchId, organizationId, canEdit }: {
  client: Client;
  branchId: string;
  organizationId: string;
  canEdit: boolean;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  const state = useFloorMap(client, branchId, organizationId);

  if (state.status === 'loading') return <LoadingCards count={2} />;
  if (state.status === 'error') return <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />;

  const { zones, setZones, saving, conflict, save, reload, branchName } = state;

  const renameZone = (clientId: string, name: string) =>
    setZones(prev => prev.map(z => (z.clientId === clientId ? { ...z, name } : z)));
  const removeZone = (clientId: string) =>
    setZones(prev => prev.filter(z => z.clientId !== clientId));
  const moveZone = (index: number, direction: -1 | 1) =>
    setZones(prev => moveByIndex(prev, index, direction));
  const addZone = () =>
    setZones(prev => [...prev, { clientId: makeClientId('zone'), zoneId: null, name: `${t('floor.zoneDefault')} ${prev.length + 1}`, seats: [] }]);

  const addSeat = (zoneClientId: string) =>
    setZones(prev => prev.map(z => (z.clientId === zoneClientId
      ? { ...z, seats: [...z.seats, { clientId: makeClientId('seat'), seatId: null, name: `${t('floor.seatDefault')}-${z.seats.length + 1}` }] }
      : z)));
  const renameSeat = (zoneClientId: string, seatClientId: string, name: string) =>
    setZones(prev => prev.map(z => (z.clientId === zoneClientId
      ? { ...z, seats: z.seats.map(s => (s.clientId === seatClientId ? { ...s, name } : s)) }
      : z)));
  const removeSeat = (zoneClientId: string, seatClientId: string) =>
    setZones(prev => prev.map(z => (z.clientId === zoneClientId
      ? { ...z, seats: z.seats.filter(s => s.clientId !== seatClientId) }
      : z)));
  const moveSeat = (zoneClientId: string, index: number, direction: -1 | 1) =>
    setZones(prev => prev.map(z => (z.clientId === zoneClientId ? { ...z, seats: moveByIndex(z.seats, index, direction) } : z)));

  async function onSave() {
    const outcome = await save();
    if (outcome === 'ok') toast({ title: t('toast.saved'), variant: 'success' });
    else if (outcome === 'error') toast({ title: t('toast.failed'), variant: 'error' });
    // 'conflict' surfaces as the inline banner below
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-semibold">{branchName}</h2>
        <div className="flex gap-2">
          <Button variant="outline" disabled={saving} onClick={reload}>{t('floor.reload')}</Button>
          {canEdit && <Button disabled={saving} onClick={() => void onSave()}>{t('floor.save')}</Button>}
        </div>
      </div>

      {conflict && (
        <Card><CardContent className="py-3 text-sm text-destructive">{t('floor.conflict')}</CardContent></Card>
      )}
      {!canEdit && (
        <p className="text-sm text-muted-foreground">{t('floor.readonly')}</p>
      )}

      {zones.length === 0 ? (
        <EmptyState message={t('floor.empty')} />
      ) : (
        <div className="flex flex-col gap-4">
          {zones.map((zone, zoneIndex) => (
            <ZoneCard
              key={zone.clientId}
              zone={zone}
              zoneIndex={zoneIndex}
              zoneCount={zones.length}
              canEdit={canEdit}
              onRenameZone={renameZone}
              onRemoveZone={removeZone}
              onMoveZone={moveZone}
              onAddSeat={addSeat}
              onRenameSeat={renameSeat}
              onRemoveSeat={removeSeat}
              onMoveSeat={moveSeat}
            />
          ))}
        </div>
      )}

      {canEdit && (
        <div>
          <Button variant="outline" onClick={addZone}>{t('floor.addZone')}</Button>
        </div>
      )}
    </div>
  );
}

function ZoneCard({ zone, zoneIndex, zoneCount, canEdit, onRenameZone, onRemoveZone, onMoveZone, onAddSeat, onRenameSeat, onRemoveSeat, onMoveSeat }: {
  zone: EditorZone;
  zoneIndex: number;
  zoneCount: number;
  canEdit: boolean;
  onRenameZone: (clientId: string, name: string) => void;
  onRemoveZone: (clientId: string) => void;
  onMoveZone: (index: number, direction: -1 | 1) => void;
  onAddSeat: (zoneClientId: string) => void;
  onRenameSeat: (zoneClientId: string, seatClientId: string, name: string) => void;
  onRemoveSeat: (zoneClientId: string, seatClientId: string) => void;
  onMoveSeat: (zoneClientId: string, index: number, direction: -1 | 1) => void;
}) {
  const { t } = useI18n();
  return (
    <Card>
      <CardHeader className="flex flex-row items-center gap-2">
        <Input aria-label={t('floor.zoneName')} value={zone.name} disabled={!canEdit}
          onChange={e => onRenameZone(zone.clientId, e.target.value)} />
        {canEdit && (
          <>
            <Button variant="ghost" size="icon" aria-label={t('floor.moveUp')} disabled={zoneIndex === 0} onClick={() => onMoveZone(zoneIndex, -1)}><ArrowUp className="size-4" /></Button>
            <Button variant="ghost" size="icon" aria-label={t('floor.moveDown')} disabled={zoneIndex === zoneCount - 1} onClick={() => onMoveZone(zoneIndex, 1)}><ArrowDown className="size-4" /></Button>
            <Button variant="ghost" size="icon" aria-label={t('floor.removeZone')} onClick={() => onRemoveZone(zone.clientId)}><Trash2 className="size-4" /></Button>
          </>
        )}
      </CardHeader>
      <CardContent className="flex flex-col gap-2">
        {zone.seats.map((seat: EditorSeat, seatIndex) => (
          <div key={seat.clientId} className="flex items-center gap-2">
            <Input aria-label={t('floor.seatName')} value={seat.name} disabled={!canEdit}
              onChange={e => onRenameSeat(zone.clientId, seat.clientId, e.target.value)} />
            {canEdit && (
              <>
                <Button variant="ghost" size="icon" aria-label={t('floor.moveUp')} disabled={seatIndex === 0} onClick={() => onMoveSeat(zone.clientId, seatIndex, -1)}><ArrowUp className="size-4" /></Button>
                <Button variant="ghost" size="icon" aria-label={t('floor.moveDown')} disabled={seatIndex === zone.seats.length - 1} onClick={() => onMoveSeat(zone.clientId, seatIndex, 1)}><ArrowDown className="size-4" /></Button>
                <Button variant="ghost" size="icon" aria-label={t('floor.removeSeat')} onClick={() => onRemoveSeat(zone.clientId, seat.clientId)}><Trash2 className="size-4" /></Button>
              </>
            )}
          </div>
        ))}
        {canEdit && (
          <div><Button variant="outline" size="sm" onClick={() => onAddSeat(zone.clientId)}>{t('floor.addSeat')}</Button></div>
        )}
      </CardContent>
    </Card>
  );
}
```

Note on primitives: this uses `Button` with `variant="ghost"`/`size="icon"`/`size="sm"` and `lucide-react` icons (both already used across the codebase). If the `Button` component does not support a `size="icon"` or `variant="ghost"` value, STOP and report BLOCKED with the supported variants/sizes (check `src/components/ui/button.tsx`) — pick the nearest supported values rather than inventing new ones; do not change the test's accessible names (the `aria-label`s).

- [ ] **Step 4: Run the tests**

Run: `npm test -- FloorMapEditor`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/venue/FloorMapEditor.tsx src/club/venue/FloorMapEditor.test.tsx
git commit -m "feat(club): add FloorMapEditor (zones/seats edit, reorder, ETag save)"
```

---

## Task 5: Wire the editor into VenueScreen

**Files:**
- Modify: `src/club/venue/VenueScreen.tsx`
- Test: `src/club/venue/VenueScreen.test.tsx`

- [ ] **Step 1: Read the current VenueScreen and its test**

Read `src/club/venue/VenueScreen.tsx` and `src/club/venue/VenueScreen.test.tsx` to confirm exact current text (props, the `value="map"` TabsContent placeholder, the fake client in the test).

- [ ] **Step 2: Update the VenueScreen test**

In `src/club/venue/VenueScreen.test.tsx`: (a) ensure the fake client also has `updateFloorMap: vi.fn(async () => ({ eTag: 'e2', zones: [], seats: [] }))` (it already has `getFloorMap`); (b) update the render to pass the new props `organizationId="org"` and `canManageLayout`; (c) add a test that switching to the map tab renders the editor. Concretely, update the render helper to:

```tsx
render(
  <I18nProvider><ToastProvider>
    <VenueScreen client={client as never} branchId="b1" organizationId="org" canManageLayout />
  </ToastProvider></I18nProvider>
);
```

and add:

```tsx
it('renders the floor-map editor in the map tab', async () => {
  setup();
  const mapTab = await screen.findByRole('tab', { name: 'Карта зала' });
  fireEvent.mouseDown(mapTab);
  fireEvent.click(mapTab);
  expect(await screen.findByRole('button', { name: 'Добавить зону' })).toBeInTheDocument();
});
```

(If the test file's `setup`/render helper or imports differ, adapt to its actual shape; ensure `I18nProvider`, `ToastProvider`, `screen`, `fireEvent`, `waitFor` are imported. The `fireEvent.mouseDown` before `click` is the accepted Radix-Tabs-in-jsdom nuance.)

- [ ] **Step 3: Run the test to see the new test fail**

Run: `npm test -- VenueScreen`
Expected: FAIL (VenueScreen doesn't accept the new props / map tab still shows placeholder).

- [ ] **Step 4: Update VenueScreen**

In `src/club/venue/VenueScreen.tsx`:
- Add the import: `import { FloorMapEditor } from './FloorMapEditor';`
- Change the component signature to accept the new props:
  ```tsx
  export function VenueScreen({ client, branchId, organizationId, canManageLayout }: { client: ClubApiClient; branchId: string; organizationId: string; canManageLayout: boolean }) {
  ```
- Replace the map TabsContent body:
  ```tsx
  <TabsContent value="map">
    <p className="text-sm text-muted-foreground">{t('venue.map.soon')}</p>
  </TabsContent>
  ```
  with:
  ```tsx
  <TabsContent value="map">
    <FloorMapEditor client={client} branchId={branchId} organizationId={organizationId} canEdit={canManageLayout} />
  </TabsContent>
  ```

- [ ] **Step 5: Run the tests**

Run: `npm test -- VenueScreen`
Expected: PASS (existing tests + the new map-tab test).

- [ ] **Step 6: Commit**

```bash
git add src/club/venue/VenueScreen.tsx src/club/venue/VenueScreen.test.tsx
git commit -m "feat(club): render the floor-map editor in the Venue map tab"
```

---

## Task 6: Thread organizationId + canManageLayout from ClubArea

**Files:**
- Modify: `src/App.tsx` (the `ClubArea` component — the `VenueScreen` render)

- [ ] **Step 1: Update the VenueScreen render in ClubArea**

In `src/App.tsx`, find the `clubVenue` render branch inside `ClubArea`:

```tsx
      ) : route.kind === 'clubVenue' ? (
        <VenueScreen client={clubClient} branchId={activeBranchId} />
```

Replace it with:

```tsx
      ) : route.kind === 'clubVenue' ? (
        <VenueScreen
          client={clubClient}
          branchId={activeBranchId}
          organizationId={session.organizationId}
          canManageLayout={session.permissions.includes('layout.manage')}
        />
```

- [ ] **Step 2: Run the full suite**

Run: `npm test`
Expected: all pass (the type change to `VenueScreen` props is satisfied by this call site; no other call sites exist).

- [ ] **Step 3: Run the build**

Run: `npm run build`
Expected: clean `tsc -b && vite build` (no type errors).

- [ ] **Step 4: Commit**

```bash
git add src/App.tsx
git commit -m "feat(club): pass org + layout permission into VenueScreen"
```

---

## Task 7: Full suite + build gate

**Files:** none (verification only)

- [ ] **Step 1: Run the whole suite**

Run: `npm test`
Expected: all test files pass (this plan adds ~4 files and ~16 tests over the post-merge baseline of 50 files / 159 tests).

- [ ] **Step 2: Run the build**

Run: `npm run build`
Expected: `tsc -b && vite build` complete, no errors.

- [ ] **Step 3: Commit (only if anything is uncommitted)**

```bash
git add -A
git commit -m "chore(club): floor-map editor green on suite and build"
```

If nothing is uncommitted, skip.

---

## Self-Review

**Spec coverage** (design spec: "Карта зала — zone/seat editor (add/rename/reorder/remove zones and seats), ETag-based optimistic-concurrency save with conflict reload, on the existing getFloorMap / updateFloorMap contracts"):
- add/rename/reorder/remove zones → Task 4 (`addZone`/rename input/`moveZone`/`removeZone`). ✓
- add/rename/reorder/remove seats → Task 4 (`addSeat`/rename input/`moveSeat`/`removeSeat`). ✓
- ETag optimistic-concurrency save → Task 3 (`If-Match` via `updateFloorMap(..., etag)`), Task 2 (`buildBulkRequest`). ✓
- conflict reload → Task 3 (`conflict` on 412/428, `reload`), Task 4 (banner + Reload button, edits preserved). ✓
- existing contracts only, no backend change → all wrappers already exist. ✓
- data-region states + role gating → Task 4 (loading/error/empty; `canEdit` read-only). ✓
- plugged into the redesigned Venue tab → Tasks 5-6. ✓

**Placeholder scan:** no TBD/"handle edge cases"; every code step is complete. The `venue.map.soon` key is removed (both locales) since the tab now renders the editor.

**Type consistency:** `EditorZone`/`EditorSeat` defined in Task 2, consumed unchanged in Tasks 3-4. `useFloorMap(client, branchId, organizationId)` → `FloorMapState` with `save(): Promise<'ok'|'conflict'|'error'>`, `zones`, `setZones`, `conflict`, `saving`, `reload`, `branchName` — consumed verbatim in Task 4. `FloorMapEditor` props `{ client, branchId, organizationId, canEdit }` match Task 5's render. `VenueScreen` props gain `organizationId`/`canManageLayout`, satisfied by Task 6's call site. Bulk request shape (`FloorMapBulkUpdateRequest`/`...ZoneRequest`/`...SeatRequest`) and the `eTag` response field match `src/api/types.ts`. The 412/428 → conflict branch matches the backend's `PreconditionFailed`/`PreconditionRequired` statuses; PUT uses `If-Match` from the GET ETag.

---

## Execution Handoff

Two execution options:

1. **Subagent-Driven (recommended)** — fresh subagent per task, two-stage review between tasks.
2. **Inline Execution** — execute tasks in this session with checkpoints.
