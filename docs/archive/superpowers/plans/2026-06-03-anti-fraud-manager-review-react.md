# §5.5 React Manager Review screen — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a manager-only «Проверка» workspace to the operator web app that lets Owner/BranchManager approve or reject pending high-risk money actions and browse the high-risk audit trail filtered by staff member and amount.

**Architecture:** New `review` workspace inside the existing monolithic `App.tsx` switcher (matches the "every workspace lives in App.tsx" convention). A new `createMoneyActionClient` in `operatorApiClients.ts` wraps the already-shipped backend endpoints; the existing `settings.getStaffUsers` roster resolves actor GUIDs to names. The screen has two segments: an approval queue and a filterable audit log.

**Tech Stack:** React + TypeScript, Bun test runner (`bun test`), Vite build (`bun run build`). Working dir for all commands: `src/AFK4.Operator.App.Web`.

---

## File structure

- **Modify** `src/AFK4.Operator.App.Web/src/operatorApiClients.ts` — add money-action contracts + `createMoneyActionClient`, register it in `createOperatorApiClients`, extend `AuditSearchRequest` with `actorStaffUserId`/`minAmount`/`maxAmount`.
- **Modify** `src/AFK4.Operator.App.Web/src/operatorApiClients.test.ts` — client unit tests.
- **Modify** `src/AFK4.Operator.App.Web/src/operatorData.ts` — add the «Проверка» nav item + icon import.
- **Modify** `src/AFK4.Operator.App.Web/src/App.tsx` — `WorkspaceId`/`workspaceIds`/`permissionNames`/`workspacePermissionRules`, the render branch, the `SummarySidePanel` exclusion, the icon import, the type import, and the new `ReviewWorkspace` component.
- **Modify** `src/AFK4.Operator.App.Web/src/App.test.tsx` — extend `allOperatorPermissions`, add money-action routes to `mockPlatformFetch`, add the staff-roster fixture wiring (route already exists), and add the new screen tests.
- **Modify** `src/AFK4.Operator.App.Web/src/styles.css` — append a small `review-*` style block.

All paths below are relative to the repo root `/home/fedya/projects/afk4.net`.

---

## Task 1: API client — money-action contracts + client + audit filter fields

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/operatorApiClients.ts`
- Test: `src/AFK4.Operator.App.Web/src/operatorApiClients.test.ts`

- [ ] **Step 1: Write the failing test**

Append this `it` block inside the existing `describe('operator API clients', ...)` in `operatorApiClients.test.ts`:

```ts
  it('maps money-action review endpoints and audit amount filters', async () => {
    const { clients, calls } = createRecordedClients();
    const requestId = '77777777-7777-7777-7777-777777777777';

    await clients.moneyActions.listPending(branchId);
    await clients.moneyActions.approve(branchId, requestId, { decisionReason: null });
    await clients.moneyActions.reject(branchId, requestId, { decisionReason: 'Нет чека' });
    await clients.audit.search({
      branchId,
      actorStaffUserId: '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134',
      minAmount: 1000,
      maxAmount: 5000,
      limit: 50
    });

    expect(calls.map((call) => `${call.method} ${call.path}`)).toEqual([
      `GET /api/branches/${branchId}/money-actions`,
      `POST /api/branches/${branchId}/money-actions/${requestId}/approve`,
      `POST /api/branches/${branchId}/money-actions/${requestId}/reject`,
      `GET /api/branches/${branchId}/audit?actorStaffUserId=3db1367b-88c6-4b1c-99c3-bcbb5f4d5134&minAmount=1000&maxAmount=5000&limit=50`
    ]);
    expect(calls[1].body).toEqual({ decisionReason: null });
    expect(calls[2].body).toEqual({ decisionReason: 'Нет чека' });
  });
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd src/AFK4.Operator.App.Web && bun test src/operatorApiClients.test.ts`
Expected: FAIL — `clients.moneyActions` is undefined / `actorStaffUserId` not a valid `AuditSearchRequest` field.

- [ ] **Step 3: Extend `AuditSearchRequest`**

In `operatorApiClients.ts`, replace the existing `AuditSearchRequest` interface with:

```ts
export interface AuditSearchRequest {
  branchId: Guid;
  action?: string | null;
  outcome?: string | null;
  targetType?: string | null;
  fromUtc?: string | Date | null;
  toUtc?: string | Date | null;
  actorStaffUserId?: string | null;
  minAmount?: number | null;
  maxAmount?: number | null;
  limit?: number | null;
}
```

(`normalizeReportQuery` already spreads every field into the query string, so the three new fields are forwarded automatically. `QueryParams` permits `number | null`.)

- [ ] **Step 4: Add money-action contracts**

In `operatorApiClients.ts`, near the other `*Dto` / billing types, add:

```ts
export interface MoneyActionRequestDto extends Record<string, unknown> {
  moneyActionRequestId: Guid;
  organizationId: Guid;
  branchId: Guid;
  shiftId: Guid;
  actionType: string;
  requestedByStaffUserId: Guid;
  amountMinorUnits: number;
  currencyCode: string;
  reason: string;
  state: string;
  createdAtUtc: string;
  expiresAtUtc: string;
}

