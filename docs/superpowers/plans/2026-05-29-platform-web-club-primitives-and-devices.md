# Club Console — Shared UI Primitives + Devices Screen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Establish the reusable UI primitives and screen patterns that the rest of sub-project 2 depends on (Table, Input, Select, Dialog, Sheet/drawer, Tabs, Toast, confirm-dialog, data-region states), proven end-to-end by redesigning the "Зал и ПК → Устройства / Ожидают" experience, and close the low-risk sub-project-1 Overview polish items.

**Architecture:** Frontend-only, in `src/AFK4.Platform.Web`. New shadcn-style primitives are vendored under `src/components/ui/` in the existing style (the `radix-ui` umbrella package + `class-variance-authority` + the `cn` helper), matching `button.tsx`. The Devices area follows the established sub-project-1 pattern: a pure view-model builder (`buildDevices`) + a data hook returning a discriminated-union state with `retry` (like `useOverview`) + a presentational screen that switches on `state.status`. The list+detail interaction uses the new **Sheet** (drawer); destructive/money-class actions use a shared **ConfirmDialog**; action outcomes are surfaced via a new **Toast** provider. A new `clubVenue` route is added and the "Зал и ПК" (`venue`) nav item is switched on; legacy device routes stay intact (non-breaking).

**Tech Stack:** React 19, TypeScript 6, Vite 8, Tailwind v4, `radix-ui` umbrella primitives, `class-variance-authority`, `lucide-react`, `recharts`; Vitest 4 + jsdom + @testing-library/react (tests colocated, `npm test` = `vitest run`, build gate `npm run build` = `tsc -b && vite build`).

---

## Scope

**In scope:**

- Vendored primitives: Toast (+ provider/hook), Table, Input, Select, Dialog, Sheet, Tabs.
- Shared composites: `ConfirmDialog`, data-region state views (`LoadingCards`, `ErrorState`, `EmptyState`).
- Devices feature: `buildDevices` view-model, `useDevices` hook, `DeviceDrawer`, `DevicesTable`, `VenueScreen` (tabs Устройства / Ожидают).
- Routing: new `clubVenue` route, `venue` nav item switched on, `ClubArea` wiring, screen title.
- i18n: RU/EN keys for venue/devices/common/toast, parity maintained.
- Overview polish (sub-project-1 backlog, low-risk subset): KPI "devices online" denominator fix; revenue-breakdown by-key access; localized recharts tooltip.

**Out of scope (explicitly deferred to later sub-project-2 plans, noted so nothing reads as "done"):**

- "Карта зала" tab (floor-map redesign), Настройки, Операторы и роли, "Все филиалы" dashboard — next existing-screen plan.
- Real branch switching (`onSelectBranch` is still a no-op pilot) and deleting the orphaned `ClubDashboard` (can only be removed once **every** legacy club screen is redesigned, since `LegacyClubScreen` is still reused) — deferred.
- Tooltip, Switch, Checkbox, Textarea, DateRangePicker, Pagination primitives — introduced by the later plans that first need them (YAGNI).

## File Structure

| File | Responsibility |
|------|----------------|
| `src/components/ui/toast.tsx` | Toast provider + `useToast()` hook + toast viewport (pure React, no new deps). |
| `src/components/ui/table.tsx` | Presentational semantic table parts (`Table`, `TableHeader`, `TableBody`, `TableRow`, `TableHead`, `TableCell`). |
| `src/components/ui/input.tsx` | Styled `<input>`. |
| `src/components/ui/select.tsx` | Radix `Select` wrapper (`Select`, `SelectTrigger`, `SelectValue`, `SelectContent`, `SelectItem`). |
| `src/components/ui/dialog.tsx` | Radix `Dialog` wrapper (centered modal parts). |
| `src/components/ui/sheet.tsx` | Radix `Dialog`-based right-side slide-over (the drawer). |
| `src/components/ui/tabs.tsx` | Radix `Tabs` wrapper (underline style). |
| `src/components/ui/states.tsx` | `LoadingCards`, `ErrorState`, `EmptyState` — DRY data-region states. |
| `src/components/shared/ConfirmDialog.tsx` | Reusable confirm-with-reason dialog for destructive actions. |
| `src/club/venue/devicesModel.ts` | `buildDevices` pure view-model + types. |
| `src/club/venue/useDevices.ts` | Data hook (devices + pending + floor map → state + retry). |
| `src/club/venue/DeviceDrawer.tsx` | Detail/edit drawer body: rename, move-seat, approve/reject/remove. |
| `src/club/venue/DevicesTable.tsx` | Presentational device table. |
| `src/club/venue/VenueScreen.tsx` | Tabs host (Устройства / Ожидают) + Sheet wiring. |
| `src/i18n/messages.ts` (modify) | New message keys, RU + EN. |
| `src/App.tsx` (modify) | `clubVenue` route kind, resolver, `ClubArea` render, title, `pathForRoute`. |
| `src/club/nav.ts` (modify) | `venue` item `soon: false`. |
| `src/main.tsx` (modify) | Wrap app in `ToastProvider`. |
| `src/club/overview/overviewModel.ts` (modify) | KPI denominator fix. |
| `src/club/overview/OverviewScreen.tsx` (modify) | By-key revenue access + tooltip formatter. |

---

## Task 1: Toast primitive (provider + hook + viewport)

A minimal, dependency-free toast: a context provides `toast({ title, variant })`; a fixed viewport renders active toasts and auto-dismisses them. Auto-dismiss uses an injectable delay so tests stay deterministic.

**Files:**
- Create: `src/AFK4.Platform.Web/src/components/ui/toast.tsx`
- Test: `src/AFK4.Platform.Web/src/components/ui/toast.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// src/components/ui/toast.test.tsx
import { render, screen, fireEvent, act } from '@testing-library/react';
import { ToastProvider, useToast } from './toast';

function Trigger() {
  const { toast } = useToast();
  return <button onClick={() => toast({ title: 'Сохранено', variant: 'success' })}>fire</button>;
}

it('shows a toast when fired and dismisses after the delay', () => {
  render(<ToastProvider autoDismissMs={1000}><Trigger /></ToastProvider>);
  expect(screen.queryByText('Сохранено')).toBeNull();
  fireEvent.click(screen.getByText('fire'));
  expect(screen.getByText('Сохранено')).toBeInTheDocument();
  act(() => { vi.advanceTimersByTime(1000); });
  expect(screen.queryByText('Сохранено')).toBeNull();
});

it('throws when useToast is used outside the provider', () => {
  function Orphan() { useToast(); return null; }
  expect(() => render(<Orphan />)).toThrow();
});
```

Add `import { vi, beforeEach, afterEach } from 'vitest';` and fake timers:

```tsx
beforeEach(() => { vi.useFakeTimers(); });
afterEach(() => { vi.useRealTimers(); });
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- toast`
Expected: FAIL — `Cannot find module './toast'`.

- [ ] **Step 3: Write minimal implementation**

```tsx
// src/components/ui/toast.tsx
import { createContext, useCallback, useContext, useRef, useState, type ReactNode } from 'react';
import { cn } from '@/lib/utils';

export type ToastVariant = 'success' | 'error';
export interface ToastOptions { title: string; variant?: ToastVariant; }
interface ActiveToast extends ToastOptions { id: number; }

interface ToastContextValue { toast: (options: ToastOptions) => void; }
const ToastContext = createContext<ToastContextValue | null>(null);

export function ToastProvider({ children, autoDismissMs = 4000 }: { children: ReactNode; autoDismissMs?: number }) {
  const [toasts, setToasts] = useState<ActiveToast[]>([]);
  const nextId = useRef(0);

  const toast = useCallback((options: ToastOptions) => {
    const id = nextId.current++;
    setToasts(prev => [...prev, { id, variant: 'success', ...options }]);
    setTimeout(() => setToasts(prev => prev.filter(t => t.id !== id)), autoDismissMs);
  }, [autoDismissMs]);

  return (
    <ToastContext.Provider value={{ toast }}>
      {children}
      <div className="pointer-events-none fixed bottom-4 right-4 z-[60] flex flex-col gap-2" role="region" aria-label="Уведомления">
        {toasts.map(t => (
          <div
            key={t.id}
            role="status"
            className={cn(
              'pointer-events-auto rounded-md border px-4 py-3 text-sm shadow-md',
              t.variant === 'error'
                ? 'border-destructive/30 bg-destructive text-destructive-foreground'
                : 'border-border bg-card text-card-foreground'
            )}
          >
            {t.title}
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  );
}

export function useToast(): ToastContextValue {
  const ctx = useContext(ToastContext);
  if (ctx === null) throw new Error('useToast must be used within ToastProvider');
  return ctx;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- toast`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/components/ui/toast.tsx src/components/ui/toast.test.tsx
git commit -m "feat(web): add Toast provider and useToast hook"
```

---

## Task 2: Mount ToastProvider in the app root

**Files:**
- Modify: `src/AFK4.Platform.Web/src/main.tsx`

- [ ] **Step 1: Read the current file**

Run: open `src/main.tsx`. It currently wraps `<App />` in `<ThemeProvider><I18nProvider>`. Add `ToastProvider` as the innermost wrapper so screens can call `useToast()`.

- [ ] **Step 2: Edit**

Add the import and wrap. The provider tree becomes:

```tsx
import { ToastProvider } from './components/ui/toast';
// ...
<ThemeProvider>
  <I18nProvider>
    <ToastProvider>
      <App apiBaseUrl={apiBaseUrl} />
    </ToastProvider>
  </I18nProvider>
