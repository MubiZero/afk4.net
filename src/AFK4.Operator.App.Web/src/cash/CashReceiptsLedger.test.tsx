import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterAll, afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../operatorToast';

const m = (minorUnits: number) => ({ currencyCode: 'TJS', minorUnits });
const refundSale = mock(async () => ({}));
const getSalesReport = mock(async () => ({
  grossSalesTotal: m(41000),
  refundsTotal: m(3000),
  rows: [{ posSaleId: 's1', state: refundSale.mock.calls.length > 0 ? 'refunded' : 'paid', total: m(1200), createdAtUtc: '2026-06-25T08:00:00Z', lineCount: 1, itemQuantity: 1 }]
}));
const getSale = mock(async () => ({
  posSaleId: 's1', state: 'paid', total: m(1200),
  lines: [{ productId: 'p1', productName: 'Cola 0.5', quantity: 1, unitPrice: m(1200), lineTotal: m(1200) }],
  payments: [{ method: 'cash', amount: m(700) }, { method: 'card', amount: m(500) }],
  latestReceipt: { receiptId: 'r1', receiptNumber: '1048', total: m(1200) }
}));
const getReceipt = mock(async () => ({ receiptId: 'r1', receiptNumber: '1048', receiptType: 'sale', total: m(1200) }));

const actualHelpers = await import('../operatorHelpers');
mock.module('../operatorHelpers', () => ({
  ...actualHelpers,
  createAuthenticatedOperatorClients: () => ({ shifts: { getSalesReport }, pos: { getSale, getReceipt, refundSale } })
}));

const { CashReceiptsLedger } = await import('./CashReceiptsLedger');

afterAll(() => {
  mock.module('../operatorHelpers', () => (globalThis as typeof globalThis & {
    __afk4RealOperatorHelpers: typeof import('../operatorHelpers');
  }).__afk4RealOperatorHelpers);
});

const session = { permissions: ['organization.receipts.view', 'organization.pos.sales.refund'], organizationId: 'o' };
const backend = { config: { platformBaseUrl: 'http://test' }, session: { accessToken: 't', ...session }, branchId: 'b1' };

function renderReceipts() {
  render(
    <I18nProvider initialLocale="ru">
      <ToastProvider>
        <CashReceiptsLedger backend={backend as never} branchId="b1" currencyCode="TJS" session={session as never} />
      </ToastProvider>
    </I18nProvider>
  );
}

describe('CashReceiptsLedger', () => {
  afterEach(() => {
    cleanup();
    getSalesReport.mockClear();
    getSale.mockClear();
    getReceipt.mockClear();
    refundSale.mockClear();
    mock.restore();
  });

  it('показывает чеки смены', async () => {
    renderReceipts();
    expect(await screen.findByText('Оплачен')).toBeInTheDocument();
  });

  it('показывает строки чека и смешанную оплату в инспекторе', async () => {
    renderReceipts();
    fireEvent.click(await screen.findByRole('row', { name: /Оплачен/ }));
    const inspector = await screen.findByLabelText('Детали выбранной записи');
    await waitFor(() => expect(inspector).toHaveTextContent('Cola 0.5'));
    expect(inspector).toHaveTextContent('Наличные');
    expect(inspector).toHaveTextContent('Карта');
    expect(inspector).toHaveTextContent('12 с.');
  });

  it('при сбое детали показывает повтор, а не ложный ноль', async () => {
    getSale.mockRejectedValueOnce(new Error('detail failed'));
    renderReceipts();
    fireEvent.click(await screen.findByRole('row', { name: /Оплачен/ }));
    expect(await screen.findByText('Не удалось загрузить детали чека')).toBeInTheDocument();
    expect(screen.queryByText(/^0 с\.$/)).toBeNull();
  });

  it('возврат выбранного чека шлёт refundSale', async () => {
    renderReceipts();
    fireEvent.click(await screen.findByText('Оплачен')); // выбрать чек → загрузить деталь
    fireEvent.click(await screen.findByRole('button', { name: /Возврат по чеку/ }));
    const reportLoadsBeforeRefund = getSalesReport.mock.calls.length;
    fireEvent.click(await screen.findByRole('button', { name: 'Подтвердить возврат' }));
    await waitFor(() => expect(refundSale).toHaveBeenCalled());
    expect((refundSale.mock.calls[0] as unknown[])?.[0]).toBe('s1');
    await waitFor(() => expect(getSalesReport.mock.calls.length).toBeGreaterThan(reportLoadsBeforeRefund));
    expect(await screen.findByText('Возврат')).toBeInTheDocument();
  });
});
