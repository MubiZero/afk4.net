import { describe, expect, it, afterEach, mock } from 'bun:test';
import { render, screen, cleanup, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { PayDebtModal } from './PayDebtModal';

afterEach(cleanup);

const base = {
  amount: '',
  reason: '',
  onChangeAmount: () => {},
  onChangeReason: () => {},
  onClose: () => {},
  onSubmit: () => {},
  busy: false,
};

const renderModal = (over: Partial<typeof base> = {}) =>
  render(<I18nProvider initialLocale="ru"><PayDebtModal {...base} {...over} /></I18nProvider>);

describe('PayDebtModal', () => {
  // Заголовок и кнопка — одно слово: окно называется тем, что делает его главная кнопка.
  it('renders the pay-debt title and amount/reason fields', () => {
    renderModal();
    expect(screen.getByRole('dialog', { name: 'Оплатить долг' })).toBeInTheDocument();
    expect(screen.getByLabelText('Сумма долга')).toBeInTheDocument();
    expect(screen.getByLabelText('Причина долга')).toBeInTheDocument();
  });

  it('fires onSubmit when the form is submitted', () => {
    const onSubmit = mock(() => {});
    renderModal({ onSubmit });
    fireEvent.click(screen.getByRole('button', { name: 'Оплатить долг' }));
    expect(onSubmit).toHaveBeenCalled();
  });

  // Приём денег — главная кнопка, а не красная: опасным рисуется только необратимое.
  it('pays with the primary button, not a danger one', () => {
    renderModal();
    expect(screen.getByRole('button', { name: 'Оплатить долг' })).toHaveClass('ui-btn--primary');
    expect(document.querySelector('.ui-btn--danger')).toBeNull();
  });

  it('disables the submit button while busy', () => {
    renderModal({ busy: true });
    expect(screen.getByRole('button', { name: 'Оплатить долг' })).toBeDisabled();
  });
});
