import { afterEach, describe, expect, it } from 'bun:test';
import { cleanup, render, screen, fireEvent, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../operatorToast';
import { CashSalesWorkspace } from './CashSalesWorkspace';

afterEach(cleanup);

// backend=null → POS в fixture-режиме, очередь заказов пустая. ToastProvider обязателен:
// сегмент «Заказы» рендерит ShopOrdersWorkspace, который дёргает useToast.
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
  it('полные права POS: видны оба сегмента, по умолчанию «Касса» (POS)', () => {
    renderSales(['pos.sales.create', 'pos.sales.pay', 'pos.sales.refund', 'pos.sales.void']);
    expect(screen.getByRole('tab', { name: 'Касса' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Заказы' })).toBeInTheDocument();
    // POS-панель активна по умолчанию.
    expect(document.querySelector('section.pos-embed')).not.toBeNull();
    expect(screen.getByText('Каталог')).toBeInTheDocument();
  });

  it('только pay (без create): сегмент «Заказы» скрыт, бар не показан, POS отрисован', () => {
    renderSales(['pos.sales.pay']);
    expect(screen.queryByRole('tab', { name: 'Заказы' })).toBeNull();
    expect(document.querySelector('section.pos-embed')).not.toBeNull();
  });

  it('переключение на «Заказы» рендерит встроенный ShopOrdersWorkspace вместо POS', async () => {
    renderSales(['pos.sales.create', 'pos.sales.pay', 'pos.sales.refund', 'pos.sales.void']);
    fireEvent.click(screen.getByRole('tab', { name: 'Заказы' }));
    await waitFor(() => expect(document.querySelector('section.shop-orders-embed')).not.toBeNull());
    expect(document.querySelector('section.pos-embed')).toBeNull();
  });
});
