import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterAll, afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../operatorToast';

const m = (minorUnits: number) => ({ currencyCode: 'TJS', minorUnits });
const refundSale = mock(async () => ({}));
const voidSale = mock(async () => ({}));
// Смена филиала, в которой пробит чек: без неё отмена своей продажи невозможна по определению.
const getCurrentShift = mock(async () => ({ shiftId: 'shift-open' }));
const getSalesReport = mock(async () => ({
  grossSalesTotal: m(41000),
  refundsTotal: m(3000),
  rows: [{ posSaleId: 's1', state: refundSale.mock.calls.length > 0 ? 'refunded' : 'paid', total: m(1200), createdAtUtc: '2026-06-25T08:00:00Z', lineCount: 1, itemQuantity: 1 }]
}));
const getSale = mock(async () => ({
  posSaleId: 's1', state: 'paid', total: m(1200),
  shiftId: 'shift-open', createdByStaffUserId: 'cashier-1', createdAtUtc: new Date().toISOString(),
  lines: [{ productId: 'p1', productName: 'Cola 0.5', quantity: 1, unitPrice: m(1200), lineTotal: m(1200) }],
  // Именно paymentMethod: раньше фикстура несла `method` и само поле `payments`, которого в
  // PosSaleDto не было вовсе, — тест был зелёным и удостоверял секцию, всегда пустую в проде.
  payments: [{ paymentMethod: 'cash', amount: m(700) }, { paymentMethod: 'card', amount: m(500) }],
  latestReceipt: { receiptId: 'r1', receiptNumber: '1048', total: m(1200) }
}));
const getReceipt = mock(async () => ({ receiptId: 'r1', receiptNumber: '1048', receiptType: 'sale', total: m(1200) }));

const actualHelpers = await import('../operatorHelpers');
mock.module('../operatorHelpers', () => ({
  ...actualHelpers,
  createAuthenticatedOperatorClients: () => ({ shifts: { getSalesReport, getCurrentShift }, pos: { getSale, getReceipt, refundSale, voidSale } })
}));

const { CashReceiptsLedger } = await import('./CashReceiptsLedger');

afterAll(() => {
  mock.module('../operatorHelpers', () => (globalThis as typeof globalThis & {
    __afk4RealOperatorHelpers: typeof import('../operatorHelpers');
  }).__afk4RealOperatorHelpers);
});

const session = {
  staffUserId: 'cashier-1',
  permissions: ['organization.receipts.view', 'organization.pos.sales.refund'],
  organizationId: 'o'
};
const backend = { config: { platformBaseUrl: 'http://test' }, session: { accessToken: 't', ...session }, branchId: 'b1' };

