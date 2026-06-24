import { afterAll, afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';

// Снимок реальных модулей ДО регистрации моков — восстанавливаем в afterAll, иначе mock.module
// протечёт process-wide и сломает ShopOrdersWorkspace.test / ShiftsWorkspace.test в subdir-прогоне.
const real = {
  pos: await import('../BackendPosWorkspace'),
  orders: await import('../ShopOrdersWorkspace'),
  payments: await import('../BackendPaymentsWorkspace'),
  shifts: await import('../ShiftsWorkspace'),
  review: await import('../ReviewWorkspace'),
  header: await import('./CashShiftHeader')
};

mock.module('../BackendPosWorkspace', () => ({ BackendPosWorkspace: () => <div>POS_PANE</div> }));
mock.module('../ShopOrdersWorkspace', () => ({ ShopOrdersWorkspace: () => <div>ORDERS_PANE</div> }));
mock.module('../BackendPaymentsWorkspace', () => ({ BackendPaymentsWorkspace: () => <div>PAYMENTS_PANE</div> }));
mock.module('../ShiftsWorkspace', () => ({ ShiftsWorkspace: () => <div>SHIFTS_PANE</div> }));
mock.module('../ReviewWorkspace', () => ({ ReviewWorkspace: () => <div>REVIEW_PANE</div> }));
mock.module('./CashShiftHeader', () => ({ CashShiftHeader: () => <div>HEADER</div> }));

const { CashWorkspace } = await import('./CashWorkspace');

afterAll(() => {
  mock.module('../BackendPosWorkspace', () => real.pos);
  mock.module('../ShopOrdersWorkspace', () => real.orders);
  mock.module('../BackendPaymentsWorkspace', () => real.payments);
  mock.module('../ShiftsWorkspace', () => real.shifts);
  mock.module('../ReviewWorkspace', () => real.review);
  mock.module('./CashShiftHeader', () => real.header);
});
afterEach(cleanup);

const backend = { config: { platformBaseUrl: 'x' }, session: { accessToken: 't' }, branchId: 'b1' } as never;

function renderWorkspace() {
  return render(
    <I18nProvider locale="ru">
      <CashWorkspace backend={backend} currencyCode="TJS" />
    </I18nProvider>
  );
}

describe('CashWorkspace', () => {
  it('по умолчанию открыта вкладка Продажи (POS) + есть шапка-якорь', () => {
    renderWorkspace();
    expect(screen.getByText('HEADER')).toBeInTheDocument();
    expect(screen.getByText('POS_PANE')).toBeInTheDocument();
    expect(screen.queryByText('REVIEW_PANE')).not.toBeInTheDocument();
  });

  it('клик по вкладке Проверка показывает ReviewWorkspace', () => {
    renderWorkspace();
    fireEvent.click(screen.getByRole('tab', { name: 'Проверка' }));
    expect(screen.getByText('REVIEW_PANE')).toBeInTheDocument();
    expect(screen.queryByText('POS_PANE')).not.toBeInTheDocument();
  });
});