export interface MoneyActionRequestListResponse {
  requests: MoneyActionRequestDto[];
}

export interface MoneyActionDecisionRequest extends Record<string, unknown> {
  decisionReason?: string | null;
}

export type MoneyActionDecisionResponse = Record<string, unknown>;
```

- [ ] **Step 5: Add the client factory**

In `operatorApiClients.ts`, alongside `createAuditClient`, add:

```ts
export function createMoneyActionClient(api: PlatformApiClient) {
  return {
    listPending(branchId: Guid): Promise<MoneyActionRequestListResponse> {
      return api.get<MoneyActionRequestListResponse>(`/api/branches/${branchId}/money-actions`);
    },
    approve(branchId: Guid, requestId: Guid, request: MoneyActionDecisionRequest): Promise<MoneyActionDecisionResponse> {
      return api.post<MoneyActionDecisionResponse, MoneyActionDecisionRequest>(`/api/branches/${branchId}/money-actions/${requestId}/approve`, request);
    },
    reject(branchId: Guid, requestId: Guid, request: MoneyActionDecisionRequest): Promise<MoneyActionDecisionResponse> {
      return api.post<MoneyActionDecisionResponse, MoneyActionDecisionRequest>(`/api/branches/${branchId}/money-actions/${requestId}/reject`, request);
    }
  };
}
```

- [ ] **Step 6: Register the client in the aggregator**

In `createOperatorApiClients`, add a `moneyActions` entry (the `audit` line is the last one — add a comma and the new line):

```ts
    audit: createAuditClient(api),
    moneyActions: createMoneyActionClient(api)
```

- [ ] **Step 7: Run test to verify it passes**

Run: `cd src/AFK4.Operator.App.Web && bun test src/operatorApiClients.test.ts`
Expected: PASS (all tests in the file).

- [ ] **Step 8: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/operatorApiClients.ts src/AFK4.Operator.App.Web/src/operatorApiClients.test.ts
git commit -m "feat(anti-fraud): money-action review client + audit amount filters (§5.5)"
```

---

## Task 2: Nav item + permission + workspace wiring (with empty ReviewWorkspace stub)

This task makes the «Проверка» rail item appear (gated) and renders an empty workspace. The full UI lands in Task 3.

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/operatorData.ts`
- Modify: `src/AFK4.Operator.App.Web/src/App.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/App.test.tsx`

- [ ] **Step 1: Write the failing gating test**

In `App.test.tsx`, first add the new permission to the `allOperatorPermissions` array (append after `'audit.view'`):

```ts
  'audit.view',
  'billing.money_action.approve'
