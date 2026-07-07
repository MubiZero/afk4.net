import { useI18n } from '@afk4/i18n';
import { CalendarClock, Play, X } from 'lucide-react';
import { initials, type PlayerClientItem } from '../operatorHelpers';
import type { LedgerEntryDto } from '../operatorApiClients';
import { Money } from '../operatorPrimitives';
import type { ClientLiveContext } from './playersModel';
import { WalletZone } from './WalletZone';
import { HistorySection } from './HistorySection';
import { ClientActionsMenu } from './ClientActionsMenu';

// Сколько последних операций показываем в мини-истории — за остальным уводит «вся история →».
const RECENT_ENTRIES_LIMIT = 4;
const TOPUP_PRESET_AMOUNTS = [50, 100, 200, 500];
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
  debtMinorUnits,
  currencyCode,
  recentEntries,
  topUpAmount,
  canTopUp,
  onChangeTopUpAmount,
  onTopUp,
  onPresetTopUp,
  canPayDebt,
  onOpenPayDebt,
  canManageClient,
  canCorrect,
  canCreateReservation,
  onCorrect,
  onCreateReservation,
  onSetPin,
  onEditProfile,
  onToggleActive,
  onOpenFullHistory,
  onClose,
}: {
  client: PlayerClientItem;
  liveContext: ClientLiveContext;
  balanceMinorUnits: number;
  debtMinorUnits: number;
  currencyCode: string;
  recentEntries: LedgerEntryDto[];
  topUpAmount: string;
  canTopUp: boolean;
  onChangeTopUpAmount: (value: string) => void;
  onTopUp: () => void;
  onPresetTopUp: (amount: number) => void;
  canPayDebt: boolean;
  onOpenPayDebt: () => void;
  canManageClient: boolean;
  canCorrect: boolean;
  canCreateReservation: boolean;
  onCorrect: () => void;
  onCreateReservation: () => void;
  onSetPin: () => void;
  onEditProfile: () => void;
  onToggleActive: () => void;
  onOpenFullHistory: () => void;
  onClose: () => void;
}) {
  const { t } = useI18n();
  const hasDebt = debtMinorUnits > 0;
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
            isActive={client.status !== 'inactive'}
            canManageClient={canManageClient}
            onEditProfile={onEditProfile}
            onSetPin={onSetPin}
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
        </div>

        <div className="wallet-sep" />

        <WalletZone
          debtMinorUnits={debtMinorUnits}
          topUpAmount={topUpAmount}
          canTopUp={canTopUp}
          onChangeTopUpAmount={onChangeTopUpAmount}
          onTopUp={onTopUp}
          presets={TOPUP_PRESET_AMOUNTS}
          onPreset={onPresetTopUp}
          canPayDebt={canPayDebt}
          onOpenPayDebt={onOpenPayDebt}
          canCorrect={canCorrect}
          onCorrect={onCorrect}
        />

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
