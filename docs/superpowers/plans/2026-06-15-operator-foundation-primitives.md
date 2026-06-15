# Operator Foundation Primitives Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Introduce three reusable Operator primitives — Toast (net-new), Skeleton and EmptyState (extracted from ad-hoc CSS) — with full states + tests, and dogfood them on obvious existing spots.

**Architecture:** Toast is a small subsystem in its own file (`operatorToast.tsx`: context + provider + `useToast` hook + viewport), mounted inside `App`'s `I18nProvider`. Skeleton/EmptyState are leaf components added to `operatorPrimitives.tsx`, rendering the existing `.skeleton-block` and a new `.empty-state` class. Reduced-motion is already handled globally (`styles.css` `@media (prefers-reduced-motion)` zeroes all animation/transition durations), so primitives only need CSS-driven motion. `useDeferredFlag` (deferred spinner) already exists and is left untouched.

**Tech Stack:** React 19, vanilla CSS (`styles.css`), `@afk4/i18n` (ICU, locales in `locales/{ru,en,tg}.json` → regenerated via `cd packages/i18n && bun run gen`), `@afk4/tokens`, `lucide-react` icons, `bun test` (happy-dom + @testing-library/react). Run bun via `~/.bun/bin/bun`.

Spec: `docs/superpowers/specs/2026-06-15-operator-foundation-primitives-design.md`

---

## File Structure

- Create `src/AFK4.Operator.App.Web/src/operatorToast.tsx` — Toast subsystem (provider, `useToast`, viewport, card).
- Create `src/AFK4.Operator.App.Web/src/operatorToast.test.tsx` — Toast tests.
- Create `src/AFK4.Operator.App.Web/src/operatorPrimitives.test.tsx` — Skeleton + EmptyState tests (no test file exists today).
- Modify `src/AFK4.Operator.App.Web/src/operatorPrimitives.tsx` — add `Skeleton`, `EmptyState`.
- Modify `src/AFK4.Operator.App.Web/src/App.tsx` — wrap `AppInner` with `ToastProvider`.
- Modify `src/AFK4.Operator.App.Web/src/MapWorkspace.tsx` — Skeleton + EmptyState dogfood.
- Modify `src/AFK4.Operator.App.Web/src/DashboardWorkspace.tsx` — Skeleton dogfood.
- Modify `src/AFK4.Operator.App.Web/src/ShopOrdersWorkspace.tsx` — EmptyState + Toast dogfood.
- Modify `src/AFK4.Operator.App.Web/src/ShopOrdersWorkspace.test.tsx` — wrap render in `ToastProvider`, assert toast.
- Modify `src/AFK4.Operator.App.Web/src/styles.css` — `.toast-viewport`, `.toast`, tones; `.empty-state`; skeleton variant modifiers.
- Modify `locales/ru.json`, `locales/en.json`, `locales/tg.json` — add keys; then regenerate `packages/i18n/src/messages.ts`.

---

## Task 1: Toast primitive (provider + useToast + viewport)

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/operatorToast.tsx`
- Create: `src/AFK4.Operator.App.Web/src/operatorToast.test.tsx`
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json`
- Modify: `src/AFK4.Operator.App.Web/src/styles.css`

- [ ] **Step 1: Add i18n keys for toast service labels**