```

Then add this test inside `describe('App', ...)`:

```ts
  it('locks the review workspace without the approve permission', async () => {
    installSessionBridge(createSession({ permissions: ['floor_map.view'] }));
    render(<App />);
    await screen.findByRole('heading', { name: /AFK4 Dushanbe/ });

    const reviewNav = screen.getByTitle('Проверка');
    expect(reviewNav.className).toContain('locked');
  });

  it('opens the review workspace for a manager', async () => {
    installSessionBridge(createSession({ displayName: 'Manager One' }));
    render(<App />);
    await screen.findByRole('heading', { name: /AFK4 Dushanbe/ });

    fireEvent.click(screen.getByTitle('Проверка'));
    expect(await screen.findByRole('heading', { name: /Проверка/ })).toBeInTheDocument();
  });
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd src/AFK4.Operator.App.Web && bun test src/App.test.tsx -t "review workspace"`
Expected: FAIL — `getByTitle('Проверка')` finds nothing.

- [ ] **Step 3: Add the nav item in `operatorData.ts`**

Add `ClipboardCheck` to the lucide import in `operatorData.ts`, then append to the `navItems` array (after the `Настройки` entry):

```ts
  { label: 'Настройки', icon: Settings },
  { label: 'Проверка', icon: ClipboardCheck }
```

- [ ] **Step 4: Wire the workspace in `App.tsx`**

(a) Add `ClipboardCheck` to the `lucide-react` import block (lines ~1-24).

(b) Add the type import from `./operatorApiClients` (line ~98 import list): add `type MoneyActionRequestDto`.

(c) Extend `WorkspaceId` (line ~109):

```ts
type WorkspaceId = 'map' | 'dashboard' | 'booking' | 'pos' | 'players' | 'payments' | 'logs' | 'settings' | 'review';
```

(d) Extend `workspaceIds` (line ~147) — append `'review'`:

```ts
const workspaceIds: WorkspaceId[] = ['map', 'dashboard', 'booking', 'pos', 'players', 'payments', 'logs', 'settings', 'review'];
```

(e) Add the permission to `permissionNames` (after `viewAudit`):

```ts
  viewAudit: 'audit.view',
  approveMoneyAction: 'billing.money_action.approve'
```

(f) Add to `workspacePermissionRules` (after the `settings` entry):

```ts
  review: [permissionNames.approveMoneyAction]
```

- [ ] **Step 5: Add the render branch + side-panel exclusion in `App.tsx`**

After the `{workspace === 'settings' && ...}` line (~10155), add:

```tsx
      {workspace === 'review' && <ReviewWorkspace currencyCode={config.currencyCode} backend={backendContext} />}
```

Then update the `SummarySidePanel` guard (the long `workspace !== ...` chain, ~10168) to also exclude `'review'`:

```tsx
      {workspace !== 'map' && workspace !== 'dashboard' && workspace !== 'booking' && workspace !== 'pos' && workspace !== 'players' && workspace !== 'payments' && workspace !== 'logs' && workspace !== 'settings' && workspace !== 'review'
        && <SummarySidePanel workspace={workspace} currencyCode={config.currencyCode} />}
```

- [ ] **Step 6: Add a minimal `ReviewWorkspace` stub in `App.tsx`**

Add this function next to `BackendLogsWorkspace` (it is replaced with the full version in Task 3, but must exist now so the gating test passes):

```tsx
function ReviewWorkspace({ currencyCode, backend }: { currencyCode: string; backend: OperatorBackendContext | null }) {
  void currencyCode;
  void backend;
  return (
    <main className="workspace-screen review-screen">
      <section className="screen-head">
        <div>
          <span>Проверка</span>
          <h1>Проверка · заявки и журнал</h1>
        </div>
      </section>
    </main>
  );
}
```

- [ ] **Step 7: Run to verify the gating tests pass**

Run: `cd src/AFK4.Operator.App.Web && bun test src/App.test.tsx -t "review workspace"`
Expected: PASS (both new tests).

- [ ] **Step 8: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/operatorData.ts src/AFK4.Operator.App.Web/src/App.tsx src/AFK4.Operator.App.Web/src/App.test.tsx
git commit -m "feat(anti-fraud): review workspace nav item + permission gating (§5.5)"
```

