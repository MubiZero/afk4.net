import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { CloseShiftModal } from './CloseShiftModal';

afterEach(cleanup);

function renderModal(overrides: Partial<Parameters<typeof CloseShiftModal>[0]> = {}) {
  const onSubmit = mock(() => {});
  render(
    <I18nProvider initialLocale="ru">
      <CloseShiftModal
        expectedCash={{ currencyCode: 'TJS', minorUnits: 11500 }}
        counted="120.00"
        note="Закрытие смены"
        currencyCode="TJS"
        onChangeCounted={() => {}}
        onChangeNote={() => {}}
        onClose={() => {}}
        onSubmit={onSubmit}
        busy={false}
        {...overrides}
      />
    </I18nProvider>
  );
  return { onSubmit };
}

describe('CloseShiftModal', () => {
  it('показывает ожидаемую сумму', () => {
    renderModal();
    expect(screen.getByText('Ожидается')).toBeInTheDocument();
    // formatMoney(11500 minor TJS) → '115 с.' (целые числа без дробной части, символ с.)
    expect(screen.getByText('115 с.')).toBeInTheDocument();
  });

  it('считает расхождение факт − ожидается (120 − 115 = +5 с.)', () => {
    renderModal();
    // 12000 − 11500 = 500 minor = 5 major → '5 с.'
    expect(screen.getByText('5 с.')).toBeInTheDocument();
  });

  it('submit вызывает onSubmit', () => {
    const { onSubmit } = renderModal();
    fireEvent.click(screen.getByRole('button', { name: 'Закрыть смену' }));
    expect(onSubmit).toHaveBeenCalledTimes(1);
  });

  it('при counted="" расхождение показывает «—» (пустой/невалидный ввод)', () => {
    renderModal({ counted: '' });
    expect(screen.getByText('—')).toBeInTheDocument();
  });

  it('при counted="0" расхождение = 0 − 115 = −115 с. (formatMoney даёт «-115 с.»)', () => {
    renderModal({ counted: '0' });
    // -11500 minor TJS → '-115 с.' (ASCII дефис, без дробной части)
    expect(screen.getByText('-115 с.')).toBeInTheDocument();
  });
});
