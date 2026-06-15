import { Hourglass, OctagonAlert, TrendingUp, TriangleAlert, WifiOff, Wrench } from 'lucide-react';
import type { ComponentType } from 'react';
import { useI18n } from '@afk4/i18n';
import type { SeatSummary, SeatTone } from './operatorData';
import { formatDurationCompact } from './floorMapState';
import { isAttentionTone, seatTileLead } from './seatTilePresentation';

// Иконка типа проблемы для мест без сессии — настоящий сигнификатор «что не так», по тону.
// Имя игрока/тариф/режим оплаты floor-map ещё не отдаёт (придёт в B1), поэтому плитку наполняем
// тем, что реально знаем: остаток времени-герой, статус и тип проблемы — без заглушек.
const PROBLEM_ICON: Partial<Record<SeatTone, ComponentType<{ size?: number; 'aria-hidden'?: boolean }>>> = {
  pending: Hourglass,
  warning: TriangleAlert,
  blocking: OctagonAlert,
  offline: WifiOff,
  service: Wrench
};

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
  const className = ['seat-tile', `state-${seat.tone}`,
    isAttentionTone(seat.tone) ? 'seat-tile--alert' : '',
    selected ? 'selected' : ''].filter(Boolean).join(' ');
  const ProblemIcon = lead.kind === 'plain' ? PROBLEM_ICON[seat.tone] : undefined;

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
        <span className="seat-id">
          <span className="seat-dot" aria-hidden="true" />
          <strong>{seat.name}</strong>
        </span>
        {lead.kind === 'postpaid' ? (
          <span className="seat-amount" aria-label={t('op.map.seatRising')}>
            {lead.amount}
            <TrendingUp size={12} aria-hidden="true" />
          </span>
        ) : (
          <span className="state-chip">{seat.stateLabel}</span>
        )}
      </header>

      {lead.kind === 'free' ? (
        // Свободное место — приглашение: крупный «＋» по центру + подпись-аффорданс.
        <div className="seat-invite">
          <span className="seat-free" aria-hidden="true">+</span>
          <span className="seat-invite-label">{t('op.map.seatInvite')}</span>
        </div>
      ) : lead.kind === 'prepaid' ? (
        // Предоплата: время-герой (компактное, без слова «осталось» — оно мелкой подписью),
        // под ним убывающая полоса.
        <div className="seat-body seat-body--metric">
          <div className="seat-clock-wrap">
            <span className="seat-clock-label">{t('op.map.seatLeft')}</span>
            <strong className="seat-clock">
              {seat.remainingSeconds != null ? formatDurationCompact(seat.remainingSeconds, t) : lead.remaining}
            </strong>
          </div>
          <span className={`seat-timebar${lead.low ? ' seat-timebar--low' : ''}`} aria-hidden="true">
            <i style={{ width: `${Math.round(lead.barRatio * 100)}%` }} />
          </span>
        </div>
      ) : lead.kind === 'postpaid' ? (
        // Открытый счёт: сумма наверху (в шапке), в теле — честный ярлык состояния оплаты.
        <div className="seat-body seat-body--metric">
          <span className="seat-open-tab">{t('op.map.seatOpenTab')}</span>
        </div>
      ) : (
        // Проблемное / ожидающее место: иконка типа проблемы + человеческая строка состояния.
        <div className="seat-body seat-body--status">
          {ProblemIcon && <ProblemIcon size={18} aria-hidden={true} />}
          <strong className="seat-status-line">{lead.remaining}</strong>
        </div>
      )}
    </article>
  );
}
