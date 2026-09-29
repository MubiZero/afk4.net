import { describe, expect, it, mock } from 'bun:test';
import { render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { DebtSection, type DebtSectionAccess } from './DebtSection';
import type { DebtRow } from '@/api/types';

const fullAccess: DebtSectionAccess = { canMarkPaid: true, canGrantGrace: true, canToggleStatus: true, canAddNote: true };
const noAccess: DebtSectionAccess = { canMarkPaid: false, canGrantGrace: false, canToggleStatus: false, canAddNote: false };

function row(overrides: Partial<DebtRow> = {}): DebtRow {
  return {
    organizationId: 'o1',
    organizationName: 'Арена',
    organizationSlug: 'arena',
    organizationStatus: 'active',
    subscriptionStatus: 'past_due',
    outstandingMinorUnits: 290000,
    currencyCode: 'TJS',
    oldestOverdueInvoiceNumber: 1,
    oldestOverdueInvoiceId: 'i1',
    daysOverdue: 10,
    dunningStage: 3,
    graceUntilUtc: null,
    settledButSuspended: false,
    ...overrides
  };
}

function fakeClient(rows: DebtRow[]) {
  return {
    debt: { listDebt: mock().mockResolvedValue(rows) },
    invoices: { markInvoicePaid: mock().mockResolvedValue({}) },
    organizations: { updateStatus: mock().mockResolvedValue({}) },
    subscriptions: { updateSubscription: mock().mockResolvedValue({}), getSubscription: mock().mockResolvedValue({}) },
    supportNotes: { createSupportNote: mock().mockResolvedValue({}) }
  } as never;
}

describe('DebtSection', () => {
  it('renders a debt row with amount, days overdue and dunning stage', async () => {
    render(
      <I18nProvider><ToastProvider><DebtSection client={fakeClient([row()])} access={fullAccess} /></ToastProvider></I18nProvider>
    );
    await waitFor(() => expect(screen.getByText('Арена')).toBeInTheDocument());

    const debtRow = screen.getByTestId('debt-row');
    // outstandingMinorUnits: 290000 must render as MAJOR units (2900), not 290000.
    const digits = (debtRow.textContent ?? '').replace(/\D/g, '');
    expect(digits).toContain('2900');
    expect(digits).not.toContain('290000');
    expect(debtRow.textContent).toContain('10');
    expect(debtRow.textContent).toContain('Третье напоминание');
  });

  it('shows the empty state when nobody owes anything', async () => {
    render(
      <I18nProvider><ToastProvider><DebtSection client={fakeClient([])} access={fullAccess} /></ToastProvider></I18nProvider>
    );
    await waitFor(() => expect(screen.getByText('Никто не должен, все клубы включены — хорошая новость.')).toBeInTheDocument());
  });

  it('marks a row under active grace as calm, not alarming', async () => {
    render(
      <I18nProvider><ToastProvider>
        <DebtSection client={fakeClient([row({ graceUntilUtc: '2026-09-01T00:00:00Z' })])} access={fullAccess} />
      </ToastProvider></I18nProvider>
    );
    await waitFor(() => expect(screen.getByTestId('debt-row')).toBeInTheDocument());
    expect(screen.getByTestId('debt-row').textContent).toContain('Отсрочка до');
    // A club under grace isn't overdue — the stage badge must not use the alarming (destructive) variant.
    expect(screen.getByTestId('debt-stage-badge').className).not.toContain('is-danger');
  });

  it('marks a club that settled its debt but is still disabled', async () => {
    render(
      <I18nProvider><ToastProvider>
        <DebtSection client={fakeClient([row({ outstandingMinorUnits: 0, settledButSuspended: true, organizationStatus: 'suspended' })])} access={fullAccess} />
      </ToastProvider></I18nProvider>
    );
    await waitFor(() => expect(screen.getByTestId('debt-row')).toBeInTheDocument());
    expect(screen.getByTestId('debt-row').textContent).toContain('Долг погашен, клуб отключён');
    // Debt is already settled — a reminder to reactivate isn't an alarm either.
    expect(screen.getByTestId('debt-stage-badge').className).not.toContain('is-danger');
  });

  it('hides row actions when no billing/organization right is granted', async () => {
    render(
      <I18nProvider><ToastProvider><DebtSection client={fakeClient([row()])} access={noAccess} /></ToastProvider></I18nProvider>
    );
    await waitFor(() => expect(screen.getByTestId('debt-row')).toBeInTheDocument());
    expect(screen.queryByRole('button', { name: 'Отметить оплаченным' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Отсрочка' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Приостановить' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Заметка' })).not.toBeInTheDocument();
  });

  // Регресс: раньше все четыре кнопки прятались/показывались под одним общим `canManage`,
  // хотя бэкенд проверяет их четырьмя разными правами. Админ только с правом на счета
  // (billing.invoices.manage) не должен видеть «Приостановить» — иначе клик даёт 403.
  it('shows only the action matching the granted right, not the other three', async () => {
    render(
      <I18nProvider><ToastProvider>
        <DebtSection client={fakeClient([row()])} access={{ canMarkPaid: true, canGrantGrace: false, canToggleStatus: false, canAddNote: false }} />
      </ToastProvider></I18nProvider>
    );
    await waitFor(() => expect(screen.getByTestId('debt-row')).toBeInTheDocument());
    expect(screen.getByRole('button', { name: 'Отметить оплаченным' })).toBeVisible();
    expect(screen.queryByRole('button', { name: 'Отсрочка' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Приостановить' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Заметка' })).not.toBeInTheDocument();
  });

  it('does not print a zero amount for a debt that is already settled', async () => {
    render(
      <I18nProvider><ToastProvider>
        <DebtSection client={fakeClient([row({ outstandingMinorUnits: 0, settledButSuspended: true, organizationStatus: 'suspended' })])} access={fullAccess} />
      </ToastProvider></I18nProvider>
    );
    const debtRow = await screen.findByTestId('debt-row');
    expect(debtRow.querySelector('.pc-queue-amount')).toBeNull();
  });

  it('shows the singular count form for a single debtor', async () => {
    render(
      <I18nProvider><ToastProvider><DebtSection client={fakeClient([row()])} access={fullAccess} /></ToastProvider></I18nProvider>
    );
    await waitFor(() => expect(screen.getByTestId('debt-row')).toBeInTheDocument());
    expect(screen.getByText('1 клуб')).toBeInTheDocument();
  });

  it('shows a description even when the only queue rows are settled-but-suspended (no totals to sum)', async () => {
    render(
      <I18nProvider><ToastProvider>
        <DebtSection client={fakeClient([row({ outstandingMinorUnits: 0, settledButSuspended: true, organizationStatus: 'suspended' })])} access={fullAccess} />
      </ToastProvider></I18nProvider>
    );
    await waitFor(() => expect(screen.getByTestId('debt-row')).toBeInTheDocument());
    expect(screen.getByText('Кто просрочил оплату и кто остался отключён после того, как расплатился.')).toBeInTheDocument();
  });
});
