import { useI18n } from '@afk4/i18n';
import { History, RefreshCw } from 'lucide-react';
import type { LedgerEntryDto } from '../operatorApiClients';
import { EmptyState, Skeleton } from '../operatorPrimitives';
import { LedgerRow } from './LedgerRow';
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
          className={`ui-chip ui-chip--filter${activeFilter === null ? ' is-active' : ''}`}
          onClick={() => onFilterChange(null)}
        >
          {t('op.players.history.filterAll')}
        </button>
        {HISTORY_FILTER_TYPES.map((type) => (
          <button
            key={type}
            type="button"
            className={`ui-chip ui-chip--filter${activeFilter === type ? ' is-active' : ''}`}
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
          <div className="clients-history-list ui-ledger-list">
            {entries.map((raw) => {
              const view = projectLedgerEntry(raw, t);
              return (
                <LedgerRow
                  key={view.id}
                  view={view}
                  currencyCode={currencyCode}
                  canRefund={canRefund}
                  onRefund={() => onRefund(raw)}
                />
              );
            })}
          </div>
          {hasMore && (
            <button type="button" className="ui-btn ui-btn--block clients-history-more" disabled={loading} onClick={onLoadMore}>
              <RefreshCw size={14} aria-hidden="true" />{t('op.players.history.loadMore')}
            </button>
          )}
        </>
      )}
    </div>
  );
}