---

## Task 3: ReviewWorkspace — approval queue (load + approve/reject)

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/App.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/App.test.tsx`

- [ ] **Step 1: Add money-action routes + fixture to `mockPlatformFetch`**

In `App.test.tsx`, add a fixture builder next to `createStaffUsers`:

```ts
function createMoneyActionRequests() {
  return {
    requests: [
      {
        moneyActionRequestId: '77777777-7777-7777-7777-777777777777',
        organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
        branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
        shiftId: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
        actionType: 'refund',
        requestedByStaffUserId: '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134',
        amountMinorUnits: 12000,
        currencyCode: 'TJS',
        reason: 'Клиент отменил заказ',
        state: 'pending',
        createdAtUtc: '2026-06-03T08:00:00Z',
        expiresAtUtc: '2026-06-04T08:00:00Z'
      }
    ]
  };
}
```

Then add these route handlers near the top of `mockPlatformFetch` (before the generic `/staff` handler, which must keep working):

```ts
  if (pathname.includes('/money-actions/') && pathname.endsWith('/approve') && init?.method === 'POST') {
    return jsonResponse({ outcome: 'approved' });
  }

  if (pathname.includes('/money-actions/') && pathname.endsWith('/reject') && init?.method === 'POST') {
    return jsonResponse({ outcome: 'rejected' });
  }

  if (pathname.endsWith('/money-actions') && init?.method !== 'POST') {
    return jsonResponse(createMoneyActionRequests());
  }
```

- [ ] **Step 2: Write the failing queue tests**

Add to `describe('App', ...)` in `App.test.tsx`:

```ts
  it('renders the pending money-action queue and approves a request', async () => {
    installSessionBridge(createSession({ displayName: 'Manager One' }));
    render(<App />);
    await screen.findByRole('heading', { name: /AFK4 Dushanbe/ });
    fireEvent.click(screen.getByTitle('Проверка'));

    expect(await screen.findByText('Клиент отменил заказ')).toBeInTheDocument();
    expect(screen.getByText(/Возврат/)).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Одобрить' }));

    await waitFor(() => {
      const approved = fetchMock.mock.calls.some(([input, init]) =>
        String(input).endsWith('/money-actions/77777777-7777-7777-7777-777777777777/approve')
        && (init as RequestInit | undefined)?.method === 'POST');
      expect(approved).toBe(true);
    });
  });

  it('requires a reason before rejecting a money action', async () => {
    installSessionBridge(createSession({ displayName: 'Manager One' }));
    render(<App />);
    await screen.findByRole('heading', { name: /AFK4 Dushanbe/ });
    fireEvent.click(screen.getByTitle('Проверка'));
    await screen.findByText('Клиент отменил заказ');

    fireEvent.click(screen.getByRole('button', { name: 'Отклонить' }));
    fireEvent.click(screen.getByRole('button', { name: 'Подтвердить отклонение' }));
    expect(await screen.findByText('Укажите причину отклонения.')).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('Причина отклонения'), { target: { value: 'Нет чека' } });
    fireEvent.click(screen.getByRole('button', { name: 'Подтвердить отклонение' }));

    await waitFor(() => {
      const rejected = fetchMock.mock.calls.find(([input, init]) =>
        String(input).endsWith('/money-actions/77777777-7777-7777-7777-777777777777/reject')
        && (init as RequestInit | undefined)?.method === 'POST');
      expect(rejected).toBeDefined();
      expect(JSON.parse(String((rejected![1] as RequestInit).body))).toEqual({ decisionReason: 'Нет чека' });
    });
  });
```

- [ ] **Step 3: Run to verify failure**

Run: `cd src/AFK4.Operator.App.Web && bun test src/App.test.tsx -t "money-action"`
Expected: FAIL — stub renders no queue / buttons.

- [ ] **Step 4: Replace the `ReviewWorkspace` stub with the full component**

Replace the stub from Task 2 with this complete implementation:

```tsx
type ReviewSegment = 'queue' | 'audit';

