import { useI18n } from '@afk4/i18n';
import { CalendarClock } from 'lucide-react';
import type { PlayerClientItem } from '../operatorHelpers';
import { formatMinorUnits } from '../operatorHelpers';
import type { LedgerEntryDto, PackageOptionDto, PlayerPackageDto } from '../operatorApiClients';
import { EmptyState } from '../operatorPrimitives';
import { playerStatusLabel, type ClientLiveContext } from './playersModel';
import { ClientContextStrip } from './ClientContextStrip';
import { WalletSection } from './WalletSection';
import { PackagesSection } from './PackagesSection';
import { HistorySection } from './HistorySection';
import { ClientActionsMenu } from './ClientActionsMenu';

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
  // На широком экране полный журнал живёт в постоянном правом рейле: вкладка «История»
  // и мини-лента «Кошелька» здесь скрываются, чтобы не дублировать его.
  showLedgerRail: boolean;
  // Кросс-контекст: играет ли сейчас и ближайшая бронь (пустой объект = ничего не показываем).
  liveContext: ClientLiveContext;
  balanceMinorUnits: number;
  debtMinorUnits: number;
  packageCount: number;
  currencyCode: string;
  packages: PlayerPackageDto[];
  options: PackageOptionDto[];
  ledgerEntries: LedgerEntryDto[];
  recentEntries: LedgerEntryDto[];
  ledgerFilter: string | null;
  ledgerHasMore: boolean;
  ledgerLoading: boolean;
  onLedgerFilterChange: (entryType: string | null) => void;
  onLedgerLoadMore: () => void;
  selectedPackageDefinitionId: string;
  packageBusy: boolean;
  packagesLoading: boolean;
  topUpAmount: string;
  topUpReason: string;
  debtAmount: string;
  debtReason: string;
  canTopUp: boolean;
  canPayDebt: boolean;
  canPurchase: boolean;
  canCreateReservation: boolean;
  canManageClient: boolean;
  onSetPin: () => void;
  onEditProfile: () => void;
  onToggleActive: () => void;
  canCorrect: boolean;
  onCorrect: () => void;
  canRefund: boolean;
  onRefund: (entry: LedgerEntryDto) => void;
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
    ...(props.showLedgerRail ? [] : [{ id: 'history' as const, label: t('op.players.tabs.history') }]),
  ];
  // Если рейл забрал «Историю», а активной была именно она — показываем «Кошелёк».
  const activeTab: ClientDetailTab = props.showLedgerRail && props.activeTab === 'history' ? 'wallet' : props.activeTab;

  return (
    <section className="clients-panel clients-detail-panel">
      <header className="client-detail-head">
        <div className="client-avatar">{initials(client.name)}</div>
        <div className="client-detail-ident">
          {client.status !== 'active' && (
            <span className={`client-detail-status is-${client.status}`}>{playerStatusLabel(client.status, t)}</span>
          )}
          <strong>{client.name}</strong>
          <em>{client.phoneNumber || t('op.pos.cart.clientNoPhone')}</em>
        </div>
        <div className="client-detail-actions">
          <button
            type="button"
            className="client-detail-reservation"
            disabled={!props.canCreateReservation}
            onClick={props.onCreateReservation}
          >
            <CalendarClock size={15} aria-hidden="true" />
            {t('op.players.detail.reservationBtn')}
          </button>
          {props.canManageClient && (
            <ClientActionsMenu
              isActive={client.status !== 'inactive'}
              onEditProfile={props.onEditProfile}
              onSetPin={props.onSetPin}
              onToggleActive={props.onToggleActive}
            />
          )}
        </div>
      </header>

      {client.status === 'inactive' && (
        <div className="client-detail-banner" role="status">
          {t('op.players.detail.deactivatedBanner')}
        </div>
      )}

      <ClientContextStrip context={props.liveContext} />

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
            aria-selected={activeTab === tab.id}
            className={`client-detail-tab${activeTab === tab.id ? ' active' : ''}`}
            onClick={() => props.onSelectTab(tab.id)}
          >
            {tab.label}
          </button>
        ))}
      </div>

      <div className="client-detail-content">
        {activeTab === 'wallet' && (
          <WalletSection
            debtMinorUnits={props.debtMinorUnits}
            currencyCode={props.currencyCode}
            recentEntries={props.recentEntries}
            showRecent={!props.showLedgerRail}
            onShowHistory={() => props.onSelectTab('history')}
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
            canCorrect={props.canCorrect}
            onCorrect={props.onCorrect}
          />
        )}
        {activeTab === 'packages' && (
          <PackagesSection
            packages={props.packages}
            options={props.options}
            selectedPackageDefinitionId={props.selectedPackageDefinitionId}
            balanceMinorUnits={props.balanceMinorUnits}
            currencyCode={props.currencyCode}
            canPurchase={props.canPurchase}
            busy={props.packageBusy}
            loading={props.packagesLoading}
            onSelectOption={props.onSelectOption}
            onBuy={props.onBuy}
          />
        )}
        {activeTab === 'history' && (
          <HistorySection
            entries={props.ledgerEntries}
            currencyCode={props.currencyCode}
            activeFilter={props.ledgerFilter}
            onFilterChange={props.onLedgerFilterChange}
            hasMore={props.ledgerHasMore}
            onLoadMore={props.onLedgerLoadMore}
            loading={props.ledgerLoading}
            canRefund={props.canRefund}
            onRefund={props.onRefund}
          />
        )}
      </div>
    </section>
  );
}
