import { Undo2 } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { Money } from '../operatorPrimitives';
import type { LedgerEntryView } from './playersModel';

export function LedgerRow({
  view,
  currencyCode,
  compact = false,
  canRefund = false,
  onRefund
}: {
  view: LedgerEntryView;
  currencyCode: string;
  compact?: boolean;
  canRefund?: boolean;
  onRefund?: () => void;
}) {
  const { t } = useI18n();
  const detail = [view.description, view.reason].filter(Boolean).join(' · ');
  const showRefund = !compact && canRefund && !view.isReversal && Boolean(onRefund);
  return (
    <div className={`ui-ledger-row${compact ? ' ui-ledger-row--compact' : ''}`}>
      <span className="ui-ledger-time">{view.timeLabel}</span>
      <div className="ui-ledger-body">
        <span className="ui-ledger-title">
          {view.typeLabel}
          {view.isReversal && <em className="ui-ledger-reversal">{t('op.players.history.reversalBadge')}</em>}
        </span>
        {!compact && detail && <span className="ui-ledger-detail">{detail}</span>}
      </div>
      <div className="ui-ledger-aside">
        <Money minorUnits={view.amountMinorUnits} currencyCode={view.currencyCode || currencyCode} signed />
        {showRefund && (
          <button type="button" className="ui-btn ui-btn--ghost ui-btn--sm ui-ledger-refund" onClick={onRefund}>
            <Undo2 size={13} aria-hidden="true" />
            {t('op.players.refund.rowBtn')}
          </button>
        )}
      </div>
    </div>
  );
}
