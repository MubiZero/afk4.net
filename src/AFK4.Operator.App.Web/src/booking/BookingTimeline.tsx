import type { MouseEvent } from 'react';
import { useI18n } from '@afk4/i18n';
import type { SeatSummary } from '../operatorData';
import { zoneLabel } from '../operatorHelpers';
import { EmptyState, Skeleton } from '../operatorPrimitives';
import type { BookingItem, TimelineAxis, ZoneRowGroup } from './bookingModel';

export function BookingTimeline({
  groups, axis, nowMs, loading, showSkeleton, selectedReservationId, branchName, onSelectBlock, onCellCreate
}: {
  groups: ZoneRowGroup[];
  axis: TimelineAxis;
  nowMs: number;
  loading: boolean;
  showSkeleton: boolean;
  selectedReservationId: string;
  branchName: string;
  onSelectBlock: (item: BookingItem) => void;
  onCellCreate: (seat: SeatSummary, startMs: number) => void;
}) {
  const { t } = useI18n();
  const hasRows = groups.some((g) => g.rows.length > 0);
  const nowPct = nowMs >= axis.startMs && nowMs <= axis.endMs
    ? ((nowMs - axis.startMs) / axis.spanMs) * 100
    : null;

  if (loading) {
    return showSkeleton ? (
      <div className="booking-grid" role="status" aria-label={t('op.booking.load.loading')}>
        {Array.from({ length: 8 }).map((_, i) => (
          <div className="booking-grid-row" key={i}>
            <Skeleton className="booking-row-label-skel" />
            <Skeleton className="booking-row-track-skel" />
          </div>
        ))}
      </div>
    ) : null;
  }

  if (!hasRows) {
    return <EmptyState title={t('op.booking.empty.dayTitle')} description={t('op.booking.empty.dayHint')} className="booking-empty" />;
  }

  // Перевод клика по дорожке в момент времени на оси.
  const cellStartMs = (event: MouseEvent<HTMLDivElement>): number => {
    const rect = event.currentTarget.getBoundingClientRect();
    const ratio = Math.min(1, Math.max(0, (event.clientX - rect.left) / rect.width));
    return axis.startMs + ratio * axis.spanMs;
  };

  return (
    <div className="booking-grid" aria-label={t('op.booking.timeline.aria')}>
      <div className="booking-grid-axis">
        <div className="booking-axis-gutter" />
        <div className="booking-axis-track">
          {axis.ticks.map((tick) => (
            <span
              key={tick.ms}
              className="booking-axis-tick"
              style={{ left: `${((tick.ms - axis.startMs) / axis.spanMs) * 100}%` }}
            >{tick.label}</span>
          ))}
        </div>
      </div>

      <div className="booking-grid-body">
        {nowPct !== null && (
          <div className="booking-now-line" style={{ left: `calc(var(--booking-gutter) + (100% - var(--booking-gutter)) * ${nowPct / 100})` }} aria-label={t('op.booking.axis.now')} />
        )}
        {groups.map((group) => (
          <section className="booking-zone" key={group.zone}>
            <header className="booking-zone-head"><span>{zoneLabel(group.zone, t)}</span><strong>{group.rows.length}</strong></header>
            {group.rows.map((row) => (
              <div className="booking-grid-row" key={row.seat.id}>
                <span className="booking-row-label" title={row.seat.name}>{row.seat.name}</span>
                <div
                  className="booking-row-track"
                  role="group"
                  onClick={(event) => onCellCreate(row.seat, cellStartMs(event))}
                >
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
                </div>
              </div>
            ))}
          </section>
        ))}
      </div>
    </div>
  );
}
