import { useI18n } from '@afk4/i18n';
import { History, RefreshCw, Undo2 } from 'lucide-react';
import type { LedgerEntryDto } from '../operatorApiClients';
import { formatMinorUnits } from '../operatorHelpers';
import { EmptyState, Skeleton } from '../operatorPrimitives';
import { ledgerTypeLabel, projectLedgerEntry } from './playersModel';

// Курируемый набор ключевых типов для фильтр-чипов (метки — через ledgerTypeLabel → ledger.type.*).
// Полный список значений в AFK4.Shared.Contracts/Billing/LedgerEntryTypeNames; здесь только частые.
const HISTORY_FILTER_TYPES = ['top_up', 'gameplay_charge', 'package_purchase', 'debt_payment', 'refund'] as const;

// Серверный журнал операций клиента (источник — paged ledger-эндпоинт). Презентационный:
// данные/фильтр/пагинацию держит оркестратор. activeFilter=null → «Все».
export function HistorySection({
  entries,
  currencyCode,
  activeFilter,
  onFilterChange,
  hasMore,
  onLoadMore,
  loading,
  canRefund,
  onRefund,
}: {
  entries: LedgerEntryDto[];
  currencyCode: string;
  activeFilter: string | null;
  onFilterChange: (entryType: string | null) => void;
  hasMore: boolean;
  onLoadMore: () => void;
  loading: boolean;
  canRefund: boolean;
  onRefund: (entry: LedgerEntryDto) => void;
}) {
  const { t } = useI18n();

  return (
    <div className="clients-history-section">
      <div className="clients-history-filters" role="group" aria-label={t('op.players.tabs.history')}>
        <button
          type="button"
          className={`clients-history-filter${activeFilter === null ? ' active' : ''}`}
          onClick={() => onFilterChange(null)}
        >
          {t('op.players.history.filterAll')}
        </button>
        {HISTORY_FILTER_TYPES.map((type) => (
          <button
            key={type}
            type="button"
            className={`clients-history-filter${activeFilter === type ? ' active' : ''}`}
            onClick={() => onFilterChange(type)}
          >
            {ledgerTypeLabel(type, t)}
          </button>
        ))}
      </div>

      {loading && entries.length === 0 ? (
        <div className="clients-history-skeleton" aria-hidden="true">
          {Array.from({ length: 6 }).map((_, index) => (
            <Skeleton key={index} className="client-history-skel" />
          ))}
        </div>
      ) : entries.length === 0 ? (
        <EmptyState
          icon={<History size={20} aria-hidden="true" />}
          title={t('op.players.history.emptyTitle')}
          description={t('op.players.history.emptyDescription')}
        />
      ) : (
        <>
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
                    {(view.description || view.reason) && (
                      <span className="client-history-detail">
                        {[view.description, view.reason].filter(Boolean).join(' · ')}
                      </span>
                    )}
                  </div>
                  <div className="client-history-aside">
                    <b className="client-history-amount">{sign}{amount}</b>
                    {canRefund && !view.isReversal && (
                      <button
                        type="button"
                        className="client-history-refund"
                        onClick={() => onRefund(raw)}
                      >
                        <Undo2 size={13} aria-hidden="true" />
                        {t('op.players.refund.rowBtn')}
                      </button>
                    )}
                  </div>
                </article>
              );
            })}
          </div>
          {hasMore && (
            <button type="button" className="clients-history-more" disabled={loading} onClick={onLoadMore}>
              <RefreshCw size={14} aria-hidden="true" />{t('op.players.history.loadMore')}
            </button>
          )}
        </>
      )}
    </div>
  );
}
