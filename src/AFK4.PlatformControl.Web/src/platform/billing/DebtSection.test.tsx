import { describe, expect, it, mock } from 'bun:test';
import { render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { DebtSection } from './DebtSection';
import type { DebtRow } from '@/api/types';

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
      <I18nProvider><ToastProvider><DebtSection client={fakeClient([row()])} canManage /></ToastProvider></I18nProvider>
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
      <I18nProvider><ToastProvider><DebtSection client={fakeClient([])} canManage /></ToastProvider></I18nProvider>
    );
    await waitFor(() => expect(screen.getByText('Никто не должен, все клубы включены — хорошая новость.')).toBeInTheDocument());
  });

  it('marks a row under active grace as calm, not alarming', async () => {
    render(
      <I18nProvider><ToastProvider>
        <DebtSection client={fakeClient([row({ graceUntilUtc: '2026-09-01T00:00:00Z' })])} canManage />
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
        <DebtSection client={fakeClient([row({ outstandingMinorUnits: 0, settledButSuspended: true, organizationStatus: 'suspended' })])} canManage />
      </ToastProvider></I18nProvider>
    );
    await waitFor(() => expect(screen.getByTestId('debt-row')).toBeInTheDocument());
    expect(screen.getByTestId('debt-row').textContent).toContain('Долг погашен, клуб отключён');
    // Debt is already settled — a reminder to reactivate isn't an alarm either.
    expect(screen.getByTestId('debt-stage-badge').className).not.toContain('is-danger');
  });

  it('hides row actions when canManage is false', async () => {
    render(
      <I18nProvider><ToastProvider><DebtSection client={fakeClient([row()])} canManage={false} /></ToastProvider></I18nProvider>
    );
    await waitFor(() => expect(screen.getByTestId('debt-row')).toBeInTheDocument());
    expect(screen.queryByRole('button', { name: 'Отметить оплаченным' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Дать отсрочку' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Приостановить' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Заметка' })).not.toBeInTheDocument();
  });

  it('shows the singular count form for a single debtor', async () => {
    render(
      <I18nProvider><ToastProvider><DebtSection client={fakeClient([row()])} canManage /></ToastProvider></I18nProvider>
    );
    await waitFor(() => expect(screen.getByTestId('debt-row')).toBeInTheDocument());
    expect(screen.getByText('1 клуб')).toBeInTheDocument();
  });
});
