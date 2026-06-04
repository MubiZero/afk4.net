# Customer Shell — Unit 3: Operator Web Top-up Surface

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Surface **pending player top-up requests** to the floor operator inside the React operator web app (`src/AFK4.Platform.Web`) and let the operator confirm one with a single action. When a player asks for money in the shell/PWA, a `pending` `PaymentIntent` is created; this plan adds the operator-facing list + a confirm button that calls the existing `POST /api/wallet/top-up-intents/{id}/fulfil` (which runs the audited, idempotent `TopUpWalletAsync`). No balance moves until the operator confirms.

**Architecture:** Mirror the existing operator-mutation pattern already used by `WalletPanel` / `AmountReasonDialog`:
- A **pure view-model module** (`pendingTopUpsModel.ts`) maps the API DTO array into a render-ready shape (amount in major units, formatted-ready fields). Tested in isolation with `bun:test`, no React — mirrors `floorMapModel.ts` / `moneyOpsModel.ts`.
- A **data hook** (`usePendingTopUps.ts`) loads + exposes `retry`, mirroring `useDevices.ts` (loading/error/ready discriminated union + `tick` refresh).
- A **panel component** (`PendingTopUpsPanel.tsx`) renders each pending request as a row "Игрок просит +N TJS" with the player name, and a confirm button that calls `fulfil`, shows optimistic disabled state + success/error toast (`useToast`), then refreshes the list — mirroring `AmountReasonDialog`'s submit/try/catch/toast flow.
- Wired into `VenueScreen` as a new tab that appears only when there are pending requests and the operator holds the top-up permission (`billing.wallet.top_up`), exactly like the existing `pending` devices tab.
- The **API client method** `listPendingTopUps(branchId)` is added to `ClubApiClient`, consuming the Unit 1 endpoint `GET /api/branches/{branchId}/wallet/top-up-intents?status=pending`, plus `fulfilTopUpIntent(intentId)` for `POST /api/wallet/top-up-intents/{intentId}/fulfil`.

**Tech Stack:** Vite + React 19 + TypeScript (strict, `tsc -b` enforces message-key parity and type safety) + Tailwind 4 + shadcn-style UI primitives. Tests run on **bun** with `@testing-library/react` (`bun:test` `it/expect/mock`, `render/screen/fireEvent/waitFor`). bun is at `/home/fedya/.bun/bin/bun` (NOT on PATH — always call by full path). The app is a workspace member; i18n lives in the shared `@afk4/i18n` package (catalog source `locales/{ru,en,tg}.json` → generated `packages/i18n/src/messages.ts` via `bun run gen`). Money: backend is `long` minor units; convert at the UI boundary with `@afk4/money` `minorToMajor`, format with the i18n `formatCurrency`.

**Money & units:** The DTO carries `amountMinorUnits: number` + `currencyCode`. The pure model converts to major units with `minorToMajor`; the component formats with `formatCurrency(major, currency)`. Never format minor units directly.

---

## Dependency on Unit 1 (verified 2026-06-03)

The operator confirm endpoint **already exists**: `POST /api/wallet/top-up-intents/{intentId:guid}/fulfil` (`Program.cs` line 1056; perm `TopUpWallet` = `"billing.wallet.top_up"`; org-scoped; idempotent via `State == "fulfilled"` guard; returns `PlayerTopUpIntentDto`).

The operator **list** endpoint `GET /api/branches/{branchId}/wallet/top-up-intents?status=pending` is **NOT yet in `Program.cs`** (grep confirms only the player-facing `GET /api/me/wallet/top-up-intents` and the fulfil endpoint exist). It is owned by the **Unit 1 plan**. This plan **consumes** that contract. The frontend tests here mock the client, so they do not block on the live endpoint — but the **Verification gate's manual smoke check** and real-world function depend on Unit 1 landing first.

**DTO shape decision (operator-facing, may differ from the player DTO):** the player-facing `PlayerTopUpIntentDto` does **not** carry who or where the request came from, which the operator needs. This plan targets an **operator list DTO** that Unit 1 must return for the list endpoint:

```
PendingTopUpDto {
  paymentIntentId: string;     // Guid as string
  playerAccountId: string;     // Guid as string
  displayName: string;         // player display name — operator must know WHO
  seatName: string | null;     // seat/PC the request came from, if known — operator must know WHERE
  amountMinorUnits: number;    // long minor units
  currencyCode: string;        // e.g. "TJS"
  createdAtUtc: string;        // ISO-8601
}
```

If Unit 1 ships only the bare `PlayerTopUpIntentDto` (no `displayName`/`seatName`), this plan's Task 1 DTO + model still compile (those fields are read defensively), but Task 2's "player name" assertion requires Unit 1 to include `displayName`. **Flag this back to Unit 1** so the list endpoint returns the shape above. The confirm endpoint returns the existing `PlayerTopUpIntentDto`; we only read `state` from it (success = `"fulfilled"`).

