import { useI18n } from '@afk4/i18n';
import { CalendarClock } from 'lucide-react';
import type { PlayerClientItem } from '../operatorHelpers';
import { dataSourceLabel, formatMinorUnits } from '../operatorHelpers';
import type { LedgerEntryDto, PackageOptionDto, PlayerPackageDto } from '../operatorApiClients';
import { EmptyState } from '../operatorPrimitives';
import { playerStatusLabel } from './playersModel';
import { WalletSection } from './WalletSection';
import { PackagesSection } from './PackagesSection';
import { HistorySection } from './HistorySection';

export type ClientDetailTab = 'wallet' | 'packages' | 'history';

// Первые две буквы имени как аватар-заглушка.
function initials(name: string): string {
  return name
    .split(' ')
    .map((part) => part[0])
    .join('')
    .slice(0, 2)
    .toUpperCase() || '—';
}

export function ClientDetail(props: {
  client: PlayerClientItem | null;
  activeTab: ClientDetailTab;
  balanceMinorUnits: number;
  debtMinorUnits: number;
  packageCount: number;
  currencyCode: string;
  packages: PlayerPackageDto[];
  options: PackageOptionDto[];
  recentEntries: LedgerEntryDto[];
  selectedPackageDefinitionId: string;
  topUpAmount: string;
  topUpReason: string;
  debtAmount: string;
  debtReason: string;
  canTopUp: boolean;
  canPayDebt: boolean;
  canPurchase: boolean;
  canCreateReservation: boolean;
  onSelectTab: (tab: ClientDetailTab) => void;
  onChangeTopUpAmount: (value: string) => void;
  onChangeTopUpReason: (value: string) => void;
  onChangeDebtAmount: (value: string) => void;
  onChangeDebtReason: (value: string) => void;
  onTopUp: () => void;
  onPayDebt: () => void;
  onSelectOption: (packageDefinitionId: string) => void;
  onBuy: () => void;
  onCreateReservation: () => void;
}) {
  const { t } = useI18n();
  const { client } = props;

  if (client === null) {
    return (
      <section className="clients-panel clients-detail-panel">
        <EmptyState
          title={t('op.players.profile.empty')}
          description={t('op.players.profile.emptyNote')}
        />
      </section>
    );
  }

  const hasDebt = props.debtMinorUnits > 0;
  const tabs: Array<{ id: ClientDetailTab; label: string }> = [
    { id: 'wallet', label: t('op.players.tabs.wallet') },
    { id: 'packages', label: t('op.players.tabs.packages') },
    { id: 'history', label: t('op.players.tabs.history') },
  ];

  return (
    <section className="clients-panel clients-detail-panel">
      <header className="client-detail-head">
        <div className="client-avatar">{initials(client.name)}</div>
        <div className="client-detail-ident">
          <span className="client-detail-status">{playerStatusLabel(client.status, t)}</span>
          <strong>{client.name}</strong>
          <em>
            {client.phoneNumber || t('op.pos.cart.clientNoPhone')}
            {' · '}
            {dataSourceLabel(client.source, t)}
          </em>
        </div>
        <button
          type="button"
          className="client-detail-reservation"
          disabled={!props.canCreateReservation}
          onClick={props.onCreateReservation}
        >
          <CalendarClock size={15} aria-hidden="true" />
          {t('op.players.detail.reservationBtn')}
        </button>
      </header>

      <div className="client-detail-chips">
        <div className="client-chip">
          <span>{t('op.players.chip.balance')}</span>
          <strong>{formatMinorUnits(props.balanceMinorUnits, props.currencyCode)}</strong>
        </div>
        <div className={`client-chip${hasDebt ? ' is-debt' : ''}`}>
          <span>{t('op.players.chip.debt')}</span>
          <strong>{formatMinorUnits(props.debtMinorUnits, props.currencyCode)}</strong>
        </div>
        <div className="client-chip">
          <span>{t('op.players.chip.packages')}</span>
          <strong>{props.packageCount}</strong>
        </div>
      </div>

      <div className="client-detail-tabs" role="tablist">
        {tabs.map((tab) => (
          <button
            key={tab.id}
            type="button"
            role="tab"
            aria-selected={props.activeTab === tab.id}
            className={`client-detail-tab${props.activeTab === tab.id ? ' active' : ''}`}
            onClick={() => props.onSelectTab(tab.id)}
          >
            {tab.label}
          </button>
        ))}
      </div>

      <div className="client-detail-content">
        {props.activeTab === 'wallet' && (
          <WalletSection
            balanceMinorUnits={props.balanceMinorUnits}
            debtMinorUnits={props.debtMinorUnits}
            currencyCode={props.currencyCode}
            topUpAmount={props.topUpAmount}
            topUpReason={props.topUpReason}
            debtAmount={props.debtAmount}
            debtReason={props.debtReason}
            canTopUp={props.canTopUp}
            canPayDebt={props.canPayDebt}
            onChangeTopUpAmount={props.onChangeTopUpAmount}
            onChangeTopUpReason={props.onChangeTopUpReason}
            onChangeDebtAmount={props.onChangeDebtAmount}
            onChangeDebtReason={props.onChangeDebtReason}
            onTopUp={props.onTopUp}
            onPayDebt={props.onPayDebt}
          />
        )}
        {props.activeTab === 'packages' && (
          <PackagesSection
            packages={props.packages}
            options={props.options}
            selectedPackageDefinitionId={props.selectedPackageDefinitionId}
            balanceMinorUnits={props.balanceMinorUnits}
            currencyCode={props.currencyCode}
            canPurchase={props.canPurchase}
            onSelectOption={props.onSelectOption}
            onBuy={props.onBuy}
          />
        )}
        {props.activeTab === 'history' && (
          // TODO Task 7: заменить заглушки на оркестраторные пропсы (activeFilter/onFilterChange/hasMore/onLoadMore/loading)
          <HistorySection
            entries={props.recentEntries}
            currencyCode={props.currencyCode}
            activeFilter={null}
            onFilterChange={() => {}}
            hasMore={false}
            onLoadMore={() => {}}
            loading={false}
          />
        )}
      </div>
    </section>
  );
}
