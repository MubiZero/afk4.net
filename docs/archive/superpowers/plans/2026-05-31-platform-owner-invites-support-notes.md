# Platform Tenant Detail — Owner-Invites + Support-Notes Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the two remaining legacy tenant-detail sections (`OwnerInvitesSection`, `SupportNotesSection`) with design-system Card sections in `src/platform/tenants/`, matching the canonical Plan-4 section pattern.

**Architecture:** Pure frontend. The backend endpoints, contracts, services, and `platformApi.ts` client methods (`listOwnerInvites`/`createOwnerInvite`/`revokeOwnerInvite`, `listSupportNotes`/`createSupportNote`/`updateSupportNote`) all already exist and are unchanged. We add a small `Textarea` UI primitive, add i18n keys, build two new presentational sections following the `TenantInvoicesSection`/`TenantStatusSection` idiom (props type-picked from `PlatformApiClient`; `loading|error|ready` via local state + a `tick` retry; `useToast` for feedback; `ConfirmDialog` for the destructive revoke), wire them into `TenantDrawer`, then delete the two legacy components.

**Tech Stack:** React + TypeScript, Vite, Tailwind, Radix-based `@/components/ui/*` primitives, Vitest (`globals:false`) + React Testing Library. Build gate `tsc -b` (`npm run build`) + `npm test`.

This is sub-project #3 Plan 5 of 7. Spec: `docs/superpowers/specs/2026-05-31-platform-admin-control-plane-design.md` (§4.2, §5 item 5). All paths below are relative to `D:\afk4.net`.

---

## Conventions used by every task

- All commands run from `src/AFK4.Platform.Web` (the web app root). Use the **Bash** tool.
- Test idiom (from `src/platform/tenants/TenantStatusSection.test.tsx`): `import { it, expect, vi, beforeAll } from 'vitest'`; wrap components in `<I18nProvider><ToastProvider>…</ToastProvider></I18nProvider>`; mock the client with `vi.fn().mockResolvedValue(...)`; drive with `fireEvent`; assert with `waitFor`. Radix `Select` needs the jsdom pointer/scroll shims in a `beforeAll` (shown in Task 3).
- The new sections take `client` as a **`Pick<PlatformApiClient, ...>`** of only the methods they call (matches `TenantInvoicesSection.tsx:13`).
- Section naming follows the existing `Tenant*Section` convention: `TenantOwnerInvitesSection`, `TenantSupportNotesSection`.
- Commit after each task. Do NOT `git add src/preview/DemoApp.tsx` (untracked scratch).

---

## File Structure

- **Create:** `src/components/ui/textarea.tsx` — multi-line text primitive mirroring `input.tsx` (none exists today; support-notes needs it).
- **Create:** `src/components/ui/textarea.test.tsx` — primitive test.
- **Create:** `src/platform/tenants/TenantOwnerInvitesSection.tsx` — redesigned setup-codes section.
- **Create:** `src/platform/tenants/TenantOwnerInvitesSection.test.tsx`
- **Create:** `src/platform/tenants/TenantSupportNotesSection.tsx` — redesigned support-notes section.
- **Create:** `src/platform/tenants/TenantSupportNotesSection.test.tsx`
- **Modify:** `src/i18n/messages.ts` — add `platform.tenant.invites.*` and `platform.tenant.notes.*` keys to BOTH the `ru` block and the `en` block (parity test enforces equality).
- **Modify:** `src/platform/tenants/TenantDrawer.tsx` — swap legacy imports/usages for the new sections.
- **Delete:** `src/components/OwnerInvitesSection.tsx`, `src/components/SupportNotesSection.tsx` (only referenced by `TenantDrawer`, verified). Leave `src/components/ui.tsx` — still used by `HealthSection`/`NewTenant`/`SignIn`/`AcceptInvite`/`StaffSignIn`.

`HealthSection` stays legacy in this plan — it is not in Plan 5's scope (handled later with the remaining legacy-admin cleanup).

---

### Task 1: `Textarea` UI primitive

**Files:**
- Create: `src/components/ui/textarea.tsx`
- Test: `src/components/ui/textarea.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `src/components/ui/textarea.test.tsx`:

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { it, expect } from 'vitest';
import { Textarea } from './textarea';

it('renders a textarea and forwards value/onChange', () => {
  const values: string[] = [];
  render(<Textarea aria-label="note" value="" onChange={e => values.push(e.target.value)} />);
  const el = screen.getByRole('textbox', { name: 'note' });
  expect(el.tagName).toBe('TEXTAREA');
  fireEvent.change(el, { target: { value: 'hello' } });
  expect(values).toEqual(['hello']);
});

it('merges custom className', () => {
  render(<Textarea aria-label="note" className="custom-x" />);
  expect(screen.getByRole('textbox', { name: 'note' }).className).toContain('custom-x');
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- textarea`
Expected: FAIL — `Failed to resolve import "./textarea"` (file does not exist yet).