---

## File Structure

**New files:**
- `src/AFK4.Platform.Web/src/club/venue/pendingTopUpsModel.ts` — pure view-model mapper.
- `src/AFK4.Platform.Web/src/club/venue/pendingTopUpsModel.test.ts` — model tests (`bun:test`, no React).
- `src/AFK4.Platform.Web/src/club/venue/usePendingTopUps.ts` — data-loading hook.
- `src/AFK4.Platform.Web/src/club/venue/usePendingTopUps.test.tsx` — hook tests.
- `src/AFK4.Platform.Web/src/club/venue/PendingTopUpsPanel.tsx` — operator panel + confirm action.
- `src/AFK4.Platform.Web/src/club/venue/PendingTopUpsPanel.test.tsx` — component tests (RTL).

**Modified files:**
- `src/AFK4.Platform.Web/src/api/types.ts` — add `PendingTopUpDto` interface.
- `src/AFK4.Platform.Web/src/api/clubApi.ts` — add `listPendingTopUps` + `fulfilTopUpIntent` methods.
- `src/AFK4.Platform.Web/src/club/venue/VenueScreen.tsx` — add the pending-top-ups tab.
- `src/AFK4.Platform.Web/src/club/venue/VenueScreen.test.tsx` — assert the new tab renders.
- `locales/ru.json`, `locales/en.json`, `locales/tg.json` — new i18n keys (then regenerate `messages.ts`).

**Conventions to mirror (verified ground truth):**
- API method: `this.send<T>('GET'|'POST', path, body?)`; path-segments wrapped in `encodeURIComponent`. (See `clubApi.ts` `listPendingDevices`, `approveDevice`.)
- Hook: discriminated union `{ status: 'loading'|'error'|'ready'; ...; retry }`, `tick`/`clientRef` refresh, `Pick<ClubApiClient, ...>` narrow type. (See `useDevices.ts`.)
- Mutation component: `useToast()` + `useI18n()`; `submit()` sets `pending`, `try { await client.x(); toast({title:t('...'), variant:'success'}); onDone(); } catch { toast({title:t('...'), variant:'error'}); } finally { setPending(false); }`; button `disabled={pending}`. (See `AmountReasonDialog.tsx`.)
- Money: `minorToMajor` from `../money` (re-export of `@afk4/money`); `formatCurrency(major, currency)` from `useI18n()`.
- Tests: `import { render, screen, fireEvent, waitFor } from '@testing-library/react'`, `import { it, expect, mock } from 'bun:test'`, wrap in `<I18nProvider><ToastProvider>…</ToastProvider></I18nProvider>`, assert on Russian strings (default locale is `ru`). (See `AmountReasonDialog.test.tsx`, `WalletPanel.test.tsx`.)
- i18n: edit `locales/{ru,en,tg}.json`, then `cd packages/i18n && /home/fedya/.bun/bin/bun run gen` to regenerate `messages.ts`. `tsc -b` fails the build if a key exists in `ru` but is missing from `en`/`tg` usage paths, and `messages.test.ts` enforces key-set parity across locales.

---

## Task 1 — API DTO + client methods + pure model

**Files:**
- Modify: `src/AFK4.Platform.Web/src/api/types.ts`
- Modify: `src/AFK4.Platform.Web/src/api/clubApi.ts`
- Create: `src/AFK4.Platform.Web/src/club/venue/pendingTopUpsModel.ts`
- Test: `src/AFK4.Platform.Web/src/club/venue/pendingTopUpsModel.test.ts`

- [ ] **Step 1: Write the failing model test.**
  Create `src/AFK4.Platform.Web/src/club/venue/pendingTopUpsModel.test.ts`:
  ```ts
  import { it, expect } from 'bun:test';
  import type { PendingTopUpDto } from '@/api/types';
  import { buildPendingTopUps, type PendingTopUpRow } from './pendingTopUpsModel';

  const dtos: PendingTopUpDto[] = [
    {
      paymentIntentId: 'i2', playerAccountId: 'p2', displayName: 'Борис', seatName: 'PC-2',
      amountMinorUnits: 2500, currencyCode: 'TJS', createdAtUtc: '2026-06-03T10:05:00.000Z'
    },
    {
      paymentIntentId: 'i1', playerAccountId: 'p1', displayName: 'Анна', seatName: null,
      amountMinorUnits: 5000, currencyCode: 'TJS', createdAtUtc: '2026-06-03T10:00:00.000Z'
    }
  ];

  it('maps minor units to major and sorts oldest request first', () => {
    const rows: PendingTopUpRow[] = buildPendingTopUps(dtos);
    expect(rows.map(r => r.paymentIntentId)).toEqual(['i1', 'i2']);
    expect(rows[0].displayName).toBe('Анна');
    expect(rows[0].amountMajor).toBe(50);
    expect(rows[0].currencyCode).toBe('TJS');
    expect(rows[1].amountMajor).toBe(25);
  });

  it('falls back to a dash seat label when the seat is unknown', () => {
    const rows = buildPendingTopUps(dtos);
    const anna = rows.find(r => r.paymentIntentId === 'i1')!;
    expect(anna.seatLabel).toBe('—');
    const boris = rows.find(r => r.paymentIntentId === 'i2')!;
    expect(boris.seatLabel).toBe('PC-2');
  });
  ```

