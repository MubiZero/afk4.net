import { TrendingUp } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import type { SeatSummary } from './operatorData';
import { appVersionLabel, commandLabel, zoneClass, zoneLabel } from './operatorHelpers';
import { isAttentionTone, seatTileLead } from './seatTilePresentation';

export function SeatTile({
  seat,
  selected,
  onSelect
}: {
  seat: SeatSummary;
  selected?: boolean;
  onSelect: () => void;
}) {
  const { t } = useI18n();
  const lead = seatTileLead(seat);
  const className = ['seat-tile', zoneClass(seat.zone), `state-${seat.tone}`,
    isAttentionTone(seat.tone) ? 'seat-tile--alert' : '',
    selected ? 'selected' : ''].filter(Boolean).join(' ');

  return (
    <article
      className={className}
      aria-label={`${seat.name} ${seat.stateLabel}`}
      aria-pressed={selected}
      onClick={onSelect}
      onKeyDown={(event) => {
        if (event.key === 'Enter' || event.key === ' ') {
          event.preventDefault();
          onSelect();
        }
      }}
      role="button"
      tabIndex={0}
    >
      <header className="seat-head">
        <div>
          <strong>{seat.name}</strong>
          <span>{zoneLabel(seat.zone, t)}</span>
        </div>
        {lead.kind === 'postpaid' ? (
          <span className="seat-amount" aria-label={t('op.map.seatRising')}>
            {lead.amount}
            <TrendingUp size={12} aria-hidden="true" />
          </span>
        ) : (
          <span className="state-chip">{seat.stateLabel}</span>
        )}
      </header>

      <div className="seat-main">
        <span>{seat.player}</span>
        <span>{appVersionLabel(seat.app, t)}</span>
      </div>

      <footer>
        {lead.kind === 'free' ? (
          <span className="seat-free" aria-label={t('op.map.seatFree')}>+</span>
        ) : lead.kind === 'prepaid' ? (
          <div className="seat-time">
            <strong>{lead.remaining}</strong>
            <span className={`seat-timebar${lead.low ? ' seat-timebar--low' : ''}`} aria-hidden="true">
              <i style={{ width: `${Math.round(lead.barRatio * 100)}%` }} />
            </span>
          </div>
        ) : lead.kind === 'postpaid' ? (
          <span className="seat-foot-note">{commandLabel(seat.command, t)}</span>
        ) : (
          <>
            <strong>{lead.remaining}</strong>
            <span>{commandLabel(seat.command, t)}</span>
          </>
        )}
      </footer>
    </article>
  );
}
