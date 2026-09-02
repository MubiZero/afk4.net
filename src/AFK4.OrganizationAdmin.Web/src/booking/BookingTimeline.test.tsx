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
    player: 'Гость', remaining: '30 мин', device: 'Device',
    command: 'Idle', app: 'Shell', deviceId: 'dev-1', activeSessionId: 'sess-1'
  };
}

const SPAN = 12 * 3_600_000; // 12 часов
const axis: TimelineAxis = { startMs: 0, endMs: SPAN, spanMs: SPAN, ticks: [] };
const groups: ZoneRowGroup[] = [{ zone: 'Зал A', rows: [{ seat: seat(), blocks: [], sessions: [] }] }];

function renderTimeline(
  onCellCreate: (seat: SeatSummary, startMs: number, durationMinutes?: number) => void,
  previewBlock: { seatIds: string[]; startMs: number; endMs: number } | null = null,
  onSeatToggle: (seat: SeatSummary, startMs: number) => void = () => {}
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
        previewBlock={previewBlock}
        dateLabel="Сегодня"
        dateValue="2026-06-19"
        isToday={true}
        onPrevDay={() => {}}
        onNextDay={() => {}}
        onToday={() => {}}
        onPickDate={() => {}}
        onSelectBlock={() => {}}
        onCellCreate={onCellCreate}
        onSeatsCreate={() => {}}
        onSeatToggle={onSeatToggle}
      />
    </I18nProvider>
  );
  const track = result.container.querySelector<HTMLElement>('.booking-row-track')!;
  // happy-dom отдаёт нулевую геометрию — подменяем, чтобы clientX → ms считались.
  track.getBoundingClientRect = () => ({ left: 0, top: 0, width: 1000, height: 38, right: 1000, bottom: 38, x: 0, y: 0, toJSON: () => ({}) }) as DOMRect;
  return track;
}

