import { useI18n } from '@afk4/i18n';
import { CalendarClock, Play, X } from 'lucide-react';
import { initials, type PlayerClientItem } from '../operatorHelpers';
import type { LedgerEntryDto, PlayerPackageDto } from '../operatorApiClients';
import { Money } from '../operatorPrimitives';
import { playerStatusLabel, type ClientLiveContext } from './playersModel';
import { WalletZone } from './WalletZone';
import { HistorySection } from './HistorySection';
import { ClientActionsMenu } from './ClientActionsMenu';
import { PackagesSection } from './PackagesSection';
import { ReputationCard } from './ReputationCard';
import type { ReputationController } from './useReputation';

// Сколько последних операций показываем в мини-истории — за остальным уводит «вся история →».
const RECENT_ENTRIES_LIMIT = 4;
const noop = () => {};

// Узкая правая панель выбранного клиента (mock-v7) — замена вертикального разреза ClientDetail
// рядом с широкой ClientsTable (Task 8 удалит ClientDetail/ClientList). Без сплита пакеты/история
// и без продажи пакетов (та уходит в Кассу) — только деньги (баланс-герой + долг-callout + форма
// пополнения) и мини-история. Презентационный: данные и money-write живут в оркестраторе, этот
// компонент только зовёт переданные колбэки.
export function ClientDrawer({
  client,
  liveContext,
  balanceMinorUnits,
  heldMinorUnits,
  debtMinorUnits,
  currencyCode,
  recentEntries,
  packages,
  packagesLoading,
  packagesErrorDetail,
  topUpAmount,
  canTopUp,
  onChangeTopUpAmount,
  onTopUp,
  onOpenDcTopUp,
  canPayDebt,
  onOpenPayDebt,
  canManageClient,
  canCorrect,
  canCreateReservation,
  onCorrect,
  onCreateReservation,
  onEditProfile,
  onToggleActive,
  onOpenFullHistory,
  onClose,
  reputation,
}: {
  client: PlayerClientItem;
  liveContext: ClientLiveContext;
  balanceMinorUnits: number;
  // Придержано под брони. Из остатка уже вычтено — это объяснение, куда делась часть денег,
  // а не второй кошелёк.
  heldMinorUnits: number;
  debtMinorUnits: number;
  currencyCode: string;
  recentEntries: LedgerEntryDto[];
  packages: PlayerPackageDto[];
  packagesLoading: boolean;
  packagesErrorDetail?: string;
  topUpAmount: string;
  canTopUp: boolean;
  onChangeTopUpAmount: (value: string) => void;
  onTopUp: () => void;
  onOpenDcTopUp: () => void;
  canPayDebt: boolean;
  onOpenPayDebt: () => void;
  canManageClient: boolean;
  canCorrect: boolean;
  canCreateReservation: boolean;
  onCorrect: () => void;
  onCreateReservation: () => void;
  onEditProfile: () => void;
  onToggleActive: () => void;
  onOpenFullHistory: () => void;
  onClose: () => void;
  // Репутацию в сети спрашивает оркестратор — карточка только рисует ответ и кнопку.
  reputation: ReputationController;
}) {
  const { t } = useI18n();
  const hasDebt = debtMinorUnits > 0;
  const hasHeld = heldMinorUnits > 0;
  const isInactive = !client.isActive;
  // Триггер «⋯» показываем, если у оператора есть ХОТЯ БЫ одно из трёх прав — иначе меню
  // рендерится пустым (см. ClientActionsMenu), а кнопка без пунктов бесполезна.
  const showActionsMenu = canManageClient || canCreateReservation || canCorrect;

  return (
    <aside className="drawer-panel">
      <div className="drawer-head">
        <div className="drawer-av" aria-hidden="true">{initials(client.name)}</div>
        <div className="drawer-id">
          <div className="drawer-name">{client.name}</div>
          <div className="drawer-phone">{client.phoneNumber || t('op.pos.cart.clientNoPhone')}</div>
        </div>
        {showActionsMenu && (
          <ClientActionsMenu
            isActive={client.isActive}
            canManageClient={canManageClient}
            onEditProfile={onEditProfile}
            onToggleActive={onToggleActive}
            canCreateReservation={canCreateReservation}
            onCreateReservation={onCreateReservation}
            canCorrect={canCorrect}
            onCorrect={onCorrect}
          />
        )}
        <button type="button" className="drawer-ic" aria-label={t('common.close')} onClick={onClose}>
          <X size={16} aria-hidden="true" />
        </button>
      </div>

      <div className="drawer-context">
        {isInactive && (
          <span className="status-pill neutral">{playerStatusLabel('inactive', t)}</span>
        )}
        {liveContext.session !== null ? (
          <span className="status-pill ok">
            <Play size={12} aria-hidden="true" />
            {t('op.players.context.playingOn', { seat: liveContext.session.seatName })}
            {' · '}
            {liveContext.session.untilLabel
              ? t('op.players.context.until', { time: liveContext.session.untilLabel })
              : t('op.players.context.openTab')}
          </span>
        ) : (
          <span className="status-pill neutral">
            <Play size={12} aria-hidden="true" />
            {t('op.players.context.notPlaying')}
          </span>
        )}
        {liveContext.nextBooking !== null ? (
          <span className="status-pill neutral">
            <CalendarClock size={12} aria-hidden="true" />
            {t('op.players.context.nextBooking', { time: liveContext.nextBooking.timeLabel })}
            {liveContext.nextBooking.seatName ? ` · ${liveContext.nextBooking.seatName}` : ''}
          </span>
        ) : (
          <span className="status-pill neutral">
            <CalendarClock size={12} aria-hidden="true" />
            {t('op.players.context.noBooking')}
          </span>
        )}
      </div>

      <div className="drawer-body">
        <div className={`wallet-money${hasDebt ? ' has-debt' : ''}`}>
          <div className="wallet-balance">
            <span className="eyebrow">{t('op.players.wallet.balanceLabel')}</span>
            <span className="val"><Money minorUnits={balanceMinorUnits} currencyCode={currencyCode} /></span>
          </div>

          {hasDebt && (
            <div className="wallet-debt">
              <span className="eyebrow">{t('op.players.wallet.debtLabel')}</span>
              <span className="val"><Money minorUnits={debtMinorUnits} currencyCode={currencyCode} /></span>
            </div>
          )}

          {/* Третья величина появляется, только когда деньги действительно придержаны: у
              большинства клиентов это вечный ноль, а нулевая строка рядом с остатком заставляет
              оператора каждый раз спрашивать себя, что она значит. */}
          {hasHeld && (
            <div className="wallet-held">
              <span className="eyebrow">{t('op.players.wallet.heldLabel')}</span>
              <span className="val"><Money minorUnits={heldMinorUnits} currencyCode={currencyCode} /></span>
              <small>{t('op.players.wallet.heldHint')}</small>
            </div>
          )}
        </div>

        <div className="wallet-sep" />

        {!isInactive && (
          <>
            <WalletZone
              debtMinorUnits={debtMinorUnits}
              topUpAmount={topUpAmount}
              canTopUp={canTopUp}
              onChangeTopUpAmount={onChangeTopUpAmount}
              onTopUp={onTopUp}
              onOpenDcTopUp={onOpenDcTopUp}
              canPayDebt={canPayDebt}
              onOpenPayDebt={onOpenPayDebt}
              canCorrect={canCorrect}
              onCorrect={onCorrect}
            />

            <div className="wallet-sep" />
          </>
        )}

        <ReputationCard controller={reputation} />

        <div className="wallet-sep" />

        <PackagesSection packages={packages} loading={packagesLoading} errorDetail={packagesErrorDetail} />

        <div className="wallet-sep" />

        <HistorySection
          entries={recentEntries}
          currencyCode={currencyCode}
          activeFilter={null}
          onFilterChange={noop}
          hasMore={false}
          onLoadMore={noop}
          loading={false}
          canRefund={false}
          onRefund={noop}
          limit={RECENT_ENTRIES_LIMIT}
          onOpenFull={onOpenFullHistory}
        />
      </div>
    </aside>
  );
}
