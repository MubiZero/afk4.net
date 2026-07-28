import { describe, expect, it, afterEach, mock } from 'bun:test';
import { render, screen, cleanup, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { CorrectionModal, correctionQuantities } from './CorrectionModal';

afterEach(cleanup);

const renderModal = (over: Partial<Parameters<typeof CorrectionModal>[0]> = {}) => {
  const onSubmit = mock(() => {});
  const onChangeDirection = mock(() => {});
  render(
    <I18nProvider initialLocale="ru">
      <CorrectionModal
        account="wallet"
        direction="credit"
        amount="50.00"
        reason="сверка"
        onChangeAccount={() => {}}
        onChangeDirection={onChangeDirection}
        onChangeAmount={() => {}}
        onChangeReason={() => {}}
        onClose={() => {}}
        onSubmit={onSubmit}
        busy={false}
        {...over}
      />
    </I18nProvider>
  );
  return { onSubmit, onChangeDirection };
};

describe('CorrectionModal', () => {
  it('converts 90 package minutes into quantitySeconds without a money amount', () => {
    expect(correctionQuantities('package_time', 'credit', '90')).toEqual({ minorUnits: 0, quantitySeconds: 5400 });
  });

  it('offers package and bonus time accounts', () => {
    const onChangeAccount = mock(() => {});
    renderModal({ onChangeAccount });
    fireEvent.click(screen.getByRole('button', { name: 'Пакетное время' }));
    expect(onChangeAccount).toHaveBeenCalledWith('package_time');
    expect(screen.getByRole('button', { name: 'Бонусное время' })).toBeInTheDocument();
  });
  it('renders amount and reason fields', () => {
    renderModal();
    expect(screen.getByLabelText('Сумма корректировки')).toBeInTheDocument();
    expect(screen.getByLabelText('Причина')).toBeInTheDocument();
  });

  it('fires onChangeDirection when «Списать» is clicked', () => {
    const { onChangeDirection } = renderModal();
    fireEvent.click(screen.getByRole('button', { name: 'Списать' }));
    expect(onChangeDirection).toHaveBeenCalledWith('debit');
  });

  it('fires onSubmit on form submit', () => {
    const { onSubmit } = renderModal();
    fireEvent.click(screen.getByRole('button', { name: /Применить корректировку/ }));
    expect(onSubmit).toHaveBeenCalled();
  });

  it('disables submit while busy', () => {
    renderModal({ busy: true });
    expect(screen.getByRole('button', { name: /Применить корректировку/ })).toBeDisabled();
  });
});