describe('BookingTimeline drag-to-create', () => {
  it('Ctrl и Command клики выбирают произвольные несвободные места в разных залах с общей точкой старта', () => {
    const onCellCreate = mock((_seat: SeatSummary, _startMs: number, _durationMinutes?: number) => {});
    const onSeatsCreate = mock((_seats: SeatSummary[], _startMs: number, _durationMinutes: number) => {});
    const onSeatToggle = mock((_seat: SeatSummary, _startMs: number) => {});
    const seatA: SeatSummary = { ...seat(), id: 'a1', zone: 'Зал A', name: 'PC-01', tone: 'active', stateLabel: 'В сессии' };
    const seatB: SeatSummary = { ...seat(), id: 'b4', zone: 'Зал B', name: 'PC-04', tone: 'service', stateLabel: 'Обслуживание' };
    const crossZoneGroups: ZoneRowGroup[] = [
      { zone: 'Зал A', rows: [{ seat: seatA, blocks: [], sessions: [] }] },
      { zone: 'Зал B', rows: [{ seat: seatB, blocks: [], sessions: [] }] }
    ];
    const result = render(
      <I18nProvider>
        <BookingTimeline
          groups={crossZoneGroups} axis={axis} nowMs={-1} loading={false} showSkeleton={false}
          selectedReservationId="" previewBlock={null} dateLabel="Сегодня"
          dateValue="2026-06-19" isToday={true}
          onPrevDay={() => {}} onNextDay={() => {}} onToday={() => {}} onPickDate={() => {}} onSelectBlock={() => {}}
          onCellCreate={onCellCreate} onSeatsCreate={onSeatsCreate} onSeatToggle={onSeatToggle}
        />
      </I18nProvider>
    );
    const tracks = result.container.querySelectorAll<HTMLElement>('.booking-row-track');
    const rect = (top: number, bottom: number) => () => ({ left: 0, top, width: 1000, height: bottom - top, right: 1000, bottom, x: 0, y: top, toJSON: () => ({}) }) as DOMRect;
    tracks[0].getBoundingClientRect = rect(0, 38);
    tracks[1].getBoundingClientRect = rect(38, 76);
    const expectedStart = 6 * 3_600_000;

    fireEvent.click(tracks[0], { clientX: 500, ctrlKey: true });
    fireEvent.click(tracks[1], { clientX: 500, metaKey: true });

    expect(onSeatToggle).toHaveBeenNthCalledWith(1, expect.objectContaining({ id: 'a1' }), expectedStart);
    expect(onSeatToggle).toHaveBeenNthCalledWith(2, expect.objectContaining({ id: 'b4' }), expectedStart);
    expect(onCellCreate).not.toHaveBeenCalled();
    expect(onSeatsCreate).not.toHaveBeenCalled();
  });

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
    const track = renderTimeline(() => {}, { seatIds: ['a3'], startMs: 2 * 3_600_000, endMs: 3 * 3_600_000 });
    const preview = track.querySelector('.booking-ghost.preview');
    expect(preview).not.toBeNull();
    expect(preview?.textContent).toContain('–');
  });

  it('протягивание по нескольким строкам создаёт массовую бронь (onSeatsCreate)', () => {
    const onSeatsCreate = mock((_seats: SeatSummary[], _start: number, _dur: number) => {});
    const seatA: SeatSummary = { ...seat(), id: 'a1', name: 'PC-01' };
    const seatB: SeatSummary = { ...seat(), id: 'a2', name: 'PC-02' };
    const twoRows: ZoneRowGroup[] = [{ zone: 'Зал A', rows: [
      { seat: seatA, blocks: [], sessions: [] },
      { seat: seatB, blocks: [], sessions: [] }
    ] }];
    const result = render(
      <I18nProvider>
        <BookingTimeline
          groups={twoRows} axis={axis} nowMs={-1} loading={false} showSkeleton={false}
          selectedReservationId="" previewBlock={null} dateLabel="Сегодня"
          dateValue="2026-06-19" isToday={true}
          onPrevDay={() => {}} onNextDay={() => {}} onToday={() => {}} onPickDate={() => {}} onSelectBlock={() => {}}
          onCellCreate={() => {}} onSeatsCreate={onSeatsCreate} onSeatToggle={() => {}}
        />
      </I18nProvider>
    );
    const tracks = result.container.querySelectorAll<HTMLElement>('.booking-row-track');
    const rect = (top: number, bottom: number) => () => ({ left: 0, top, width: 1000, height: bottom - top, right: 1000, bottom, x: 0, y: top, toJSON: () => ({}) }) as DOMRect;
    tracks[0].getBoundingClientRect = rect(0, 38);
    tracks[1].getBoundingClientRect = rect(38, 76);

    fireEvent.mouseDown(tracks[0], { button: 0, clientX: 100, clientY: 10 });
    fireEvent.mouseMove(document, { clientX: 300, clientY: 50 }); // курсор ушёл во вторую строку
    fireEvent.mouseUp(document, { clientX: 300, clientY: 50 });

    expect(onSeatsCreate).toHaveBeenCalledTimes(1);
    const [seats] = onSeatsCreate.mock.calls[0];
    expect(seats.map((s) => s.id)).toEqual(['a1', 'a2']);
  });

  it('кнопка возврата к сегодня видна только не на текущей дате и зовёт onToday', () => {
    const onToday = mock(() => {});
    const props = {
      groups, axis, nowMs: -1, loading: false, showSkeleton: false,
      selectedReservationId: '', previewBlock: null,
      dateLabel: '20 июня', dateValue: '2026-06-20',
      onPrevDay: () => {}, onNextDay: () => {}, onPickDate: () => {},
      onSelectBlock: () => {}, onCellCreate: () => {}, onSeatsCreate: () => {}, onSeatToggle: () => {}
    };
    const today = render(<I18nProvider><BookingTimeline {...props} isToday={true} dateLabel="Сегодня" onToday={onToday} /></I18nProvider>);
    expect(today.container.querySelector('.booking-gutter-today')).toBeNull();
    cleanup();

    const other = render(<I18nProvider><BookingTimeline {...props} isToday={false} onToday={onToday} /></I18nProvider>);
    const back = other.container.querySelector<HTMLButtonElement>('.booking-gutter-today');
    expect(back).not.toBeNull();
    fireEvent.click(back!);
    expect(onToday).toHaveBeenCalledTimes(1);
  });

  it('выбор даты в нативном инпуте зовёт onPickDate с yyyy-mm-dd', () => {
    const onPickDate = mock((_value: string) => {});
    const result = render(
      <I18nProvider>
        <BookingTimeline
          groups={groups} axis={axis} nowMs={-1} loading={false} showSkeleton={false}
          selectedReservationId="" previewBlock={null}
          dateLabel="Сегодня" dateValue="2026-06-19" isToday={true}
          onPrevDay={() => {}} onNextDay={() => {}} onToday={() => {}} onPickDate={onPickDate}
          onSelectBlock={() => {}} onCellCreate={() => {}} onSeatsCreate={() => {}} onSeatToggle={() => {}}
        />
      </I18nProvider>
    );
    const input = result.container.querySelector<HTMLInputElement>('.booking-gutter-date input[type="date"]')!;
    fireEvent.change(input, { target: { value: '2026-07-01' } });
    expect(onPickDate).toHaveBeenCalledWith('2026-07-01');
  });

  it('персистентная подсветка массовой брони покрывает все её строки', () => {
    const seatA: SeatSummary = { ...seat(), id: 'a1', name: 'PC-01' };
    const seatB: SeatSummary = { ...seat(), id: 'a2', name: 'PC-02' };
    const twoRows: ZoneRowGroup[] = [{ zone: 'Зал A', rows: [
      { seat: seatA, blocks: [], sessions: [] },
      { seat: seatB, blocks: [], sessions: [] }
    ] }];
    const result = render(
      <I18nProvider>
        <BookingTimeline
          groups={twoRows} axis={axis} nowMs={-1} loading={false} showSkeleton={false}
          selectedReservationId=""
          previewBlock={{ seatIds: ['a1', 'a2'], startMs: 2 * 3_600_000, endMs: 3 * 3_600_000 }}
          dateLabel="Сегодня" dateValue="2026-06-19" isToday={true}
          onPrevDay={() => {}} onNextDay={() => {}} onToday={() => {}} onPickDate={() => {}} onSelectBlock={() => {}}
          onCellCreate={() => {}} onSeatsCreate={() => {}} onSeatToggle={() => {}}
        />
      </I18nProvider>
    );
    // Призрак-preview на каждой из двух строк группы (раньше показывался только на одной).
    expect(result.container.querySelectorAll('.booking-ghost.preview').length).toBe(2);
  });
});
