import { afterEach, describe, expect, it } from 'bun:test';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../operatorToast';
import { CashWorkspace } from './CashWorkspace';

afterEach(cleanup);

const permissions = [
  'organization.pos.sales.create',
  'organization.shop.orders.serve',
  'organization.receipts.view'
];

function renderCash(openOrder: { orderId: string } | null) {
  const session = { permissions, organizationId: 'o' } as never;
  return (
    <I18nProvider initialLocale="ru">
      <ToastProvider>
        <CashWorkspace backend={null} currencyCode="TJS" session={session} openOrder={openOrder} />
      </ToastProvider>
    </I18nProvider>
  );
}

describe('CashWorkspace · заказ из палитры', () => {
  // Лента заказов живёт во вкладке «Продажи»: заказ из палитры открывается там, даже если касса
  // осталась на чеках.
  it('переключает кассу на продажи, где лента заказов', () => {
    const view = render(renderCash(null));
    fireEvent.click(screen.getByRole('tab', { name: 'Чеки' }));
    expect(document.querySelector('section.pos-orders-ticker')).toBeNull();

    view.rerender(renderCash({ orderId: 'o1' }));

    expect(document.querySelector('section.pos-orders-ticker')).not.toBeNull();
  });
});

// Журнал кассы был вкладкой с вкладками, а в «Согласованиях» — ещё вкладки: три уровня. Теперь
// операции, чеки и согласования — вкладки самой кассы, и глубже одного уровня внутри нет.
describe('CashWorkspace · вкладки одним уровнем', () => {
  it('операции, чеки и согласования — вкладки кассы, у каждой свои права', () => {
    const session = { permissions: ['organization.reports.view', 'organization.receipts.view', 'organization.billing.money_action.approve'], organizationId: 'o' } as never;
    render(
      <I18nProvider initialLocale="ru">
        <ToastProvider>
          <CashWorkspace backend={null} currencyCode="TJS" session={session} />
        </ToastProvider>
      </I18nProvider>
    );
    expect(screen.getAllByRole('tab').map((tab) => tab.textContent)).toEqual(['Смена', 'Кассовые операции', 'Чеки', 'Согласования']);
    expect(screen.queryByRole('tab', { name: 'Журнал кассы' })).toBeNull();
    fireEvent.click(screen.getByRole('tab', { name: 'Согласования' }));
    // Два ряда вкладок: касса и очередь согласований. Третьего нет.
    expect(screen.getAllByRole('tablist')).toHaveLength(2);
  });

  it('чек из палитры открывает вкладку «Чеки»', () => {
    const session = { permissions: ['organization.reports.view', 'organization.receipts.view'], organizationId: 'o' } as never;
    const ui = (openReceipt: { receiptId: string } | null) => (
      <I18nProvider initialLocale="ru">
        <ToastProvider>
          <CashWorkspace backend={null} currencyCode="TJS" session={session} openReceipt={openReceipt} />
        </ToastProvider>
      </I18nProvider>
    );
    const view = render(ui(null));
    expect(screen.getByRole('tab', { name: 'Смена' })).toHaveAttribute('aria-selected', 'true');
    view.rerender(ui({ receiptId: 'r1' }));
    expect(screen.getByRole('tab', { name: 'Чеки' })).toHaveAttribute('aria-selected', 'true');
  });
});
