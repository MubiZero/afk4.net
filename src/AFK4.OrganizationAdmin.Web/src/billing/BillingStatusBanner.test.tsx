import { render, screen } from '@testing-library/react';
import { it, expect } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { BillingStatusBanner } from './BillingStatusBanner';
import type { OrganizationBillingStatusDto } from '../operatorApiClients';

function status(overrides: Partial<OrganizationBillingStatusDto> = {}): OrganizationBillingStatusDto {
  return {
    inArrears: true,
    outstandingMinorUnits: 450000,
    currencyCode: 'TJS',
    oldestOverdueInvoiceNumber: 42,
    daysOverdue: 5,
    graceUntilUtc: null,
    ...overrides
  };
}

it('показывает номер счёта, сумму и дни просрочки', () => {
  render(
    <I18nProvider>
      <BillingStatusBanner status={status()} />
    </I18nProvider>
  );

  expect(screen.getByText(/№42/)).toBeDefined();
  expect(screen.getByText(/4 500 с\./)).toBeDefined();
  expect(screen.getByText(/5 дней/)).toBeDefined();
});

it('не группирует разряды большого номера счёта', () => {
  render(
    <I18nProvider>
      <BillingStatusBanner status={status({ oldestOverdueInvoiceNumber: 12345 })} />
    </I18nProvider>
  );

  expect(screen.getByText(/№12345/)).toBeDefined();
  expect(screen.queryByText(/№12 345/)).toBeNull();
});

it('при действующей отсрочке показывает спокойный текст с датой вместо тревоги', () => {
  render(
    <I18nProvider>
      <BillingStatusBanner status={status({ graceUntilUtc: '2026-09-01T00:00:00Z' })} />
    </I18nProvider>
  );

  expect(screen.getByText('Отсрочка платежа')).toBeDefined();
  expect(screen.queryByText(/№42/)).toBeNull();
  expect(screen.getByText(/Оплата отсрочена до/)).toBeDefined();
});

it('не рендерится, когда долга нет', () => {
  const { container } = render(
    <I18nProvider>
      <BillingStatusBanner status={status({ inArrears: false })} />
    </I18nProvider>
  );

  expect(container.textContent).toBe('');
});
