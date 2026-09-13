import { afterEach, describe, expect, it } from 'bun:test';
import { cleanup, render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../operatorToast';
import { CashSalesWorkspace } from './CashSalesWorkspace';

afterEach(cleanup);

// backend=null → POS в fixture-режиме, лента заказов пустая. ToastProvider обязателен:
// PosOrdersTicker дёргает useToast.
function renderSales(permissions: string[]) {
  const session = { permissions, organizationId: 'o' } as never;
  render(
    <I18nProvider initialLocale="ru">
      <ToastProvider>
        <CashSalesWorkspace backend={null} currencyCode="TJS" session={session} />
      </ToastProvider>
    </I18nProvider>
  );
}

const POS_FULL = [
  'organization.pos.sales.create',
  'organization.pos.sales.pay',
  'organization.pos.sales.refund',
  'organization.pos.sales.void'
];

describe('CashSalesWorkspace', () => {
  it('полные права POS и заказов: лента заказов и POS на одном экране, без переключателя', () => {
    renderSales([...POS_FULL, 'organization.shop.orders.manage']);
    // POS встроен.
    expect(document.querySelector('section.pos-embed')).not.toBeNull();
    expect(screen.getByText('Каталог')).toBeInTheDocument();
    // Лента заказов сверху; сегментов-переключателей больше нет.
    expect(document.querySelector('section.pos-orders-ticker')).not.toBeNull();
    expect(screen.queryByRole('tab', { name: 'Заказы' })).toBeNull();
    expect(screen.queryByRole('tab', { name: 'Касса' })).toBeNull();
  });

  // Сервер спрашивает organization.shop.orders.manage на «Принять/Выдать/Отменить», и у роли
  // оператора его нет. Пока лента висела на праве создавать продажу, кассир видел заказы и
  // получал 403 на каждое действие — кнопка обещала, что заказ обслужен.
  it('кассир без права на заказы: лента скрыта, POS отрисован', () => {
    renderSales(['organization.pos.sales.create', 'organization.pos.sales.pay']);
    expect(document.querySelector('section.pos-embed')).not.toBeNull();
    expect(document.querySelector('section.pos-orders-ticker')).toBeNull();
  });

  it('только pay (без create): POS отрисован', () => {
    renderSales(['organization.pos.sales.pay']);
    expect(document.querySelector('section.pos-embed')).not.toBeNull();
    expect(document.querySelector('section.pos-orders-ticker')).toBeNull();
  });
});
