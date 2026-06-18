import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import type { SeatSummary } from '../operatorData';
import type { TimelineAxis, ZoneRowGroup } from './bookingModel';
import { BookingTimeline } from './BookingTimeline';

afterEach(cleanup);

function seat(): SeatSummary {
  return {
    id: 'a3', zone: 'Зал A', name: 'PC-03', tone: 'active', stateLabel: 'В сессии',
    player: 'Гость', remaining: '30 мин', billing: 'Wallet', device: 'Device',
    command: 'Idle', app: 'Shell', deviceId: 'dev-1', activeSessionId: 'sess-1'
  };
}

const SPAN = 12 * 3_600_000; // 12 часов
const axis: TimelineAxis = { startMs: 0, endMs: SPAN, spanMs: SPAN, ticks: [] };
const groups: ZoneRowGroup[] = [{ zone: 'Зал A', rows: [{ seat: seat(), blocks: [] }] }];

function renderTimeline(
  onCellCreate: (seat: SeatSummary, startMs: number, durationMinutes?: number) => void,
  previewBlock: { seatId: string; startMs: number; endMs: number } | null = null
) {
  const result = render(
    <I18nProvider>
      <BookingTimeline
        groups={groups}
        axis={axis}
        nowMs={-1}
        loading={false}
        showSkeleton={false}
        selectedReservationId=""
        branchName="AFK4"
        previewBlock={previewBlock}
        dateLabel="Сегодня"
        onPrevDay={() => {}}
        onNextDay={() => {}}
        onSelectBlock={() => {}}
        onCellCreate={onCellCreate}
      />
    </I18nProvider>
  );
  const track = result.container.querySelector<HTMLElement>('.booking-row-track')!;
  // happy-dom отдаёт нулевую геометрию — подменяем, чтобы clientX → ms считались.
  track.getBoundingClientRect = () => ({ left: 0, top: 0, width: 1000, height: 38, right: 1000, bottom: 38, x: 0, y: 0, toJSON: () => ({}) }) as DOMRect;
  return track;
}

describe('BookingTimeline drag-to-create', () => {
  it('протягивание создаёт бронь с длительностью из диапазона и гасит хвостовой click', () => {
    const onCellCreate = mock((_seat: SeatSummary, _startMs: number, _durationMinutes?: number) => {});
    const track = renderTimeline(onCellCreate);

    fireEvent.mouseDown(track, { button: 0, clientX: 100 });
    fireEvent.mouseMove(document, { clientX: 300 });
    fireEvent.mouseUp(document, { clientX: 300 });
    // Реальный браузер после протягивания шлёт click — он не должен перезаписать диапазон.
    fireEvent.click(track, { clientX: 300 });

    expect(onCellCreate).toHaveBeenCalledTimes(1);
    const [, startMs, durationMinutes] = onCellCreate.mock.calls[0];
    expect(durationMinutes).toBeGreaterThanOrEqual(60); // диапазон, а не клик-дефолт
    expect(startMs).toBeLessThan(300 / 1000 * SPAN); // старт у левого края протягивания
  });

  it('одиночный клик без движения создаёт бронь по умолчанию (без длительности)', () => {
    const onCellCreate = mock((_seat: SeatSummary, _startMs: number, _durationMinutes?: number) => {});
    const track = renderTimeline(onCellCreate);

    fireEvent.mouseDown(track, { button: 0, clientX: 500 });
    fireEvent.mouseUp(document, { clientX: 500 });
    fireEvent.click(track, { clientX: 500 });

    expect(onCellCreate).toHaveBeenCalledTimes(1);
    expect(onCellCreate.mock.calls[0][2]).toBeUndefined();
  });

  it('держит персистентную подсветку выбранного интервала', () => {
    const track = renderTimeline(() => {}, { seatId: 'a3', startMs: 2 * 3_600_000, endMs: 3 * 3_600_000 });
    const preview = track.querySelector('.booking-ghost.preview');
    expect(preview).not.toBeNull();
    expect(preview?.textContent).toContain('–');
  });
});