- [ ] **Step 3: Write minimal implementation**

Create `src/components/ui/textarea.tsx` (mirrors `src/components/ui/input.tsx`):

```tsx
import type { ComponentProps } from 'react';
import { cn } from '@/lib/utils';

export function Textarea({ className, ...props }: ComponentProps<'textarea'>) {
  return (
    <textarea
      data-slot="textarea"
      className={cn(
        'flex min-h-16 w-full rounded-md border border-input bg-background px-3 py-2 text-sm shadow-xs outline-none transition-colors',
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

Run: `npm test -- textarea`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/components/ui/textarea.tsx src/components/ui/textarea.test.tsx
git commit -m "feat(platform-web): add Textarea UI primitive"
```

---

### Task 2: i18n keys for the two sections

**Files:**
- Modify: `src/i18n/messages.ts` (the `ru` block ends ~line 530; the `en` block ~line 1059; a `messages.test.ts` enforces ru/en key parity)

- [ ] **Step 1: Run the parity test first to confirm a clean baseline**

Run: `npm test -- messages`
Expected: PASS. (If it already fails, stop and investigate before adding keys.)

- [ ] **Step 2: Add the keys to the `ru` block**

In `src/i18n/messages.ts`, find the line `'platform.tenant.invoices.generate': 'Сгенерировать инвойс',` inside the `ru:` block (near line 163). Insert immediately AFTER it:

```ts
    'platform.tenant.section.invites': 'Коды настройки',
    'platform.tenant.invites.branch': 'Филиал',
    'platform.tenant.invites.ownerUserName': 'Логин владельца (email)',
    'platform.tenant.invites.ownerDisplayName': 'Имя владельца',
    'platform.tenant.invites.create': 'Создать код',
    'platform.tenant.invites.empty': 'Кодов настройки пока нет.',
    'platform.tenant.invites.colStatus': 'Статус',
    'platform.tenant.invites.colCode': 'Код',
    'platform.tenant.invites.colOwner': 'Владелец',
    'platform.tenant.invites.colExpires': 'Истекает',
    'platform.tenant.invites.revoke': 'Отозвать',
    'platform.tenant.invites.revokeTitle': 'Отозвать код настройки?',
    'platform.tenant.invites.revokeReason': 'Причина',
    'platform.tenant.invites.revokeConfirm': 'Отозвать',
    'platform.tenant.invites.created': 'Код создан',
    'platform.tenant.invites.revoked': 'Код отозван',
    'platform.tenant.invites.status.pending': 'Ожидает',
    'platform.tenant.invites.status.accepted': 'Принят',
    'platform.tenant.invites.status.revoked': 'Отозван',
    'platform.tenant.invites.status.expired': 'Истёк',
    'platform.tenant.section.notes': 'Заметки поддержки',
    'platform.tenant.notes.newNote': 'Новая заметка',
    'platform.tenant.notes.hint': 'До 4000 символов. Видно только администраторам платформы.',
    'platform.tenant.notes.add': 'Добавить заметку',
    'platform.tenant.notes.empty': 'Заметок поддержки пока нет.',
    'platform.tenant.notes.edit': 'Редактировать',
    'platform.tenant.notes.save': 'Сохранить',
    'platform.tenant.notes.cancel': 'Отмена',
    'platform.tenant.notes.created': 'Заметка добавлена',
    'platform.tenant.notes.updated': 'Заметка обновлена',
```

- [ ] **Step 3: Add the same keys (English values) to the `en` block**

Find `'platform.tenant.invoices.generate': 'Generate invoice',` inside the `en:` block (near line 690). Insert immediately AFTER it:

