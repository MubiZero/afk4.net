import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { CashMovementModal } from './CashMovementModal';

afterEach(cleanup);

function renderModal(overrides: Partial<Parameters<typeof CashMovementModal>[0]> = {}) {
  const onSubmit = mock(() => {});
  const onClose = mock(() => {});
  render(
    <I18nProvider initialLocale="ru">
      <CashMovementModal
        movementType="cash_in"
        amount="10.00"
        reason="Размен кассы"
        onChangeAmount={() => {}}
        onChangeReason={() => {}}
        onClose={onClose}
        onSubmit={onSubmit}
        busy={false}
        {...overrides}
      />
    </I18nProvider>
  );
  return { onSubmit, onClose };
}

describe('CashMovementModal', () => {
  it('тип cash_in → заголовок «Внесение наличных»', () => {
    renderModal({ movementType: 'cash_in' });
    expect(screen.getByText('Внесение наличных')).toBeInTheDocument();
  });

  it('тип cash_out → заголовок «Изъятие наличных»', () => {
    renderModal({ movementType: 'cash_out' });
    expect(screen.getByText('Изъятие наличных')).toBeInTheDocument();
  });

  // Кнопка называет действие окна, а не безликое «Подтвердить».
  it('кнопка «Внести» вызывает onSubmit', () => {
    const { onSubmit } = renderModal({ movementType: 'cash_in' });
    fireEvent.click(screen.getByRole('button', { name: 'Внести' }));
    expect(onSubmit).toHaveBeenCalledTimes(1);
  });

  it('«Отмена» закрывает окно, не записывая движение', () => {
    const { onSubmit, onClose } = renderModal({ movementType: 'cash_out' });
    expect(screen.getByRole('button', { name: 'Изъять' })).toBeInTheDocument();
    fireEvent.click(screen.getByText('Отмена', { selector: 'button' }));
    expect(onClose).toHaveBeenCalledTimes(1);
    expect(onSubmit).not.toHaveBeenCalled();
  });
});
