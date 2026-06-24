import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { CashMovementModal } from './CashMovementModal';

afterEach(cleanup);

function renderModal(overrides: Partial<Parameters<typeof CashMovementModal>[0]> = {}) {
  const onSubmit = mock(() => {});
  render(
    <I18nProvider initialLocale="ru">
      <CashMovementModal
        movementType="cash_in"
        amount="10.00"
        reason="Размен кассы"
        onChangeAmount={() => {}}
        onChangeReason={() => {}}
        onClose={() => {}}
        onSubmit={onSubmit}
        busy={false}
        {...overrides}
      />
    </I18nProvider>
  );
  return { onSubmit };
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

  it('submit вызывает onSubmit', () => {
    const { onSubmit } = renderModal();
    fireEvent.click(screen.getByRole('button', { name: 'Подтвердить' }));
    expect(onSubmit).toHaveBeenCalledTimes(1);
  });
});
