import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, render, screen, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ShiftReportModal } from './ShiftReportModal';
import type { ShiftReportData } from './shiftReport';

afterEach(cleanup);
const m = (minorUnits: number) => ({ currencyCode: 'TJS', minorUnits });
const data: ShiftReportData = {
  openedAtUtc: '2026-06-24T08:00:00Z', closedAtUtc: null,
  earned: { time: m(80000), goods: m(41000), noShow: m(2000), total: m(123000) },
  inflow: { cash: m(90000), nonCash: m(33000), walletTopUps: m(15000) },
  cash: { starting: m(100000), expected: m(190000), counted: null, difference: null }
};

function renderModal(variant: 'x' | 'z', onPrint = mock(() => {})) {
  render(
    <I18nProvider initialLocale="ru">
      <ShiftReportModal variant={variant} data={data} currencyCode="TJS" onClose={() => {}} onPrint={onPrint} />
    </I18nProvider>
  );
  return onPrint;
}

describe('ShiftReportModal', () => {
  it('X-вариант: заголовок X-отчёт + выручка + сверка', () => {
    renderModal('x');
    expect(screen.getByText('X-отчёт')).toBeInTheDocument();
    expect(screen.getByText('Выручка смены')).toBeInTheDocument();
    expect(screen.getByText('Сверка кассы')).toBeInTheDocument();
    // 123000 minor units → 1230 major → "1 230 с." (NBSP thousands separator)
    expect(screen.getByText(/230\s*с\./)).toBeInTheDocument();
  });

  it('counted=null → «Смена не закрыта», не «0 с.»', () => {
    renderModal('x');
    expect(screen.getAllByText('Смена не закрыта').length).toBeGreaterThanOrEqual(2);
  });

  it('кнопка «Печать» зовёт onPrint', () => {
    const onPrint = renderModal('z');
    fireEvent.click(screen.getByRole('button', { name: 'Печать' }));
    expect(onPrint).toHaveBeenCalledTimes(1);
  });
});
