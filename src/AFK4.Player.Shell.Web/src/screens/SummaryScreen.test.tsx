import { describe, expect, it, mock } from 'bun:test';
import { fireEvent, render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { devScenarioState } from '../host/devHost';
import { SummaryScreen } from './SummaryScreen';

function renderSummary(refundMinor: number, packageMinutes = 0) {
  const onPlayMore = mock(() => {});
  const onLeave = mock(() => {});
  render(
    <I18nProvider initialLocale="ru">
      <SummaryScreen
        state={devScenarioState('idle')!}
        result={{ billedMinutes: 35, refunded: { currencyCode: 'TJS', minorUnits: refundMinor }, packageMinutesReturned: packageMinutes }}
        onPlayMore={onPlayMore}
        onLeave={onLeave}
      />
    </I18nProvider>
  );
  return { onPlayMore, onLeave };
}

describe('итог раннего выхода', () => {
  it('говорит, сколько сыграно и сколько вернулось', () => {
    renderSummary(1_000, 20);

    expect(screen.getByText('Сыграно 35 мин')).toBeInTheDocument();
    expect(screen.getByText(/Вернули на счёт 10/)).toBeInTheDocument();
    expect(screen.getByText('В пакет вернулось 20 мин')).toBeInTheDocument();
  });

  it('ничего не вернулось — строки о возврате нет, а не «вернули 0»', () => {
    renderSummary(0);

    expect(screen.queryByText(/Вернули/)).toBeNull();
  });

  it('«Играть ещё» и «Выйти» делают своё', () => {
    const { onPlayMore, onLeave } = renderSummary(1_000);

    fireEvent.click(screen.getByRole('button', { name: 'Играть ещё' }));
    fireEvent.click(screen.getByRole('button', { name: 'Выйти' }));

    expect(onPlayMore).toHaveBeenCalledTimes(1);
    expect(onLeave).toHaveBeenCalledTimes(1);
  });
});