```ts
    'platform.tenant.section.invites': 'Setup codes',
    'platform.tenant.invites.branch': 'Branch',
    'platform.tenant.invites.ownerUserName': 'Owner username (email)',
    'platform.tenant.invites.ownerDisplayName': 'Owner display name',
    'platform.tenant.invites.create': 'Create code',
    'platform.tenant.invites.empty': 'No setup codes yet.',
    'platform.tenant.invites.colStatus': 'Status',
    'platform.tenant.invites.colCode': 'Code',
    'platform.tenant.invites.colOwner': 'Owner',
    'platform.tenant.invites.colExpires': 'Expires',
    'platform.tenant.invites.revoke': 'Revoke',
    'platform.tenant.invites.revokeTitle': 'Revoke setup code?',
    'platform.tenant.invites.revokeReason': 'Reason',
    'platform.tenant.invites.revokeConfirm': 'Revoke',
    'platform.tenant.invites.created': 'Setup code created',
    'platform.tenant.invites.revoked': 'Setup code revoked',
    'platform.tenant.invites.status.pending': 'Pending',
    'platform.tenant.invites.status.accepted': 'Accepted',
    'platform.tenant.invites.status.revoked': 'Revoked',
    'platform.tenant.invites.status.expired': 'Expired',
    'platform.tenant.section.notes': 'Support notes',
    'platform.tenant.notes.newNote': 'New note',
    'platform.tenant.notes.hint': 'Up to 4000 characters. Visible to platform admins only.',
    'platform.tenant.notes.add': 'Add note',
    'platform.tenant.notes.empty': 'No support notes yet.',
    'platform.tenant.notes.edit': 'Edit',
    'platform.tenant.notes.save': 'Save',
    'platform.tenant.notes.cancel': 'Cancel',
    'platform.tenant.notes.created': 'Note added',
    'platform.tenant.notes.updated': 'Note updated',
```

- [ ] **Step 4: Run the parity test to verify ru/en stayed in sync**

Run: `npm test -- messages`
Expected: PASS (no missing-key mismatch).

- [ ] **Step 5: Commit**

```bash
git add src/i18n/messages.ts
git commit -m "i18n(platform-web): add owner-invites + support-notes section keys"
```

---

### Task 3: `TenantOwnerInvitesSection` (redesigned setup-codes section)

**Files:**
- Create: `src/platform/tenants/TenantOwnerInvitesSection.tsx`
- Test: `src/platform/tenants/TenantOwnerInvitesSection.test.tsx`