function reviewActionTypeLabel(actionType: string): string {
  switch (actionType) {
    case 'refund':
      return 'Возврат';
    case 'manual_correction':
      return 'Коррекция';
    case 'debt_write_off':
      return 'Списание долга';
    default:
      return actionType;
  }
}

function ReviewWorkspace({ currencyCode, backend }: { currencyCode: string; backend: OperatorBackendContext | null }) {
  const [activeSegment, setActiveSegment] = useState<ReviewSegment>('queue');
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const [loadStatus, setLoadStatus] = useState<LoadStatus>('fixture');
  const [loadError, setLoadError] = useState<string | null>(null);

  const [requests, setRequests] = useState<MoneyActionRequestDto[]>([]);
  const [staffNames, setStaffNames] = useState<Record<string, string>>({});
  const [rejectingId, setRejectingId] = useState('');
  const [decisionReason, setDecisionReason] = useState('');

  const [auditResult, setAuditResult] = useState<AuditSearchResultDto | null>(null);
  const [auditActor, setAuditActor] = useState('');
  const [auditMinAmount, setAuditMinAmount] = useState('');
  const [auditMaxAmount, setAuditMaxAmount] = useState('');

  const resolveStaffName = (staffUserId: string) =>
    staffNames[staffUserId.toLowerCase()] ?? `${staffUserId.slice(0, 8)}…`;

  const loadQueue = async (nextBackend = backend) => {
    if (nextBackend === null) {
      setLoadStatus('fixture');
      setLoadError(null);
      return;
    }
    setLoadStatus('loading');
    setLoadError(null);
    try {
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const [feed, staff] = await Promise.all([
        apiClients.moneyActions.listPending(nextBackend.branchId),
        apiClients.settings.getStaffUsers(nextBackend.branchId)
      ]);
      setRequests(readArray<MoneyActionRequestDto>(feed, 'requests'));
      const names: Record<string, string> = {};
      for (const user of staff) {
        names[readString(user, 'staffUserId').toLowerCase()] = operatorDisplayNameLabel(readString(user, 'displayName'));
      }
      setStaffNames(names);
      setLoadStatus('backend');
    } catch (error) {
      const detail = projectOperatorError(error).detail;
      setLoadStatus('failed');
      setLoadError(detail);
      setFeedback({ label: 'Проверка', state: 'failed', detail });
    }
  };

  useEffect(() => {
    void loadQueue();
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken]);

  const approveRequest = async (request: MoneyActionRequestDto) => {
    setFeedback({ label: 'Одобрение', state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      await apiClients.moneyActions.approve(nextBackend.branchId, request.moneyActionRequestId, { decisionReason: null });
      setFeedback({ label: 'Одобрение', state: 'confirmed' });
      await loadQueue();
    } catch (error) {
      setFeedback({ label: 'Одобрение', state: 'failed', detail: projectOperatorError(error).detail });
    }
  };

  const confirmReject = async (request: MoneyActionRequestDto) => {
    const reason = decisionReason.trim();
    if (reason.length === 0) {
      setFeedback({ label: 'Отклонение', state: 'failed', detail: 'Укажите причину отклонения.' });
      return;
    }
    setFeedback({ label: 'Отклонение', state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      await apiClients.moneyActions.reject(nextBackend.branchId, request.moneyActionRequestId, { decisionReason: reason });
      setRejectingId('');
      setDecisionReason('');
      setFeedback({ label: 'Отклонение', state: 'confirmed' });
      await loadQueue();
    } catch (error) {
      setFeedback({ label: 'Отклонение', state: 'failed', detail: projectOperatorError(error).detail });
    }
  };

  const applyAuditSearch = async () => {
    setFeedback({ label: 'Журнал', state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const min = auditMinAmount.trim() === '' ? null : Number(auditMinAmount);
      const max = auditMaxAmount.trim() === '' ? null : Number(auditMaxAmount);
      const result = await apiClients.audit.search({
        branchId: nextBackend.branchId,
        actorStaffUserId: auditActor.trim() === '' ? null : auditActor.trim(),
        minAmount: min !== null && Number.isFinite(min) ? min : null,
        maxAmount: max !== null && Number.isFinite(max) ? max : null,
        limit: 50
      });
      setAuditResult(result);
      setFeedback({ label: 'Журнал', state: 'confirmed' });
    } catch (error) {
      setFeedback({ label: 'Журнал', state: 'failed', detail: projectOperatorError(error).detail });
    }
  };

  const auditRecords = readArray<Record<string, unknown>>(auditResult, 'records');
  const staffOptions = Object.entries(staffNames);

  return (
    <main className="workspace-screen review-screen">
      <section className="screen-head review-head">
        <div>
          <span>Проверка</span>
          <h1>Проверка · заявки и журнал</h1>
        </div>
        <div className="screen-actions">
          <span className={`map-load-state ${loadStatus === 'backend' ? 'ready' : loadStatus}`}>{workspaceLoadStatusLabel(loadStatus, 'Заявки загружены')}</span>
        </div>
      </section>

      <section className="state-strip review-state-strip" aria-label="Сводка проверки">
        <StateFlag label="Заявки" value={String(requests.length)} critical={requests.length > 0} />
        <StateFlag label="Источник" value={workspaceLoadStatusLabel(loadStatus, 'Платформа')} critical={loadStatus !== 'backend'} />
      </section>

      <div className="review-segments" role="tablist">
        <button type="button" role="tab" aria-selected={activeSegment === 'queue'} className={activeSegment === 'queue' ? 'active' : undefined} onClick={() => setActiveSegment('queue')}>Заявки на одобрение</button>
        <button type="button" role="tab" aria-selected={activeSegment === 'audit'} className={activeSegment === 'audit' ? 'active' : undefined} onClick={() => setActiveSegment('audit')}>Журнал операций</button>
      </div>

      {activeSegment === 'queue' && (
        <section className="review-panel review-queue-panel">
          {requests.length === 0 ? (
            <p className="review-empty">{loadError ?? 'Нет заявок на одобрение'}</p>
          ) : (
            requests.map((request) => (
              <article key={request.moneyActionRequestId} className="review-request-row">
                <div className="review-request-head">
                  <strong>{reviewActionTypeLabel(request.actionType)}</strong>
                  <b>{formatMinorUnits(request.amountMinorUnits, request.currencyCode || currencyCode)}</b>
                </div>
                <em>{request.reason}</em>
                <div className="review-request-meta">
                  <span>Запросил: {resolveStaffName(request.requestedByStaffUserId)}</span>
                  <span>Создано: {formatTime(request.createdAtUtc)}</span>
                  <span>Истекает: {formatTime(request.expiresAtUtc)}</span>
                </div>
                {rejectingId === request.moneyActionRequestId ? (
                  <div className="review-reject-form">
                    <label>
                      Причина отклонения
                      <input value={decisionReason} onChange={(event) => setDecisionReason(event.currentTarget.value)} placeholder="почему отклонено" />
                    </label>
                    <div className="review-request-actions">
                      <button type="button" onClick={() => void confirmReject(request)}>Подтвердить отклонение</button>
                      <button type="button" onClick={() => { setRejectingId(''); setDecisionReason(''); }}>Отмена</button>
                    </div>
                  </div>
                ) : (
                  <div className="review-request-actions">
                    <button type="button" onClick={() => void approveRequest(request)}>Одобрить</button>
                    <button type="button" onClick={() => { setRejectingId(request.moneyActionRequestId); setDecisionReason(''); }}>Отклонить</button>
                  </div>
                )}
              </article>
            ))
          )}
          <FeedbackNotice feedback={feedback} />
        </section>
      )}

      {activeSegment === 'audit' && (
        <section className="review-panel review-audit-panel">
          <div className="review-audit-filters">
            <label>
              Сотрудник
              <select value={auditActor} onChange={(event) => setAuditActor(event.currentTarget.value)}>
                <option value="">Все сотрудники</option>
                {staffOptions.map(([staffUserId, name]) => (
                  <option key={staffUserId} value={staffUserId}>{name}</option>
                ))}
              </select>
            </label>
            <label>Сумма от<input inputMode="numeric" value={auditMinAmount} onChange={(event) => setAuditMinAmount(event.currentTarget.value)} placeholder="мин" /></label>
            <label>Сумма до<input inputMode="numeric" value={auditMaxAmount} onChange={(event) => setAuditMaxAmount(event.currentTarget.value)} placeholder="макс" /></label>
            <button type="button" onClick={() => void applyAuditSearch()}>Применить фильтр</button>
          </div>
          <div className="review-audit-list">
            {auditRecords.length === 0 ? (
              <p className="review-empty">Записей нет — задайте фильтр</p>
            ) : (
              auditRecords.map((record) => (
                <article key={readString(record, 'auditRecordId')} className="review-audit-row">
                  <span>{formatTime(readString(record, 'createdAtUtc'))}</span>
                  <strong>{auditActorLabel(record, backend)}</strong>
                  <em>{auditActionLabel(readString(record, 'action'))}</em>
                  <b>{readNumber(record, 'amountMinorUnits', 0) > 0 ? formatMinorUnits(readNumber(record, 'amountMinorUnits', 0), currencyCode) : '—'}</b>
                </article>
              ))
            )}
          </div>
          <FeedbackNotice feedback={feedback} />
        </section>
      )}
    </main>
  );
}
```

Note: `auditActorLabel` and `auditActionLabel` already exist in `App.tsx` (used by `BackendLogsWorkspace`). `requireBackend`, `readArray`, `readString`, `readNumber`, `operatorDisplayNameLabel`, `formatTime`, `formatMinorUnits`, `workspaceLoadStatusLabel`, `StateFlag`, `FeedbackNotice` are all existing module-level helpers/components.

- [ ] **Step 5: Run to verify the queue tests pass**

Run: `cd src/AFK4.Operator.App.Web && bun test src/App.test.tsx -t "money-action"`
Expected: PASS (both queue tests).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/App.tsx src/AFK4.Operator.App.Web/src/App.test.tsx
git commit -m "feat(anti-fraud): money-action approval queue UI (§5.5)"
```

---

## Task 4: Audit-filter tab test

The audit tab is already implemented in Task 3; this task adds its test.

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/App.test.tsx`

- [ ] **Step 1: Write the failing test**

Add to `describe('App', ...)`:

```ts
  it('builds an audit query from staff and amount filters', async () => {
    installSessionBridge(createSession({ displayName: 'Manager One' }));
    render(<App />);
    await screen.findByRole('heading', { name: /AFK4 Dushanbe/ });
    fireEvent.click(screen.getByTitle('Проверка'));
    await screen.findByText('Клиент отменил заказ');

    fireEvent.click(screen.getByRole('tab', { name: 'Журнал операций' }));
    fireEvent.change(screen.getByLabelText('Сотрудник'), { target: { value: '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134' } });
    fireEvent.change(screen.getByLabelText('Сумма от'), { target: { value: '1000' } });
    fireEvent.change(screen.getByLabelText('Сумма до'), { target: { value: '5000' } });
    fireEvent.click(screen.getByRole('button', { name: 'Применить фильтр' }));

    await waitFor(() => {
      const auditCall = fetchMock.mock.calls.find(([input]) =>
        String(input).includes('/audit?') && String(input).includes('actorStaffUserId=3db1367b'));
      expect(auditCall).toBeDefined();
      const url = String(auditCall![0]);
      expect(url).toContain('minAmount=1000');
      expect(url).toContain('maxAmount=5000');
    });
  });
```

(The staff dropdown is populated from the `/staff` roster fixture loaded on queue mount, so the option value is available after the queue renders.)

- [ ] **Step 2: Run to verify it passes**

The implementation already exists, so this should pass immediately. Run:
`cd src/AFK4.Operator.App.Web && bun test src/App.test.tsx -t "audit query"`
Expected: PASS. If it FAILS because the `/audit` route returns no `records`, confirm `mockPlatformFetch` has an `endsWith('/audit')` handler (it does, used by `BackendLogsWorkspace`). The test only asserts the request URL, not the response body.

- [ ] **Step 3: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/App.test.tsx
git commit -m "test(anti-fraud): audit filter query for review screen (§5.5)"
```

---

## Task 5: Styles + full verification

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/styles.css`

- [ ] **Step 1: Append review styles**

Append to the end of `styles.css`:

```css
.review-segments {
  display: flex;
  gap: 8px;
  padding: 0 24px;
  margin-bottom: 12px;
}

.review-segments button {
  padding: 8px 16px;
  border-radius: 10px;
  border: 1px solid var(--line, #2a3142);
  background: transparent;
  color: inherit;
  cursor: pointer;
}

.review-segments button.active {
  background: var(--accent-soft, #1f6feb22);
  border-color: var(--accent, #1f6feb);
}

.review-panel {
  margin: 0 24px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.review-request-row,
.review-audit-row {
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding: 14px 16px;
  border-radius: 12px;
  border: 1px solid var(--line, #2a3142);
  background: var(--panel, #161b26);
}

.review-request-head {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
}

.review-request-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 16px;
  font-size: 12px;
  opacity: 0.75;
}

.review-request-actions {
  display: flex;
  gap: 8px;
}

.review-request-actions button {
  padding: 6px 14px;
  border-radius: 8px;
  border: 1px solid var(--line, #2a3142);
  background: transparent;
  color: inherit;
  cursor: pointer;
}

.review-reject-form label,
.review-audit-filters label {
  display: flex;
  flex-direction: column;
  gap: 4px;
  font-size: 12px;
}

.review-audit-filters {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  align-items: flex-end;
}

.review-audit-row {
  display: grid;
  grid-template-columns: 120px 1fr 1fr 120px;
  align-items: center;
}

.review-empty {
  opacity: 0.7;
  padding: 16px;
}
```

(CSS variable fallbacks are provided so the block renders even if the exact theme tokens differ; align the variable names to the project's existing tokens if they differ when you eyeball the running app.)

- [ ] **Step 2: Run the full web test suite**

Run: `cd src/AFK4.Operator.App.Web && bun test`
Expected: PASS — all existing tests (157 baseline) plus the 5 new ones.

- [ ] **Step 3: Build**

Run: `cd src/AFK4.Operator.App.Web && bun run build`
Expected: clean build, no TypeScript errors.

- [ ] **Step 4: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/styles.css
git commit -m "style(anti-fraud): review workspace styling (§5.5)"
```

---

## Self-review notes

- **Spec coverage:** New workspace + gating (spec §"Architecture / placement") → Task 2. Approval queue with approve/reject + reason (§"Tab A") → Task 3. Audit filters by actor/amount (§"Tab B") → Task 3 impl + Task 4 test. Client + audit field extension (§"Data flow / client") → Task 1. Error/empty handling (§"Error handling") → Task 3 (`review-empty`, feedback, fixture mode). Tests (§"Tests") → Tasks 2–4. Styles → Task 5.
- **Type consistency:** `moneyActions.listPending/approve/reject`, `MoneyActionRequestDto` (camelCase, matches ASP.NET camelCase JSON), `MoneyActionDecisionRequest.decisionReason`, `AuditSearchRequest.{actorStaffUserId,minAmount,maxAmount}` are used identically in client, component, and tests.
- **Out of scope (per spec):** WPF parity, comp valuation/§5.4 checkout boundary, any backend change.