In `locales/ru.json`, `locales/en.json`, `locales/tg.json`, add two keys (place them anywhere among the other `op.*` keys; keep each file's existing formatting). These are REAL translations, not copies (#37) — the Tajik below is a genuine translation, flag for native review if unsure:

`locales/ru.json`:
```json
  "op.toast.close": "Закрыть",
  "op.toast.region": "Уведомления",
```
`locales/en.json`:
```json
  "op.toast.close": "Close",
  "op.toast.region": "Notifications",
```
`locales/tg.json`:
```json
  "op.toast.close": "Пӯшидан",
  "op.toast.region": "Огоҳиномаҳо",
```

- [ ] **Step 2: Regenerate i18n messages**

Run: `cd packages/i18n && ~/.bun/bin/bun run gen`
Expected: `packages/i18n/src/messages.ts` regenerated, includes `op.toast.close` / `op.toast.region` for all three locales. No error.

- [ ] **Step 3: Write the failing Toast test**

Create `src/AFK4.Operator.App.Web/src/operatorToast.test.tsx`:

```tsx
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider, useToast } from './operatorToast';

afterEach(cleanup);

const undoSpy = mock(() => {});

function Harness() {
  const toast = useToast();
  return (
    <div>
      <button onClick={() => toast.success('Сохранено')}>fire-success</button>
      <button onClick={() => toast.error('Ошибка')}>fire-error</button>
      <button onClick={() => toast.info('Готово', { durationMs: 30 })}>fire-info-fast</button>
      <button onClick={() => toast.success('С действием', { action: { label: 'Отменить', onClick: undoSpy } })}>fire-action</button>
      <button onClick={() => { toast.success('a'); toast.success('b'); toast.success('c'); toast.success('d'); }}>fire-four</button>
    </div>
  );
}

function renderHarness() {
  return render(
    <I18nProvider>
      <ToastProvider>
        <Harness />
      </ToastProvider>
    </I18nProvider>
  );
}

describe('Toast', () => {
  it('shows a success toast with status role', () => {
    renderHarness();
    fireEvent.click(screen.getByText('fire-success'));
    const toast = screen.getByText('Сохранено').closest('.toast');
    expect(toast).not.toBeNull();
    expect(toast).toHaveAttribute('role', 'status');
  });

  it('renders error toast as assertive alert and does NOT auto-dismiss', async () => {
    renderHarness();
    fireEvent.click(screen.getByText('fire-error'));
    const toast = screen.getByText('Ошибка').closest('.toast');
    expect(toast).toHaveAttribute('role', 'alert');
    // Wait past a typical short timer window; error must still be present (sticky).
    await new Promise((resolve) => setTimeout(resolve, 60));
    expect(screen.getByText('Ошибка')).toBeInTheDocument();
  });

  it('auto-dismisses success/info after its duration', async () => {
    renderHarness();
    fireEvent.click(screen.getByText('fire-info-fast'));
    expect(screen.getByText('Готово')).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByText('Готово')).not.toBeInTheDocument());
  });

  it('shows at most 3 toasts at once', () => {
    renderHarness();
    fireEvent.click(screen.getByText('fire-four'));
    expect(document.querySelectorAll('.toast')).toHaveLength(3);
  });

  it('dismisses a toast via its close button', async () => {
    renderHarness();
    fireEvent.click(screen.getByText('fire-success'));
    fireEvent.click(screen.getByLabelText('Закрыть'));
    await waitFor(() => expect(screen.queryByText('Сохранено')).not.toBeInTheDocument());
  });

  it('runs the optional action and dismisses', async () => {
    renderHarness();
    fireEvent.click(screen.getByText('fire-action'));
    fireEvent.click(screen.getByText('Отменить'));
    expect(undoSpy).toHaveBeenCalled();
    await waitFor(() => expect(screen.queryByText('С действием')).not.toBeInTheDocument());
  });

  it('throws when useToast is used without a provider', () => {
    function Orphan() { useToast(); return null; }
    expect(() => render(<Orphan />)).toThrow(/ToastProvider/);
  });
});
```

- [ ] **Step 4: Run the test to verify it fails**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun test src/operatorToast.test.tsx`
Expected: FAIL — `operatorToast` module / `ToastProvider` / `useToast` do not exist yet.

- [ ] **Step 5: Implement the Toast subsystem**

Create `src/AFK4.Operator.App.Web/src/operatorToast.tsx`:

```tsx
import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { CheckCircle2, Info, X, XCircle, type LucideIcon } from 'lucide-react';
import { useI18n } from '@afk4/i18n';

export type ToastTone = 'success' | 'error' | 'info';
export interface ToastAction { label: string; onClick: () => void }
export interface ToastOptions { tone: ToastTone; message: string; durationMs?: number; action?: ToastAction }

interface ActiveToast { id: string; tone: ToastTone; message: string; durationMs: number; action?: ToastAction }

export interface ToastApi {
  show: (options: ToastOptions) => string;
  success: (message: string, options?: Omit<ToastOptions, 'tone' | 'message'>) => string;
  error: (message: string, options?: Omit<ToastOptions, 'tone' | 'message'>) => string;
  info: (message: string, options?: Omit<ToastOptions, 'tone' | 'message'>) => string;
  dismiss: (id: string) => void;
}

const MAX_VISIBLE = 3;
const DEFAULT_DURATION = 4000;
const TONE_ICON: Record<ToastTone, LucideIcon> = { success: CheckCircle2, error: XCircle, info: Info };

const ToastContext = createContext<ToastApi | null>(null);

export function useToast(): ToastApi {
  const api = useContext(ToastContext);
  if (api === null) {
    throw new Error('useToast must be used within a ToastProvider');
  }
  return api;
}

export function ToastProvider({ children }: { children: ReactNode }) {
  const { t } = useI18n();
  const [toasts, setToasts] = useState<ActiveToast[]>([]);
  const seq = useRef(0);
  const timers = useRef<Map<string, ReturnType<typeof setTimeout>>>(new Map());

  const dismiss = useCallback((id: string) => {
    setToasts((current) => current.filter((toast) => toast.id !== id));
    const timer = timers.current.get(id);
    if (timer !== undefined) {
      clearTimeout(timer);
      timers.current.delete(id);
    }
  }, []);

  const show = useCallback((options: ToastOptions) => {
    seq.current += 1;
    const id = String(seq.current);
    setToasts((current) => [...current, {
      id,
      tone: options.tone,
      message: options.message,
      durationMs: options.durationMs ?? DEFAULT_DURATION,
      action: options.action
    }]);
    return id;
  }, []);

  const api = useMemo<ToastApi>(() => ({
    show,
    success: (message, options) => show({ ...options, tone: 'success', message }),
    error: (message, options) => show({ ...options, tone: 'error', message }),
    info: (message, options) => show({ ...options, tone: 'info', message }),
    dismiss
  }), [show, dismiss]);

  const visible = toasts.slice(0, MAX_VISIBLE);

  // Start the auto-dismiss timer when a toast first becomes VISIBLE (so a queued toast that only
  // appears after a slot frees up still gets its full on-screen lifetime). Errors are sticky.
  useEffect(() => {
    visible.forEach((toast) => {
      if (toast.tone === 'error' || timers.current.has(toast.id)) {
        return;
      }
      timers.current.set(toast.id, setTimeout(() => dismiss(toast.id), toast.durationMs));
    });
  }, [visible, dismiss]);

  useEffect(() => () => {
    timers.current.forEach((timer) => clearTimeout(timer));
    timers.current.clear();
  }, []);

  return (
    <ToastContext.Provider value={api}>
      {children}
      <div className="toast-viewport" role="region" aria-label={t('op.toast.region')}>
        {visible.map((toast) => (
          <ToastCard key={toast.id} toast={toast} closeLabel={t('op.toast.close')} onDismiss={() => dismiss(toast.id)} />
        ))}
      </div>
    </ToastContext.Provider>
  );
}

function ToastCard({ toast, closeLabel, onDismiss }: { toast: ActiveToast; closeLabel: string; onDismiss: () => void }) {
  const Icon = TONE_ICON[toast.tone];
  const isError = toast.tone === 'error';
  return (
    <div className={`toast toast-${toast.tone}`} role={isError ? 'alert' : 'status'} aria-live={isError ? 'assertive' : 'polite'}>
      <Icon className="toast-icon" aria-hidden="true" size={18} />
      <span className="toast-message">{toast.message}</span>
      {toast.action ? (
        <button type="button" className="toast-action" onClick={() => { toast.action!.onClick(); onDismiss(); }}>
          {toast.action.label}
        </button>
      ) : null}
      <button type="button" className="toast-close" aria-label={closeLabel} onClick={onDismiss}>
        <X size={16} aria-hidden="true" />
      </button>
    </div>
  );
}
```

- [ ] **Step 6: Add Toast CSS**

Append to `src/AFK4.Operator.App.Web/src/styles.css` (use existing tokens; light/dark parity comes from the tokens, no hex):

```css
/* Toast — non-blocking ephemeral notices. Motion is CSS-only so the global
   prefers-reduced-motion rule neutralizes it automatically. */
.toast-viewport {
  position: fixed;
  right: var(--space-4, 16px);
  bottom: var(--space-4, 16px);
  z-index: 60;
  display: flex;
  flex-direction: column-reverse;
  gap: var(--space-2, 8px);
  max-width: min(380px, calc(100vw - 32px));
  pointer-events: none;
}

.toast {
  pointer-events: auto;
  display: flex;
  align-items: center;
  gap: var(--space-2, 8px);
  padding: 10px 12px;
  border-radius: var(--radius-md, 8px);
  background: var(--surface-raised, #1d2330);
  color: var(--text-primary, #f4f6fb);
  border-left: 3px solid var(--border-subtle, #3a4256);
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.35);
  animation: toast-in var(--duration-medium, 200ms) var(--ease-out, ease);
}

.toast-success { border-left-color: var(--success, #36b37e); }
.toast-error { border-left-color: var(--danger, #e5484d); }
.toast-info { border-left-color: var(--accent, #4c8dff); }

.toast-icon { flex: none; }
.toast-success .toast-icon { color: var(--success, #36b37e); }
.toast-error .toast-icon { color: var(--danger, #e5484d); }
.toast-info .toast-icon { color: var(--accent, #4c8dff); }

.toast-message { flex: 1 1 auto; font-size: var(--text-base, 14px); line-height: 1.35; }

.toast-action {
  flex: none;
  background: transparent;
  border: none;
  color: var(--accent, #4c8dff);
  font-weight: 600;
  cursor: pointer;
  padding: 2px 4px;
}

.toast-close {
  flex: none;
  display: inline-flex;
  background: transparent;
  border: none;
  color: var(--text-secondary, #aab3c5);
  cursor: pointer;
  padding: 2px;
  border-radius: var(--radius-sm, 6px);
}
.toast-close:hover { color: var(--text-primary, #f4f6fb); }

@keyframes toast-in {
  from { opacity: 0; transform: translateY(8px); }
  to { opacity: 1; transform: translateY(0); }
}
```

(If any referenced token name doesn't exist in `packages/tokens/tokens.css`, the fallback after the comma applies — verify the token names against that file and prefer the real token, dropping the fallback once confirmed.)

- [ ] **Step 7: Run the Toast test to verify it passes**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun test src/operatorToast.test.tsx`
Expected: PASS (all 7 tests).

- [ ] **Step 8: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/operatorToast.tsx src/AFK4.Operator.App.Web/src/operatorToast.test.tsx src/AFK4.Operator.App.Web/src/styles.css locales/ru.json locales/en.json locales/tg.json packages/i18n/src/messages.ts
git commit -m "feat(operator): add Toast primitive (provider, useToast, viewport)"
```

---

## Task 2: Mount ToastProvider in App

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/App.tsx:70-76`

- [ ] **Step 1: Wrap AppInner with ToastProvider**

Add the import near the other local imports in `App.tsx`:
```tsx
import { ToastProvider } from './operatorToast';
```

Replace the `App` component body:
```tsx
export function App() {
  return (
    <I18nProvider>
      <ToastProvider>
        <AppInner />
      </ToastProvider>
    </I18nProvider>
  );
}
```

- [ ] **Step 2: Run the full Operator suite (no regressions)**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun test`
Expected: PASS — every existing test still green (ToastProvider mounts an empty viewport; `<App/>`-based tests now include it).

- [ ] **Step 3: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/App.tsx
git commit -m "feat(operator): mount ToastProvider at app root"
```

---

## Task 3: Skeleton + EmptyState primitives

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/operatorPrimitives.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/operatorPrimitives.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/styles.css`

- [ ] **Step 1: Write the failing primitives test**

Create `src/AFK4.Operator.App.Web/src/operatorPrimitives.test.tsx`:

```tsx
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { EmptyState, Skeleton } from './operatorPrimitives';

afterEach(cleanup);

describe('Skeleton', () => {
  it('renders a block placeholder hidden from a11y, keeping a custom class', () => {
    const { container } = render(<Skeleton className="seat-skeleton" />);
    const block = container.querySelector('.skeleton-block');
    expect(block).not.toBeNull();
    expect(block).toHaveClass('seat-skeleton');
    expect(block).toHaveAttribute('aria-hidden', 'true');
  });

  it('renders the requested number of text lines', () => {
    const { container } = render(<Skeleton variant="text" lines={3} />);
    expect(container.querySelectorAll('.skeleton-block')).toHaveLength(3);
  });

  it('renders a circle variant', () => {
    const { container } = render(<Skeleton variant="circle" />);
    expect(container.querySelector('.skeleton-circle')).not.toBeNull();
  });
});

describe('EmptyState', () => {
  it('renders title and description', () => {
    render(<EmptyState title="Нет ПК" description="Смените фильтр" />);
    expect(screen.getByText('Нет ПК')).toBeInTheDocument();
    expect(screen.getByText('Смените фильтр')).toBeInTheDocument();
  });

  it('renders an action button that fires onClick', () => {
    const onClick = mock(() => {});
    render(<EmptyState title="Пусто" action={{ label: 'Создать', onClick }} />);
    fireEvent.click(screen.getByText('Создать'));
    expect(onClick).toHaveBeenCalled();
  });

  it('omits description and action when not provided', () => {
    const { container } = render(<EmptyState title="Заказов нет" />);
    expect(screen.getByText('Заказов нет')).toBeInTheDocument();
    expect(container.querySelector('button')).toBeNull();
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun test src/operatorPrimitives.test.tsx`
Expected: FAIL — `Skeleton` / `EmptyState` are not exported from `operatorPrimitives`.

- [ ] **Step 3: Implement Skeleton + EmptyState**

Append to `src/AFK4.Operator.App.Web/src/operatorPrimitives.tsx` (the file already imports `ReactNode`):

```tsx
export function Skeleton({
  variant = 'block',
  lines = 1,
  className
}: {
  variant?: 'block' | 'text' | 'circle';
  lines?: number;
  className?: string;
}) {
  if (variant === 'text') {
    return (
      <div className={`skeleton-text-group${className ? ` ${className}` : ''}`} aria-hidden="true">
        {Array.from({ length: lines }).map((_, index) => (
          <div key={index} className="skeleton-block skeleton-text" />
        ))}
      </div>
    );
  }
  const shape = variant === 'circle' ? ' skeleton-circle' : '';
  return <div className={`skeleton-block${shape}${className ? ` ${className}` : ''}`} aria-hidden="true" />;
}

export function EmptyState({
  icon,
  title,
  description,
  action,
  className
}: {
  icon?: ReactNode;
  title: string;
  description?: string;
  action?: { label: string; onClick: () => void };
  className?: string;
}) {
  return (
    <div className={`empty-state${className ? ` ${className}` : ''}`}>
      {icon ? <div className="empty-state-icon" aria-hidden="true">{icon}</div> : null}
      <strong>{title}</strong>
      {description ? <span>{description}</span> : null}
      {action ? (
        <button type="button" className="empty-state-action" onClick={action.onClick}>{action.label}</button>
      ) : null}
    </div>
  );
}
```

- [ ] **Step 4: Add Skeleton/EmptyState CSS**

Append to `src/AFK4.Operator.App.Web/src/styles.css`:

```css
/* Skeleton variant modifiers (base .skeleton-block + skeleton-pulse already exist). */
.skeleton-circle { border-radius: var(--radius-pill, 999px); aspect-ratio: 1; }
.skeleton-text-group { display: flex; flex-direction: column; gap: 6px; }
.skeleton-text { height: 12px; border-radius: var(--radius-xs, 4px); }

/* EmptyState — shared empty-set presentation (0 records is reality, #28). */
.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 6px;
  padding: var(--space-5, 24px);
  text-align: center;
  color: var(--text-secondary, #aab3c5);
}
.empty-state strong { color: var(--text-primary, #f4f6fb); font-size: var(--text-md, 16px); }
.empty-state-icon { color: var(--text-tertiary, #7e8aa3); }
.empty-state-action {
  margin-top: 8px;
  padding: 6px 12px;
  border-radius: var(--radius-sm, 6px);
  border: 1px solid var(--border-subtle, #3a4256);
  background: var(--accent, #4c8dff);
  color: #fff;
  cursor: pointer;
}
```

- [ ] **Step 5: Run the primitives test to verify it passes**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun test src/operatorPrimitives.test.tsx`
Expected: PASS (6 tests).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/operatorPrimitives.tsx src/AFK4.Operator.App.Web/src/operatorPrimitives.test.tsx src/AFK4.Operator.App.Web/src/styles.css
git commit -m "feat(operator): add Skeleton and EmptyState primitives"
```

---

## Task 4: Dogfood Skeleton/EmptyState in Map + Dashboard

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/MapWorkspace.tsx:258-268`
- Modify: `src/AFK4.Operator.App.Web/src/DashboardWorkspace.tsx:353-358`

- [ ] **Step 1: Import primitives in MapWorkspace**

In `MapWorkspace.tsx`, add `Skeleton` and `EmptyState` to the existing import from `./operatorPrimitives` (or add a new import line if none exists):
```tsx
import { EmptyState, Skeleton } from './operatorPrimitives';
```

- [ ] **Step 2: Replace Map seat skeletons and empty state**

Replace the seat-skeleton block (currently lines ~258-262):
```tsx
            <div className="seat-grid" role="status" aria-label={t('op.map.loading')}>
              {Array.from({ length: 10 }).map((_, index) => (
                <Skeleton key={index} className="seat-skeleton" />
              ))}
            </div>
```

Replace the map empty state (currently lines ~265-268):
```tsx
          <EmptyState title={t('op.map.emptyTitle')} description={t('op.map.emptyHint')} className="map-empty-state" />
```

- [ ] **Step 3: Replace Dashboard skeleton blocks**

In `DashboardWorkspace.tsx`, add the import:
```tsx
import { Skeleton } from './operatorPrimitives';
```

Replace the four skeleton divs (currently lines ~354-357):
```tsx
          <Skeleton className="dashboard-skeleton-now" />
          <Skeleton className="dashboard-skeleton-queue" />
          <Skeleton className="dashboard-skeleton-control" />
          <Skeleton className="dashboard-skeleton-pulse" />
```

- [ ] **Step 4: Run the full Operator suite (no regressions)**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun test`
Expected: PASS — Map/Dashboard render identical DOM (`.skeleton-block` + geometry class; empty state with strong+span). Existing tests asserting `op.map.emptyTitle` / loading still pass.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/MapWorkspace.tsx src/AFK4.Operator.App.Web/src/DashboardWorkspace.tsx
git commit -m "refactor(operator): dogfood Skeleton/EmptyState in Map and Dashboard"
```

---

## Task 5: Dogfood Toast + EmptyState in ShopOrders

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/ShopOrdersWorkspace.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/ShopOrdersWorkspace.test.tsx`
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json`

- [ ] **Step 1: Add toast message keys for order actions**

Add three keys to each locale file (real translations; verify Tajik if unsure, #38):

`locales/ru.json`:
```json
  "op.shopOrders.toast.accept": "Заказ принят",
  "op.shopOrders.toast.deliver": "Заказ выдан",
  "op.shopOrders.toast.cancel": "Заказ отменён",
```
`locales/en.json`:
```json
  "op.shopOrders.toast.accept": "Order accepted",
  "op.shopOrders.toast.deliver": "Order delivered",
  "op.shopOrders.toast.cancel": "Order cancelled",
```
`locales/tg.json`:
```json
  "op.shopOrders.toast.accept": "Фармоиш қабул шуд",
  "op.shopOrders.toast.deliver": "Фармоиш супорида шуд",
  "op.shopOrders.toast.cancel": "Фармоиш бекор карда шуд",
```

- [ ] **Step 2: Regenerate i18n messages**

Run: `cd packages/i18n && ~/.bun/bin/bun run gen`
Expected: `messages.ts` includes the three `op.shopOrders.toast.*` keys for all locales.

- [ ] **Step 3: Update the ShopOrders test to expect a toast (failing)**

In `ShopOrdersWorkspace.test.tsx`, import the provider and wrap renders. Add to the imports:
```tsx
import { ToastProvider } from './operatorToast';
```

Find every `render(<ShopOrdersWorkspace ... />)` call and wrap the element in `<ToastProvider>…</ToastProvider>`. There is already an `I18nProvider`/mock wrapper in the file — place `ToastProvider` directly around `<ShopOrdersWorkspace />`. Then add this test inside the existing top-level `describe`:

```tsx
it('confirms an accepted order with a toast', async () => {
  render(
    <ToastProvider>
      <ShopOrdersWorkspace />
    </ToastProvider>
  );
  await waitFor(() => expect(accept).toBeDefined());
  const acceptButton = await screen.findByRole('button', { name: /Принять/ });
  fireEvent.click(acceptButton);
  await waitFor(() => expect(screen.getByText('Заказ принят')).toBeInTheDocument());
});
```

NOTE: match the accept button's accessible name to the real label rendered by `ShopOrdersWorkspace` (open the file and use the exact `op.shopOrders.action.accept` text — `Принять` in ru). If the existing tests already wrap with a shared helper, reuse it; the only required change is that a `ToastProvider` is an ancestor of `ShopOrdersWorkspace`.

- [ ] **Step 4: Run the test to verify it fails**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun test src/ShopOrdersWorkspace.test.tsx`
Expected: FAIL — no toast appears yet (handler doesn't call `useToast`).

- [ ] **Step 5: Wire the toast into the order action and migrate the empty state**

In `ShopOrdersWorkspace.tsx`:

Add imports:
```tsx
import { EmptyState } from './operatorPrimitives';
import { useToast } from './operatorToast';
```

Inside the component (near the top, alongside `useI18n`), get the toast api:
```tsx
  const toast = useToast();
```

In `runAction`, show a success toast after the optimistic update succeeds:
```tsx
    try {
      const updated = await clients.shopOrders[verb](backend.branchId, order.id, order.version);
      setOrders((current) => applyAction(current, { ...order, ...updated }));
      toast.success(t(`op.shopOrders.toast.${verb}`));
    } catch {
      // A 409 means another operator already acted; realtime reconciles the queue.
    }
```

Replace the empty paragraph (currently `<p className="shop-orders-empty">{t('op.shopOrders.empty')}</p>`):
```tsx
        <EmptyState title={t('op.shopOrders.empty')} className="shop-orders-empty" />
```

- [ ] **Step 6: Run the test to verify it passes**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun test src/ShopOrdersWorkspace.test.tsx`
Expected: PASS — accepting an order now surfaces the "Заказ принят" toast; empty-state test still passes.

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/ShopOrdersWorkspace.tsx src/AFK4.Operator.App.Web/src/ShopOrdersWorkspace.test.tsx locales/ru.json locales/en.json locales/tg.json packages/i18n/src/messages.ts
git commit -m "feat(operator): toast on shop-order actions, dogfood EmptyState"
```

---

## Task 6: Final gates + memory

**Files:**
- Modify: `.claude/memory/operator-redesign-phase0-decisions.md`

- [ ] **Step 1: Full Operator gates**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun test && ~/.bun/bin/bun run build`
Expected: all tests pass; build succeeds, 0 errors.

- [ ] **Step 2: Update the resume-point memory**

In `.claude/memory/operator-redesign-phase0-decisions.md`, mark the «Примитивы» piece DONE (Toast/Skeleton/EmptyState introduced + dogfooded; `useDeferredFlag` already existed; warning-tone intentionally absent) and note the only remaining Этап-0 piece is **shell-каркас**, then Этап 1 «Карта».

- [ ] **Step 3: Commit**

```bash
git add .claude/memory/operator-redesign-phase0-decisions.md
git commit -m "docs(operator): mark primitives piece done in resume memory"
```

---

## Self-Review notes

- **Spec coverage:** Toast (§1) → Task 1+2; Skeleton (§2) → Task 3+4; EmptyState (§3) → Task 3+4+5; Toast dogfood (§4) → Task 5; CSS/tokens (§5) → Tasks 1/3; tests (§6) → every task; готовность (§7) → Task 6. `useDeferredFlag`/FeedbackNotice untouched (§8) — no task touches them. Warning tone intentionally absent (§1) — not implemented.
- **Placeholder scan:** the only deferred detail in the spec (exact Toast dogfood site) is resolved here to ShopOrders order actions. Step 5.3 asks the engineer to match the real accept-button label — that is a lookup against existing code, not a placeholder.
- **Type consistency:** `ToastApi` / `ToastTone` / `ToastOptions` / `ToastAction` names match across Task 1 implementation, test, and Task 5 usage (`toast.success(string)`). `Skeleton({variant,lines,className})` and `EmptyState({icon,title,description,action,className})` signatures match between Task 3 implementation, its test, and the Task 4/5 dogfood call sites (Map passes `className="map-empty-state"`, ShopOrders passes `className="shop-orders-empty"`, both valid optional props).
- **Risk:** ShopOrders test already uses `mock.module('./operatorHelpers', …)`; `operatorToast` and `operatorPrimitives` do not import `operatorHelpers`, so the module mock won't interfere. The new `useToast()` requires a `ToastProvider` ancestor — Step 5.3 guarantees it for the isolated test; in production it's present via Task 2.
</content>
