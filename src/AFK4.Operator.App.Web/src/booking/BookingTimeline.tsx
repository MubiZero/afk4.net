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

interface DragState {
  seat: SeatSummary;
  anchorMs: number;
  currentMs: number;
}

export function BookingTimeline({
  groups, axis, nowMs, loading, showSkeleton, selectedReservationId, branchName, previewBlock, dateLabel, onPrevDay, onNextDay, onSelectBlock, onCellCreate
}: {
  groups: ZoneRowGroup[];
  axis: TimelineAxis;
  nowMs: number;
  loading: boolean;
  showSkeleton: boolean;
  selectedReservationId: string;
  branchName: string;
  previewBlock: { seatId: string; startMs: number; endMs: number } | null;
  dateLabel: string;
  onPrevDay: () => void;
  onNextDay: () => void;
  onSelectBlock: (item: BookingItem) => void;
  onCellCreate: (seat: SeatSummary, startMs: number, durationMinutes?: number) => void;
}) {
  const { t } = useI18n();
  const hasRows = groups.some((g) => g.rows.length > 0);
  const nowPct = nowMs >= axis.startMs && nowMs <= axis.endMs
    ? ((nowMs - axis.startMs) / axis.spanMs) * 100
    : null;

  const [drag, setDrag] = useState<DragState | null>(null);
  const trackRef = useRef<HTMLDivElement | null>(null);
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
    setDrag({ seat, anchorMs: ms, currentMs: ms });
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
      setDrag((d) => (d ? { ...d, currentMs: msFromClientX(track, e.clientX) } : d));
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
      onCellCreate(d.seat, start, Math.round((end - start) / 60_000));
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
        return { seatId: drag.seat.id, left, width: pctOf(hi) - left, label: `${hhmm(lo)} – ${hhmm(hi)}` };
      })()
    : null;
  const preview = previewBlock && Number.isFinite(previewBlock.startMs) && Number.isFinite(previewBlock.endMs)
    ? (() => {
        const left = Math.min(100, Math.max(0, pctOf(previewBlock.startMs)));
        const right = Math.min(100, Math.max(0, pctOf(previewBlock.endMs)));
        return { seatId: previewBlock.seatId, left, width: Math.max(0.6, right - left), label: `${hhmm(previewBlock.startMs)} – ${hhmm(previewBlock.endMs)}` };
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
      <div className="booking-axis-track">
        {axis.ticks.slice(1).map((tick) => (
          <span
            key={tick.ms}
            className="booking-axis-tick"
            style={{ left: `${((tick.ms - axis.startMs) / axis.spanMs) * 100}%` }}
          >{tick.label}</span>
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
                      className={`booking-block ${block.item.tone}${block.item.reservationId === selectedReservationId ? ' active' : ''}`}
                      style={{ left: `${block.leftPct}%`, width: `${block.widthPct}%` }}
                      onClick={(event) => { event.stopPropagation(); onSelectBlock(block.item); }}
                    >
                      <b>{block.item.customerName}</b>
                    </button>
                  ))}
                  {ghost && ghost.seatId === row.seat.id && (
                    <div className="booking-ghost" style={{ left: `${ghost.left}%`, width: `${ghost.width}%` }}>
                      <span>{ghost.label}</span>
                    </div>
                  )}
                  {!ghost && preview && preview.seatId === row.seat.id && (
                    <div className="booking-ghost preview" style={{ left: `${preview.left}%`, width: `${preview.width}%` }}>
                      <span>{preview.label}</span>
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
