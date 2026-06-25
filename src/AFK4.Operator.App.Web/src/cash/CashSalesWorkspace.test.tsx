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

describe('CashSalesWorkspace', () => {
  it('полные права POS: лента заказов и POS на одном экране, без переключателя', () => {
    renderSales(['pos.sales.create', 'pos.sales.pay', 'pos.sales.refund', 'pos.sales.void']);
    // POS встроен.
    expect(document.querySelector('section.pos-embed')).not.toBeNull();
    expect(screen.getByText('Каталог')).toBeInTheDocument();
    // Лента заказов сверху; сегментов-переключателей больше нет.
    expect(document.querySelector('section.pos-orders-ticker')).not.toBeNull();
    expect(screen.queryByRole('tab', { name: 'Заказы' })).toBeNull();
    expect(screen.queryByRole('tab', { name: 'Касса' })).toBeNull();
  });

  it('только pay (без create): лента заказов скрыта, POS отрисован', () => {
    renderSales(['pos.sales.pay']);
    expect(document.querySelector('section.pos-embed')).not.toBeNull();
    expect(document.querySelector('section.pos-orders-ticker')).toBeNull();
  });
});
