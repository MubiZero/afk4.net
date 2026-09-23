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
  // осталась на журнале.
  it('переключает кассу на продажи, где лента заказов', () => {
    const view = render(renderCash(null));
    fireEvent.click(screen.getByRole('tab', { name: 'Журнал кассы' }));
    expect(document.querySelector('section.pos-orders-ticker')).toBeNull();

    view.rerender(renderCash({ orderId: 'o1' }));

    expect(document.querySelector('section.pos-orders-ticker')).not.toBeNull();
  });
});
