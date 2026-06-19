import { useEffect, useRef, useState } from 'react';
import type { MouseEvent as ReactMouseEvent, ReactNode } from 'react';
import { useI18n } from '@afk4/i18n';
import type { SeatSummary } from '../operatorData';
import { zoneLabel } from '../operatorHelpers';
import { EmptyState, Skeleton } from '../operatorPrimitives';
import type { BookingItem, SessionItem, TimelineAxis, ZoneRowGroup } from './bookingModel';

// Протягивание по дорожке выставляет интервал брони: снап по 15 мин, короткое
// нажатие без движения трактуем как клик (бронь по умолчанию на 60 мин в родителе).
const SNAP_MS = 15 * 60_000;
const CLICK_THRESHOLD_MS = 5 * 60_000;
const HOUR_MS = 3_600_000;
const QUARTER_MS = 15 * 60_000;
// Минимум ширины на час, при котором часовые подписи не наезжают друг на друга. Ниже — показываем
// метки реже (раз в LABEL_STEP_HOURS, т.е. major), как было: это режим узкого/минимального окна.
const HOURLY_LABEL_MIN_PX = 46;

interface DragState {
  anchorSeatId: string;
  currentSeatId: string;
  anchorMs: number;
  currentMs: number;
}

export function BookingTimeline({
  groups, axis, nowMs, loading, showSkeleton, selectedReservationId, branchName, previewBlock, dateLabel, onPrevDay, onNextDay, onSelectBlock, onCellCreate, onSeatsCreate
}: {
  groups: ZoneRowGroup[];
  axis: TimelineAxis;
  nowMs: number;
  loading: boolean;
  showSkeleton: boolean;
  selectedReservationId: string;
  branchName: string;
  previewBlock: { seatIds: string[]; startMs: number; endMs: number } | null;
  dateLabel: string;
  onPrevDay: () => void;
  onNextDay: () => void;
  onSelectBlock: (item: BookingItem) => void;
  onCellCreate: (seat: SeatSummary, startMs: number, durationMinutes?: number) => void;
  onSeatsCreate: (seats: SeatSummary[], startMs: number, durationMinutes: number) => void;
}) {
  const { t } = useI18n();
  const hasRows = groups.some((g) => g.rows.length > 0);
  const nowPct = nowMs >= axis.startMs && nowMs <= axis.endMs
    ? ((nowMs - axis.startMs) / axis.spanMs) * 100
    : null;

  // Плоский порядок мест (через все залы) — для выбора диапазона строк протягиванием по вертикали.
  const flatSeats = groups.flatMap((g) => g.rows.map((r) => r.seat));
  const seatIndex = new Map(flatSeats.map((seat, i) => [seat.id, i] as const));

  // Связь блоков одной группы: наведение на любой блок подсвечивает все блоки его группы (через
  // залы); группа выбранной брони подсвечена постоянно. Пустой id не группирует.
  const [hoveredGroupId, setHoveredGroupId] = useState<string>('');
  const selectedGroupId = groups
    .flatMap((g) => g.rows)
    .flatMap((r) => r.blocks)
    .find((b) => b.item.reservationId === selectedReservationId)?.item.reservationGroupId ?? '';
  const isGroupPeer = (groupId: string): boolean =>
    groupId !== '' && (groupId === hoveredGroupId || groupId === selectedGroupId);

  // Ширина оси времени → плотность подписей. Широкое окно — метки каждый час; узкое/минимальное —
  // реже (каждые LABEL_STEP_HOURS), чтобы подписи не наезжали.
  const axisTrackRef = useRef<HTMLDivElement | null>(null);
  const [axisWidth, setAxisWidth] = useState(0);
  useEffect(() => {
    const el = axisTrackRef.current;
    if (!el || typeof ResizeObserver === 'undefined') {
      return undefined;
    }
    const observer = new ResizeObserver((entries) => {
      setAxisWidth(entries[0]?.contentRect.width ?? 0);
    });
    observer.observe(el);
    setAxisWidth(el.getBoundingClientRect().width);
    return () => observer.disconnect();
  }, []);
  const hourCount = Math.max(1, Math.round(axis.spanMs / HOUR_MS));
  const showHourlyLabels = axisWidth > 0 && axisWidth / hourCount >= HOURLY_LABEL_MIN_PX;
  // Часовой режим — мельче засечки: короткие риски каждые 15 мин между подписями (сами часы
  // несут подпись). В 3-часовом режиме засечки остаются часовыми (немажорные тики axis.ticks).
  const quarterTicks: number[] = [];
  if (showHourlyLabels) {
    for (let ms = axis.startMs + QUARTER_MS; ms < axis.endMs; ms += QUARTER_MS) {
      if ((ms - axis.startMs) % HOUR_MS !== 0) {
        quarterTicks.push(ms);
      }
    }
  }

  const [drag, setDrag] = useState<DragState | null>(null);
  const trackRef = useRef<HTMLDivElement | null>(null);
  const rowEls = useRef<Map<string, HTMLElement>>(new Map());
  const dragRef = useRef<DragState | null>(null);
  const draggedRef = useRef(false); // только что было протягивание — хвостовой click надо погасить
  dragRef.current = drag;

  // Постоянный перехватчик в capture-фазе: глушит ровно один хвостовой click после протягивания,
  // раньше делегированного React-обработчика и независимо от того, по какому элементу он попал.
  useEffect(() => {
    const onClickCapture = (e: Event): void => {
      if (draggedRef.current) {
        e.stopPropagation();
        draggedRef.current = false;
      }
    };
    window.addEventListener('click', onClickCapture, true);
    return () => window.removeEventListener('click', onClickCapture, true);
  }, []);

  const msFromClientX = (track: HTMLElement, clientX: number): number => {
    const rect = track.getBoundingClientRect();
    const ratio = Math.min(1, Math.max(0, (clientX - rect.left) / rect.width));
    return axis.startMs + ratio * axis.spanMs;
  };
  const snap = (ms: number): number => Math.round(ms / SNAP_MS) * SNAP_MS;
  const pctOf = (ms: number): number => ((ms - axis.startMs) / axis.spanMs) * 100;
  const hhmm = (ms: number): string => {
    const d = new Date(ms);
    return `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`;
  };

  // Какая строка под курсором по вертикали: попадание в трек, иначе ближайший по расстоянию
  // (курсор в шапке зала или промежутке между залами — тянемся к ближайшему месту).
  const seatIdAtClientY = (clientY: number): string | null => {
    let nearestId: string | null = null;
    let nearestGap = Infinity;
    for (const [id, el] of rowEls.current) {
      const rect = el.getBoundingClientRect();
      if (clientY >= rect.top && clientY <= rect.bottom) return id;
      const gap = clientY < rect.top ? rect.top - clientY : clientY - rect.bottom;
      if (gap < nearestGap) { nearestGap = gap; nearestId = id; }
    }
    return nearestId;
  };

  // Места выбранного протягиванием диапазона строк (от якоря до текущей, в плоском порядке).
  const seatsInRange = (state: DragState): SeatSummary[] => {
    const a = seatIndex.get(state.anchorSeatId);
    const b = seatIndex.get(state.currentSeatId);
    if (a === undefined || b === undefined) return [];
    return flatSeats.slice(Math.min(a, b), Math.max(a, b) + 1);
  };

  const moved = drag !== null && Math.abs(drag.currentMs - drag.anchorMs) >= CLICK_THRESHOLD_MS;

  // Подпись-тултип сессии: кто играет + признак открытой (без конца).
  const sessionTitle = (s: SessionItem): string => {
    const who = s.playerName || t('op.booking.guest');
    return s.open ? t('op.booking.session.titleOpen', { who }) : t('op.booking.session.titleBounded', { who });
  };

  // Начало возможного протягивания. Обычный клик (без движения) обрабатывает onClick ниже,
  // поэтому здесь только запускаем отслеживание; click-аффорданс сохраняется для тестов и мыши.
  const beginDrag = (event: ReactMouseEvent<HTMLDivElement>, seat: SeatSummary): void => {
    if (event.button !== 0) return;
    if ((event.target as HTMLElement).closest('.booking-block')) return; // нажатие по блоку — выбор, не протягивание
    draggedRef.current = false; // новый жест — сбрасываем гаситель клика
    trackRef.current = event.currentTarget;
    const ms = msFromClientX(event.currentTarget, event.clientX);
    setDrag({ anchorSeatId: seat.id, currentSeatId: seat.id, anchorMs: ms, currentMs: ms });
  };

  // Клик по дорожке создаёт бронь по умолчанию (60 мин). После настоящего протягивания
  // браузер шлёт хвостовой click — его глушит capture-перехватчик выше, поэтому сюда он не доходит.
  const handleTrackClick = (event: ReactMouseEvent<HTMLDivElement>, seat: SeatSummary): void => {
    if ((event.target as HTMLElement).closest('.booking-block')) return;
    onCellCreate(seat, snap(msFromClientX(event.currentTarget, event.clientX)));
  };

  useEffect(() => {
    if (!drag) return undefined;
    const onMove = (e: MouseEvent): void => {
      const track = trackRef.current;
      if (!track) return;
      const ms = msFromClientX(track, e.clientX);
      const overSeatId = seatIdAtClientY(e.clientY);
      setDrag((d) => (d ? { ...d, currentMs: ms, currentSeatId: overSeatId ?? d.currentSeatId } : d));
    };
    const onUp = (): void => {
      const d = dragRef.current;
      setDrag(null);
      trackRef.current = null;
      if (!d) return;
      const lo = Math.min(d.anchorMs, d.currentMs);
      const hi = Math.max(d.anchorMs, d.currentMs);
      if (hi - lo < CLICK_THRESHOLD_MS) return; // не двигали — пусть отработает click
      draggedRef.current = true; // настоящий drag — погасить хвостовой click
      const start = snap(lo);
      const end = Math.max(start + SNAP_MS, snap(hi));
      const minutes = Math.round((end - start) / 60_000);
      const seats = seatsInRange(d);
      if (seats.length === 0) return;
      // Одна строка → обычная бронь; несколько строк → массовая (групповая) бронь.
      if (seats.length === 1) {
        onCellCreate(seats[0], start, minutes);
      } else {
        onSeatsCreate(seats, start, minutes);
      }
    };
    window.addEventListener('mousemove', onMove);
    window.addEventListener('mouseup', onUp);
    return () => {
      window.removeEventListener('mousemove', onMove);
      window.removeEventListener('mouseup', onUp);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [drag !== null]);

  // ghost/preview считаем всегда (нужны только в теле, но это дёшево и упрощает структуру).
  const ghost = drag && moved
    ? (() => {
        const lo = snap(Math.min(drag.anchorMs, drag.currentMs));
        const hi = Math.max(lo + SNAP_MS, snap(Math.max(drag.anchorMs, drag.currentMs)));
        const left = pctOf(lo);
        const seats = seatsInRange(drag);
        return { seatIds: new Set(seats.map((seat) => seat.id)), labelSeatId: seats[0]?.id ?? null, left, width: pctOf(hi) - left, label: `${hhmm(lo)} – ${hhmm(hi)}` };
      })()
    : null;
  const preview = previewBlock && previewBlock.seatIds.length > 0 && Number.isFinite(previewBlock.startMs) && Number.isFinite(previewBlock.endMs)
    ? (() => {
        const left = Math.min(100, Math.max(0, pctOf(previewBlock.startMs)));
        const right = Math.min(100, Math.max(0, pctOf(previewBlock.endMs)));
        const ids = new Set(previewBlock.seatIds);
        const labelSeatId = flatSeats.find((seat) => ids.has(seat.id))?.id ?? null;
        return { seatIds: ids, labelSeatId, left, width: Math.max(0.6, right - left), label: `${hhmm(previewBlock.startMs)} – ${hhmm(previewBlock.endMs)}` };
      })()
    : null;

  // Ось с дата-навигацией в «нулевой» ячейке (гаттере) — всегда, чтобы переключение дат не
  // пропадало во время загрузки.
  const axisHeader = (
    <div className="booking-grid-axis">
      <div className="booking-axis-gutter">
        <div className="booking-gutter-datenav">
          <button type="button" aria-label={t('op.booking.dateNav.prev')} onClick={onPrevDay}>‹</button>
          <strong>{dateLabel}</strong>
          <button type="button" aria-label={t('op.booking.dateNav.next')} onClick={onNextDay}>›</button>
        </div>
      </div>
      <div className="booking-axis-track" ref={axisTrackRef}>
        {axis.ticks.slice(1).map((tick) => {
          // Подпись — у major-часов всегда, у промежуточных только когда ось широкая.
          const labeled = tick.major || showHourlyLabels;
          return (
            <span
              key={tick.ms}
              className={`booking-axis-tick${labeled ? ' is-major' : ' is-minor'}`}
              style={{ left: `${((tick.ms - axis.startMs) / axis.spanMs) * 100}%` }}
            >{labeled ? tick.label : null}</span>
          );
        })}
        {/* Часовой режим: короткие риски каждые 15 мин между часовыми подписями. */}
        {quarterTicks.map((ms) => (
          <span
            key={`q-${ms}`}
            className="booking-axis-tick is-minor"
            style={{ left: `${((ms - axis.startMs) / axis.spanMs) * 100}%` }}
          />
        ))}
      </div>
    </div>
  );

  let body: ReactNode;
  if (loading) {
    body = showSkeleton ? (
      <div className="booking-grid-body" role="status" aria-label={t('op.booking.load.loading')}>
        {Array.from({ length: 8 }).map((_, i) => (
          <div className="booking-grid-row" key={i}>
            <Skeleton className="booking-row-label-skel" />
            <Skeleton className="booking-row-track-skel" />
          </div>
        ))}
      </div>
    ) : <div className="booking-grid-body" />;
  } else if (!hasRows) {
    body = (
      <div className="booking-grid-body">
        <EmptyState title={t('op.booking.empty.dayTitle')} description={t('op.booking.empty.dayHint')} className="booking-empty" />
      </div>
    );
  } else {
    body = (
      <div className="booking-grid-body">
        {nowPct !== null && (
          <div className="booking-now-line" style={{ left: `calc(var(--booking-gutter) + (100% - var(--booking-gutter)) * ${nowPct / 100})` }} aria-label={t('op.booking.axis.now')} />
        )}
        {groups.map((group) => (
          <section className="booking-zone" key={group.zone}>
            <header className="booking-zone-head"><span>{zoneLabel(group.zone, t)}</span><strong>{group.rows.length}</strong></header>
            {group.rows.map((row) => (
              <div className="booking-grid-row" key={row.seat.id}>
                <span className="booking-row-label" title={`${row.seat.name} · ${row.seat.stateLabel}`}>
                  <span className="booking-row-name">
                    <i className={`booking-seat-dot state-${row.seat.tone}`} aria-hidden="true" />
                    {row.seat.name}
                  </span>
                  <span className="booking-row-status">{row.seat.stateLabel}</span>
                </span>
                <div
                  className="booking-row-track"
                  role="group"
                  ref={(el) => { if (el) rowEls.current.set(row.seat.id, el); else rowEls.current.delete(row.seat.id); }}
                  onMouseDown={(event) => beginDrag(event, row.seat)}
                  onClick={(event) => handleTrackClick(event, row.seat)}
                >
                  {/* Фоновый слой: идущие сессии (занятость ПК сейчас). Открытая тянется до края
                      и растворяется — конец неизвестен. Не перехватывает мышь — поверх создаются брони. */}
                  {row.sessions.map((session) => (
                    <div
                      key={session.item.sessionId}
                      className={`booking-session${session.open ? ' open' : ''}`}
                      style={{ left: `${session.leftPct}%`, width: `${session.widthPct}%` }}
                      title={sessionTitle(session.item)}
                      aria-hidden="true"
                    >
                      <span className="booking-session-label">{session.item.playerName || t('op.booking.guest')}</span>
                      {session.open && <span className="booking-session-arrow" aria-hidden="true">→</span>}
                    </div>
                  ))}
                  {row.blocks.map((block) => (
                    <button
                      key={block.item.reservationId}
                      type="button"
                      className={`booking-block ${block.item.tone}${block.item.reservationId === selectedReservationId ? ' active' : ''}${isGroupPeer(block.item.reservationGroupId) ? ' group-peer' : ''}`}
                      style={{ left: `${block.leftPct}%`, width: `${block.widthPct}%` }}
                      onMouseEnter={() => setHoveredGroupId(block.item.reservationGroupId)}
                      onMouseLeave={() => setHoveredGroupId('')}
                      onClick={(event) => { event.stopPropagation(); onSelectBlock(block.item); }}
                    >
                      <b>{block.item.customerName}</b>
                    </button>
                  ))}
                  {ghost && ghost.seatIds.has(row.seat.id) && (
                    <div className={`booking-ghost${ghost.seatIds.size > 1 ? ' group' : ''}`} style={{ left: `${ghost.left}%`, width: `${ghost.width}%` }}>
                      {row.seat.id === ghost.labelSeatId && <span>{ghost.label}</span>}
                    </div>
                  )}
                  {!ghost && preview && preview.seatIds.has(row.seat.id) && (
                    <div className={`booking-ghost preview${preview.seatIds.size > 1 ? ' group' : ''}`} style={{ left: `${preview.left}%`, width: `${preview.width}%` }}>
                      {row.seat.id === preview.labelSeatId && <span>{preview.label}</span>}
                    </div>
                  )}
                </div>
              </div>
            ))}
          </section>
        ))}
      </div>
    );
  }

  return (
    <div className={`booking-grid${moved ? ' dragging' : ''}`} aria-label={t('op.booking.timeline.aria')}>
      {axisHeader}
      {body}
    </div>
  );
}