- [ ] **Step 2: Run the test — expect FAIL** (module + `PendingTopUpDto` do not exist yet):
  ```
  /home/fedya/.bun/bin/bun test src/AFK4.Platform.Web/src/club/venue/pendingTopUpsModel.test.ts
  ```
  Expected: fails to resolve `./pendingTopUpsModel` / `PendingTopUpDto`.

- [ ] **Step 3: Add the DTO and the model (minimal impl).**
  In `src/AFK4.Platform.Web/src/api/types.ts`, add near the other player/wallet DTOs:
  ```ts
  export interface PendingTopUpDto {
    paymentIntentId: string;
    playerAccountId: string;
    displayName: string;
    seatName: string | null;
    amountMinorUnits: number;
    currencyCode: string;
    createdAtUtc: string;
  }
  ```
  Create `src/AFK4.Platform.Web/src/club/venue/pendingTopUpsModel.ts`:
  ```ts
  import type { PendingTopUpDto } from '@/api/types';
  import { minorToMajor } from '../money';

  export interface PendingTopUpRow {
    paymentIntentId: string;
    playerAccountId: string;
    displayName: string;
    seatLabel: string;
    amountMajor: number;
    currencyCode: string;
    createdAtUtc: string;
  }

  /** Map operator pending top-up DTOs to render-ready rows, oldest request first. */
  export function buildPendingTopUps(dtos: PendingTopUpDto[]): PendingTopUpRow[] {
    return dtos
      .map<PendingTopUpRow>(d => ({
        paymentIntentId: d.paymentIntentId,
        playerAccountId: d.playerAccountId,
        displayName: d.displayName,
        seatLabel: d.seatName !== null && d.seatName.length > 0 ? d.seatName : '—',
        amountMajor: minorToMajor(d.amountMinorUnits),
        currencyCode: d.currencyCode,
        createdAtUtc: d.createdAtUtc
      }))
      .sort((a, b) => a.createdAtUtc.localeCompare(b.createdAtUtc));
  }
  ```

- [ ] **Step 4: Add the client methods (still part of this task).**
  In `src/AFK4.Platform.Web/src/api/clubApi.ts`: import `PendingTopUpDto` and `PlayerTopUpIntentDto`-shaped result in the type list (add a local type if `PlayerTopUpIntentDto` is not already exported from `types.ts` — define `FulfilledTopUpDto { paymentIntentId: string; state: string }` in `types.ts` for the fulfil response, since the client only reads `state`). Add the two methods next to `listPendingDevices`/`approveDevice`:
  ```ts
  public listPendingTopUps(branchId: string): Promise<PendingTopUpDto[]> {
    return this.send<PendingTopUpDto[]>(
      'GET',
      `/api/branches/${encodeURIComponent(branchId)}/wallet/top-up-intents?status=pending`
    );
  }

  public fulfilTopUpIntent(intentId: string): Promise<FulfilledTopUpDto> {
    return this.send<FulfilledTopUpDto>(
      'POST',
      `/api/wallet/top-up-intents/${encodeURIComponent(intentId)}/fulfil`
    );
  }
  ```
  Add to `types.ts`:
  ```ts
  export interface FulfilledTopUpDto {
    paymentIntentId: string;
    state: string;
  }
  ```

- [ ] **Step 5: Run the model test — expect PASS:**
  ```
  /home/fedya/.bun/bin/bun test src/AFK4.Platform.Web/src/club/venue/pendingTopUpsModel.test.ts
  ```
  Expected: 2 passing.

- [ ] **Step 6: Commit.**
  ```
  git add src/AFK4.Platform.Web/src/api/types.ts src/AFK4.Platform.Web/src/api/clubApi.ts src/AFK4.Platform.Web/src/club/venue/pendingTopUpsModel.ts src/AFK4.Platform.Web/src/club/venue/pendingTopUpsModel.test.ts
  git commit -m "feat(platform-web): pending top-up DTO, client methods, and pure model"
  ```

---