Behavior to preserve from the legacy `OwnerInvitesSection`: list invites (code shown masked as `•••• {codeSuffix}` until revealed); create a code for a chosen branch (the create response's full `code` is revealed in-session via a `Map`); `initialInvite` (if passed) is pre-revealed; revoke a `pending` invite with a reason. Differences from legacy: design-system `Card`/`Table`/`Badge`/`Select`/`Input`/`Button`, `useToast` for feedback (no inline `ErrorBanner`), and the revoke reason captured via the shared `ConfirmDialog` (destructive) instead of an inline form. Data refreshes via a `tick` counter (canonical pattern).

- [ ] **Step 1: Write the failing test**

Create `src/platform/tenants/TenantOwnerInvitesSection.test.tsx`:

```tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi, beforeAll } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { TenantOwnerInvitesSection } from './TenantOwnerInvitesSection';
import type { OwnerInviteSummary, TenantBranch } from '@/api/types';

beforeAll(() => {
  window.HTMLElement.prototype.hasPointerCapture = () => false;
  window.HTMLElement.prototype.scrollIntoView = () => {};
  window.HTMLElement.prototype.releasePointerCapture = () => {};
});

const branches: TenantBranch[] = [
  { branchId: 'b1', slug: 'main', name: 'Main', city: 'Moscow', createdAtUtc: '2026-01-01T00:00:00Z' }
];

function summary(over: Partial<OwnerInviteSummary>): OwnerInviteSummary {
  return {
    ownerInviteId: 'i1', organizationId: 'o1', branchId: 'b1', codeSuffix: '1234',
    status: 'pending', ownerUserName: 'owner@x.io', ownerDisplayName: 'Owner',
    expiresAtUtc: '2026-02-01T00:00:00Z', acceptedAtUtc: null, revokedAtUtc: null,
    revokedReason: null, createdAtUtc: '2026-01-01T00:00:00Z', ...over
  };
}

function renderSection(client: any) {
  return render(
    <I18nProvider><ToastProvider>
      <TenantOwnerInvitesSection client={client} organizationId="o1" branches={branches} initialInvite={null} />
    </ToastProvider></I18nProvider>
  );
}

it('lists invites with a masked code', async () => {
  const client = { listOwnerInvites: vi.fn().mockResolvedValue([summary({})]), createOwnerInvite: vi.fn(), revokeOwnerInvite: vi.fn() };
  renderSection(client);
  expect(await screen.findByText('•••• 1234')).toBeTruthy();
  expect(screen.getByText('owner@x.io')).toBeTruthy();
});

it('creates a code and reveals the full code', async () => {
  const client = {
    listOwnerInvites: vi.fn().mockResolvedValue([]),
    createOwnerInvite: vi.fn().mockResolvedValue({
      ownerInviteId: 'i9', organizationId: 'o1', branchId: 'b1', code: 'FULL-CODE-9',
      status: 'pending', ownerUserName: null, ownerDisplayName: null,
      expiresAtUtc: '2026-02-01T00:00:00Z', acceptedAtUtc: null, revokedAtUtc: null,
      revokedReason: null, createdAtUtc: '2026-01-01T00:00:00Z'
    }),
    revokeOwnerInvite: vi.fn()
  };
  // after create, the section re-lists; return the new invite masked
  client.listOwnerInvites.mockResolvedValueOnce([]).mockResolvedValueOnce([summary({ ownerInviteId: 'i9', codeSuffix: 'DE-9', ownerUserName: null })]);
  renderSection(client);
  await screen.findByText('Кодов настройки пока нет.');

  fireEvent.click(screen.getByRole('button', { name: 'Создать код' }));
  await waitFor(() => expect(client.createOwnerInvite).toHaveBeenCalledWith('o1', 'b1', null, null, null));
  expect(await screen.findByText('FULL-CODE-9')).toBeTruthy();
});

it('revokes a pending invite with a reason', async () => {
  const client = {
    listOwnerInvites: vi.fn().mockResolvedValue([summary({})]),
    createOwnerInvite: vi.fn(),
    revokeOwnerInvite: vi.fn().mockResolvedValue(summary({ status: 'revoked' }))
  };
  renderSection(client);
  fireEvent.click(await screen.findByRole('button', { name: 'Отозвать' }));

  const reason = await screen.findByLabelText('Причина');
  fireEvent.change(reason, { target: { value: 'fraud' } });
  // ConfirmDialog confirm button shares the revoke label
  const confirmButtons = screen.getAllByRole('button', { name: 'Отозвать' });
  fireEvent.click(confirmButtons[confirmButtons.length - 1]);

  await waitFor(() => expect(client.revokeOwnerInvite).toHaveBeenCalledWith('i1', 'fraud'));
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- TenantOwnerInvitesSection`
Expected: FAIL — `Failed to resolve import "./TenantOwnerInvitesSection"`.

- [ ] **Step 3: Write the implementation**

Create `src/platform/tenants/TenantOwnerInvitesSection.tsx`:

```tsx
import { useEffect, useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge, type BadgeVariant } from '@/components/ui/badge';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { ConfirmDialog } from '@/components/shared/ConfirmDialog';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { PlatformApiClient } from '@/api/platformApi';
import type { OwnerInvite, OwnerInviteSummary, TenantBranch } from '@/api/types';

type Client = Pick<PlatformApiClient, 'listOwnerInvites' | 'createOwnerInvite' | 'revokeOwnerInvite'>;

const INVITE_STATUS_VARIANT: Record<string, BadgeVariant> = {
  pending: 'secondary',
  accepted: 'success',
  revoked: 'outline',
  expired: 'outline'
};
const INVITE_STATUS_LABEL: Record<string, string> = {
  pending: 'platform.tenant.invites.status.pending',
  accepted: 'platform.tenant.invites.status.accepted',
  revoked: 'platform.tenant.invites.status.revoked',
  expired: 'platform.tenant.invites.status.expired'
};

interface Props {
  client: Client;
  organizationId: string;
  branches: TenantBranch[];
  initialInvite?: OwnerInvite | null;
}

export function TenantOwnerInvitesSection({ client, organizationId, branches, initialInvite }: Props) {
  const { t, formatDate } = useI18n();
  const { toast } = useToast();
  const [tick, setTick] = useState(0);
  const [invites, setInvites] = useState<OwnerInviteSummary[] | null>(null);
  const [error, setError] = useState(false);
  const [revealed, setRevealed] = useState<Map<string, string>>(() => {
    const seed = new Map<string, string>();
    if (initialInvite) seed.set(initialInvite.ownerInviteId, initialInvite.code);
    return seed;
  });
  const [branchId, setBranchId] = useState(branches[0]?.branchId ?? '');
  const [ownerUserName, setOwnerUserName] = useState('');
  const [ownerDisplayName, setOwnerDisplayName] = useState('');
  const [creating, setCreating] = useState(false);
  const [revokeId, setRevokeId] = useState<string | null>(null);
  const [revoking, setRevoking] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setInvites(null); setError(false);
    client.listOwnerInvites(organizationId)
      .then(rows => { if (!cancelled) setInvites(rows); })
      .catch(() => { if (!cancelled) setError(true); });
    return () => { cancelled = true; };
  }, [client, organizationId, tick]);

  async function create() {
    if (branchId === '') return;
    setCreating(true);
    try {
      const made = await client.createOwnerInvite(
        organizationId,
        branchId,
        ownerUserName.trim() === '' ? null : ownerUserName.trim(),
        ownerDisplayName.trim() === '' ? null : ownerDisplayName.trim(),
        null
      );
      setRevealed(cur => new Map(cur).set(made.ownerInviteId, made.code));
      setOwnerUserName(''); setOwnerDisplayName('');
      toast({ title: t('platform.tenant.invites.created'), variant: 'success' });
      setTick(n => n + 1);
    } catch {
      toast({ title: t('platform.tenant.action.error'), variant: 'error' });
    } finally {
      setCreating(false);
    }
  }

  async function revoke(reason: string) {
    if (revokeId === null) return;
    setRevoking(true);
    try {
      await client.revokeOwnerInvite(revokeId, reason);
      setRevealed(cur => { const next = new Map(cur); next.delete(revokeId); return next; });
      setRevokeId(null);
      toast({ title: t('platform.tenant.invites.revoked'), variant: 'success' });
      setTick(n => n + 1);
    } catch {
      toast({ title: t('platform.tenant.action.error'), variant: 'error' });
    } finally {
      setRevoking(false);
    }
  }

  return (
    <Card>
      <CardHeader><CardTitle>{t('platform.tenant.section.invites')}</CardTitle></CardHeader>
      <CardContent className="flex flex-col gap-4 text-sm">
        <div className="flex flex-col gap-3">
          <label className="block">
            <span className="mb-1 block text-muted-foreground">{t('platform.tenant.invites.branch')}</span>
            <Select value={branchId} onValueChange={setBranchId}>
              <SelectTrigger aria-label={t('platform.tenant.invites.branch')}><SelectValue /></SelectTrigger>
              <SelectContent>
                {branches.map(b => <SelectItem key={b.branchId} value={b.branchId}>{b.name} ({b.city})</SelectItem>)}
              </SelectContent>
            </Select>
          </label>
          <label className="block">
            <span className="mb-1 block text-muted-foreground">{t('platform.tenant.invites.ownerUserName')}</span>
            <Input aria-label={t('platform.tenant.invites.ownerUserName')} value={ownerUserName} onChange={e => setOwnerUserName(e.target.value)} />
          </label>
          <label className="block">
            <span className="mb-1 block text-muted-foreground">{t('platform.tenant.invites.ownerDisplayName')}</span>
            <Input aria-label={t('platform.tenant.invites.ownerDisplayName')} value={ownerDisplayName} onChange={e => setOwnerDisplayName(e.target.value)} />
          </label>
          <div>
            <Button onClick={() => void create()} disabled={creating || branchId === ''}>{t('platform.tenant.invites.create')}</Button>
          </div>
        </div>

        {error ? (
          <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={() => setTick(n => n + 1)} />
        ) : invites === null ? (
          <LoadingCards count={1} />
        ) : invites.length === 0 ? (
          <EmptyState message={t('platform.tenant.invites.empty')} />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('platform.tenant.invites.colStatus')}</TableHead>
                <TableHead>{t('platform.tenant.invites.colCode')}</TableHead>
                <TableHead>{t('platform.tenant.invites.colOwner')}</TableHead>
                <TableHead>{t('platform.tenant.invites.colExpires')}</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {invites.map(inv => {
                const code = revealed.get(inv.ownerInviteId);
                return (
                  <TableRow key={inv.ownerInviteId}>
                    <TableCell>
                      <Badge variant={INVITE_STATUS_VARIANT[inv.status] ?? 'outline'}>
                        {INVITE_STATUS_LABEL[inv.status] ? t(INVITE_STATUS_LABEL[inv.status]) : inv.status}
                      </Badge>
                    </TableCell>
                    <TableCell><code className="font-mono text-xs">{code !== undefined ? code : `•••• ${inv.codeSuffix}`}</code></TableCell>
                    <TableCell>{inv.ownerUserName ?? '—'}</TableCell>
                    <TableCell className="tabular-nums">{formatDate(inv.expiresAtUtc)}</TableCell>
                    <TableCell className="text-right">
                      {inv.status === 'pending' && (
                        <Button variant="ghost" size="sm" onClick={() => setRevokeId(inv.ownerInviteId)}>
                          {t('platform.tenant.invites.revoke')}
                        </Button>
                      )}
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        )}
      </CardContent>
      <ConfirmDialog
        open={revokeId !== null}
        title={t('platform.tenant.invites.revokeTitle')}
        confirmLabel={t('platform.tenant.invites.revokeConfirm')}
        cancelLabel={t('platform.tenant.statusForm.cancel')}
        reasonLabel={t('platform.tenant.invites.revokeReason')}
        destructive
        pending={revoking}
        onConfirm={reason => void revoke(reason)}
        onOpenChange={open => { if (!open) setRevokeId(null); }}
      />
    </Card>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- TenantOwnerInvitesSection`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/platform/tenants/TenantOwnerInvitesSection.tsx src/platform/tenants/TenantOwnerInvitesSection.test.tsx
git commit -m "feat(platform-web): redesigned tenant owner-invites section"
```

---

### Task 4: `TenantSupportNotesSection` (redesigned support-notes section)

**Files:**
- Create: `src/platform/tenants/TenantSupportNotesSection.tsx`
- Test: `src/platform/tenants/TenantSupportNotesSection.test.tsx`

Behavior to preserve from legacy `SupportNotesSection`: list notes (author + created date + body, `whitespace-pre-wrap`); add a note from a textarea draft; edit a note inline (textarea + save/cancel). Differences: design-system `Card`/`Textarea`/`Button`, `useToast` for feedback, and a `tick`-based reload after each mutation (canonical pattern) instead of optimistic list mutation.

- [ ] **Step 1: Write the failing test**

Create `src/platform/tenants/TenantSupportNotesSection.test.tsx`:

```tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { TenantSupportNotesSection } from './TenantSupportNotesSection';
import type { TenantSupportNote } from '@/api/types';

function note(over: Partial<TenantSupportNote>): TenantSupportNote {
  return {
    tenantSupportNoteId: 'n1', organizationId: 'o1', authorPlatformAdminId: 'a1',
    authorDisplayName: 'Admin', body: 'first note', createdAtUtc: '2026-01-01T00:00:00Z', ...over
  };
}

function renderSection(client: any) {
  return render(
    <I18nProvider><ToastProvider>
      <TenantSupportNotesSection client={client} organizationId="o1" />
    </ToastProvider></I18nProvider>
  );
}

it('lists existing notes', async () => {
  const client = { listSupportNotes: vi.fn().mockResolvedValue([note({})]), createSupportNote: vi.fn(), updateSupportNote: vi.fn() };
  renderSection(client);
  expect(await screen.findByText('first note')).toBeTruthy();
  expect(screen.getByText('Admin')).toBeTruthy();
});

it('creates a note from the draft', async () => {
  const client = {
    listSupportNotes: vi.fn().mockResolvedValue([]),
    createSupportNote: vi.fn().mockResolvedValue(note({ tenantSupportNoteId: 'n2', body: 'added' })),
    updateSupportNote: vi.fn()
  };
  renderSection(client);
  await screen.findByText('Заметок поддержки пока нет.');

  fireEvent.change(screen.getByRole('textbox', { name: 'Новая заметка' }), { target: { value: 'added' } });
  fireEvent.click(screen.getByRole('button', { name: 'Добавить заметку' }));
  await waitFor(() => expect(client.createSupportNote).toHaveBeenCalledWith('o1', 'added'));
});

it('edits a note inline', async () => {
  const client = {
    listSupportNotes: vi.fn().mockResolvedValue([note({})]),
    createSupportNote: vi.fn(),
    updateSupportNote: vi.fn().mockResolvedValue(note({ body: 'edited' }))
  };
  renderSection(client);
  fireEvent.click(await screen.findByRole('button', { name: 'Редактировать' }));

  const editors = screen.getAllByRole('textbox', { name: 'Новая заметка' });
  const editor = editors[editors.length - 1];
  fireEvent.change(editor, { target: { value: 'edited' } });
  fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));
  await waitFor(() => expect(client.updateSupportNote).toHaveBeenCalledWith('o1', 'n1', 'edited'));
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npm test -- TenantSupportNotesSection`
Expected: FAIL — `Failed to resolve import "./TenantSupportNotesSection"`.

- [ ] **Step 3: Write the implementation**

Create `src/platform/tenants/TenantSupportNotesSection.tsx`:

```tsx
import { useEffect, useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Textarea } from '@/components/ui/textarea';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { PlatformApiClient } from '@/api/platformApi';
import type { TenantSupportNote } from '@/api/types';

type Client = Pick<PlatformApiClient, 'listSupportNotes' | 'createSupportNote' | 'updateSupportNote'>;

export function TenantSupportNotesSection({ client, organizationId }: { client: Client; organizationId: string }) {
  const { t, formatDate } = useI18n();
  const { toast } = useToast();
  const [tick, setTick] = useState(0);
  const [notes, setNotes] = useState<TenantSupportNote[] | null>(null);
  const [error, setError] = useState(false);
  const [draft, setDraft] = useState('');
  const [creating, setCreating] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editingBody, setEditingBody] = useState('');
  const [savingEdit, setSavingEdit] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setNotes(null); setError(false);
    client.listSupportNotes(organizationId)
      .then(rows => { if (!cancelled) setNotes(rows); })
      .catch(() => { if (!cancelled) setError(true); });
    return () => { cancelled = true; };
  }, [client, organizationId, tick]);

  async function create() {
    if (draft.trim().length === 0) return;
    setCreating(true);
    try {
      await client.createSupportNote(organizationId, draft.trim());
      setDraft('');
      toast({ title: t('platform.tenant.notes.created'), variant: 'success' });
      setTick(n => n + 1);
    } catch {
      toast({ title: t('platform.tenant.action.error'), variant: 'error' });
    } finally {
      setCreating(false);
    }
  }

  function startEdit(n: TenantSupportNote) {
    setEditingId(n.tenantSupportNoteId);
    setEditingBody(n.body);
  }

  async function saveEdit() {
    if (editingId === null || editingBody.trim().length === 0) return;
    setSavingEdit(true);
    try {
      await client.updateSupportNote(organizationId, editingId, editingBody.trim());
      setEditingId(null); setEditingBody('');
      toast({ title: t('platform.tenant.notes.updated'), variant: 'success' });
      setTick(n => n + 1);
    } catch {
      toast({ title: t('platform.tenant.action.error'), variant: 'error' });
    } finally {
      setSavingEdit(false);
    }
  }

  return (
    <Card>
      <CardHeader><CardTitle>{t('platform.tenant.section.notes')}</CardTitle></CardHeader>
      <CardContent className="flex flex-col gap-4 text-sm">
        <div className="flex flex-col gap-2">
          <label className="block">
            <span className="mb-1 block text-muted-foreground">{t('platform.tenant.notes.newNote')}</span>
            <Textarea aria-label={t('platform.tenant.notes.newNote')} rows={3} maxLength={4000} value={draft} onChange={e => setDraft(e.target.value)} />
          </label>
          <p className="text-xs text-muted-foreground">{t('platform.tenant.notes.hint')}</p>
          <div>
            <Button onClick={() => void create()} disabled={creating || draft.trim().length === 0}>{t('platform.tenant.notes.add')}</Button>
          </div>
        </div>

        {error ? (
          <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={() => setTick(n => n + 1)} />
        ) : notes === null ? (
          <LoadingCards count={1} />
        ) : notes.length === 0 ? (
          <EmptyState message={t('platform.tenant.notes.empty')} />
        ) : (
          <ul className="flex flex-col gap-3">
            {notes.map(n => (
              <li key={n.tenantSupportNoteId} className="rounded-md border border-border p-3">
                <div className="mb-1 flex items-center justify-between text-xs text-muted-foreground">
                  <span>{n.authorDisplayName.length === 0 ? n.authorPlatformAdminId : n.authorDisplayName}</span>
                  <span className="tabular-nums">{formatDate(n.createdAtUtc)}</span>
                </div>
                {editingId === n.tenantSupportNoteId ? (
                  <div className="flex flex-col gap-2">
                    <Textarea aria-label={t('platform.tenant.notes.newNote')} rows={4} maxLength={4000} value={editingBody} onChange={e => setEditingBody(e.target.value)} />
                    <div className="flex gap-2">
                      <Button variant="outline" size="sm" disabled={savingEdit} onClick={() => setEditingId(null)}>{t('platform.tenant.notes.cancel')}</Button>
                      <Button size="sm" disabled={savingEdit || editingBody.trim().length === 0} onClick={() => void saveEdit()}>{t('platform.tenant.notes.save')}</Button>
                    </div>
                  </div>
                ) : (
                  <div className="flex flex-col gap-2">
                    <p className="whitespace-pre-wrap">{n.body}</p>
                    <div>
                      <Button variant="ghost" size="sm" onClick={() => startEdit(n)}>{t('platform.tenant.notes.edit')}</Button>
                    </div>
                  </div>
                )}
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- TenantSupportNotesSection`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/platform/tenants/TenantSupportNotesSection.tsx src/platform/tenants/TenantSupportNotesSection.test.tsx
git commit -m "feat(platform-web): redesigned tenant support-notes section"
```

---

### Task 5: Wire new sections into `TenantDrawer`; delete legacy components

**Files:**
- Modify: `src/platform/tenants/TenantDrawer.tsx`
- Delete: `src/components/OwnerInvitesSection.tsx`, `src/components/SupportNotesSection.tsx`

- [ ] **Step 1: Swap the imports in `TenantDrawer.tsx`**

In `src/platform/tenants/TenantDrawer.tsx`, replace these two import lines:

```tsx
import { OwnerInvitesSection } from '@/components/OwnerInvitesSection';
import { SupportNotesSection } from '@/components/SupportNotesSection';
```

with:

```tsx
import { TenantOwnerInvitesSection } from './TenantOwnerInvitesSection';
import { TenantSupportNotesSection } from './TenantSupportNotesSection';
```

- [ ] **Step 2: Swap the usages in `TenantDrawer.tsx`**

Replace this block:

```tsx
      {/* Interim: legacy sections embedded unchanged until later plans redesign them. */}
      <OwnerInvitesSection client={client} organizationId={tenant.organizationId} branches={tenant.branches} initialInvite={initialInvite} />
      <SupportNotesSection client={client} organizationId={tenant.organizationId} />
      <HealthSection client={client} organizationId={tenant.organizationId} />
```

with:

```tsx
      <TenantOwnerInvitesSection client={client} organizationId={tenant.organizationId} branches={tenant.branches} initialInvite={initialInvite} />
      <TenantSupportNotesSection client={client} organizationId={tenant.organizationId} />

      {/* Interim: legacy Health section embedded unchanged until later plans redesign it. */}
      <HealthSection client={client} organizationId={tenant.organizationId} />
```

- [ ] **Step 3: Delete the legacy section files**

```bash
git rm src/components/OwnerInvitesSection.tsx src/components/SupportNotesSection.tsx
```

- [ ] **Step 4: Verify nothing else references the deleted files**

Run: `git grep -n "OwnerInvitesSection\|SupportNotesSection" -- "src/*.ts" "src/*.tsx"`
Expected: ONLY matches for the NEW `Tenant*Section` names (in `TenantDrawer.tsx` and the new section files + their tests). No reference to `@/components/OwnerInvitesSection` or `@/components/SupportNotesSection` remains. If any other reference exists, fix that import too before continuing.

- [ ] **Step 5: Run the drawer test + the two section tests together**

Run: `npm test -- TenantDrawer TenantOwnerInvitesSection TenantSupportNotesSection`
Expected: PASS. (If `TenantDrawer.test.tsx` mocked the old sections by import path, update it to the new section names so it still renders.)

- [ ] **Step 6: Commit**

```bash
git add src/platform/tenants/TenantDrawer.tsx
git commit -m "refactor(platform-web): mount redesigned invites/notes sections, drop legacy components"
```

---

### Task 6: Full gates (type-check + full test run)

**Files:** none (verification only).

- [ ] **Step 1: Type-check via the build gate**

Run: `npm run build`
Expected: `tsc -b` completes with no errors and Vite builds. (Vitest/esbuild skip type-checks, so this gate is required — per the project's frontend-gate rule.)

If `tsc -b` reports an error in `src/preview/DemoApp.tsx`, fix it in the working tree to keep the build green but DO NOT `git add` it (it is the user's untracked scratch).

- [ ] **Step 2: Run the full frontend test suite**

Run: `npm test`
Expected: All tests pass (the prior baseline was 361; this plan adds the textarea + two section suites).

- [ ] **Step 3: Commit any build-gate fixups (only if tracked files changed)**

```bash
git status --short
# If a TRACKED file needed a fixup for tsc -b, commit it:
git commit -am "chore(platform-web): keep tsc -b green after invites/notes redesign"
```

(If only `src/preview/DemoApp.tsx` changed, leave it untracked — do not commit.)

---

## Self-Review (completed by plan author)

**Spec coverage (§5 item 5 + §4.2):** "Owner-invites + support-notes sections redesigned in tenant detail" — Task 3 (owner-invites) + Task 4 (support-notes) + Task 5 (wire-in & delete legacy). Health is explicitly left legacy (out of Plan 5 scope). ✓

**No backend work:** verified all endpoints (`/owner-invites`, `/support-notes`), contracts, services, and `platformApi.ts` client methods already exist and need no change. ✓

**Type consistency:** `Client` picks (`listOwnerInvites|createOwnerInvite|revokeOwnerInvite`, `listSupportNotes|createSupportNote|updateSupportNote`) match the real `platformApi.ts` signatures, including `createOwnerInvite(organizationId, branchId, ownerUserName|null, ownerDisplayName|null, lifetime|null)` and `revokeOwnerInvite(ownerInviteId, reason)`. DTO field names (`codeSuffix`, `ownerInviteId`, `code`, `tenantSupportNoteId`, `authorDisplayName`, `body`) match `src/api/types.ts`. `BadgeVariant`, `Button` `size="sm"`/`variant="ghost"|"outline"`, `EmptyState({message})`, `ErrorState({message,retryLabel,onRetry})`, `LoadingCards({count})`, `ConfirmDialog` props all verified against source. ✓

**i18n parity:** identical key set added to both `ru` and `en` blocks (Task 2 Steps 2–3); parity test run in Step 4. ✓

**Placeholder scan:** no TBD/"add error handling"/"similar to" — every step has full code or an exact command + expected output. ✓
