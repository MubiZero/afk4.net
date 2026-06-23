import { useI18n } from '@afk4/i18n';
import { History } from 'lucide-react';
import type { LedgerEntryDto } from '../operatorApiClients';
import { HistorySection } from './HistorySection';

// Постоянный правый рейл с полным журналом операций — на широком экране (≥1280px)
// он заменяет вкладку «История» и мини-ленту «Кошелька», давая работу пространству,
// которое иначе пустует справа от карточки. Данные/фильтр/пагинацию держит оркестратор;
// рейл — лишь панель-обёртка над тем же HistorySection.
export function ClientLedgerRail(props: {
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
    <aside className="clients-panel clients-ledger-rail" aria-label={t('op.players.ledgerRail.title')}>
      <header className="clients-ledger-rail-head">
        <History size={15} aria-hidden="true" />
        <strong>{t('op.players.ledgerRail.title')}</strong>
      </header>
      <div className="clients-ledger-rail-body">
        <HistorySection
          entries={props.entries}
          currencyCode={props.currencyCode}
          activeFilter={props.activeFilter}
          onFilterChange={props.onFilterChange}
          hasMore={props.hasMore}
          onLoadMore={props.onLoadMore}
          loading={props.loading}
          canRefund={props.canRefund}
          onRefund={props.onRefund}
        />
      </div>
    </aside>
  );
}
