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
  it('renders the pay-debt title and amount/reason fields', () => {
    renderModal();
    expect(screen.getByText('Погасить долг')).toBeInTheDocument();
    expect(screen.getByLabelText('Сумма долга')).toBeInTheDocument();
    expect(screen.getByLabelText('Причина долга')).toBeInTheDocument();
  });

  it('fires onSubmit when the form is submitted', () => {
    const onSubmit = mock(() => {});
    renderModal({ onSubmit });
    fireEvent.click(screen.getByRole('button', { name: 'Списать долг' }));
    expect(onSubmit).toHaveBeenCalled();
  });

  it('disables the submit button while busy', () => {
    renderModal({ busy: true });
    expect(screen.getByRole('button', { name: 'Списать долг' })).toBeDisabled();
  });
});