## Task 2 — i18n keys (ru/en/tg)

**Files:**
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json`
- Generated (do not hand-edit): `packages/i18n/src/messages.ts`

- [ ] **Step 1: Write/extend the failing key-parity test expectation.**
  `packages/i18n/src/messages.test.ts` already asserts that all three locales share the same key set. After adding keys to only one locale it will fail — that is the "failing test" guard. (No new test code needed; the existing parity test is the gate. If you prefer an explicit assertion, append to `messages.test.ts`:)
  ```ts
  it('exposes the operator pending top-up keys', () => {
    expect(messages.ru['venue.tab.topUps']).toBeDefined();
    expect(messages.en['venue.tab.topUps']).toBeDefined();
    expect(messages.tg['venue.tab.topUps']).toBeDefined();
  });
  ```

- [ ] **Step 2: Run the i18n tests — expect FAIL:**
  ```
  /home/fedya/.bun/bin/bun test packages/i18n/src/messages.test.ts
  ```
  Expected: the new assertion (or the parity test, once one locale is edited) fails — keys undefined.

- [ ] **Step 3: Add the keys to all three locale catalogs (minimal impl).**
  Add to `locales/ru.json`:
  ```json
  "venue.tab.topUps": "Пополнения",
  "topUps.empty": "Нет запросов на пополнение",
  "topUps.request": "Игрок просит",
  "topUps.player": "Игрок",
  "topUps.seat": "Место",
  "topUps.amount": "Сумма",
  "topUps.confirm": "Подтвердить",
  "topUps.confirming": "Подтверждаем…",
  "topUps.confirmed": "Пополнение подтверждено",
  "topUps.confirmError": "Не удалось подтвердить пополнение"
  ```
  Add to `locales/en.json` (same keys):
  ```json
  "venue.tab.topUps": "Top-ups",
  "topUps.empty": "No top-up requests",
  "topUps.request": "Player requests",
  "topUps.player": "Player",
  "topUps.seat": "Seat",
  "topUps.amount": "Amount",
  "topUps.confirm": "Confirm",
  "topUps.confirming": "Confirming…",
  "topUps.confirmed": "Top-up confirmed",
  "topUps.confirmError": "Could not confirm the top-up"
  ```
  Add to `locales/tg.json` — per the localization state, tg is currently a **ru STOPGAP** pending real Tajik, so copy the **ru** values verbatim (key parity is what `tsc -b` / the parity test enforce):
  ```json
  "venue.tab.topUps": "Пополнения",
  "topUps.empty": "Нет запросов на пополнение",
  "topUps.request": "Игрок просит",
  "topUps.player": "Игрок",
  "topUps.seat": "Место",
  "topUps.amount": "Сумма",
  "topUps.confirm": "Подтвердить",
  "topUps.confirming": "Подтверждаем…",
  "topUps.confirmed": "Пополнение подтверждено",
  "topUps.confirmError": "Не удалось подтвердить пополнение"
  ```

- [ ] **Step 4: Regenerate `messages.ts` and run the test — expect PASS:**
  ```
  /home/fedya/.bun/bin/bun --cwd packages/i18n run gen
  /home/fedya/.bun/bin/bun test packages/i18n/src/messages.test.ts
  ```
  Expected: parity + new assertion pass. (`--cwd` avoids `cd`; if your bun rejects `--cwd run`, run `/home/fedya/.bun/bin/bun packages/i18n/scripts/generate-messages.ts` instead.)

- [ ] **Step 5: Commit.**
  ```
  git add locales/ru.json locales/en.json locales/tg.json packages/i18n/src/messages.ts packages/i18n/src/messages.test.ts
  git commit -m "i18n(platform-web): operator pending top-up keys (ru/en, tg ru-stopgap)"
  ```

---

## Task 3 — Data hook `usePendingTopUps`

**Files:**
- Create: `src/AFK4.Platform.Web/src/club/venue/usePendingTopUps.ts`
- Test: `src/AFK4.Platform.Web/src/club/venue/usePendingTopUps.test.tsx`

- [ ] **Step 1: Write the failing hook test.**
  Create `src/AFK4.Platform.Web/src/club/venue/usePendingTopUps.test.tsx` (mirrors `useDevices.test.tsx` style — render a tiny probe component that consumes the hook):
  ```tsx
  import { render, screen, waitFor } from '@testing-library/react';
  import { it, expect, mock } from 'bun:test';
  import type { PendingTopUpDto } from '@/api/types';
  import { usePendingTopUps } from './usePendingTopUps';

  const dtos: PendingTopUpDto[] = [{
    paymentIntentId: 'i1', playerAccountId: 'p1', displayName: 'Анна', seatName: 'PC-1',
    amountMinorUnits: 5000, currencyCode: 'TJS', createdAtUtc: '2026-06-03T10:00:00.000Z'
  }];

  function Probe({ client }: { client: { listPendingTopUps: (b: string) => Promise<PendingTopUpDto[]> } }) {
    const state = usePendingTopUps(client, 'b1');
    if (state.status !== 'ready') return <div>{state.status}</div>;
    return <div>{state.rows.map(r => <span key={r.paymentIntentId}>{r.displayName}:{r.amountMajor}</span>)}</div>;
  }

  it('loads and maps pending top-ups for the branch', async () => {
    const client = { listPendingTopUps: mock(async () => dtos) };
    render(<Probe client={client} />);
    expect(screen.getByText('loading')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByText('Анна:50')).toBeInTheDocument());
    expect(client.listPendingTopUps.mock.calls[0][0]).toBe('b1');
  });

  it('surfaces an error state when the load fails', async () => {
    const client = { listPendingTopUps: mock(async () => { throw new Error('boom'); }) };
    render(<Probe client={client} />);
    await waitFor(() => expect(screen.getByText('error')).toBeInTheDocument());
  });
  ```

- [ ] **Step 2: Run the test — expect FAIL** (hook does not exist):
  ```
  /home/fedya/.bun/bin/bun test src/AFK4.Platform.Web/src/club/venue/usePendingTopUps.test.tsx
  ```

- [ ] **Step 3: Implement the hook (minimal impl), mirroring `useDevices.ts`.**
  Create `src/AFK4.Platform.Web/src/club/venue/usePendingTopUps.ts`:
  ```ts
  import { useCallback, useEffect, useRef, useState } from 'react';
  import type { ClubApiClient } from '@/api/clubApi';
  import { buildPendingTopUps, type PendingTopUpRow } from './pendingTopUpsModel';

  export type PendingTopUpsState =
    | { status: 'loading'; retry: () => void }
    | { status: 'error'; message: string; retry: () => void }
    | { status: 'ready'; rows: PendingTopUpRow[]; retry: () => void };

  type Loadable = Pick<ClubApiClient, 'listPendingTopUps'>;

  export function usePendingTopUps(client: Loadable, branchId: string): PendingTopUpsState {
    const [tick, setTick] = useState(0);
    const [state, setState] = useState<{ status: 'loading' | 'error' | 'ready'; rows?: PendingTopUpRow[]; message?: string }>({ status: 'loading' });
    const retry = useCallback(() => setTick(t => t + 1), []);
    const clientRef = useRef(client);
    clientRef.current = client;

    useEffect(() => {
      let cancelled = false;
      setState({ status: 'loading' });
      clientRef.current.listPendingTopUps(branchId)
        .then(dtos => { if (!cancelled) setState({ status: 'ready', rows: buildPendingTopUps(dtos) }); })
        .catch((err: unknown) => { if (!cancelled) setState({ status: 'error', message: err instanceof Error ? err.message : 'error' }); });
      return () => { cancelled = true; };
    }, [branchId, tick]);

    if (state.status === 'ready') return { status: 'ready', rows: state.rows!, retry };
    if (state.status === 'error') return { status: 'error', message: state.message ?? 'error', retry };
    return { status: 'loading', retry };
  }
  ```

- [ ] **Step 4: Run the test — expect PASS:**
  ```
  /home/fedya/.bun/bin/bun test src/AFK4.Platform.Web/src/club/venue/usePendingTopUps.test.tsx
  ```
  Expected: 2 passing.

- [ ] **Step 5: Commit.**
  ```
  git add src/AFK4.Platform.Web/src/club/venue/usePendingTopUps.ts src/AFK4.Platform.Web/src/club/venue/usePendingTopUps.test.tsx
  git commit -m "feat(platform-web): usePendingTopUps data hook"
  ```

---

## Task 4 — `PendingTopUpsPanel` (list + confirm action)

**Files:**
- Create: `src/AFK4.Platform.Web/src/club/venue/PendingTopUpsPanel.tsx`
- Test: `src/AFK4.Platform.Web/src/club/venue/PendingTopUpsPanel.test.tsx`

- [ ] **Step 1: Write the failing component test (RTL), mirroring `AmountReasonDialog.test.tsx`/`WalletPanel.test.tsx`.**
  Create `src/AFK4.Platform.Web/src/club/venue/PendingTopUpsPanel.test.tsx`:
  ```tsx
  import { render, screen, fireEvent, waitFor } from '@testing-library/react';
  import { it, expect, mock } from 'bun:test';
  import { I18nProvider } from '@/i18n/I18nProvider';
  import { ToastProvider } from '@/components/ui/toast';
  import type { PendingTopUpDto } from '@/api/types';
  import { PendingTopUpsPanel } from './PendingTopUpsPanel';

  const dtos: PendingTopUpDto[] = [{
    paymentIntentId: 'i1', playerAccountId: 'p1', displayName: 'Анна', seatName: 'PC-1',
    amountMinorUnits: 5000, currencyCode: 'TJS', createdAtUtc: '2026-06-03T10:00:00.000Z'
  }];

  function fakeClient() {
    return {
      listPendingTopUps: mock(async () => dtos),
      fulfilTopUpIntent: mock(async () => ({ paymentIntentId: 'i1', state: 'fulfilled' }))
    };
  }

  function renderPanel(client = fakeClient()) {
    render(
      <I18nProvider><ToastProvider>
        <PendingTopUpsPanel client={client as never} branchId="b1" />
      </ToastProvider></I18nProvider>
    );
    return client;
  }

  it('renders a pending request with player name and formatted amount', async () => {
    renderPanel();
    expect(await screen.findByText('Анна')).toBeInTheDocument();
    // formatCurrency(50, 'TJS') in ru-RU — assert the numeric part is present.
    expect(screen.getByText(/50/)).toBeInTheDocument();
    expect(screen.getByText('PC-1')).toBeInTheDocument();
  });

  it('confirms a request via fulfilTopUpIntent and shows a success toast', async () => {
    const client = renderPanel();
    await screen.findByText('Анна');
    fireEvent.click(screen.getByRole('button', { name: 'Подтвердить' }));
    await waitFor(() => expect(client.fulfilTopUpIntent).toHaveBeenCalled());
    expect(client.fulfilTopUpIntent.mock.calls[0][0]).toBe('i1');
    expect(await screen.findByText('Пополнение подтверждено')).toBeInTheDocument();
    // list refreshed after confirm
    await waitFor(() => expect(client.listPendingTopUps.mock.calls.length).toBeGreaterThan(1));
  });

  it('shows an error toast when confirm fails', async () => {
    const client = {
      listPendingTopUps: mock(async () => dtos),
      fulfilTopUpIntent: mock(async () => { throw new Error('boom'); })
    };
    renderPanel(client);
    await screen.findByText('Анна');
    fireEvent.click(screen.getByRole('button', { name: 'Подтвердить' }));
    expect(await screen.findByText('Не удалось подтвердить пополнение')).toBeInTheDocument();
  });

  it('shows the empty state when there are no pending requests', async () => {
    const client = {
      listPendingTopUps: mock(async () => [] as PendingTopUpDto[]),
      fulfilTopUpIntent: mock(async () => ({ paymentIntentId: 'x', state: 'fulfilled' }))
    };
    renderPanel(client);
    expect(await screen.findByText('Нет запросов на пополнение')).toBeInTheDocument();
  });
  ```

- [ ] **Step 2: Run the test — expect FAIL** (panel does not exist):
  ```
  /home/fedya/.bun/bin/bun test src/AFK4.Platform.Web/src/club/venue/PendingTopUpsPanel.test.tsx
  ```

- [ ] **Step 3: Implement the panel (minimal impl).**
  Create `src/AFK4.Platform.Web/src/club/venue/PendingTopUpsPanel.tsx`:
  ```tsx
  import { useState } from 'react';
  import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
  import { Button } from '@/components/ui/button';
  import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
  import { useToast } from '@/components/ui/toast';
  import { useI18n } from '@/i18n/I18nProvider';
  import type { ClubApiClient } from '@/api/clubApi';
  import { usePendingTopUps } from './usePendingTopUps';

  type Client = Pick<ClubApiClient, 'listPendingTopUps' | 'fulfilTopUpIntent'>;

  export function PendingTopUpsPanel({ client, branchId }: { client: Client; branchId: string }) {
    const { t, formatCurrency } = useI18n();
    const { toast } = useToast();
    const state = usePendingTopUps(client, branchId);
    const [confirming, setConfirming] = useState<string | null>(null);

    if (state.status === 'loading') return <LoadingCards count={2} />;
    if (state.status === 'error') return <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />;
    if (state.rows.length === 0) return <EmptyState message={t('topUps.empty')} />;

    async function confirm(intentId: string): Promise<void> {
      setConfirming(intentId);
      try {
        await client.fulfilTopUpIntent(intentId);
        toast({ title: t('topUps.confirmed'), variant: 'success' });
        state.retry();
      } catch {
        toast({ title: t('topUps.confirmError'), variant: 'error' });
      } finally {
        setConfirming(null);
      }
    }

    return (
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>{t('topUps.player')}</TableHead>
            <TableHead>{t('topUps.seat')}</TableHead>
            <TableHead>{t('topUps.amount')}</TableHead>
            <TableHead />
          </TableRow>
        </TableHeader>
        <TableBody>
          {state.rows.map(row => (
            <TableRow key={row.paymentIntentId}>
              <TableCell>{row.displayName}</TableCell>
              <TableCell>{row.seatLabel}</TableCell>
              <TableCell className="tabular-nums">{formatCurrency(row.amountMajor, row.currencyCode)}</TableCell>
              <TableCell>
                <Button
                  size="sm"
                  disabled={confirming !== null}
                  onClick={() => void confirm(row.paymentIntentId)}
                >
                  {confirming === row.paymentIntentId ? t('topUps.confirming') : t('topUps.confirm')}
                </Button>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    );
  }
  ```
  > Note: `state` is the `ready` union member inside `confirm` (called only when `status === 'ready'`); if `tsc` cannot narrow `state.retry` inside the closure, capture `const { rows, retry } = state;` after the guards and reference `retry`/`rows` directly (this matches `WalletPanel.tsx`'s `const { balance, ledger, retry } = state;` pattern).

- [ ] **Step 4: Run the test — expect PASS:**
  ```
  /home/fedya/.bun/bin/bun test src/AFK4.Platform.Web/src/club/venue/PendingTopUpsPanel.test.tsx
  ```
  Expected: 4 passing.

- [ ] **Step 5: Commit.**
  ```
  git add src/AFK4.Platform.Web/src/club/venue/PendingTopUpsPanel.tsx src/AFK4.Platform.Web/src/club/venue/PendingTopUpsPanel.test.tsx
  git commit -m "feat(platform-web): operator pending top-ups panel with confirm action"
  ```

---

## Task 5 — Wire the panel into `VenueScreen` (and gate it by branch top-up permission)

**Files:**
- Modify: `src/AFK4.Platform.Web/src/club/venue/VenueScreen.tsx`
- Modify: `src/AFK4.Platform.Web/src/App.tsx` (pass `canConfirmTopUps`)
- Test: `src/AFK4.Platform.Web/src/club/venue/VenueScreen.test.tsx`

- [ ] **Step 1: Add a failing test to `VenueScreen.test.tsx`.**
  The existing test renders `VenueScreen` with a fake client; extend the fake client with `listPendingTopUps`/`fulfilTopUpIntent` and assert the new tab. Append:
  ```tsx
  it('shows the top-ups tab when the operator can confirm and there are pending requests', async () => {
    const client = {
      ...fakeVenueClient(), // existing helper — extend it to also return listDevices/listPendingDevices/getFloorMap
      listPendingTopUps: mock(async () => ([{
        paymentIntentId: 'i1', playerAccountId: 'p1', displayName: 'Анна', seatName: 'PC-1',
        amountMinorUnits: 5000, currencyCode: 'TJS', createdAtUtc: '2026-06-03T10:00:00.000Z'
      }])),
      fulfilTopUpIntent: mock(async () => ({ paymentIntentId: 'i1', state: 'fulfilled' }))
    };
    render(
      <I18nProvider><ToastProvider>
        <VenueScreen client={client as never} branchId="b1" organizationId="org" canManageLayout={false} canConfirmTopUps />
      </ToastProvider></I18nProvider>
    );
    expect(await screen.findByRole('tab', { name: /Пополнения/ })).toBeInTheDocument();
  });

  it('hides the top-ups tab when the operator lacks the permission', async () => {
    const client = {
      ...fakeVenueClient(),
      listPendingTopUps: mock(async () => ([])),
      fulfilTopUpIntent: mock(async () => ({ paymentIntentId: 'x', state: 'fulfilled' }))
    };
    render(
      <I18nProvider><ToastProvider>
        <VenueScreen client={client as never} branchId="b1" organizationId="org" canManageLayout={false} canConfirmTopUps={false} />
      </ToastProvider></I18nProvider>
    );
    await screen.findByRole('tab', { name: /Зал/ });
    expect(screen.queryByRole('tab', { name: /Пополнения/ })).not.toBeInTheDocument();
  });
  ```
  > Read the existing `VenueScreen.test.tsx` first and adapt to its actual fake-client helper name and provider wrapping (the snippet above assumes a `fakeVenueClient()` helper — rename to match what is there; if the existing test wraps only in `I18nProvider`, add `ToastProvider` since the panel uses `useToast`).

- [ ] **Step 2: Run the test — expect FAIL** (`canConfirmTopUps` prop + tab do not exist):
  ```
  /home/fedya/.bun/bin/bun test src/AFK4.Platform.Web/src/club/venue/VenueScreen.test.tsx
  ```

- [ ] **Step 3: Wire the tab into `VenueScreen.tsx` (minimal impl).**
  Add the prop and a conditional tab. The pending count comes from `usePendingTopUps`; to avoid double-loading, load it in `VenueScreen` and pass rows down — but to keep this minimal and mirror the existing `pending` devices pattern (tab visible only when non-empty), gate the **tab trigger** on `canConfirmTopUps` and render the panel inside the tab content (the panel self-loads). Add an import and:
  ```tsx
  import { PendingTopUpsPanel } from './PendingTopUpsPanel';
  ```
  Change the signature:
  ```tsx
  export function VenueScreen({ client, branchId, organizationId, canManageLayout, canConfirmTopUps }: { client: ClubApiClient; branchId: string; organizationId: string; canManageLayout: boolean; canConfirmTopUps: boolean }) {
  ```
  Add the trigger (after the existing pending-devices trigger) and content (after the map content):
  ```tsx
  {canConfirmTopUps && <TabsTrigger value="topUps">{t('venue.tab.topUps')}</TabsTrigger>}
  ```
  ```tsx
  {canConfirmTopUps && (
    <TabsContent value="topUps">
      <PendingTopUpsPanel client={client} branchId={branchId} />
    </TabsContent>
  )}
  ```
  > Design decision: the tab is shown whenever the operator **can** confirm (permission), not only when there are pending requests — the panel renders its own empty state (`topUps.empty`). This keeps the surface discoverable. (Contrast with the devices `pending` tab, which hides when empty; here discoverability matters more because a player may be waiting at the counter.) If product prefers hide-when-empty parity, lift `usePendingTopUps` into `VenueScreen` and gate the trigger on `rows.length > 0 && canConfirmTopUps`.

- [ ] **Step 4: Pass the permission from `App.tsx`.**
  In `src/AFK4.Platform.Web/src/App.tsx`, at the `<VenueScreen … />` render (around line 398), add:
  ```tsx
  canConfirmTopUps={session.permissions.includes('billing.wallet.top_up')}
  ```
  (`billing.wallet.top_up` is the web-side string for the backend `TopUpWallet` permission — verified in `StaffPermissionNames.cs`.)

- [ ] **Step 5: Run the test — expect PASS:**
  ```
  /home/fedya/.bun/bin/bun test src/AFK4.Platform.Web/src/club/venue/VenueScreen.test.tsx
  ```
  Expected: existing + 2 new tests pass.

- [ ] **Step 6: Commit.**
  ```
  git add src/AFK4.Platform.Web/src/club/venue/VenueScreen.tsx src/AFK4.Platform.Web/src/club/venue/VenueScreen.test.tsx src/AFK4.Platform.Web/src/App.tsx
  git commit -m "feat(platform-web): surface pending top-ups tab on the venue screen, gated by top-up permission"
  ```

---

## Verification gate

All of the following must be green before claiming Unit 3 complete (per superpowers:verification-before-completion — run the commands, paste the real output, do not assert from memory):

- [ ] **Full web test suite green:**
  ```
  /home/fedya/.bun/bin/bun test
  ```
  Run from the repo root (or `src/AFK4.Platform.Web` if the suite is scoped there — match the existing `bun test` invocation in `package.json`). Expect 0 failures, including the new model/hook/panel/venue/i18n tests.

- [ ] **Type-check + production build clean** (this is what enforces i18n key parity and strict types):
  ```
  /home/fedya/.bun/bin/bun run build
  ```
  in `src/AFK4.Platform.Web` (= `tsc -b && vite build`). Expect no TS errors (every `t('topUps.*')` / `t('venue.tab.topUps')` key must resolve against the generated `messages.ts` for `ru`, `en`, and `tg`).

- [ ] **i18n catalog regenerated and committed:** confirm `packages/i18n/src/messages.ts` includes all new keys for ru/en/tg and is committed (it is a generated mirror — never hand-edited).

### Dependency reminder (blocking for real-world function, not for the test suite)

- **Unit 1's list endpoint** `GET /api/branches/{branchId}/wallet/top-up-intents?status=pending` must exist and return the `PendingTopUpDto` shape (with `displayName` + `seatName`) for this surface to work against a live backend. It is **not yet in `Program.cs`** as of 2026-06-03. The frontend tests mock the client, so they pass without it — but a manual smoke test (player creates an intent → operator sees + confirms it) requires Unit 1 landed first. If Unit 1 ships the bare `PlayerTopUpIntentDto` instead, file a follow-up so the list endpoint enriches it with `displayName`/`seatName` (the operator must know who and where).

### Out of scope (future parity)

- The **WPF operator app** (`AFK4.Operator.App`) gets no top-up surface in this plan — per the spec, Unit 3 is the **web** operator surface. A WPF parity pass is future work.
- Real-time push of new pending requests (the panel loads on mount + refreshes after a confirm; live SignalR/poll updates are not in scope). An operator switching to the tab triggers a fresh load via `usePendingTopUps`.
