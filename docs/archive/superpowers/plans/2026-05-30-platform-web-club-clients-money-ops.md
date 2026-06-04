# Clients Money Operations (Plan 6b) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add wallet money operations to a client's card — top-up, pay-debt, manual correction, and refund-a-ledger-entry — as permission-gated dialogs inside `WalletPanel`, each refetching the wallet summary on success.

**Architecture:** Builds directly on Plan 6a. `WalletPanel` already owns the `useWalletSummary` hook (so it has the loaded `balance` incl. its `currencyCode`, the `ledger` rows, and `retry`). 6b adds: request types + 4 `clubApi` wrappers; a pure `moneyOpsModel` (major→minor / minutes→seconds request builders); four self-contained dialog components; and integration into `WalletPanel` (action buttons + a refund button per ledger row). Action availability is driven by an optional `moneyPerms` prop (defaults to all-false → no buttons), threaded from `App.tsx` through `ClientsScreen` → `ClientDetail` → `WalletPanel` in the final task. An optional `onMutated` callback lets the screen also refresh the search list (whose row balances would otherwise go stale).

**Tech Stack:** React 19, TypeScript, Vite, Vitest 4 + jsdom + @testing-library/react (`globals: false` — import `{ it, expect, vi }` from `'vitest'`). Money via `src/club/money.ts` (`majorToMinor`/`minorToMajor`). Radix `Select` for account-type choice (tests rely on the DEFAULT selection — do NOT open the dropdown in jsdom). i18n RU/EN parity enforced by a test.

**Spec:** `docs/superpowers/specs/2026-05-30-platform-web-club-clients-crm-design.md`

