import { TrendingUp } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import type { SeatSummary } from './operatorData';
import { billingLabel } from './operatorHelpers';
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
  const hasSession = lead.kind === 'prepaid' || lead.kind === 'postpaid';
  // Режим оплаты показываем только когда он что-то значит — на активной сессии. На технику
  // (версии Agent/Shell, «команду» вроде No route) на плитке не место: это в правой панели.
  const billing = hasSession ? billingLabel(seat.billing, t) : null;
  const className = ['seat-tile', `state-${seat.tone}`,
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
        <strong>{seat.name}</strong>
        {lead.kind === 'postpaid' ? (
          <span className="seat-amount" aria-label={t('op.map.seatRising')}>
            {lead.amount}
            <TrendingUp size={12} aria-hidden="true" />
          </span>
        ) : (
          <span className="state-chip">{seat.stateLabel}</span>
        )}
      </header>

      {lead.kind !== 'free' && (
        <div className="seat-main">
          <strong className="seat-player">{seat.player}</strong>
          {billing !== null && <span className="seat-billing">{billing}</span>}
        </div>
      )}

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
        ) : lead.kind === 'plain' ? (
          <strong className="seat-status-line">{lead.remaining}</strong>
        ) : null}
      </footer>
    </article>
  );
}
