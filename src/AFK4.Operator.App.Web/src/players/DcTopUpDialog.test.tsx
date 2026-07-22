import { describe, it, expect, mock, afterEach } from 'bun:test';
import { render, screen, fireEvent, cleanup, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../operatorToast';
import { DcTopUpDialog, type DcTopUpDialogBackend } from './DcTopUpDialog';

afterEach(cleanup);

// Мини-помощник, собирающий `backend`-проп диалога: он несёт только `dcTopUps` (нужный
// поддомен клиента), а не весь auth-контекст — так тест не завязан на createAuthenticatedOperatorClients
// и не трогает process-wide mock.module.
function makeBackend(overrides: Partial<DcTopUpDialogBackend['dcTopUps']>): DcTopUpDialogBackend {
  return {
    dcTopUps: {
      create: overrides.create ?? mock(async () => { throw new Error('dcTopUps.create not mocked'); }),
      cancel: overrides.cancel ?? mock(async () => {}),
      confirm: overrides.confirm ?? mock(async () => ({}))
    }
  };
}

const view = (backend: DcTopUpDialogBackend, over?: Partial<Parameters<typeof DcTopUpDialog>[0]>) =>
  render(
    <I18nProvider initialLocale="ru">
      <ToastProvider>
        <DcTopUpDialog
          backend={backend}
          branchId="b1"
          playerAccountId="p1"
          onClose={() => {}}
          onCredited={() => {}}
          {...over}
        />
      </ToastProvider>
    </I18nProvider>
  );

describe('DcTopUpDialog', () => {
  it('создаёт намерение, рендерит QR, подтверждает', async () => {
    const create = mock(async () => ({ intentId: 'i1', payUrl: 'http://pay.dc.tj/?A=1&s=50.00&c=AFK4-abc&f1=133', comment: 'AFK4-abc', amountMinorUnits: 5000, currencyCode: 'TJS', cardLast4: '3456' }));
    const confirm = mock(async () => ({}));
    const backend = makeBackend({ create, cancel: mock(async () => {}), confirm });
    view(backend);
    fireEvent.change(screen.getByLabelText(/сумм/i), { target: { value: '50' } });
    fireEvent.click(screen.getByRole('button', { name: /показать qr|создать/i }));
    await waitFor(() => expect(create).toHaveBeenCalled());
    await screen.findByRole('img'); // QR отрисован
    fireEvent.click(screen.getByRole('button', { name: /оплата получена/i }));
    await waitFor(() => expect(confirm).toHaveBeenCalledWith('i1'));
  });

  it('передаёт минорные единицы (major*100) и код валюты в create', async () => {
    const create = mock(async () => ({ intentId: 'i2', payUrl: 'http://pay.dc.tj/?A=1', comment: 'AFK4-xyz', amountMinorUnits: 12345, currencyCode: 'TJS', cardLast4: '9999' }));
    const backend = makeBackend({ create });
    view(backend);
    fireEvent.change(screen.getByLabelText(/сумм/i), { target: { value: '123.45' } });
    fireEvent.click(screen.getByRole('button', { name: /показать qr|создать/i }));
    await waitFor(() => expect(create).toHaveBeenCalledWith('b1', { playerAccountId: 'p1', amountMinorUnits: 12345, currencyCode: 'TJS' }));
  });

  it('«Отмена» на экране QR отменяет намерение и закрывает диалог', async () => {
    const create = mock(async () => ({ intentId: 'i3', payUrl: 'http://pay.dc.tj/?A=1', comment: 'AFK4-zzz', amountMinorUnits: 5000, currencyCode: 'TJS', cardLast4: '1111' }));
    const cancel = mock(async () => {});
    const onClose = mock(() => {});
    const backend = makeBackend({ create, cancel });
    view(backend, { onClose });
    fireEvent.change(screen.getByLabelText(/сумм/i), { target: { value: '50' } });
    fireEvent.click(screen.getByRole('button', { name: /показать qr|создать/i }));
    await screen.findByRole('img');
    // Header X и footer «Отмена» ведут на один и тот же аккессибл-нейм («Отмена») — берём
    // последний (footer, наглядная парная кнопка рядом с «Оплата получена»).
    const cancelButtons = screen.getAllByRole('button', { name: /отмена/i });
    fireEvent.click(cancelButtons[cancelButtons.length - 1]);
    await waitFor(() => expect(cancel).toHaveBeenCalledWith('b1', 'i3'));
    await waitFor(() => expect(onClose).toHaveBeenCalled());
  });

  it('показывает тост с ошибкой, если подтверждение упало (например, смена не открыта), и не падает', async () => {
    const create = mock(async () => ({ intentId: 'i4', payUrl: 'http://pay.dc.tj/?A=1', comment: 'AFK4-www', amountMinorUnits: 5000, currencyCode: 'TJS', cardLast4: '2222' }));
    const confirm = mock(async () => { throw new Error('Смена не открыта'); });
    const onCredited = mock(() => {});
    const backend = makeBackend({ create, confirm });
    view(backend, { onCredited });
    fireEvent.change(screen.getByLabelText(/сумм/i), { target: { value: '50' } });
    fireEvent.click(screen.getByRole('button', { name: /показать qr|создать/i }));
    await screen.findByRole('img');
    fireEvent.click(screen.getByRole('button', { name: /оплата получена/i }));
    await waitFor(() => expect(confirm).toHaveBeenCalled());
    expect(await screen.findByText('Смена не открыта')).toBeInTheDocument();
    expect(onCredited).not.toHaveBeenCalled();
    // Диалог остаётся на экране QR — оператор может закрыть смену/повторить, кнопка всё ещё там.
    expect(screen.getByRole('button', { name: /оплата получена/i })).toBeInTheDocument();
  });
});
