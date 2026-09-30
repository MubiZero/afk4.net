import { describe, expect, it, afterEach, mock } from 'bun:test';
import { render, screen, cleanup, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { WalletZone } from './WalletZone';

afterEach(cleanup);

const renderZone = (over: Partial<Parameters<typeof WalletZone>[0]> = {}) =>
  render(
    <I18nProvider initialLocale="ru">
      <WalletZone topUpAmount="" onChangeTopUpAmount={() => {}} onTopUp={() => {}} {...over} />
    </I18nProvider>
  );

// Форма пополнения — только поле и главная кнопка. Без права её не рисуют вовсе (решает
// ClientDrawer), поэтому гасить и объяснять здесь нечего.
describe('WalletZone', () => {
  it('exposes the amount field labelled "Сумма пополнения"', () => {
    renderZone();
    expect(screen.getByLabelText('Сумма пополнения')).toBeInTheDocument();
  });

  it('passes typed amount up', () => {
    const onChangeTopUpAmount = mock(() => {});
    renderZone({ onChangeTopUpAmount });
    fireEvent.change(screen.getByLabelText('Сумма пополнения'), { target: { value: '50' } });
    expect(onChangeTopUpAmount).toHaveBeenCalledWith('50');
  });

  it('fires onTopUp from the button and from Enter in the field', () => {
    const onTopUp = mock(() => {});
    renderZone({ onTopUp });
    fireEvent.click(screen.getByRole('button', { name: 'Пополнить баланс' }));
    fireEvent.submit(screen.getByLabelText('Сумма пополнения').closest('form')!);
    expect(onTopUp).toHaveBeenCalledTimes(2);
  });

  // приёмка 30.09.2026: при закрытой смене кнопка была активна и молчала до нажатия.
  it('говорит причину рядом с погашенной кнопкой и не пополняет по Enter', () => {
    const onTopUp = mock(() => {});
    renderZone({ onTopUp, blockedReason: 'Откройте смену.' });
    const button = screen.getByRole('button', { name: 'Пополнить баланс' });
    expect(button).toBeDisabled();
    expect(screen.getByText('Откройте смену.')).toBeInTheDocument();
    expect(button).toHaveAccessibleDescription('Откройте смену.');
    fireEvent.submit(screen.getByLabelText('Сумма пополнения').closest('form')!);
    expect(onTopUp).not.toHaveBeenCalled();
  });
});