</ThemeProvider>
```

(Keep the existing `apiBaseUrl`/props exactly as they are in the current file — only insert the `ToastProvider` wrapper and its import.)

- [ ] **Step 3: Verify build**

Run: `npm run build`
Expected: PASS (tsc + vite, no type errors).

- [ ] **Step 4: Commit**

```bash
git add src/main.tsx
git commit -m "feat(web): mount ToastProvider at app root"
```

---

## Task 3: Table primitive

Presentational semantic table parts. No logic — render-only.

**Files:**
- Create: `src/AFK4.Platform.Web/src/components/ui/table.tsx`
- Test: `src/AFK4.Platform.Web/src/components/ui/table.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// src/components/ui/table.test.tsx
import { render, screen } from '@testing-library/react';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from './table';

it('renders a semantic table with header and cells', () => {
  render(
    <Table>
      <TableHeader><TableRow><TableHead>Имя</TableHead></TableRow></TableHeader>
      <TableBody><TableRow><TableCell>ПК-1</TableCell></TableRow></TableBody>
    </Table>
  );
  expect(screen.getByRole('table')).toBeInTheDocument();
  expect(screen.getByRole('columnheader', { name: 'Имя' })).toBeInTheDocument();
  expect(screen.getByRole('cell', { name: 'ПК-1' })).toBeInTheDocument();
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- table`
Expected: FAIL — `Cannot find module './table'`.

- [ ] **Step 3: Write minimal implementation**

```tsx
// src/components/ui/table.tsx
import type { ComponentProps } from 'react';
import { cn } from '@/lib/utils';

export function Table({ className, ...props }: ComponentProps<'table'>) {
  return (
    <div className="w-full overflow-x-auto">
      <table className={cn('w-full caption-bottom text-sm', className)} {...props} />
    </div>
  );
}
export function TableHeader({ className, ...props }: ComponentProps<'thead'>) {
  return <thead className={cn('[&_tr]:border-b [&_tr]:border-border', className)} {...props} />;
}
export function TableBody({ className, ...props }: ComponentProps<'tbody'>) {
  return <tbody className={cn('[&_tr:last-child]:border-0', className)} {...props} />;
}
export function TableRow({ className, ...props }: ComponentProps<'tr'>) {
  return <tr className={cn('border-b border-border transition-colors hover:bg-accent/50 data-[clickable=true]:cursor-pointer', className)} {...props} />;
}
export function TableHead({ className, ...props }: ComponentProps<'th'>) {
  return <th className={cn('h-10 px-3 text-left align-middle text-xs font-medium uppercase tracking-wide text-muted-foreground', className)} {...props} />;
}
export function TableCell({ className, ...props }: ComponentProps<'td'>) {
  return <td className={cn('px-3 py-2.5 align-middle', className)} {...props} />;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- table`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/components/ui/table.tsx src/components/ui/table.test.tsx
git commit -m "feat(web): add Table primitive"
```

---

## Task 4: Input primitive

**Files:**
- Create: `src/AFK4.Platform.Web/src/components/ui/input.tsx`
- Test: `src/AFK4.Platform.Web/src/components/ui/input.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// src/components/ui/input.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { useState } from 'react';
import { Input } from './input';

it('renders and is controllable', () => {
  function Host() {
    const [v, setV] = useState('');
    return <Input aria-label="name" value={v} onChange={e => setV(e.target.value)} />;
  }
  render(<Host />);
  const input = screen.getByLabelText('name') as HTMLInputElement;
  fireEvent.change(input, { target: { value: 'ПК-7' } });
  expect(input.value).toBe('ПК-7');
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- input`
Expected: FAIL — `Cannot find module './input'`.

- [ ] **Step 3: Write minimal implementation**

```tsx
// src/components/ui/input.tsx
import type { ComponentProps } from 'react';
import { cn } from '@/lib/utils';

export function Input({ className, ...props }: ComponentProps<'input'>) {
  return (
    <input
      data-slot="input"
      className={cn(
        'flex h-9 w-full rounded-md border border-input bg-background px-3 py-1 text-sm shadow-xs outline-none transition-colors',
        'placeholder:text-muted-foreground focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50',
        'disabled:cursor-not-allowed disabled:opacity-50',
        className
      )}
      {...props}
    />
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- input`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/components/ui/input.tsx src/components/ui/input.test.tsx
git commit -m "feat(web): add Input primitive"
```

---

## Task 5: Select primitive (Radix)

Wrap the `radix-ui` umbrella `Select` namespace. Uses `lucide-react` chevron/check icons (already a dependency).

**Files:**
- Create: `src/AFK4.Platform.Web/src/components/ui/select.tsx`
- Test: `src/AFK4.Platform.Web/src/components/ui/select.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// src/components/ui/select.test.tsx
import { render, screen } from '@testing-library/react';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from './select';

it('renders the trigger with a placeholder', () => {
  render(
    <Select>
      <SelectTrigger aria-label="seat"><SelectValue placeholder="Выберите место" /></SelectTrigger>
      <SelectContent>
        <SelectItem value="s1">Зона A · Место 1</SelectItem>
      </SelectContent>
    </Select>
  );
  expect(screen.getByLabelText('seat')).toBeInTheDocument();
  expect(screen.getByText('Выберите место')).toBeInTheDocument();
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- select`
Expected: FAIL — `Cannot find module './select'`.

- [ ] **Step 3: Write minimal implementation**

```tsx
// src/components/ui/select.tsx
import type { ComponentProps } from 'react';
import { Select as SelectPrimitive } from 'radix-ui';
import { ChevronDown, Check } from 'lucide-react';
import { cn } from '@/lib/utils';

export const Select = SelectPrimitive.Root;
export const SelectValue = SelectPrimitive.Value;

export function SelectTrigger({ className, children, ...props }: ComponentProps<typeof SelectPrimitive.Trigger>) {
  return (
    <SelectPrimitive.Trigger
      className={cn(
        'flex h-9 w-full items-center justify-between rounded-md border border-input bg-background px-3 py-2 text-sm shadow-xs outline-none',
        'focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 disabled:cursor-not-allowed disabled:opacity-50',
        className
      )}
      {...props}
    >
      {children}
      <SelectPrimitive.Icon><ChevronDown className="size-4 opacity-60" /></SelectPrimitive.Icon>
    </SelectPrimitive.Trigger>
  );
}

export function SelectContent({ className, children, ...props }: ComponentProps<typeof SelectPrimitive.Content>) {
  return (
    <SelectPrimitive.Portal>
      <SelectPrimitive.Content
        position="popper"
        className={cn(
          'z-50 min-w-[8rem] overflow-hidden rounded-md border border-border bg-popover text-popover-foreground shadow-md',
          className
        )}
        {...props}
      >
        <SelectPrimitive.Viewport className="p-1">{children}</SelectPrimitive.Viewport>
      </SelectPrimitive.Content>
    </SelectPrimitive.Portal>
  );
}

export function SelectItem({ className, children, ...props }: ComponentProps<typeof SelectPrimitive.Item>) {
  return (
    <SelectPrimitive.Item
      className={cn(
        'relative flex w-full cursor-pointer select-none items-center rounded-sm py-1.5 pl-8 pr-2 text-sm outline-none',
        'focus:bg-accent focus:text-accent-foreground data-[disabled]:pointer-events-none data-[disabled]:opacity-50',
        className
      )}
      {...props}
    >
      <span className="absolute left-2 flex size-4 items-center justify-center">
        <SelectPrimitive.ItemIndicator><Check className="size-4" /></SelectPrimitive.ItemIndicator>
      </span>
      <SelectPrimitive.ItemText>{children}</SelectPrimitive.ItemText>
    </SelectPrimitive.Item>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- select`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/components/ui/select.tsx src/components/ui/select.test.tsx
git commit -m "feat(web): add Select primitive"
```

---

## Task 6: Dialog primitive (Radix)

Centered modal parts, used by `ConfirmDialog`.

**Files:**
- Create: `src/AFK4.Platform.Web/src/components/ui/dialog.tsx`
- Test: `src/AFK4.Platform.Web/src/components/ui/dialog.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// src/components/ui/dialog.test.tsx
import { render, screen } from '@testing-library/react';
import { Dialog, DialogContent, DialogTitle } from './dialog';

it('renders content when controlled-open', () => {
  render(
    <Dialog open onOpenChange={() => {}}>
      <DialogContent><DialogTitle>Удалить устройство?</DialogTitle></DialogContent>
    </Dialog>
  );
  expect(screen.getByText('Удалить устройство?')).toBeInTheDocument();
});

it('does not render content when closed', () => {
  render(
    <Dialog open={false} onOpenChange={() => {}}>
      <DialogContent><DialogTitle>Удалить устройство?</DialogTitle></DialogContent>
    </Dialog>
  );
  expect(screen.queryByText('Удалить устройство?')).toBeNull();
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- "ui/dialog"`
Expected: FAIL — `Cannot find module './dialog'`.

- [ ] **Step 3: Write minimal implementation**

```tsx
// src/components/ui/dialog.tsx
import type { ComponentProps } from 'react';
import { Dialog as DialogPrimitive } from 'radix-ui';
import { cn } from '@/lib/utils';

export const Dialog = DialogPrimitive.Root;
export const DialogTrigger = DialogPrimitive.Trigger;
export const DialogClose = DialogPrimitive.Close;

export function DialogContent({ className, children, ...props }: ComponentProps<typeof DialogPrimitive.Content>) {
  return (
    <DialogPrimitive.Portal>
      <DialogPrimitive.Overlay className="fixed inset-0 z-50 bg-black/40" />
      <DialogPrimitive.Content
        className={cn(
          'fixed left-1/2 top-1/2 z-50 w-full max-w-md -translate-x-1/2 -translate-y-1/2 rounded-lg border border-border bg-card p-5 shadow-lg outline-none',
          className
        )}
        {...props}
      >
        {children}
      </DialogPrimitive.Content>
    </DialogPrimitive.Portal>
  );
}

export function DialogTitle({ className, ...props }: ComponentProps<typeof DialogPrimitive.Title>) {
  return <DialogPrimitive.Title className={cn('text-base font-semibold', className)} {...props} />;
}
export function DialogDescription({ className, ...props }: ComponentProps<typeof DialogPrimitive.Description>) {
  return <DialogPrimitive.Description className={cn('mt-1 text-sm text-muted-foreground', className)} {...props} />;
}
export function DialogFooter({ className, ...props }: ComponentProps<'div'>) {
  return <div className={cn('mt-5 flex justify-end gap-2', className)} {...props} />;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- "ui/dialog"`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/components/ui/dialog.tsx src/components/ui/dialog.test.tsx
git commit -m "feat(web): add Dialog primitive"
```

---

## Task 7: Sheet primitive (right-side drawer)

A right-anchored slide-over built on Radix `Dialog`. This is the canonical list+detail surface.

**Files:**
- Create: `src/AFK4.Platform.Web/src/components/ui/sheet.tsx`
- Test: `src/AFK4.Platform.Web/src/components/ui/sheet.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// src/components/ui/sheet.test.tsx
import { render, screen } from '@testing-library/react';
import { Sheet, SheetContent, SheetTitle } from './sheet';

it('renders the drawer content when open', () => {
  render(
    <Sheet open onOpenChange={() => {}}>
      <SheetContent><SheetTitle>ПК-3</SheetTitle></SheetContent>
    </Sheet>
  );
  expect(screen.getByText('ПК-3')).toBeInTheDocument();
});

it('renders nothing when closed', () => {
  render(
    <Sheet open={false} onOpenChange={() => {}}>
      <SheetContent><SheetTitle>ПК-3</SheetTitle></SheetContent>
    </Sheet>
  );
  expect(screen.queryByText('ПК-3')).toBeNull();
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- sheet`
Expected: FAIL — `Cannot find module './sheet'`.

- [ ] **Step 3: Write minimal implementation**

```tsx
// src/components/ui/sheet.tsx
import type { ComponentProps } from 'react';
import { Dialog as DialogPrimitive } from 'radix-ui';
import { X } from 'lucide-react';
import { cn } from '@/lib/utils';

export const Sheet = DialogPrimitive.Root;
export const SheetClose = DialogPrimitive.Close;

export function SheetContent({ className, children, ...props }: ComponentProps<typeof DialogPrimitive.Content>) {
  return (
    <DialogPrimitive.Portal>
      <DialogPrimitive.Overlay className="fixed inset-0 z-50 bg-black/40" />
      <DialogPrimitive.Content
        className={cn(
          'fixed inset-y-0 right-0 z-50 flex w-full max-w-md flex-col gap-4 border-l border-border bg-card p-5 shadow-lg outline-none',
          className
        )}
        {...props}
      >
        {children}
        <DialogPrimitive.Close
          aria-label="Закрыть"
          className="absolute right-4 top-4 rounded-sm opacity-60 transition-opacity hover:opacity-100"
        >
          <X className="size-4" />
        </DialogPrimitive.Close>
      </DialogPrimitive.Content>
    </DialogPrimitive.Portal>
  );
}

export function SheetTitle({ className, ...props }: ComponentProps<typeof DialogPrimitive.Title>) {
  return <DialogPrimitive.Title className={cn('text-lg font-semibold', className)} {...props} />;
}
export function SheetDescription({ className, ...props }: ComponentProps<typeof DialogPrimitive.Description>) {
  return <DialogPrimitive.Description className={cn('text-sm text-muted-foreground', className)} {...props} />;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- sheet`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/components/ui/sheet.tsx src/components/ui/sheet.test.tsx
git commit -m "feat(web): add Sheet (drawer) primitive"
```

---

## Task 8: Tabs primitive (Radix)

Underline-style tabs for the "Зал и ПК" area.

**Files:**
- Create: `src/AFK4.Platform.Web/src/components/ui/tabs.tsx`
- Test: `src/AFK4.Platform.Web/src/components/ui/tabs.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// src/components/ui/tabs.test.tsx
import { render, screen } from '@testing-library/react';
import { Tabs, TabsList, TabsTrigger, TabsContent } from './tabs';

it('shows the active tab content', () => {
  render(
    <Tabs defaultValue="devices">
      <TabsList>
        <TabsTrigger value="devices">Устройства</TabsTrigger>
        <TabsTrigger value="pending">Ожидают</TabsTrigger>
      </TabsList>
      <TabsContent value="devices">Список устройств</TabsContent>
      <TabsContent value="pending">Очередь</TabsContent>
    </Tabs>
  );
  expect(screen.getByText('Список устройств')).toBeInTheDocument();
  expect(screen.getByRole('tab', { name: 'Устройства' })).toHaveAttribute('data-state', 'active');
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- tabs`
Expected: FAIL — `Cannot find module './tabs'`.

- [ ] **Step 3: Write minimal implementation**

```tsx
// src/components/ui/tabs.tsx
import type { ComponentProps } from 'react';
import { Tabs as TabsPrimitive } from 'radix-ui';
import { cn } from '@/lib/utils';

export const Tabs = TabsPrimitive.Root;

export function TabsList({ className, ...props }: ComponentProps<typeof TabsPrimitive.List>) {
  return <TabsPrimitive.List className={cn('flex gap-1 border-b border-border', className)} {...props} />;
}

export function TabsTrigger({ className, ...props }: ComponentProps<typeof TabsPrimitive.Trigger>) {
  return (
    <TabsPrimitive.Trigger
      className={cn(
        '-mb-px border-b-2 border-transparent px-3 py-2 text-sm font-medium text-muted-foreground transition-colors outline-none',
        'hover:text-foreground data-[state=active]:border-primary data-[state=active]:text-foreground',
        className
      )}
      {...props}
    />
  );
}

export function TabsContent({ className, ...props }: ComponentProps<typeof TabsPrimitive.Content>) {
  return <TabsPrimitive.Content className={cn('pt-4 outline-none', className)} {...props} />;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- tabs`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/components/ui/tabs.tsx src/components/ui/tabs.test.tsx
git commit -m "feat(web): add Tabs primitive"
```

---

## Task 9: Data-region state views (DRY loading/empty/error)

Small presentational components so every screen renders consistent states. They take localized strings as props (no i18n coupling) to stay pure and reusable.

**Files:**
- Create: `src/AFK4.Platform.Web/src/components/ui/states.tsx`
- Test: `src/AFK4.Platform.Web/src/components/ui/states.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// src/components/ui/states.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { LoadingCards, ErrorState, EmptyState } from './states';

it('renders the requested number of loading skeletons', () => {
  render(<LoadingCards count={3} />);
  expect(screen.getAllByTestId('loading-skeleton')).toHaveLength(3);
});

it('renders an error message and calls retry', () => {
  const retry = vi.fn();
  render(<ErrorState message="Не удалось загрузить" retryLabel="Повторить" onRetry={retry} />);
  fireEvent.click(screen.getByRole('button', { name: 'Повторить' }));
  expect(retry).toHaveBeenCalledOnce();
});

it('renders an empty message', () => {
  render(<EmptyState message="Пусто" />);
  expect(screen.getByText('Пусто')).toBeInTheDocument();
});
```

(Add `import { vi } from 'vitest';`.)

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- states`
Expected: FAIL — `Cannot find module './states'`.

- [ ] **Step 3: Write minimal implementation**

```tsx
// src/components/ui/states.tsx
import { Button } from './button';
import { Card, CardContent } from './card';
import { Skeleton } from './skeleton';

export function LoadingCards({ count = 4 }: { count?: number }) {
  return (
    <div className="grid grid-cols-1 gap-4 md:grid-cols-4">
      {Array.from({ length: count }, (_, i) => (
        <Skeleton key={i} data-testid="loading-skeleton" className="h-24 w-full rounded-lg" />
      ))}
    </div>
  );
}

export function ErrorState({ message, retryLabel, onRetry }: { message: string; retryLabel: string; onRetry: () => void }) {
  return (
    <Card><CardContent className="flex flex-col items-center gap-3 py-10">
      <p className="text-muted-foreground">{message}</p>
      <Button onClick={onRetry}>{retryLabel}</Button>
    </CardContent></Card>
  );
}

export function EmptyState({ message }: { message: string }) {
  return (
    <Card><CardContent className="py-10 text-center text-sm text-muted-foreground">{message}</CardContent></Card>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- states`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/components/ui/states.tsx src/components/ui/states.test.tsx
git commit -m "feat(web): add shared data-region state views"
```

---

## Task 10: ConfirmDialog (destructive-action gate with reason)

Reusable confirm dialog for destructive/irreversible actions (device remove/reject; later: refunds, corrections). Captures an optional reason and reports the typed reason on confirm. Pure presentational + controlled.

**Files:**
- Create: `src/AFK4.Platform.Web/src/components/shared/ConfirmDialog.tsx`
- Test: `src/AFK4.Platform.Web/src/components/shared/ConfirmDialog.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// src/components/shared/ConfirmDialog.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { ConfirmDialog } from './ConfirmDialog';

it('confirms with the typed reason and disables confirm while pending', () => {
  const onConfirm = vi.fn();
  render(
    <ConfirmDialog
      open
      title="Удалить устройство?"
      description="Действие необратимо."
      confirmLabel="Удалить"
      cancelLabel="Отмена"
      reasonLabel="Причина"
      destructive
      pending={false}
      onConfirm={onConfirm}
      onOpenChange={() => {}}
    />
  );
  fireEvent.change(screen.getByLabelText('Причина'), { target: { value: 'списано' } });
  fireEvent.click(screen.getByRole('button', { name: 'Удалить' }));
  expect(onConfirm).toHaveBeenCalledWith('списано');
});

it('disables the confirm button while pending', () => {
  render(
    <ConfirmDialog open title="t" confirmLabel="Удалить" cancelLabel="Отмена"
      pending onConfirm={() => {}} onOpenChange={() => {}} />
  );
  expect(screen.getByRole('button', { name: 'Удалить' })).toBeDisabled();
});
```

(Add `import { vi } from 'vitest';`.)

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- ConfirmDialog`
Expected: FAIL — `Cannot find module './ConfirmDialog'`.

- [ ] **Step 3: Write minimal implementation**

```tsx
// src/components/shared/ConfirmDialog.tsx
import { useState } from 'react';
import { Dialog, DialogContent, DialogTitle, DialogDescription, DialogFooter } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';

export interface ConfirmDialogProps {
  open: boolean;
  title: string;
  description?: string;
  confirmLabel: string;
  cancelLabel: string;
  reasonLabel?: string;
  destructive?: boolean;
  pending: boolean;
  onConfirm: (reason: string) => void;
  onOpenChange: (open: boolean) => void;
}

export function ConfirmDialog(props: ConfirmDialogProps) {
  const [reason, setReason] = useState('');
  return (
    <Dialog open={props.open} onOpenChange={props.onOpenChange}>
      <DialogContent>
        <DialogTitle>{props.title}</DialogTitle>
        {props.description && <DialogDescription>{props.description}</DialogDescription>}
        {props.reasonLabel && (
          <label className="mt-3 block text-sm">
            <span className="mb-1 block text-muted-foreground">{props.reasonLabel}</span>
            <Input aria-label={props.reasonLabel} value={reason} onChange={e => setReason(e.target.value)} />
          </label>
        )}
        <DialogFooter>
          <Button variant="outline" disabled={props.pending} onClick={() => props.onOpenChange(false)}>
            {props.cancelLabel}
          </Button>
          <Button
            variant={props.destructive ? 'destructive' : 'default'}
            disabled={props.pending}
            onClick={() => props.onConfirm(reason)}
          >
            {props.confirmLabel}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- ConfirmDialog`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/components/shared/ConfirmDialog.tsx src/components/shared/ConfirmDialog.test.tsx
git commit -m "feat(web): add reusable ConfirmDialog"
```

---

## Task 11: Devices view-model (`buildDevices`)

Pure transform of the device + pending + floor-map DTOs into table rows + seat options. Mirrors `buildOverview`.

**Files:**
- Create: `src/AFK4.Platform.Web/src/club/venue/devicesModel.ts`
- Test: `src/AFK4.Platform.Web/src/club/venue/devicesModel.test.ts`

- [ ] **Step 1: Write the failing test**

```ts
// src/club/venue/devicesModel.test.ts
import { buildDevices } from './devicesModel';
import type { DeviceInventoryItem, FloorMap } from '@/api/types';

function device(over: Partial<DeviceInventoryItem>): DeviceInventoryItem {
  return {
    organizationId: 'org', branchId: 'b1', deviceId: 'd1', machineName: 'PC-RAW',
    agentVersion: '1', shellVersion: '1', enrolledAtUtc: '2026-05-01T00:00:00Z',
    lastHeartbeatAtUtc: null, isOnline: true, isLocked: false,
    seatId: null, seatName: null, zoneId: null, zoneName: null,
    activeCredentialCount: 0, installedAppCount: 0, pendingCommandCount: 0,
    failedCommandCount: 0, displayName: 'ПК-1', role: 'workstation', enrollmentState: 'active',
    ...over
  };
}

const floorMap: FloorMap = {
  branchId: 'b1', branchName: 'Главный', zones: [{ zoneId: 'z1', name: 'Зона A', sortOrder: 0 }],
  seats: [{
    seatId: 's1', seatName: 'Место 1', zoneId: 'z1', zoneName: 'Зона A', sortOrder: 0,
    state: 'free', deviceId: null, deviceName: null, isDeviceOnline: null, isDeviceLocked: null,
    lastHeartbeatAtUtc: null, agentVersion: null, shellVersion: null, activeSessionId: null, remainingSeconds: null
  }]
};

it('maps online/offline status and seat label', () => {
  const vm = buildDevices(
    [device({ deviceId: 'on', isOnline: true, zoneName: 'Зона A', seatName: 'Место 1', seatId: 's1' }),
     device({ deviceId: 'off', isOnline: false })],
    [],
    floorMap
  );
  expect(vm.active.find(r => r.deviceId === 'on')!.status).toBe('online');
  expect(vm.active.find(r => r.deviceId === 'on')!.seatLabel).toBe('Зона A · Место 1');
  expect(vm.active.find(r => r.deviceId === 'off')!.status).toBe('offline');
  expect(vm.active.find(r => r.deviceId === 'off')!.seatLabel).toBe('—');
});

it('marks pending devices and builds seat options', () => {
  const vm = buildDevices([], [device({ deviceId: 'p', enrollmentState: 'pending' })], floorMap);
  expect(vm.pending[0].status).toBe('pending');
  expect(vm.seatOptions).toEqual([{ seatId: 's1', label: 'Зона A · Место 1' }]);
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- devicesModel`
Expected: FAIL — `Cannot find module './devicesModel'`.

- [ ] **Step 3: Write minimal implementation**

```ts
// src/club/venue/devicesModel.ts
import type { DeviceInventoryItem, FloorMap } from '@/api/types';

export type DeviceRowStatus = 'online' | 'offline' | 'pending';

export interface DeviceRow {
  deviceId: string;
  organizationId: string;
  displayName: string;
  machineName: string;
  seatId: string | null;
  seatLabel: string;
  status: DeviceRowStatus;
  lastHeartbeatAtUtc: string | null;
  failedCommandCount: number;
}

export interface SeatOption { seatId: string; label: string; }

export interface DevicesViewModel {
  active: DeviceRow[];
  pending: DeviceRow[];
  seatOptions: SeatOption[];
}

function seatLabel(device: DeviceInventoryItem): string {
  if (device.zoneName && device.seatName) return `${device.zoneName} · ${device.seatName}`;
  if (device.seatName) return device.seatName;
  return '—';
}

function toRow(device: DeviceInventoryItem, status: DeviceRowStatus): DeviceRow {
  return {
    deviceId: device.deviceId,
    organizationId: device.organizationId,
    displayName: device.displayName,
    machineName: device.machineName,
    seatId: device.seatId,
    seatLabel: seatLabel(device),
    status,
    lastHeartbeatAtUtc: device.lastHeartbeatAtUtc,
    failedCommandCount: device.failedCommandCount
  };
}

export function buildDevices(
  devices: DeviceInventoryItem[],
  pending: DeviceInventoryItem[],
  floorMap: FloorMap
): DevicesViewModel {
  return {
    active: devices.map(d => toRow(d, d.isOnline ? 'online' : 'offline')),
    pending: pending.map(d => toRow(d, 'pending')),
    seatOptions: floorMap.seats.map(s => ({ seatId: s.seatId, label: `${s.zoneName} · ${s.seatName}` }))
  };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- devicesModel`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/venue/devicesModel.ts src/club/venue/devicesModel.test.ts
git commit -m "feat(web): add devices view-model builder"
```

---

## Task 12: `useDevices` hook

Loads devices + pending + floor map in parallel, builds the view-model, exposes a discriminated-union state with `retry`. Mirrors `useOverview` exactly (uses `useRef` for the client to avoid effect churn; deps `[branchId, tick]`).

**Files:**
- Create: `src/AFK4.Platform.Web/src/club/venue/useDevices.ts`
- Test: `src/AFK4.Platform.Web/src/club/venue/useDevices.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// src/club/venue/useDevices.test.tsx
import { renderHook, waitFor, act } from '@testing-library/react';
import { useDevices } from './useDevices';
import type { FloorMap } from '@/api/types';

const floorMap: FloorMap = { branchId: 'b1', branchName: 'X', zones: [], seats: [] };

function fakeClient(overrides: Partial<Record<'listDevices' | 'listPendingDevices' | 'getFloorMap', unknown>> = {}) {
  return {
    listDevices: vi.fn().mockResolvedValue([]),
    listPendingDevices: vi.fn().mockResolvedValue([]),
    getFloorMap: vi.fn().mockResolvedValue({ etag: null, floorMap }),
    ...overrides
  } as never;
}

it('reaches ready state with a view-model', async () => {
  const { result } = renderHook(() => useDevices(fakeClient(), 'b1'));
  await waitFor(() => expect(result.current.status).toBe('ready'));
  if (result.current.status === 'ready') expect(result.current.data.active).toEqual([]);
});

it('reaches error state and retry reloads', async () => {
  const client = fakeClient({ listDevices: vi.fn().mockRejectedValueOnce(new Error('boom')).mockResolvedValue([]) });
  const { result } = renderHook(() => useDevices(client, 'b1'));
  await waitFor(() => expect(result.current.status).toBe('error'));
  act(() => result.current.retry());
  await waitFor(() => expect(result.current.status).toBe('ready'));
});
```

(Add `import { vi } from 'vitest';`.)

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- useDevices`
Expected: FAIL — `Cannot find module './useDevices'`.

- [ ] **Step 3: Write minimal implementation**

```ts
// src/club/venue/useDevices.ts
import { useCallback, useEffect, useRef, useState } from 'react';
import type { ClubApiClient } from '@/api/clubApi';
import { buildDevices, type DevicesViewModel } from './devicesModel';

export type DevicesState =
  | { status: 'loading'; retry: () => void }
  | { status: 'error'; message: string; retry: () => void }
  | { status: 'ready'; data: DevicesViewModel; retry: () => void };

type Loadable = Pick<ClubApiClient, 'listDevices' | 'listPendingDevices' | 'getFloorMap'>;

export function useDevices(client: Loadable, branchId: string): DevicesState {
  const [tick, setTick] = useState(0);
  const [state, setState] = useState<{ status: 'loading' | 'error' | 'ready'; data?: DevicesViewModel; message?: string }>({ status: 'loading' });
  const retry = useCallback(() => setTick(t => t + 1), []);
  const clientRef = useRef(client);
  clientRef.current = client;

  useEffect(() => {
    let cancelled = false;
    setState({ status: 'loading' });
    const c = clientRef.current;
    Promise.all([c.listDevices(branchId), c.listPendingDevices(branchId), c.getFloorMap(branchId)])
      .then(([devices, pending, floor]) => {
        if (cancelled) return;
        setState({ status: 'ready', data: buildDevices(devices, pending, floor.floorMap) });
      })
      .catch((err: unknown) => {
        if (cancelled) return;
        setState({ status: 'error', message: err instanceof Error ? err.message : 'error' });
      });
    return () => { cancelled = true; };
  }, [branchId, tick]);

  if (state.status === 'ready') return { status: 'ready', data: state.data!, retry };
  if (state.status === 'error') return { status: 'error', message: state.message ?? 'error', retry };
  return { status: 'loading', retry };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- useDevices`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/venue/useDevices.ts src/club/venue/useDevices.test.tsx
git commit -m "feat(web): add useDevices data hook"
```

---

## Task 13: i18n keys for venue / devices / common / toast

Add all new keys to BOTH `ru` and `en`, keeping parity (the `MessageKey` type is derived from `messages.ru`, so EN must match or `t()` falls back to the key).

**Files:**
- Modify: `src/AFK4.Platform.Web/src/i18n/messages.ts`
- Test: `src/AFK4.Platform.Web/src/i18n/messages.test.ts`

- [ ] **Step 1: Write the failing test**

```ts
// src/i18n/messages.test.ts
import { messages } from './messages';

it('ru and en have identical key sets', () => {
  expect(Object.keys(messages.en).sort()).toEqual(Object.keys(messages.ru).sort());
});

it('includes the new venue/devices keys', () => {
  for (const key of ['venue.title', 'venue.tab.devices', 'venue.tab.pending',
    'devices.col.name', 'devices.action.rename', 'devices.action.remove',
    'common.save', 'common.cancel', 'toast.saved'] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- messages`
Expected: FAIL — keys undefined.

- [ ] **Step 3: Add the keys**

Insert these entries into `messages.ru` and the matching English into `messages.en` (place before the closing `}` of each locale object, keeping the existing `as const`):

```ts
// add inside messages.ru
'venue.title': 'Зал и ПК',
'venue.tab.devices': 'Устройства',
'venue.tab.pending': 'Ожидают',
'venue.tab.map': 'Карта зала',
'venue.map.soon': 'Редактор карты зала появится в следующем обновлении.',
'devices.col.name': 'Устройство',
'devices.col.seat': 'Место',
'devices.col.status': 'Статус',
'devices.col.heartbeat': 'Связь',
'devices.status.online': 'Онлайн',
'devices.status.offline': 'Офлайн',
'devices.status.pending': 'Ожидает',
'devices.empty.active': 'Нет подключённых устройств.',
'devices.empty.pending': 'Очередь подтверждения пуста.',
'devices.heartbeat.never': 'Нет данных',
'devices.action.rename': 'Переименовать',
'devices.action.moveSeat': 'Переместить на место',
'devices.action.approve': 'Подтвердить',
'devices.action.reject': 'Отклонить',
'devices.action.remove': 'Удалить устройство',
'devices.remove.confirm': 'Удалить устройство? Действие необратимо.',
'devices.reject.confirm': 'Отклонить заявку устройства?',
'devices.reason': 'Причина',
'common.save': 'Сохранить',
'common.cancel': 'Отмена',
'common.name': 'Название',
'toast.saved': 'Изменения сохранены',
'toast.failed': 'Не удалось выполнить действие',
'state.empty': 'Пусто',
```

```ts
// add inside messages.en
'venue.title': 'Floor & PCs',
'venue.tab.devices': 'Devices',
'venue.tab.pending': 'Pending',
'venue.tab.map': 'Floor map',
'venue.map.soon': 'The floor-map editor arrives in a later update.',
'devices.col.name': 'Device',
'devices.col.seat': 'Seat',
'devices.col.status': 'Status',
'devices.col.heartbeat': 'Heartbeat',
'devices.status.online': 'Online',
'devices.status.offline': 'Offline',
'devices.status.pending': 'Pending',
'devices.empty.active': 'No connected devices.',
'devices.empty.pending': 'The approval queue is empty.',
'devices.heartbeat.never': 'No data',
'devices.action.rename': 'Rename',
'devices.action.moveSeat': 'Move to seat',
'devices.action.approve': 'Approve',
'devices.action.reject': 'Reject',
'devices.action.remove': 'Remove device',
'devices.remove.confirm': 'Remove this device? This cannot be undone.',
'devices.reject.confirm': 'Reject this device enrollment?',
'devices.reason': 'Reason',
'common.save': 'Save',
'common.cancel': 'Cancel',
'common.name': 'Name',
'toast.saved': 'Changes saved',
'toast.failed': 'The action could not be completed',
'state.empty': 'Empty',
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- messages`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/i18n/messages.ts src/i18n/messages.test.ts
git commit -m "feat(web): add i18n keys for venue/devices/common/toast"
```

---

## Task 14: DevicesTable (presentational)

Renders a device list as a table; row click invokes `onSelect(row)`. Status rendered as a localized `Badge`.

**Files:**
- Create: `src/AFK4.Platform.Web/src/club/venue/DevicesTable.tsx`
- Test: `src/AFK4.Platform.Web/src/club/venue/DevicesTable.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// src/club/venue/DevicesTable.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { DevicesTable } from './DevicesTable';
import type { DeviceRow } from './devicesModel';

const row: DeviceRow = {
  deviceId: 'd1', organizationId: 'org', displayName: 'ПК-1', machineName: 'PC-RAW',
  seatId: 's1', seatLabel: 'Зона A · Место 1', status: 'online', lastHeartbeatAtUtc: null, failedCommandCount: 0
};

function renderTable(rows: DeviceRow[], onSelect = vi.fn()) {
  render(<I18nProvider><DevicesTable rows={rows} emptyMessage="Нет устройств" onSelect={onSelect} /></I18nProvider>);
  return onSelect;
}

it('renders rows and fires onSelect on row click', () => {
  const onSelect = renderTable([row]);
  fireEvent.click(screen.getByText('ПК-1'));
  expect(onSelect).toHaveBeenCalledWith(row);
});

it('renders the empty message when there are no rows', () => {
  renderTable([]);
  expect(screen.getByText('Нет устройств')).toBeInTheDocument();
});
```

(Add `import { vi } from 'vitest';`.)

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- DevicesTable`
Expected: FAIL — `Cannot find module './DevicesTable'`.

- [ ] **Step 3: Write minimal implementation**

```tsx
// src/club/venue/DevicesTable.tsx
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { EmptyState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { MessageKey } from '@/i18n/messages';
import type { DeviceRow, DeviceRowStatus } from './devicesModel';

const STATUS_LABEL: Record<DeviceRowStatus, MessageKey> = {
  online: 'devices.status.online',
  offline: 'devices.status.offline',
  pending: 'devices.status.pending'
};
const STATUS_VARIANT: Record<DeviceRowStatus, 'default' | 'secondary' | 'destructive'> = {
  online: 'default',
  offline: 'destructive',
  pending: 'secondary'
};

export function DevicesTable({ rows, emptyMessage, onSelect }: {
  rows: DeviceRow[];
  emptyMessage: string;
  onSelect: (row: DeviceRow) => void;
}) {
  const { t, formatDate } = useI18n();
  if (rows.length === 0) return <EmptyState message={emptyMessage} />;
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t('devices.col.name')}</TableHead>
          <TableHead>{t('devices.col.seat')}</TableHead>
          <TableHead>{t('devices.col.status')}</TableHead>
          <TableHead>{t('devices.col.heartbeat')}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {rows.map(row => (
          <TableRow key={row.deviceId} data-clickable="true" onClick={() => onSelect(row)}>
            <TableCell>
              <div className="font-medium">{row.displayName}</div>
              <div className="text-xs text-muted-foreground">{row.machineName}</div>
            </TableCell>
            <TableCell>{row.seatLabel}</TableCell>
            <TableCell><Badge variant={STATUS_VARIANT[row.status]}>{t(STATUS_LABEL[row.status])}</Badge></TableCell>
            <TableCell className="text-sm text-muted-foreground">
              {row.lastHeartbeatAtUtc ? formatDate(row.lastHeartbeatAtUtc) : t('devices.heartbeat.never')}
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- DevicesTable`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/venue/DevicesTable.tsx src/club/venue/DevicesTable.test.tsx
git commit -m "feat(web): add DevicesTable"
```

---

## Task 15: DeviceDrawer (detail + actions)

The drawer body for a selected device. For active devices: rename (Input + Save), move-seat (Select + Save), and Remove (→ ConfirmDialog). For pending devices: Approve (direct) and Reject (→ ConfirmDialog). Every action calls the client, shows a toast, and calls `onDone()` (which refreshes the list and closes the drawer). Server-confirmed only.

**Files:**
- Create: `src/AFK4.Platform.Web/src/club/venue/DeviceDrawer.tsx`
- Test: `src/AFK4.Platform.Web/src/club/venue/DeviceDrawer.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// src/club/venue/DeviceDrawer.test.tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { DeviceDrawer } from './DeviceDrawer';
import type { DeviceRow, SeatOption } from './devicesModel';

const activeRow: DeviceRow = {
  deviceId: 'd1', organizationId: 'org', displayName: 'ПК-1', machineName: 'PC-RAW',
  seatId: 's1', seatLabel: 'Зона A · Место 1', status: 'online', lastHeartbeatAtUtc: null, failedCommandCount: 0
};
const seatOptions: SeatOption[] = [{ seatId: 's2', label: 'Зона B · Место 2' }];

function fakeClient() {
  return {
    renameDevice: vi.fn().mockResolvedValue({}),
    moveDeviceSeat: vi.fn().mockResolvedValue({}),
    removeDevice: vi.fn().mockResolvedValue({}),
    approveDevice: vi.fn().mockResolvedValue({}),
    rejectDevice: vi.fn().mockResolvedValue({})
  };
}

function renderDrawer(row: DeviceRow, client = fakeClient(), onDone = vi.fn()) {
  render(
    <I18nProvider><ToastProvider>
      <DeviceDrawer device={row} seatOptions={seatOptions} client={client as never} onDone={onDone} />
    </ToastProvider></I18nProvider>
  );
  return { client, onDone };
}

it('renames a device and calls onDone', async () => {
  const { client, onDone } = renderDrawer(activeRow);
  fireEvent.change(screen.getByLabelText('Название'), { target: { value: 'ПК-новый' } });
  fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));
  await waitFor(() => expect(client.renameDevice).toHaveBeenCalledWith('d1', 'org', 'ПК-новый'));
  await waitFor(() => expect(onDone).toHaveBeenCalled());
});

it('removes a device through the confirm dialog', async () => {
  const { client } = renderDrawer(activeRow);
  fireEvent.click(screen.getByRole('button', { name: 'Удалить устройство' }));
  fireEvent.click(screen.getByRole('button', { name: 'Удалить' })); // confirm
  await waitFor(() => expect(client.removeDevice).toHaveBeenCalled());
});

it('approves a pending device', async () => {
  const { client } = renderDrawer({ ...activeRow, status: 'pending' });
  fireEvent.click(screen.getByRole('button', { name: 'Подтвердить' }));
  await waitFor(() => expect(client.approveDevice).toHaveBeenCalledWith('d1', 'org', expect.any(String)));
});
```

(Add `import { vi } from 'vitest';`.)

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- DeviceDrawer`
Expected: FAIL — `Cannot find module './DeviceDrawer'`.

- [ ] **Step 3: Write minimal implementation**

```tsx
// src/club/venue/DeviceDrawer.tsx
import { useState } from 'react';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select';
import { ConfirmDialog } from '@/components/shared/ConfirmDialog';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import type { DeviceRow, SeatOption } from './devicesModel';

type DeviceActions = Pick<ClubApiClient, 'renameDevice' | 'moveDeviceSeat' | 'removeDevice' | 'approveDevice' | 'rejectDevice'>;
const DEFAULT_APPROVE_REASON = 'Подтверждено из веб-консоли';

export function DeviceDrawer({ device, seatOptions, client, onDone }: {
  device: DeviceRow;
  seatOptions: SeatOption[];
  client: DeviceActions;
  onDone: () => void;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [name, setName] = useState(device.displayName);
  const [seatId, setSeatId] = useState<string | undefined>(device.seatId ?? undefined);
  const [pending, setPending] = useState(false);
  const [confirm, setConfirm] = useState<null | 'remove' | 'reject'>(null);

  async function run(action: () => Promise<unknown>) {
    setPending(true);
    try {
      await action();
      toast({ title: t('toast.saved'), variant: 'success' });
      onDone();
    } catch {
      toast({ title: t('toast.failed'), variant: 'error' });
    } finally {
      setPending(false);
      setConfirm(null);
    }
  }

  if (device.status === 'pending') {
    return (
      <div className="flex flex-col gap-3">
        <Button disabled={pending}
          onClick={() => void run(() => client.approveDevice(device.deviceId, device.organizationId, DEFAULT_APPROVE_REASON))}>
          {t('devices.action.approve')}
        </Button>
        <Button variant="destructive" disabled={pending} onClick={() => setConfirm('reject')}>
          {t('devices.action.reject')}
        </Button>
        <ConfirmDialog
          open={confirm === 'reject'} title={t('devices.reject.confirm')}
          confirmLabel={t('devices.action.reject')} cancelLabel={t('common.cancel')}
          reasonLabel={t('devices.reason')} destructive pending={pending}
          onConfirm={reason => void run(() => client.rejectDevice(device.deviceId, device.organizationId, reason))}
          onOpenChange={open => { if (!open) setConfirm(null); }}
        />
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-5">
      <label className="block text-sm">
        <span className="mb-1 block text-muted-foreground">{t('common.name')}</span>
        <Input aria-label={t('common.name')} value={name} onChange={e => setName(e.target.value)} />
      </label>
      <Button disabled={pending} onClick={() => void run(() => client.renameDevice(device.deviceId, device.organizationId, name))}>
        {t('common.save')}
      </Button>

      <label className="block text-sm">
        <span className="mb-1 block text-muted-foreground">{t('devices.action.moveSeat')}</span>
        <Select value={seatId} onValueChange={setSeatId}>
          <SelectTrigger aria-label={t('devices.action.moveSeat')}><SelectValue placeholder={t('devices.col.seat')} /></SelectTrigger>
          <SelectContent>
            {seatOptions.map(s => <SelectItem key={s.seatId} value={s.seatId}>{s.label}</SelectItem>)}
          </SelectContent>
        </Select>
      </label>
      <Button variant="outline" disabled={pending || seatId === undefined}
        onClick={() => { if (seatId) void run(() => client.moveDeviceSeat(device.deviceId, device.organizationId, seatId)); }}>
        {t('devices.action.moveSeat')}
      </Button>

      <Button variant="destructive" disabled={pending} onClick={() => setConfirm('remove')}>
        {t('devices.action.remove')}
      </Button>
      <ConfirmDialog
        open={confirm === 'remove'} title={t('devices.remove.confirm')}
        confirmLabel={t('devices.action.remove')} cancelLabel={t('common.cancel')}
        reasonLabel={t('devices.reason')} destructive pending={pending}
        onConfirm={reason => void run(() => client.removeDevice(device.deviceId, device.organizationId, reason))}
        onOpenChange={open => { if (!open) setConfirm(null); }}
      />
    </div>
  );
}
```

Note: the Remove confirm button label `devices.action.remove` ("Удалить устройство") differs from the destructive ConfirmDialog confirm label — the test clicks the button named "Удалить" which is the ConfirmDialog's confirm. To keep the test query unambiguous, set the remove ConfirmDialog `confirmLabel` to a short `common`-level "Удалить". Add these two keys in Task 13's lists if not already present: `'common.delete': 'Удалить'` (ru) / `'Delete'` (en), and use `confirmLabel={t('common.delete')}` for the remove dialog. (Update the Task 13 additions accordingly — add `common.delete` to both locales and the parity test still passes.)

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- DeviceDrawer`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/venue/DeviceDrawer.tsx src/club/venue/DeviceDrawer.test.tsx src/i18n/messages.ts
git commit -m "feat(web): add DeviceDrawer with rename/move/approve/reject/remove"
```

---

## Task 16: VenueScreen (tabs + drawer wiring)

The "Зал и ПК" screen: tabs Устройства / Ожидают (with a pending count), each a `DevicesTable`; selecting a row opens the `Sheet` drawer hosting `DeviceDrawer`. Renders loading/error states from `useDevices`. The Карта tab is present but shows a "soon" note (floor-map redesign is the next plan).

**Files:**
- Create: `src/AFK4.Platform.Web/src/club/venue/VenueScreen.tsx`
- Test: `src/AFK4.Platform.Web/src/club/venue/VenueScreen.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// src/club/venue/VenueScreen.test.tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { VenueScreen } from './VenueScreen';
import type { DeviceInventoryItem, FloorMap } from '@/api/types';

const floorMap: FloorMap = { branchId: 'b1', branchName: 'X', zones: [], seats: [] };
function device(over: Partial<DeviceInventoryItem>): DeviceInventoryItem {
  return {
    organizationId: 'org', branchId: 'b1', deviceId: 'd1', machineName: 'PC', agentVersion: '1', shellVersion: '1',
    enrolledAtUtc: '2026-05-01T00:00:00Z', lastHeartbeatAtUtc: null, isOnline: true, isLocked: false,
    seatId: null, seatName: null, zoneId: null, zoneName: null, activeCredentialCount: 0, installedAppCount: 0,
    pendingCommandCount: 0, failedCommandCount: 0, displayName: 'ПК-1', role: 'workstation', enrollmentState: 'active', ...over
  };
}
function client() {
  return {
    listDevices: vi.fn().mockResolvedValue([device({ deviceId: 'd1', displayName: 'ПК-1' })]),
    listPendingDevices: vi.fn().mockResolvedValue([]),
    getFloorMap: vi.fn().mockResolvedValue({ etag: null, floorMap })
  } as never;
}

it('renders the devices table and opens the drawer on row click', async () => {
  render(<I18nProvider><ToastProvider><VenueScreen client={client()} branchId="b1" /></ToastProvider></I18nProvider>);
  await waitFor(() => expect(screen.getByText('ПК-1')).toBeInTheDocument());
  fireEvent.click(screen.getByText('ПК-1'));
  expect(screen.getByLabelText('Название')).toBeInTheDocument(); // drawer body
});
```

(Add `import { vi } from 'vitest';`.)

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- VenueScreen`
Expected: FAIL — `Cannot find module './VenueScreen'`.

- [ ] **Step 3: Write minimal implementation**

```tsx
// src/club/venue/VenueScreen.tsx
import { useState } from 'react';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import { Sheet, SheetContent, SheetTitle } from '@/components/ui/sheet';
import { LoadingCards, ErrorState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import { useDevices } from './useDevices';
import { DevicesTable } from './DevicesTable';
import { DeviceDrawer } from './DeviceDrawer';
import type { DeviceRow } from './devicesModel';

export function VenueScreen({ client, branchId }: { client: ClubApiClient; branchId: string }) {
  const { t } = useI18n();
  const state = useDevices(client, branchId);
  const [selected, setSelected] = useState<DeviceRow | null>(null);

  if (state.status === 'loading') return <LoadingCards count={3} />;
  if (state.status === 'error') return <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />;

  const { active, pending, seatOptions } = state.data;
  return (
    <>
      <Tabs defaultValue="devices">
        <TabsList>
          <TabsTrigger value="devices">{t('venue.tab.devices')}</TabsTrigger>
          {pending.length > 0 && <TabsTrigger value="pending">{`${t('venue.tab.pending')} (${pending.length})`}</TabsTrigger>}
          <TabsTrigger value="map">{t('venue.tab.map')}</TabsTrigger>
        </TabsList>
        <TabsContent value="devices">
          <DevicesTable rows={active} emptyMessage={t('devices.empty.active')} onSelect={setSelected} />
        </TabsContent>
        {pending.length > 0 && (
          <TabsContent value="pending">
            <DevicesTable rows={pending} emptyMessage={t('devices.empty.pending')} onSelect={setSelected} />
          </TabsContent>
        )}
        <TabsContent value="map">
          <p className="text-sm text-muted-foreground">{t('venue.map.soon')}</p>
        </TabsContent>
      </Tabs>

      <Sheet open={selected !== null} onOpenChange={open => { if (!open) setSelected(null); }}>
        <SheetContent>
          {selected && (
            <>
              <SheetTitle>{selected.displayName}</SheetTitle>
              <DeviceDrawer
                device={selected}
                seatOptions={seatOptions}
                client={client}
                onDone={() => { setSelected(null); state.retry(); }}
              />
            </>
          )}
        </SheetContent>
      </Sheet>
    </>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- VenueScreen`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/club/venue/VenueScreen.tsx src/club/venue/VenueScreen.test.tsx
git commit -m "feat(web): add VenueScreen (Зал и ПК) with tabs and device drawer"
```

---

## Task 17: Route the "Зал и ПК" nav item to VenueScreen

Add a `clubVenue` route kind, resolve `/club/venue`, switch the `venue` nav item on, render `VenueScreen` in `ClubArea`, set the title, and include `clubVenue` in `pathForRoute`, `isClubRoute`, and the resolver. Legacy device routes remain intact.

**Files:**
- Modify: `src/AFK4.Platform.Web/src/App.tsx`
- Modify: `src/AFK4.Platform.Web/src/club/nav.ts`
- Test: `src/AFK4.Platform.Web/src/App.routing.test.ts` (new)
- Test: `src/AFK4.Platform.Web/src/club/nav.test.ts` (new)

- [ ] **Step 1: Write the failing tests**

```ts
// src/App.routing.test.ts
import { resolvePlatformRoute } from './App';

it('resolves /club/venue to clubVenue', () => {
  expect(resolvePlatformRoute('/club/venue', null, '', 'club').route).toEqual({ kind: 'clubVenue' });
});

it('still resolves the legacy devices route', () => {
  expect(resolvePlatformRoute('/club/branches/b1/devices', null, '', 'club').route)
    .toEqual({ kind: 'clubBranchDevices', branchId: 'b1' });
});
```

```ts
// src/club/nav.test.ts
import { visibleNav, clubNav } from './nav';

it('exposes Зал и ПК as an active (non-soon) item', () => {
  const venue = clubNav.flatMap(g => g.items).find(i => i.key === 'venue');
  expect(venue?.soon).toBe(false);
});

it('owner sees the venue item', () => {
  const items = visibleNav('owner').flatMap(g => g.items).map(i => i.key);
  expect(items).toContain('venue');
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `npm test -- "App.routing" "club/nav"`
Expected: FAIL — `/club/venue` resolves to `notFound`; `venue.soon` is `true`.

- [ ] **Step 3: Implement the changes**

In `src/club/nav.ts`, change the `venue` item to `soon: false`:

```ts
{ key: 'venue', labelKey: 'nav.venue', path: '/club/venue', ownerOnly: false, soon: false },
```

In `src/App.tsx`:

(a) Add `clubVenue` to the `ClubRoute` union:

```ts
export type ClubRoute =
  | { kind: 'clubDashboard' }
  | { kind: 'clubVenue' }
  | { kind: 'clubInstall' }
  // ...rest unchanged
```

(b) Import `VenueScreen`:

```ts
import { VenueScreen } from './club/venue/VenueScreen';
```

(c) In `resolvePlatformRoute`, inside the `allowsClubRoutes` block, add after the `/club` case:

```ts
if (path === '/club/venue') {
  return { route: { kind: 'clubVenue' } };
}
```

(d) In `isClubRoute`, add `|| route.kind === 'clubVenue'`.

(e) In `CLUB_SCREEN_TITLE`, add `clubVenue: 'Зал и ПК',`.

(f) In `pathForRoute`, add a case returning the nav path:

```ts
case 'clubVenue':
  return '/club/venue';
```

(g) In `ClubArea`'s body, render `VenueScreen` for `clubVenue` (it needs the active `branchId`, already computed in `ClubArea`):

```tsx
{route.kind === 'clubDashboard' ? (
  <OverviewScreen state={overviewState} />
) : route.kind === 'clubVenue' ? (
  <VenueScreen client={clubClient} branchId={branchId} />
) : (
  <LegacyClubScreen client={clubClient} route={route} session={session} onNavigate={onNavigate} />
)}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `npm test -- "App.routing" "club/nav"`
Expected: PASS.

- [ ] **Step 5: Verify build and full suite**

Run: `npm run build && npm test`
Expected: PASS (tsc + vite clean; all tests green).

- [ ] **Step 6: Commit**

```bash
git add src/App.tsx src/club/nav.ts src/App.routing.test.ts src/club/nav.test.ts
git commit -m "feat(web): route Зал и ПК nav item to the new VenueScreen"
```

---

## Task 18: Overview polish — KPI "devices online" denominator

The Overview KPI denominator uses `utilization.totalSeats` as a proxy for total devices. Fix `buildOverview` to use real device totals (`onlineDevices + offlineDevices`).

**Files:**
- Modify: `src/AFK4.Platform.Web/src/club/overview/overviewModel.ts`
- Modify: `src/AFK4.Platform.Web/src/club/overview/overviewModel.test.ts` (add case; if the file does not exist, create it)

- [ ] **Step 1: Write the failing test**

```ts
// add to src/club/overview/overviewModel.test.ts
import { buildOverview } from './overviewModel';
import type { OperatorDashboardSummary } from '@/api/types';

function summary(over: Partial<OperatorDashboardSummary['utilization']>): OperatorDashboardSummary {
  return {
    organizationId: 'o', branchId: 'b', fromUtc: '', toUtc: '', generatedAtUtc: '',
    utilization: { totalSeats: 99, activeSessions: 0, endingSessions: 0, onlineDevices: 6, offlineDevices: 2, sessionStarts: 0, utilizationPercent: 0, ...over },
    alertPressure: { pendingCommands: 0, failedCommands: 0, offlineDevices: 0, endingSessions: 0, totalAlerts: 0 },
    revenue: { posNetSales: { amount: 0, currencyCode: 'TJS' }, gameplayRevenue: { amount: 0, currencyCode: 'TJS' }, totalRevenue: { amount: 0, currencyCode: 'TJS' }, posCheckCount: 0, newPlayerCount: 0 }
  };
}

it('uses online+offline devices as the online denominator, not totalSeats', () => {
  const vm = buildOverview(summary({}), [], []);
  expect(vm.kpis.devicesOnline).toEqual({ online: 6, total: 8 });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- overviewModel`
Expected: FAIL — `total` is `99`.

- [ ] **Step 3: Implement the fix**

In `overviewModel.ts`, change the `devicesOnline` mapping:

```ts
devicesOnline: {
  online: summary.utilization.onlineDevices,
  total: summary.utilization.onlineDevices + summary.utilization.offlineDevices
},
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- overviewModel`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/club/overview/overviewModel.ts src/club/overview/overviewModel.test.ts
git commit -m "fix(web): use real device total for Overview online KPI denominator"
```

---

## Task 19: Overview polish — revenue breakdown by-key + localized tooltip

Replace positional `revenueBreakdown[0]/[1]` access with a key lookup, and give the recharts `Tooltip` a formatter so it shows localized labels + currency instead of raw slice keys.

**Files:**
- Modify: `src/AFK4.Platform.Web/src/club/overview/OverviewScreen.tsx`
- Test: `src/AFK4.Platform.Web/src/club/overview/OverviewScreen.test.tsx` (create if absent)

- [ ] **Step 1: Write the failing test**

```tsx
// src/club/overview/OverviewScreen.test.tsx
import { render, screen } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { OverviewScreen } from './OverviewScreen';
import type { OverviewState } from './useOverview';

const ready: OverviewState = {
  status: 'ready',
  retry: () => {},
  data: {
    kpis: { devicesOnline: { online: 1, total: 2 }, activeSessions: 0, utilizationPercent: 0, revenueToday: { amount: 100, currencyCode: 'TJS' }, attention: 0 },
    // intentionally reversed order to prove by-key (not positional) access
    revenueBreakdown: [{ key: 'pos', amount: 30 }, { key: 'gameplay', amount: 70 }],
    attention: []
  }
};

it('shows gameplay and pos amounts by key regardless of array order', () => {
  render(<I18nProvider><OverviewScreen state={ready} /></I18nProvider>);
  const gameplay = screen.getByText('Игровое время:').closest('span')!;
  const pos = screen.getByText('Бар и товары:').closest('span')!;
  expect(gameplay.textContent).toContain('70');
  expect(pos.textContent).toContain('30');
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- OverviewScreen`
Expected: FAIL — positional access maps gameplay→30, pos→70 (reversed).

- [ ] **Step 3: Implement the fix**

In `OverviewScreen.tsx`, add a by-key helper and use it; add a tooltip formatter.

```tsx
// helper near the top of the file, after SLICE_COLOR
function sliceAmount(breakdown: { key: string; amount: number }[], key: string): number {
  return breakdown.find(s => s.key === key)?.amount ?? 0;
}
```

Replace the two `revenueBreakdown[0]/[1]` lines with:

```tsx
<span><b>{t('overview.revenue.gameplay')}:</b> {formatCurrency(sliceAmount(revenueBreakdown, 'gameplay'), kpis.revenueToday.currencyCode)}</span>
<span><b>{t('overview.revenue.pos')}:</b> {formatCurrency(sliceAmount(revenueBreakdown, 'pos'), kpis.revenueToday.currencyCode)}</span>
```

Give the chart `Tooltip` a formatter that localizes the slice name and formats the value:

```tsx
<Tooltip
  formatter={(value: number, name: string) => [
    formatCurrency(value, kpis.revenueToday.currencyCode),
    t(name === 'gameplay' ? 'overview.revenue.gameplay' : 'overview.revenue.pos')
  ]}
/>
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- OverviewScreen`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/club/overview/OverviewScreen.tsx src/club/overview/OverviewScreen.test.tsx
git commit -m "fix(web): access Overview revenue slices by key and localize chart tooltip"
```

---

## Task 20: Final verification gate

- [ ] **Step 1: Full build**

Run: `npm run build`
Expected: PASS — `tsc -b` no type errors, `vite build` succeeds.

- [ ] **Step 2: Full test suite**

Run: `npm test`
Expected: PASS — all colocated tests green (sub-project-1 suite + the new primitives, devices, routing, and Overview-polish tests).

- [ ] **Step 3: Manual smoke (optional, dev server)**

Run: `npm run dev`, open the club audience, sign in as an owner, click "Зал и ПК": confirm the Устройства tab lists devices, a row opens the drawer, rename/move/remove and (when present) the Ожидают tab approve/reject all show a toast and refresh. Confirm light/dark and RU/EN still work.

- [ ] **Step 4: No commit needed** unless the smoke surfaced a fix.

---

## Self-Review

**Spec coverage (against `2026-05-29-platform-web-club-console-design.md`):**
- New primitives (Table, Input, Select, Dialog, Sheet, Tabs, Toast) → Tasks 1,3–8. ✓ (Tooltip/Switch/Checkbox/Textarea/DateRangePicker/Pagination explicitly deferred per spec "added as needed".)
- List+detail = drawer pattern → Sheet (Task 7) used by VenueScreen (Task 16). ✓
- Tabbed-page pattern → Tabs (Task 8) used by VenueScreen. ✓
- Data-region states (loading/empty/error+retry) → Task 9, used throughout. ✓
- Money/destructive safety = confirm dialog, server-confirmed only, toast → ConfirmDialog (Task 10) + DeviceDrawer `run()` (Task 15). ✓
- Зал и ПК redesign (Устройства, Ожидают) → Tasks 11–16. (Карта зала tab deferred, surfaced via a "soon" note — Task 16 — not silently dropped.) ✓
- i18n RU/EN parity → Task 13 (+ parity test). ✓
- Reuse existing contracts, no new backend → all client calls are existing `clubApi` methods. ✓
- Polish backlog: KPI denominator (Task 18), revenue by-key + tooltip (Task 19). Branch-switcher real switching and `ClubDashboard` deletion explicitly deferred (Scope section) because they depend on later screens. ✓

**Placeholder scan:** No "TBD"/"handle errors"/"similar to" — every code step has full code and exact commands. ✓

**Type consistency:** `DeviceRow`/`SeatOption`/`DevicesViewModel` defined in Task 11 are used unchanged in Tasks 12,14,15,16. `DevicesState` (Task 12) mirrors `OverviewState`. `ConfirmDialogProps` (Task 10) matches its callers in Task 15. The `common.delete` key dependency is called out in Task 15 with an instruction to add it in Task 13. Client method signatures match `clubApi.ts` exactly (`renameDevice(deviceId, organizationId, displayName)`, `moveDeviceSeat(deviceId, organizationId, seatId)`, `removeDevice/approveDevice/rejectDevice(deviceId, organizationId, reason)`). ✓

## Notes for the Implementer

- Run all commands from `src/AFK4.Platform.Web`.
- `npm test -- <pattern>` filters by file path substring (Vitest). The full gate is `npm test`.
- jsdom + Radix: test dialogs/sheets/selects in their **controlled-open** state (pass `open`/`defaultValue`) rather than driving pointer interactions, to avoid jsdom pointer-capture flakiness.
- Keep every new user-facing string going through `t(...)` — no hard-coded literals in components.
- This plan is the first of the sub-project-2 sequence; the next plan redesigns the remaining existing screens (Карта зала, Настройки, Операторы и роли) and the "Все филиалы" dashboard, reusing every primitive built here.
