import { useI18n } from '@afk4/i18n';
import { CalendarClock } from 'lucide-react';
import type { PlayerClientItem } from '../operatorHelpers';
import type { LedgerEntryDto, PackageOptionDto, PlayerPackageDto } from '../operatorApiClients';
import { EmptyState } from '../operatorPrimitives';
import { playerStatusLabel, type ClientLiveContext } from './playersModel';
import { ClientContextStrip } from './ClientContextStrip';
import { WalletZone } from './WalletZone';
import { PackagesSection } from './PackagesSection';
import { HistorySection } from './HistorySection';
import { ClientActionsMenu } from './ClientActionsMenu';

// Первые две буквы имени как аватар-заглушка.
function initials(name: string): string {
  return name
    .split(' ')
    .map((part) => part[0])
    .join('')
    .slice(0, 2)
    .toUpperCase() || '—';
}

// Центральная карточка-воркспейс (tabless): личность → зона денег → низ в две колонки
// Пакеты | История. Отдельного правого рейла истории больше нет — журнал живёт правой колонкой
// карточки. Данные/фильтр/пагинация держит оркестратор.
export function ClientDetail(props: {
  client: PlayerClientItem | null;
  // Список клиентов ещё грузится: не показываем «нет выбранного клиента», иначе пустая карточка
  // мигает до прихода данных (см. isLoading в ClientList).
  isLoading: boolean;
  liveContext: ClientLiveContext;
  balanceMinorUnits: number;
  debtMinorUnits: number;
  packageCount: number;
  currencyCode: string;
  packages: PlayerPackageDto[];
  options: PackageOptionDto[];
  ledgerEntries: LedgerEntryDto[];
  ledgerFilter: string | null;
  ledgerHasMore: boolean;
  ledgerLoading: boolean;
  onLedgerFilterChange: (entryType: string | null) => void;
  onLedgerLoadMore: () => void;
  selectedPackageDefinitionId: string;
  packageBusy: boolean;
  packagesLoading: boolean;
  topUpAmount: string;
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
  onChangeTopUpAmount: (value: string) => void;
  onTopUp: () => void;
  onOpenPayDebt: () => void;
  onSelectOption: (packageDefinitionId: string) => void;
  onBuy: () => void;
  onCreateReservation: () => void;
}) {
  const { t } = useI18n();
  const { client } = props;

  if (client === null) {
    // Пока грузимся — держим панель пустой (без layout-jump), но без «нет выбранного клиента»,
    // чтобы не мигала на входе. Empty-state показываем только когда загрузка устаканилась.
    return (
      <section className="clients-panel clients-detail-panel" aria-hidden={props.isLoading || undefined}>
        {!props.isLoading && (
          <EmptyState
            title={t('op.players.profile.empty')}
            description={t('op.players.profile.emptyNote')}
          />
        )}
      </section>
    );
  }

  return (
    <section className="clients-panel clients-detail-panel">
      <div className="clients-detail-top">
        <header className="client-detail-head">
          <div className="client-avatar">{initials(client.name)}</div>
          <div className="client-detail-ident">
            {client.status !== 'active' && (
              <span className={`client-detail-status is-${client.status}`}>{playerStatusLabel(client.status, t)}</span>
            )}
            <strong>{client.name}</strong>
            <em>{client.phoneNumber || t('op.pos.cart.clientNoPhone')}</em>
            <ClientContextStrip context={props.liveContext} />
          </div>
          <div className="client-detail-actions">
            <button
              type="button"
              className="ui-btn"
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

        <WalletZone
          balanceMinorUnits={props.balanceMinorUnits}
          debtMinorUnits={props.debtMinorUnits}
          currencyCode={props.currencyCode}
          topUpAmount={props.topUpAmount}
          canTopUp={props.canTopUp}
          onChangeTopUpAmount={props.onChangeTopUpAmount}
          onTopUp={props.onTopUp}
          canPayDebt={props.canPayDebt}
          onOpenPayDebt={props.onOpenPayDebt}
          canCorrect={props.canCorrect}
          onCorrect={props.onCorrect}
        />
      </div>

      <div className="clients-detail-split">
        <section className="clients-subpanel">
          <header className="clients-subpanel-head">
            <span>{t('op.players.tabs.packages')}</span>
            {props.packageCount > 0 && (
              <span className="ui-chip ui-chip--status ui-chip--xs is-neutral" aria-hidden="true">
                {props.packageCount}
              </span>
            )}
          </header>
          <div className="clients-subpanel-body">
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
          </div>
        </section>

        <section className="clients-subpanel">
          <header className="clients-subpanel-head">
            <span>{t('op.players.ledgerRail.title')}</span>
          </header>
          <div className="clients-subpanel-body">
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
          </div>
        </section>
      </div>
    </section>
  );
}
