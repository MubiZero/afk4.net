import { useI18n } from '@afk4/i18n';
import { History } from 'lucide-react';
import type { LedgerEntryDto } from '../operatorApiClients';
import { formatMinorUnits } from '../operatorHelpers';
import { EmptyState } from '../operatorPrimitives';
import { projectLedgerEntry } from './playersModel';

// Богатый журнал операций клиента поверх снимка wallet-summary.recentEntries. Источник правды
// (paged ledger-эндпоинт) и фильтр/пагинация — S1b; здесь только человекочитаемый рендер.
export function HistorySection({ entries, currencyCode }: { entries: LedgerEntryDto[]; currencyCode: string }) {
  const { t } = useI18n();

  if (entries.length === 0) {
    return (
      <EmptyState
        icon={<History size={20} aria-hidden="true" />}
        title={t('op.players.history.emptyTitle')}
        description={t('op.players.history.emptyDescription')}
      />
    );
  }

  return (
    <div className="clients-history-list">
      {entries.map((raw) => {
        const view = projectLedgerEntry(raw, t);
        const sign = view.isCredit ? '+' : '−';
        const amount = formatMinorUnits(Math.abs(view.amountMinorUnits), view.currencyCode || currencyCode);
        return (
          <article key={view.id} className={`client-history-row ${view.isCredit ? 'is-credit' : 'is-debit'}`}>
            <span className="client-history-time">{view.timeLabel}</span>
            <div className="client-history-body">
              <strong>
                {view.typeLabel}
                {view.isReversal && <em className="client-history-reversal">{t('op.players.history.reversalBadge')}</em>}
              </strong>
              {view.description && <span className="client-history-detail">{view.description}</span>}
              {view.reason && <span className="client-history-detail">{view.reason}</span>}
            </div>
            <b className="client-history-amount">{sign}{amount}</b>
          </article>
        );
      })}
    </div>
  );
}
