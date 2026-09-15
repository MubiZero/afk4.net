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
        toleranceMinorUnits={2000}
        signOffCandidates={[{ staffUserId: 'staff-2', displayName: 'Мадина К.' }]}
        signOffStaffUserId=""
        signOffReason=""
        onChangeCounted={() => {}}
        onChangeNote={() => {}}
        onChangeSignOffStaffUserId={() => {}}
        onChangeSignOffReason={() => {}}
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

  /**
   * Расхождение больше допуска филиала не закрывает смену без подписи второго менеджера.
   * Раньше отправить подпись было нечем вовсе: сервер отказывал, и смена не закрывалась никак —
   * ни в этот вечер, ни на следующий день.
   */
  it('сверх допуска требует подпись и держит кнопку закрытой, пока её не выбрали', () => {
    // Ожидается 115.00, насчитали 120.00 — расхождение 500 при допуске 2000: подпись не нужна.
    renderModal();
    expect(screen.queryByLabelText('Кто подписывает')).toBeNull();
    cleanup();

    // Насчитали 150.00 — расхождение 3500, больше допуска.
    renderModal({ counted: '150.00' });
    const picker = screen.getByLabelText('Кто подписывает');
    expect(picker).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Закрыть смену/ })).toBeDisabled();
    expect(screen.getByRole('option', { name: 'Мадина К.' })).toBeInTheDocument();
  });

  it('с выбранным подписантом кнопка снова доступна', () => {
    renderModal({ counted: '150.00', signOffStaffUserId: 'staff-2' });
    expect(screen.getByRole('button', { name: /Закрыть смену/ })).not.toBeDisabled();
  });

  // Допуск ещё не загружен — подпись не навязываем: решает сервер, как и до этой правки.
  it('без известного допуска подпись не требует', () => {
    renderModal({ counted: '150.00', toleranceMinorUnits: null });
    expect(screen.queryByLabelText('Кто подписывает')).toBeNull();
    expect(screen.getByRole('button', { name: /Закрыть смену/ })).not.toBeDisabled();
  });
});