**Backend contracts (verified 2026-05-30 against C#):**
| Verb | Path | Request (C# record) | Response | Permission |
|---|---|---|---|---|
| POST | `/api/players/{playerAccountId}/wallet/top-ups` | `TopUpWalletRequest(OrganizationId, Amount: MoneyDto, Reason, IdempotencyKey)` | `LedgerEntryDto` | `billing.wallet.top_up` |
| POST | `/api/players/{playerAccountId}/debts/payments` | `PayDebtRequest(OrganizationId, Amount, Reason, IdempotencyKey)` | `LedgerEntryDto` | `billing.debt.pay` |
| POST | `/api/players/{playerAccountId}/ledger/manual-corrections` | `ManualLedgerCorrectionRequest(OrganizationId, AccountType, Amount, QuantitySeconds, Reason, IdempotencyKey)` | `LedgerEntryDto` | `billing.manual_correction` |
| POST | `/api/players/{playerAccountId}/ledger/{ledgerEntryId}/refunds` | `RefundLedgerEntryRequest(OrganizationId, LedgerEntryId, Amount, Reason, IdempotencyKey)` | `LedgerEntryDto` | `billing.refund` |

- `MoneyDto` on the wire = `{ currencyCode, minorUnits }` (frontend type `MoneyMinor`, already defined). `minorUnits` is a signed `long` — manual corrections MAY be negative.
- `AccountType` ∈ `wallet | debt | package_time | bonus_time`. For money accounts the correction uses `Amount` (minorUnits) with `QuantitySeconds = 0`; for time accounts it uses `QuantitySeconds` with `Amount.minorUnits = 0` (currencyCode still required — use the wallet's currency).
- The currency code for all operations comes from the loaded `balance.walletCurrency` (single currency per account).
- `LedgerEntry`, `WalletSummary`, `MoneyMinor` types and `getWalletSummary` already exist (Plan 6a).

**npm cwd:** `D:\afk4.net\src\AFK4.Platform.Web` (all `npm`/`git` commands assume this directory).

---

### Task 1: i18n keys (money operations)

**Files:**
- Modify: `src/i18n/messages.ts` (insert into both `ru` and `en` blocks, after the `ledger.account.bonus_time` key added in Plan 6a, before the block-closing brace)
- Modify: `src/i18n/messages.test.ts` (add a parity-coverage block)

- [ ] **Step 1: Add the parity test block first.** In `src/i18n/messages.test.ts`, append after the `'includes the clients/CRM keys'` test:

```ts
it('includes the money-operations keys', () => {
  for (const key of [
    'money.topUp', 'money.topUp.title', 'money.payDebt', 'money.payDebt.title',
    'money.correction', 'money.correction.title', 'money.refund', 'money.refund.title',
    'money.field.amount', 'money.field.minutes', 'money.field.reason', 'money.field.account',
    'money.submit'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});
```

- [ ] **Step 2: Run `npm test -- messages`** — expect FAIL (keys undefined).

- [ ] **Step 3: Add the RU keys.** In `src/i18n/messages.ts`, the `ru` block (added in 6a) currently ends:
```ts
    'ledger.account.bonus_time': 'Бонусное время'
  },
  en: {
```
Change the `'ledger.account.bonus_time'` line to add a trailing comma and insert before the `  },`:
```ts
    'ledger.account.bonus_time': 'Бонусное время',
    'money.topUp': 'Пополнить',
    'money.topUp.title': 'Пополнение кошелька',
    'money.payDebt': 'Оплатить долг',
    'money.payDebt.title': 'Оплата долга',
    'money.correction': 'Коррекция',
    'money.correction.title': 'Ручная коррекция',
    'money.refund': 'Возврат',
    'money.refund.title': 'Возврат операции',
    'money.field.amount': 'Сумма',
    'money.field.minutes': 'Минуты',
    'money.field.reason': 'Причина',
    'money.field.account': 'Счёт',
    'money.submit': 'Подтвердить'
  },
  en: {
```

- [ ] **Step 4: Add the EN keys.** The `en` block currently ends:
```ts
    'ledger.account.bonus_time': 'Bonus time'
  }
} as const;
```
Change the `'ledger.account.bonus_time'` line to add a trailing comma and insert before the `  }`:
```ts
    'ledger.account.bonus_time': 'Bonus time',
    'money.topUp': 'Top up',
    'money.topUp.title': 'Top up wallet',
    'money.payDebt': 'Pay debt',
    'money.payDebt.title': 'Pay debt',
    'money.correction': 'Correction',
    'money.correction.title': 'Manual correction',
    'money.refund': 'Refund',
    'money.refund.title': 'Refund transaction',
    'money.field.amount': 'Amount',
    'money.field.minutes': 'Minutes',
    'money.field.reason': 'Reason',
    'money.field.account': 'Account',
    'money.submit': 'Confirm'
  }
} as const;
```

- [ ] **Step 5: Run `npm test -- messages`** — expect PASS (new test + identical-key-sets test).

- [ ] **Step 6: Commit**

```bash
git add src/i18n/messages.ts src/i18n/messages.test.ts
git commit -m "feat(clients): i18n keys for wallet money operations"
```

---

### Task 2: Request types + clubApi wrappers

**Files:**
- Modify: `src/api/types.ts` (append after `CreatePlayerAccountRequest`, end of file)
- Modify: `src/api/clubApi.ts` (add type imports; add 4 methods after `getPlayerPackages`)

- [ ] **Step 1: Add the request types.** Append to `src/api/types.ts`:

```ts

export interface TopUpWalletRequest {
  organizationId: string;
  amount: MoneyMinor;
  reason: string;
  idempotencyKey: string;
}

export interface PayDebtRequest {
  organizationId: string;
  amount: MoneyMinor;
  reason: string;
  idempotencyKey: string;
}

export interface ManualLedgerCorrectionRequest {
  organizationId: string;
  accountType: string;
  amount: MoneyMinor;
  quantitySeconds: number;
  reason: string;
  idempotencyKey: string;
}

export interface RefundLedgerEntryRequest {
  organizationId: string;
  ledgerEntryId: string;
  amount: MoneyMinor;
  reason: string;
  idempotencyKey: string;
}
```

- [ ] **Step 2: Add the type imports to clubApi.ts.** In the `import type { … } from './types'` block, add these four names (anywhere inside the braces): `ManualLedgerCorrectionRequest`, `PayDebtRequest`, `RefundLedgerEntryRequest`, `TopUpWalletRequest`. Also ensure `LedgerEntry` is imported (it is the response type) — add it if not already present.

- [ ] **Step 3: Add the 4 methods.** In `src/api/clubApi.ts`, insert immediately AFTER the `getPlayerPackages` method (added in Plan 6a) and BEFORE `private async send<T>(`:

```ts
  public topUpWallet(playerAccountId: string, request: TopUpWalletRequest): Promise<LedgerEntry> {
    return this.send<LedgerEntry>('POST', `/api/players/${encodeURIComponent(playerAccountId)}/wallet/top-ups`, request);
  }

  public payDebt(playerAccountId: string, request: PayDebtRequest): Promise<LedgerEntry> {
    return this.send<LedgerEntry>('POST', `/api/players/${encodeURIComponent(playerAccountId)}/debts/payments`, request);
  }

  public createManualCorrection(playerAccountId: string, request: ManualLedgerCorrectionRequest): Promise<LedgerEntry> {
    return this.send<LedgerEntry>('POST', `/api/players/${encodeURIComponent(playerAccountId)}/ledger/manual-corrections`, request);
  }

  public refundLedgerEntry(playerAccountId: string, ledgerEntryId: string, request: RefundLedgerEntryRequest): Promise<LedgerEntry> {
    return this.send<LedgerEntry>(
      'POST',
      `/api/players/${encodeURIComponent(playerAccountId)}/ledger/${encodeURIComponent(ledgerEntryId)}/refunds`,
      request
    );
  }
```

- [ ] **Step 4: Verify it compiles.** Run: `npx tsc -b` — expect exit 0.

- [ ] **Step 5: Commit**

```bash
git add src/api/types.ts src/api/clubApi.ts
git commit -m "feat(clients): money-operation request types and clubApi wrappers"
```

---

### Task 3: moneyOpsModel (pure builders)

**Files:**
- Create: `src/club/clients/moneyOpsModel.ts`
- Test: `src/club/clients/moneyOpsModel.test.ts`

`buildAmountReasonRequest` returns the shape shared by top-up and pay-debt (both are structurally `{ organizationId, amount, reason, idempotencyKey }`). The manual-correction builder takes BOTH an amount (major) and minutes; callers pass `0` for the dimension that does not apply to the chosen account type.

- [ ] **Step 1: Write the failing test** — create `src/club/clients/moneyOpsModel.test.ts`:

```ts
import { it, expect } from 'vitest';
import { buildAmountReasonRequest, buildManualCorrectionRequest, buildRefundRequest } from './moneyOpsModel';

it('builds an amount+reason request (top-up / pay-debt shape): major to minor, trims reason', () => {
  expect(buildAmountReasonRequest('org', 'TJS', 50, '  касса  ', 'idem')).toEqual({
    organizationId: 'org', amount: { currencyCode: 'TJS', minorUnits: 5000 }, reason: 'касса', idempotencyKey: 'idem'
  });
});

it('builds a money-account correction: amount to minor, zero seconds', () => {
  expect(buildManualCorrectionRequest('org', 'wallet', 'TJS', -5, 0, 'правка', 'idem')).toEqual({
    organizationId: 'org', accountType: 'wallet', amount: { currencyCode: 'TJS', minorUnits: -500 },
    quantitySeconds: 0, reason: 'правка', idempotencyKey: 'idem'
  });
});

it('builds a time-account correction: minutes to seconds, zero amount', () => {
  expect(buildManualCorrectionRequest('org', 'package_time', 'TJS', 0, 30, 'бонус', 'idem')).toEqual({
    organizationId: 'org', accountType: 'package_time', amount: { currencyCode: 'TJS', minorUnits: 0 },
    quantitySeconds: 1800, reason: 'бонус', idempotencyKey: 'idem'
  });
});

it('builds a refund request for a ledger entry', () => {
  expect(buildRefundRequest('org', 'l1', 'TJS', 50, 'возврат', 'idem')).toEqual({
    organizationId: 'org', ledgerEntryId: 'l1', amount: { currencyCode: 'TJS', minorUnits: 5000 },
    reason: 'возврат', idempotencyKey: 'idem'
  });
});
```

- [ ] **Step 2: Run `npm test -- moneyOpsModel`** — expect FAIL (module not found).

- [ ] **Step 3: Write the implementation** — create `src/club/clients/moneyOpsModel.ts`:

```ts
import type {
  ManualLedgerCorrectionRequest, RefundLedgerEntryRequest, TopUpWalletRequest
} from '@/api/types';
import { majorToMinor } from '../money';

/** Shared shape of TopUpWalletRequest and PayDebtRequest (structurally identical). */
export function buildAmountReasonRequest(
  organizationId: string,
  currencyCode: string,
  amountMajor: number,
  reason: string,
  idempotencyKey: string
): TopUpWalletRequest {
  return {
    organizationId,
    amount: { currencyCode, minorUnits: majorToMinor(amountMajor) },
    reason: reason.trim(),
    idempotencyKey
  };
}

export function buildManualCorrectionRequest(
  organizationId: string,
  accountType: string,
  currencyCode: string,
  amountMajor: number,
  minutes: number,
  reason: string,
  idempotencyKey: string
): ManualLedgerCorrectionRequest {
  return {
    organizationId,
    accountType,
    amount: { currencyCode, minorUnits: majorToMinor(amountMajor) },
    quantitySeconds: Math.round(minutes * 60),
    reason: reason.trim(),
    idempotencyKey
  };
}

export function buildRefundRequest(
  organizationId: string,
  ledgerEntryId: string,
  currencyCode: string,
  amountMajor: number,
  reason: string,
  idempotencyKey: string
): RefundLedgerEntryRequest {
  return {
    organizationId,
    ledgerEntryId,
    amount: { currencyCode, minorUnits: majorToMinor(amountMajor) },
    reason: reason.trim(),
    idempotencyKey
  };
}
```

Note: `buildAmountReasonRequest` is typed `TopUpWalletRequest` but is structurally identical to `PayDebtRequest`, so its result is assignable to `payDebt(...)` too.

- [ ] **Step 4: Run `npm test -- moneyOpsModel`** — expect PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/clients/moneyOpsModel.ts src/club/clients/moneyOpsModel.test.ts
git commit -m "feat(clients): pure moneyOpsModel request builders"
```

---

### Task 4: AmountReasonDialog (top-up & pay-debt)

**Files:**
- Create: `src/club/clients/AmountReasonDialog.tsx`
- Test: `src/club/clients/AmountReasonDialog.test.tsx`

One self-contained dialog reused for top-up and pay-debt, selected by `kind`. It builds the request, calls the matching API method, toasts the result, and calls `onDone` on success.

- [ ] **Step 1: Write the failing test** — create `src/club/clients/AmountReasonDialog.test.tsx`:

```tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { AmountReasonDialog } from './AmountReasonDialog';

function setup(kind: 'topUp' | 'payDebt') {
  const client = {
    topUpWallet: vi.fn<(id: string, req: object) => Promise<object>>(async () => ({ ledgerEntryId: 'l9' })),
    payDebt: vi.fn<(id: string, req: object) => Promise<object>>(async () => ({ ledgerEntryId: 'l9' }))
  };
  const onDone = vi.fn();
  render(
    <I18nProvider><ToastProvider>
      <AmountReasonDialog
        open kind={kind} client={client as never} playerAccountId="p1" organizationId="org"
        currencyCode="TJS" onOpenChange={() => {}} onDone={onDone}
      />
    </ToastProvider></I18nProvider>
  );
  return { client, onDone };
}

it('disables submit until amount and reason are filled', () => {
  setup('topUp');
  expect(screen.getByRole('button', { name: 'Подтвердить' })).toBeDisabled();
  fireEvent.change(screen.getByLabelText('Сумма'), { target: { value: '50' } });
  fireEvent.change(screen.getByLabelText('Причина'), { target: { value: 'касса' } });
  expect(screen.getByRole('button', { name: 'Подтвердить' })).toBeEnabled();
});

it('tops up the wallet with minor units', async () => {
  const { client, onDone } = setup('topUp');
  fireEvent.change(screen.getByLabelText('Сумма'), { target: { value: '50' } });
  fireEvent.change(screen.getByLabelText('Причина'), { target: { value: 'касса' } });
  fireEvent.click(screen.getByRole('button', { name: 'Подтвердить' }));
  await waitFor(() => expect(client.topUpWallet).toHaveBeenCalled());
  expect(client.topUpWallet.mock.calls[0][0]).toBe('p1');
  expect(client.topUpWallet.mock.calls[0][1]).toMatchObject({
    organizationId: 'org', amount: { currencyCode: 'TJS', minorUnits: 5000 }, reason: 'касса'
  });
  await waitFor(() => expect(onDone).toHaveBeenCalled());
});

it('pays debt when kind is payDebt', async () => {
  const { client } = setup('payDebt');
  fireEvent.change(screen.getByLabelText('Сумма'), { target: { value: '15' } });
  fireEvent.change(screen.getByLabelText('Причина'), { target: { value: 'долг' } });
  fireEvent.click(screen.getByRole('button', { name: 'Подтвердить' }));
  await waitFor(() => expect(client.payDebt).toHaveBeenCalled());
  expect(client.payDebt.mock.calls[0][1]).toMatchObject({ amount: { currencyCode: 'TJS', minorUnits: 1500 } });
});
```

- [ ] **Step 2: Run `npm test -- AmountReasonDialog`** — expect FAIL (module not found).

- [ ] **Step 3: Write the implementation** — create `src/club/clients/AmountReasonDialog.tsx`:

```tsx
import { useState } from 'react';
import { Dialog, DialogContent, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import { buildAmountReasonRequest } from './moneyOpsModel';

type Actions = Pick<ClubApiClient, 'topUpWallet' | 'payDebt'>;

export function AmountReasonDialog({ open, kind, client, playerAccountId, organizationId, currencyCode, onOpenChange, onDone }: {
  open: boolean;
  kind: 'topUp' | 'payDebt';
  client: Actions;
  playerAccountId: string;
  organizationId: string;
  currencyCode: string;
  onOpenChange: (open: boolean) => void;
  onDone: () => void;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [amount, setAmount] = useState('');
  const [reason, setReason] = useState('');
  const [pending, setPending] = useState(false);

  const valid = Number(amount) > 0 && reason.trim() !== '';

  async function submit() {
    setPending(true);
    try {
      const request = buildAmountReasonRequest(organizationId, currencyCode, Number(amount), reason, crypto.randomUUID());
      if (kind === 'topUp') {
        await client.topUpWallet(playerAccountId, request);
      } else {
        await client.payDebt(playerAccountId, request);
      }
      toast({ title: t('toast.saved'), variant: 'success' });
      onDone();
      onOpenChange(false);
    } catch {
      toast({ title: t('toast.failed'), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogTitle>{kind === 'topUp' ? t('money.topUp.title') : t('money.payDebt.title')}</DialogTitle>
        <div className="flex flex-col gap-3">
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('money.field.amount')}</span>
            <Input aria-label={t('money.field.amount')} value={amount} onChange={e => setAmount(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('money.field.reason')}</span>
            <Input aria-label={t('money.field.reason')} value={reason} onChange={e => setReason(e.target.value)} />
          </label>
        </div>
        <DialogFooter>
          <Button variant="outline" disabled={pending} onClick={() => onOpenChange(false)}>{t('common.cancel')}</Button>
          <Button disabled={pending || !valid} onClick={() => void submit()}>{t('money.submit')}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
```

- [ ] **Step 4: Run `npm test -- AmountReasonDialog`** — expect PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/clients/AmountReasonDialog.tsx src/club/clients/AmountReasonDialog.test.tsx
git commit -m "feat(clients): AmountReasonDialog for top-up and pay-debt"
```

---

### Task 5: ManualCorrectionDialog

**Files:**
- Create: `src/club/clients/ManualCorrectionDialog.tsx`
- Test: `src/club/clients/ManualCorrectionDialog.test.tsx`

Account type chosen via Radix `Select` (default `wallet`). When the account is `wallet`/`debt` an amount field shows; when `package_time`/`bonus_time` a minutes field shows. The test exercises the default (`wallet` → amount). The account labels reuse the existing `ledger.account.*` i18n keys.

- [ ] **Step 1: Write the failing test** — create `src/club/clients/ManualCorrectionDialog.test.tsx`:

```tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { ManualCorrectionDialog } from './ManualCorrectionDialog';

function setup() {
  const client = {
    createManualCorrection: vi.fn<(id: string, req: object) => Promise<object>>(async () => ({ ledgerEntryId: 'l9' }))
  };
  const onDone = vi.fn();
  render(
    <I18nProvider><ToastProvider>
      <ManualCorrectionDialog
        open client={client as never} playerAccountId="p1" organizationId="org"
        currencyCode="TJS" onOpenChange={() => {}} onDone={onDone}
      />
    </ToastProvider></I18nProvider>
  );
  return { client, onDone };
}

it('submits a wallet correction (default account) with minor units', async () => {
  const { client, onDone } = setup();
  fireEvent.change(screen.getByLabelText('Сумма'), { target: { value: '-5' } });
  fireEvent.change(screen.getByLabelText('Причина'), { target: { value: 'правка' } });
  fireEvent.click(screen.getByRole('button', { name: 'Подтвердить' }));
  await waitFor(() => expect(client.createManualCorrection).toHaveBeenCalled());
  expect(client.createManualCorrection.mock.calls[0][0]).toBe('p1');
  expect(client.createManualCorrection.mock.calls[0][1]).toMatchObject({
    organizationId: 'org', accountType: 'wallet',
    amount: { currencyCode: 'TJS', minorUnits: -500 }, quantitySeconds: 0, reason: 'правка'
  });
  await waitFor(() => expect(onDone).toHaveBeenCalled());
});

it('disables submit until amount and reason are set', () => {
  setup();
  expect(screen.getByRole('button', { name: 'Подтвердить' })).toBeDisabled();
  fireEvent.change(screen.getByLabelText('Сумма'), { target: { value: '5' } });
  fireEvent.change(screen.getByLabelText('Причина'), { target: { value: 'x' } });
  expect(screen.getByRole('button', { name: 'Подтвердить' })).toBeEnabled();
});
```

- [ ] **Step 2: Run `npm test -- ManualCorrectionDialog`** — expect FAIL (module not found).

- [ ] **Step 3: Write the implementation** — create `src/club/clients/ManualCorrectionDialog.tsx`:

```tsx
import { useState } from 'react';
import { Dialog, DialogContent, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import { buildManualCorrectionRequest } from './moneyOpsModel';

type Actions = Pick<ClubApiClient, 'createManualCorrection'>;

const MONEY_ACCOUNTS = ['wallet', 'debt'];

export function ManualCorrectionDialog({ open, client, playerAccountId, organizationId, currencyCode, onOpenChange, onDone }: {
  open: boolean;
  client: Actions;
  playerAccountId: string;
  organizationId: string;
  currencyCode: string;
  onOpenChange: (open: boolean) => void;
  onDone: () => void;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [accountType, setAccountType] = useState('wallet');
  const [amount, setAmount] = useState('');
  const [minutes, setMinutes] = useState('');
  const [reason, setReason] = useState('');
  const [pending, setPending] = useState(false);

  const isMoney = MONEY_ACCOUNTS.includes(accountType);
  const valueValid = isMoney ? Number(amount) !== 0 : Number(minutes) !== 0;
  const valid = valueValid && reason.trim() !== '';

  async function submit() {
    setPending(true);
    try {
      const request = buildManualCorrectionRequest(
        organizationId, accountType, currencyCode,
        isMoney ? Number(amount) : 0,
        isMoney ? 0 : Number(minutes),
        reason, crypto.randomUUID()
      );
      await client.createManualCorrection(playerAccountId, request);
      toast({ title: t('toast.saved'), variant: 'success' });
      onDone();
      onOpenChange(false);
    } catch {
      toast({ title: t('toast.failed'), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogTitle>{t('money.correction.title')}</DialogTitle>
        <div className="flex flex-col gap-3">
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('money.field.account')}</span>
            <Select value={accountType} onValueChange={setAccountType}>
              <SelectTrigger aria-label={t('money.field.account')}>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="wallet">{t('ledger.account.wallet')}</SelectItem>
                <SelectItem value="debt">{t('ledger.account.debt')}</SelectItem>
                <SelectItem value="package_time">{t('ledger.account.package_time')}</SelectItem>
                <SelectItem value="bonus_time">{t('ledger.account.bonus_time')}</SelectItem>
              </SelectContent>
            </Select>
          </label>
          {isMoney ? (
            <label className="block text-sm">
              <span className="mb-1 block text-muted-foreground">{t('money.field.amount')}</span>
              <Input aria-label={t('money.field.amount')} value={amount} onChange={e => setAmount(e.target.value)} />
            </label>
          ) : (
            <label className="block text-sm">
              <span className="mb-1 block text-muted-foreground">{t('money.field.minutes')}</span>
              <Input aria-label={t('money.field.minutes')} value={minutes} onChange={e => setMinutes(e.target.value)} />
            </label>
          )}
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('money.field.reason')}</span>
            <Input aria-label={t('money.field.reason')} value={reason} onChange={e => setReason(e.target.value)} />
          </label>
        </div>
        <DialogFooter>
          <Button variant="outline" disabled={pending} onClick={() => onOpenChange(false)}>{t('common.cancel')}</Button>
          <Button disabled={pending || !valid} onClick={() => void submit()}>{t('money.submit')}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
```

- [ ] **Step 4: Run `npm test -- ManualCorrectionDialog`** — expect PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/clients/ManualCorrectionDialog.tsx src/club/clients/ManualCorrectionDialog.test.tsx
git commit -m "feat(clients): ManualCorrectionDialog (money/time accounts)"
```

---

### Task 6: RefundDialog

**Files:**
- Create: `src/club/clients/RefundDialog.tsx`
- Test: `src/club/clients/RefundDialog.test.tsx`

Opened from a ledger row's refund button. The amount is pre-filled from the entry (editable — partial refunds allowed) and the currency comes from the entry. Takes the entry's `ledgerEntryId`, `amountMajor`, and `currencyCode` via an `entry` prop shaped like the relevant `LedgerRow` fields.

- [ ] **Step 1: Write the failing test** — create `src/club/clients/RefundDialog.test.tsx`:

```tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { RefundDialog } from './RefundDialog';

function setup() {
  const client = {
    refundLedgerEntry: vi.fn<(id: string, lid: string, req: object) => Promise<object>>(async () => ({ ledgerEntryId: 'l9' }))
  };
  const onDone = vi.fn();
  render(
    <I18nProvider><ToastProvider>
      <RefundDialog
        open client={client as never} playerAccountId="p1" organizationId="org"
        entry={{ ledgerEntryId: 'l1', amountMajor: 50, currencyCode: 'TJS' }}
        onOpenChange={() => {}} onDone={onDone}
      />
    </ToastProvider></I18nProvider>
  );
  return { client, onDone };
}

it('pre-fills the amount and refunds the entry', async () => {
  const { client, onDone } = setup();
  expect((screen.getByLabelText('Сумма') as HTMLInputElement).value).toBe('50');
  fireEvent.change(screen.getByLabelText('Причина'), { target: { value: 'брак' } });
  fireEvent.click(screen.getByRole('button', { name: 'Подтвердить' }));
  await waitFor(() => expect(client.refundLedgerEntry).toHaveBeenCalled());
  expect(client.refundLedgerEntry.mock.calls[0][0]).toBe('p1');
  expect(client.refundLedgerEntry.mock.calls[0][1]).toBe('l1');
  expect(client.refundLedgerEntry.mock.calls[0][2]).toMatchObject({
    organizationId: 'org', ledgerEntryId: 'l1', amount: { currencyCode: 'TJS', minorUnits: 5000 }, reason: 'брак'
  });
  await waitFor(() => expect(onDone).toHaveBeenCalled());
});
```

- [ ] **Step 2: Run `npm test -- RefundDialog`** — expect FAIL (module not found).

- [ ] **Step 3: Write the implementation** — create `src/club/clients/RefundDialog.tsx`:

```tsx
import { useState } from 'react';
import { Dialog, DialogContent, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import { buildRefundRequest } from './moneyOpsModel';

type Actions = Pick<ClubApiClient, 'refundLedgerEntry'>;

export interface RefundTarget {
  ledgerEntryId: string;
  amountMajor: number;
  currencyCode: string;
}

export function RefundDialog({ open, client, playerAccountId, organizationId, entry, onOpenChange, onDone }: {
  open: boolean;
  client: Actions;
  playerAccountId: string;
  organizationId: string;
  entry: RefundTarget;
  onOpenChange: (open: boolean) => void;
  onDone: () => void;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [amount, setAmount] = useState(String(entry.amountMajor));
  const [reason, setReason] = useState('');
  const [pending, setPending] = useState(false);

  const valid = Number(amount) > 0 && reason.trim() !== '';

  async function submit() {
    setPending(true);
    try {
      const request = buildRefundRequest(organizationId, entry.ledgerEntryId, entry.currencyCode, Number(amount), reason, crypto.randomUUID());
      await client.refundLedgerEntry(playerAccountId, entry.ledgerEntryId, request);
      toast({ title: t('toast.saved'), variant: 'success' });
      onDone();
      onOpenChange(false);
    } catch {
      toast({ title: t('toast.failed'), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogTitle>{t('money.refund.title')}</DialogTitle>
        <div className="flex flex-col gap-3">
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('money.field.amount')}</span>
            <Input aria-label={t('money.field.amount')} value={amount} onChange={e => setAmount(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('money.field.reason')}</span>
            <Input aria-label={t('money.field.reason')} value={reason} onChange={e => setReason(e.target.value)} />
          </label>
        </div>
        <DialogFooter>
          <Button variant="outline" disabled={pending} onClick={() => onOpenChange(false)}>{t('common.cancel')}</Button>
          <Button disabled={pending || !valid} onClick={() => void submit()}>{t('money.submit')}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
```

- [ ] **Step 4: Run `npm test -- RefundDialog`** — expect PASS (1 test).

- [ ] **Step 5: Commit**

```bash
git add src/club/clients/RefundDialog.tsx src/club/clients/RefundDialog.test.tsx
git commit -m "feat(clients): RefundDialog for ledger entries"
```

---

### Task 7: Integrate money actions into WalletPanel

**Files:**
- Modify: `src/club/clients/WalletPanel.tsx`
- Modify: `src/club/clients/WalletPanel.test.tsx`

Add an optional `moneyPerms` prop (defaults to all-false → no buttons, so existing callers keep building) and an optional `onMutated` callback. Render action buttons in the balances header and a refund button per ledger row, opening the dialogs built in Tasks 4-6. After any successful op, refetch (`retry()`) and call `onMutated?.()`. Expand the client `Pick` to include the money-op methods.

- [ ] **Step 1: Update the WalletPanel test.** Replace the ENTIRE contents of `src/club/clients/WalletPanel.test.tsx` with:

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import type { WalletSummary } from '@/api/types';
import { WalletPanel } from './WalletPanel';

const summary: WalletSummary = {
  playerAccountId: 'p1',
  walletBalance: { currencyCode: 'TJS', minorUnits: 50000 },
  debtBalance: { currencyCode: 'TJS', minorUnits: 1500 },
  recentEntries: [{
    ledgerEntryId: 'l1', organizationId: 'org', branchId: 'b1', playerAccountId: 'p1',
    sessionId: null, playerPackageId: null, entryType: 'top_up', accountType: 'wallet',
    amount: { currencyCode: 'TJS', minorUnits: 50000 }, quantitySeconds: 0,
    description: 'd', reason: 'Касса', reversesLedgerEntryId: null,
    createdByStaffUserId: 's1', createdAtUtc: '2026-05-30T10:00:00.000Z'
  }]
};

function fakeClient() {
  return {
    getWalletSummary: vi.fn(async () => summary),
    topUpWallet: vi.fn(async () => ({ ledgerEntryId: 'l9' })),
    payDebt: vi.fn(async () => ({ ledgerEntryId: 'l9' })),
    createManualCorrection: vi.fn(async () => ({ ledgerEntryId: 'l9' })),
    refundLedgerEntry: vi.fn(async () => ({ ledgerEntryId: 'l9' }))
  };
}

function renderPanel(moneyPerms?: { topUp: boolean; payDebt: boolean; correct: boolean; refund: boolean }) {
  render(
    <I18nProvider><ToastProvider>
      <WalletPanel client={fakeClient() as never} playerAccountId="p1" organizationId="org" moneyPerms={moneyPerms} />
    </ToastProvider></I18nProvider>
  );
}

it('shows balances and a translated ledger entry type', async () => {
  renderPanel();
  expect(await screen.findByText('Пополнение')).toBeInTheDocument();
  expect(screen.getByText('Касса')).toBeInTheDocument();
  expect(screen.getByText('История операций')).toBeInTheDocument();
});

it('hides all action buttons when no permissions are given', async () => {
  renderPanel();
  await screen.findByText('Пополнение');
  expect(screen.queryByRole('button', { name: 'Пополнить' })).not.toBeInTheDocument();
  expect(screen.queryByRole('button', { name: 'Возврат' })).not.toBeInTheDocument();
});

it('opens the top-up dialog when permitted', async () => {
  renderPanel({ topUp: true, payDebt: false, correct: false, refund: false });
  await screen.findByText('Пополнение');
  fireEvent.click(screen.getByRole('button', { name: 'Пополнить' }));
  expect(await screen.findByText('Пополнение кошелька')).toBeInTheDocument();
});

it('shows a refund button per ledger row when permitted', async () => {
  renderPanel({ topUp: false, payDebt: false, correct: false, refund: true });
  await screen.findByText('Пополнение');
  expect(screen.getByRole('button', { name: 'Возврат' })).toBeInTheDocument();
});
```

- [ ] **Step 2: Run `npm test -- WalletPanel`** — expect FAIL (TypeScript/`moneyPerms` prop & buttons not implemented; the dialogs/buttons do not exist yet).

- [ ] **Step 3: Rewrite `src/club/clients/WalletPanel.tsx`** with the integration. Replace the entire file with:

```tsx
import { useState } from 'react';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { Button } from '@/components/ui/button';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { MessageKey } from '@/i18n/messages';
import type { ClubApiClient } from '@/api/clubApi';
import { useWalletSummary } from './useWalletSummary';
import { AmountReasonDialog } from './AmountReasonDialog';
import { ManualCorrectionDialog } from './ManualCorrectionDialog';
import { RefundDialog, type RefundTarget } from './RefundDialog';

type Client = Pick<ClubApiClient,
  'getWalletSummary' | 'topUpWallet' | 'payDebt' | 'createManualCorrection' | 'refundLedgerEntry'>;

export interface MoneyPerms {
  topUp: boolean;
  payDebt: boolean;
  correct: boolean;
  refund: boolean;
}

const NO_PERMS: MoneyPerms = { topUp: false, payDebt: false, correct: false, refund: false };

const ENTRY_TYPE_KEY: Record<string, MessageKey> = {
  top_up: 'ledger.type.top_up',
  gameplay_charge: 'ledger.type.gameplay_charge',
  package_purchase: 'ledger.type.package_purchase',
  package_consumption: 'ledger.type.package_consumption',
  bonus_grant: 'ledger.type.bonus_grant',
  bonus_consumption: 'ledger.type.bonus_consumption',
  refund: 'ledger.type.refund',
  manual_correction: 'ledger.type.manual_correction',
  postpaid_debt: 'ledger.type.postpaid_debt',
  debt_payment: 'ledger.type.debt_payment',
  reversal: 'ledger.type.reversal'
};

const ACCOUNT_TYPE_KEY: Record<string, MessageKey> = {
  wallet: 'ledger.account.wallet',
  debt: 'ledger.account.debt',
  package_time: 'ledger.account.package_time',
  bonus_time: 'ledger.account.bonus_time'
};

export function WalletPanel({ client, playerAccountId, organizationId, moneyPerms = NO_PERMS, onMutated }: {
  client: Client;
  playerAccountId: string;
  organizationId: string;
  moneyPerms?: MoneyPerms;
  onMutated?: () => void;
}) {
  const { t, formatCurrency, formatNumber, formatDate } = useI18n();
  const state = useWalletSummary(client, playerAccountId);
  const [dialog, setDialog] = useState<'topUp' | 'payDebt' | 'correct' | null>(null);
  const [refundTarget, setRefundTarget] = useState<RefundTarget | null>(null);

  if (state.status === 'loading') return <LoadingCards count={2} />;
  if (state.status === 'error') return <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />;

  const { balance, ledger, retry } = state;
  const entryLabel = (type: string): string => (ENTRY_TYPE_KEY[type] ? t(ENTRY_TYPE_KEY[type]) : type);
  const accountLabel = (type: string): string => (ACCOUNT_TYPE_KEY[type] ? t(ACCOUNT_TYPE_KEY[type]) : type);
  const afterMutation = (): void => { retry(); onMutated?.(); };

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div className="flex flex-wrap gap-6">
          <div>
            <p className="text-xs text-muted-foreground">{t('clients.balance.wallet')}</p>
            <p className="text-lg font-semibold tabular-nums">{formatCurrency(balance.walletMajor, balance.walletCurrency)}</p>
          </div>
          <div>
            <p className="text-xs text-muted-foreground">{t('clients.balance.debt')}</p>
            <p className="text-lg font-semibold tabular-nums">{formatCurrency(balance.debtMajor, balance.debtCurrency)}</p>
          </div>
        </div>
        <div className="flex flex-wrap gap-2">
          {moneyPerms.topUp && <Button size="sm" onClick={() => setDialog('topUp')}>{t('money.topUp')}</Button>}
          {moneyPerms.payDebt && <Button size="sm" variant="outline" onClick={() => setDialog('payDebt')}>{t('money.payDebt')}</Button>}
          {moneyPerms.correct && <Button size="sm" variant="outline" onClick={() => setDialog('correct')}>{t('money.correction')}</Button>}
        </div>
      </div>

      <div>
        <h3 className="mb-2 text-sm font-medium">{t('clients.history.title')}</h3>
        {ledger.length === 0 ? (
          <EmptyState message={t('clients.history.empty')} />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('clients.history.col.date')}</TableHead>
                <TableHead>{t('clients.history.col.type')}</TableHead>
                <TableHead>{t('clients.history.col.account')}</TableHead>
                <TableHead>{t('clients.history.col.amount')}</TableHead>
                <TableHead>{t('clients.history.col.minutes')}</TableHead>
                <TableHead>{t('clients.history.col.reason')}</TableHead>
                {moneyPerms.refund && <TableHead />}
              </TableRow>
            </TableHeader>
            <TableBody>
              {ledger.map(row => (
                <TableRow key={row.ledgerEntryId}>
                  <TableCell>{formatDate(row.createdAtUtc)}</TableCell>
                  <TableCell>{entryLabel(row.entryType)}</TableCell>
                  <TableCell>{accountLabel(row.accountType)}</TableCell>
                  <TableCell className="tabular-nums">{formatCurrency(row.amountMajor, row.currencyCode)}</TableCell>
                  <TableCell className="tabular-nums">{row.quantityMinutes === 0 ? '—' : formatNumber(row.quantityMinutes)}</TableCell>
                  <TableCell>{row.reason}</TableCell>
                  {moneyPerms.refund && (
                    <TableCell>
                      <Button size="xs" variant="ghost" onClick={() => setRefundTarget({
                        ledgerEntryId: row.ledgerEntryId, amountMajor: row.amountMajor, currencyCode: row.currencyCode
                      })}>
                        {t('money.refund')}
                      </Button>
                    </TableCell>
                  )}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
        <p className="mt-2 text-xs text-muted-foreground">{t('clients.history.note')}</p>
      </div>

      {(dialog === 'topUp' || dialog === 'payDebt') && (
        <AmountReasonDialog
          open kind={dialog} client={client} playerAccountId={playerAccountId}
          organizationId={organizationId} currencyCode={balance.walletCurrency}
          onOpenChange={o => { if (!o) setDialog(null); }}
          onDone={afterMutation}
        />
      )}
      {dialog === 'correct' && (
        <ManualCorrectionDialog
          open client={client} playerAccountId={playerAccountId}
          organizationId={organizationId} currencyCode={balance.walletCurrency}
          onOpenChange={o => { if (!o) setDialog(null); }}
          onDone={afterMutation}
        />
      )}
      {refundTarget !== null && (
        <RefundDialog
          open client={client} playerAccountId={playerAccountId}
          organizationId={organizationId} entry={refundTarget}
          onOpenChange={o => { if (!o) setRefundTarget(null); }}
          onDone={afterMutation}
        />
      )}
    </div>
  );
}
```

Notes on this rewrite:
- `WalletPanel` now requires `organizationId` (threaded to the dialogs). Its caller `ClientDetail` does not pass it yet, so updating `WalletPanel` alone would break `npx tsc -b`. To keep THIS task's commit green, also apply the `ClientDetail.tsx` change from Task 8 Step 1 (add `organizationId: string` prop + forward it to `WalletPanel`) and add `organizationId={organizationId}` to `ClientsScreen`'s `<ClientDetail ... />` usage (`ClientsScreen` already has `organizationId` as a prop). Those two edits are listed in this task's commit (Step 6). Task 8 then adds `moneyPerms`/`onMutated` on top.
- `Button` sizes `sm`/`xs` and `variant="ghost"`/`"outline"` are all part of the shared Button primitive.

- [ ] **Step 4: Run `npm test -- WalletPanel`** — expect PASS (4 tests).

- [ ] **Step 5: Verify the whole app still type-checks.** Run: `npx tsc -b` — expect exit 0. Fix any prop-threading type errors in `ClientDetail.tsx`/`ClientsScreen.tsx` introduced by the new `organizationId` requirement.

- [ ] **Step 6: Commit**

```bash
git add src/club/clients/WalletPanel.tsx src/club/clients/WalletPanel.test.tsx src/club/clients/ClientDetail.tsx src/club/clients/ClientsScreen.tsx
git commit -m "feat(clients): wire money-op dialogs and refund into WalletPanel"
```

---

### Task 8: Thread permissions + onMutated from App; full suite + build gate

**Files:**
- Modify: `src/club/clients/ClientDetail.tsx` (add `organizationId`, `moneyPerms`, `onMutated`; forward to WalletPanel) — partially done in Task 7; finish here.
- Modify: `src/club/clients/ClientsScreen.tsx` (build `moneyPerms`/`onMutated`; pass to ClientDetail) 
- Modify: `src/App.tsx` (compute `moneyPerms` from `session.permissions`; pass to `ClientsScreen`)
- Modify the tests of ClientDetail / ClientsScreen if their render helpers need the new props.

- [ ] **Step 1: Update `ClientDetail.tsx`.** It must accept and forward `organizationId`, `moneyPerms`, and `onMutated`. Final version:

```tsx
import { Card } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import type { PlayerRow } from './clientsModel';
import { WalletPanel, type MoneyPerms } from './WalletPanel';

type Client = Pick<ClubApiClient,
  'getWalletSummary' | 'topUpWallet' | 'payDebt' | 'createManualCorrection' | 'refundLedgerEntry'>;

export function ClientDetail({ client, player, organizationId, canViewBilling, moneyPerms, onMutated }: {
  client: Client;
  player: PlayerRow;
  organizationId: string;
  canViewBilling: boolean;
  moneyPerms?: MoneyPerms;
  onMutated?: () => void;
}) {
  const { t } = useI18n();
  return (
    <Card className="flex flex-col gap-4 p-4">
      <div className="flex items-center justify-between gap-3">
        <div>
          <p className="text-lg font-semibold">{player.displayName}</p>
          <p className="text-sm text-muted-foreground">{player.phone === '' ? '—' : player.phone}</p>
        </div>
        <Badge variant={player.isActive ? 'default' : 'secondary'}>
          {player.isActive ? t('clients.status.active') : t('clients.status.inactive')}
        </Badge>
      </div>

      {canViewBilling ? (
        <WalletPanel
          client={client} playerAccountId={player.playerAccountId} organizationId={organizationId}
          moneyPerms={moneyPerms} onMutated={onMutated}
        />
      ) : (
        <p className="text-sm text-muted-foreground">{t('clients.billing.noAccess')}</p>
      )}

      <p className="text-xs text-muted-foreground">{t('clients.editUnavailable')}</p>
    </Card>
  );
}
```

Update `ClientDetail.test.tsx`'s renders to pass `organizationId="org"` (the existing three tests construct `<ClientDetail client=... player=... canViewBilling .../>` — add `organizationId="org"` to each).

- [ ] **Step 2: Update `ClientsScreen.tsx`.** Add `moneyPerms?: MoneyPerms` to its props and forward `organizationId`, `moneyPerms`, and an `onMutated` that refetches the search list. Specifically:
  - Add the import: `import type { MoneyPerms } from './WalletPanel';`
  - Add `moneyPerms` to the destructured props and the props type: `moneyPerms?: MoneyPerms;`
  - Change the `<ClientDetail ... />` usage to:
```tsx
        <ClientDetail
          key={selected.playerAccountId}
          client={client}
          player={selected}
          organizationId={organizationId}
          canViewBilling={canViewBilling}
          moneyPerms={moneyPerms}
          onMutated={() => { if (state.status === 'ready') state.retry(); }}
        />
```
The existing `ClientsScreen.test.tsx` does not pass `moneyPerms` (it is optional) — no test change needed, but verify it still passes.

- [ ] **Step 3: Update `App.tsx`.** In the `clubClients` render branch (added in Plan 6a), pass a computed `moneyPerms`. Change the `<ClientsScreen ... />` usage to:
```tsx
          <ClientsScreen
            client={clubClient}
            branchId={activeBranchId}
            organizationId={session.organizationId}
            canCreate={session.permissions.includes('players.create')}
            canViewBilling={session.permissions.includes('billing.view')}
            moneyPerms={{
              topUp: session.permissions.includes('billing.wallet.top_up'),
              payDebt: session.permissions.includes('billing.debt.pay'),
              correct: session.permissions.includes('billing.manual_correction'),
              refund: session.permissions.includes('billing.refund')
            }}
          />
```

- [ ] **Step 4: Run the full suite.** Run: `npm test` — expect ALL green.

- [ ] **Step 5: BUILD GATE (the real type check — vitest does NOT type-check).** Run: `npm run build` — `tsc -b && vite build`, expect exit 0.
  - If `tsc` errors, fix before committing. Watch for: a `vi.fn(async () => …)` whose `mock.calls[0][1]` is typed as an empty tuple — give the mock an explicit signature `vi.fn<(a: string, b: object) => Promise<object>>(…)` (this exact issue occurred in Plan 6a). Also check the `MoneyPerms` import path is `./WalletPanel`.

- [ ] **Step 6: Commit**

```bash
git add src/App.tsx src/club/clients/ClientDetail.tsx src/club/clients/ClientDetail.test.tsx src/club/clients/ClientsScreen.tsx
git commit -m "feat(clients): thread money permissions and list refresh into clients screen"
```

---

## Self-Review notes (for the executor)

- **Spec coverage:** top-up (T4/T7), pay-debt (T4/T7), manual correction money+time (T3/T5/T7), refund-a-ledger-entry (T6/T7), permission gating per action (T8 builds `moneyPerms` from `billing.wallet.top_up`/`billing.debt.pay`/`billing.manual_correction`/`billing.refund`), list refresh after a mutation (T8 `onMutated`). All money minor↔major via `money.ts`; time minutes↔seconds in the correction builder.
- **`organizationId` threading:** WalletPanel/ClientDetail gain a required `organizationId`; the chain is `App` (`session.organizationId`) → `ClientsScreen` (already a prop) → `ClientDetail` → `WalletPanel` → dialogs. Tasks 7 and 8 together keep the build green; the `ClientDetail`/`ClientsScreen` edits are introduced in Task 7 (to compile) and finalized in Task 8.
- **Type consistency:** `MoneyPerms` is defined once in `WalletPanel.tsx` and imported by `ClientDetail`/`ClientsScreen`. Request builders return shapes matching the C# records exactly (`TopUpWalletRequest`/`PayDebtRequest` identical; `ManualLedgerCorrectionRequest` has `accountType` + `quantitySeconds`; `RefundLedgerEntryRequest` has `ledgerEntryId`).
- **jsdom/Radix:** the `Select` in `ManualCorrectionDialog` is tested only at its default (`wallet` → amount field); the dropdown is never opened. The time path is covered by the pure `moneyOpsModel` test.
- **Refund scope:** a refund button shows on EVERY ledger row when `billing.refund` is held; the backend validates whether a given entry is refundable. This is intentional (no client-side refundability heuristic).
- **Build gate is mandatory** at Task 8 — `npm test` (esbuild) does not type-check; only `npm run build` (`tsc -b`) does.
