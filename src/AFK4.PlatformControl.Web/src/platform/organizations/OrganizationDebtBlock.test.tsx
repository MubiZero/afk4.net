import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import type { DebtRow } from '@/api/types';
import { OrganizationDebtBlock } from './OrganizationDebtBlock';

function row(overrides: Partial<DebtRow> = {}): DebtRow {
  return {
    organizationId: 'o1',
    organizationName: 'Арена',
    organizationSlug: 'arena',
    organizationStatus: 'active',
    subscriptionStatus: 'past_due',
    outstandingMinorUnits: 290000,
    currencyCode: 'TJS',
    oldestOverdueInvoiceNumber: 42,
    oldestOverdueInvoiceId: 'i1',
    daysOverdue: 10,
    dunningStage: 3,
    graceUntilUtc: null,
    settledButSuspended: false,
    ...overrides
  };
}

describe('OrganizationDebtBlock', () => {
  it('shows the amount, invoice number and days overdue', () => {
    render(<I18nProvider><OrganizationDebtBlock row={row()} /></I18nProvider>);
    const block = screen.getByTestId('passport-debt');
    // outstandingMinorUnits: 290000 must render as MAJOR units (2900), not 290000.
    const digits = (block.textContent ?? '').replace(/\D/g, '');
    expect(digits).toContain('2900');
    expect(digits).not.toContain('290000');
    expect(block.textContent).toContain('42');
    expect(block.textContent).toContain('10');
  });

  it('shows a calm state without an alarming badge when there is no debt', () => {
    render(<I18nProvider><OrganizationDebtBlock row={null} /></I18nProvider>);
    const block = screen.getByTestId('passport-debt');
    expect(block.textContent).toContain('Долгов нет');
    expect(block.querySelector('.is-danger')).toBeNull();
  });

  it('shows the grace period end date instead of an overdue alarm', () => {
    render(<I18nProvider><OrganizationDebtBlock row={row({ graceUntilUtc: '2026-09-01T00:00:00Z' })} /></I18nProvider>);
    const block = screen.getByTestId('passport-debt');
    expect(block.textContent).toContain('Отсрочка до');
    expect(screen.getByTestId('passport-debt-stage').className).not.toContain('is-danger');
  });

  // A staffer without platform.billing.view (or a request that failed over a flaky network) must
  // never see the confident "Долгов нет" — that reads as a verified fact, but we simply never
  // asked. `status="unknown"` renders a distinct calm-but-honest state instead.
  it('shows an unknown state instead of claiming there is no debt when the caller never asked', () => {
    render(<I18nProvider><OrganizationDebtBlock row={null} status="unknown" /></I18nProvider>);
    const block = screen.getByTestId('passport-debt');
    expect(block.textContent).toContain('Неизвестно');
    expect(block.textContent).not.toContain('Долгов нет');
  });

  it('renders a large invoice number as plain text, not grouped by thousands', () => {
    render(<I18nProvider><OrganizationDebtBlock row={row({ oldestOverdueInvoiceNumber: 12345 })} /></I18nProvider>);
    const block = screen.getByTestId('passport-debt');
    expect(block.textContent).toContain('№12345');
    expect(block.textContent).not.toContain('12 345');
  });
});