function renderReceipts(overrides: Partial<typeof session> = {}, openReceipt: { receiptId: string } | null = null) {
  const merged = { ...session, ...overrides };
  const mergedBackend = { ...backend, session: { accessToken: 't', ...merged } };
  render(
    <I18nProvider initialLocale="ru">
      <ToastProvider>
        <CashReceiptsLedger
          backend={mergedBackend as never}
          branchId="b1"
          currencyCode="TJS"
          session={merged as never}
          openReceipt={openReceipt}
        />
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
    voidSale.mockClear();
    getCurrentShift.mockClear();
    mock.restore();
  });

  it('показывает чеки смены', async () => {
    renderReceipts();
    expect(await screen.findByText('Оплачен')).toBeInTheDocument();
  });

  // Пустая лента чеков говорила одно слово — «сервер». Теперь — что продаж нет и когда появятся.
  it('пустая лента чеков говорит, что продаж нет и откуда они появятся', async () => {
    getSalesReport.mockImplementationOnce(async () => ({ ...(await getSalesReport()), rows: [] }));
    renderReceipts();
    expect(await screen.findByText('Продаж пока нет')).toBeInTheDocument();
    expect(screen.getByText('Чек появится здесь, как только в кассе проведут продажу.')).toBeInTheDocument();
    expect(screen.queryByText('сервер')).toBeNull();
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

  // Отмены не было в интерфейсе ни для кого: серверный маршрут существовал, клиентский метод
  // был объявлен и не вызывался. Кассир при этом не мог исправить собственную опечатку вовсе —
  // ошибка не исправлялась, а обходилась, и расходилась история продаж.
  it('кассир отменяет свой только что пробитый чек', async () => {
    renderReceipts({ permissions: ['organization.receipts.view', 'organization.pos.sales.void_own_recent'] });
    fireEvent.click(await screen.findByRole('row', { name: /Оплачен/ }));

    const voidButton = await screen.findByRole('button', { name: 'Отменить чек' });
    fireEvent.click(voidButton);

    fireEvent.change(screen.getByLabelText('Почему отменяем'), { target: { value: 'Пробил не тот товар' } });
    fireEvent.click(screen.getByRole('button', { name: 'Подтвердить отмену' }));

    await waitFor(() => expect(voidSale).toHaveBeenCalled());
  });

  // Окно закрывает бытовую опечатку и не открывает возврат задним числом: чужой чек кассиру
  // не отменить, даже свежий.
  it('чужой чек кассиру отменить нечем', async () => {
    getSale.mockImplementationOnce(async () => ({
      posSaleId: 's1', state: 'paid', total: m(1200),
      shiftId: 'shift-open', createdByStaffUserId: 'someone-else', createdAtUtc: new Date().toISOString(),
      lines: [{ productId: 'p1', productName: 'Cola 0.5', quantity: 1, unitPrice: m(1200), lineTotal: m(1200) }],
      payments: [{ paymentMethod: 'cash', amount: m(1200) }],
      latestReceipt: { receiptId: 'r1', receiptNumber: '1048', total: m(1200) }
    }));

    renderReceipts({ permissions: ['organization.receipts.view', 'organization.pos.sales.void_own_recent'] });
    fireEvent.click(await screen.findByRole('row', { name: /Оплачен/ }));
    await screen.findByLabelText('Детали выбранной записи');

    await waitFor(() => expect(getSale).toHaveBeenCalled());
    expect(screen.queryByRole('button', { name: 'Отменить чек' })).toBeNull();
  });

  // Чек из палитры открывается по номеру, а лента показывает только последние 50 продаж смены:
  // с чеком приходят и через неделю, и такой чек не должен упираться в «выберите из списка».
  it('открывает чек, которого нет в ленте смены', async () => {
    getReceipt.mockImplementationOnce(async () => ({
      receiptId: 'r-old', receiptNumber: 'POS-20260601-0003', receiptType: 'sale', posSaleId: 's-old', total: m(4500)
    }));
    getSale.mockImplementationOnce(async () => ({
      posSaleId: 's-old', state: 'paid', total: m(4500),
      shiftId: 'shift-old', createdByStaffUserId: 'cashier-1', createdAtUtc: '2026-06-01T08:00:00Z',
      lines: [{ productId: 'p9', productName: 'Пицца', quantity: 1, unitPrice: m(4500), lineTotal: m(4500) }],
      payments: [{ paymentMethod: 'cash', amount: m(4500) }],
      latestReceipt: { receiptId: 'r-old', receiptNumber: 'POS-20260601-0003', total: m(4500) }
    }));

    renderReceipts({}, { receiptId: 'r-old' });

    const inspector = await screen.findByLabelText('Детали выбранной записи');
    await waitFor(() => expect(inspector).toHaveTextContent('POS-20260601-0003'));
    expect(inspector).toHaveTextContent('Пицца');
    expect((getReceipt.mock.calls[0] as unknown[])?.[0]).toBe('r-old');
  });

  // Возврат по такому чеку должен быть доступен: состояние продажи знает сама продажа, а не
  // строка ленты, которой там нет.
  it('по чеку вне ленты возврат всё равно предлагается', async () => {
    getReceipt.mockImplementationOnce(async () => ({
      receiptId: 'r-old', receiptNumber: 'POS-20260601-0003', receiptType: 'sale', posSaleId: 's-old', total: m(4500)
    }));
    getSale.mockImplementationOnce(async () => ({
      posSaleId: 's-old', state: 'paid', total: m(4500),
      shiftId: 'shift-old', createdByStaffUserId: 'cashier-1', createdAtUtc: '2026-06-01T08:00:00Z',
      lines: [{ productId: 'p9', productName: 'Пицца', quantity: 1, unitPrice: m(4500), lineTotal: m(4500) }],
      payments: [{ paymentMethod: 'cash', amount: m(4500) }],
      latestReceipt: { receiptId: 'r-old', receiptNumber: 'POS-20260601-0003', total: m(4500) }
    }));

    renderReceipts({}, { receiptId: 'r-old' });

    expect(await screen.findByRole('button', { name: /Возврат по чеку/ })).toBeInTheDocument();
  });

  // Чек закрытия сессии продажи не имеет вовсе — это нормальный чек, а не сбой загрузки.
  it('чек без продажи открывается и не притворяется ошибкой', async () => {
    getReceipt.mockImplementationOnce(async () => ({
      receiptId: 'r-session', receiptNumber: 'POS-20260601-0009', receiptType: 'session', sessionId: 'sess-1', total: m(8000)
    }));

    renderReceipts({}, { receiptId: 'r-session' });

    const inspector = await screen.findByLabelText('Детали выбранной записи');
    await waitFor(() => expect(inspector).toHaveTextContent('POS-20260601-0009'));
    expect(inspector).toHaveTextContent('80 с.');
    expect(screen.queryByText('Не удалось загрузить детали чека')).toBeNull();
  });

  // Старший отменяет что угодно: его право шире, и окно к нему не применяется.
  it('старший смены отменяет и чужой чек', async () => {
    getSale.mockImplementationOnce(async () => ({
      posSaleId: 's1', state: 'paid', total: m(1200),
      shiftId: 'shift-old', createdByStaffUserId: 'someone-else', createdAtUtc: '2026-06-25T08:00:00Z',
      lines: [{ productId: 'p1', productName: 'Cola 0.5', quantity: 1, unitPrice: m(1200), lineTotal: m(1200) }],
      payments: [{ paymentMethod: 'cash', amount: m(1200) }],
      latestReceipt: { receiptId: 'r1', receiptNumber: '1048', total: m(1200) }
    }));

    renderReceipts({ permissions: ['organization.receipts.view', 'organization.pos.sales.void'] });
    fireEvent.click(await screen.findByRole('row', { name: /Оплачен/ }));

    expect(await screen.findByRole('button', { name: 'Отменить чек' })).toBeInTheDocument();
  });
});
