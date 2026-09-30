import { useI18n } from '@afk4/i18n';
import { displayPhone } from '@afk4/formatting';
import { Cake, Smartphone } from 'lucide-react';
import { Button, Inspector, Money, StatusBadge, type Fact } from '@afk4/ui/react';
import { describeBirthday, type PlayerClientItem } from '../operatorHelpers';
import type { LedgerEntryDto, PlayerPackageDto } from '../operatorApiClients';
import { playerStatusLabel, type ClientLiveContext } from './playersModel';
import { WalletZone } from './WalletZone';
import { HistorySection } from './HistorySection';
import { clientMenuActions } from './ClientActionsMenu';
import { PackagesSection } from './PackagesSection';
import { ReputationCard } from './ReputationCard';
import type { ReputationController } from './useReputation';

// Сколько последних операций показываем в мини-истории — за остальным уводит «вся история →».
const RECENT_ENTRIES_LIMIT = 4;
const noop = () => {};

// Карточка выбранного клиента справа от таблицы — на инспекторе кита: кто это → баланс одной
// крупной цифрой → «Пополнить баланс» во всю ширину и частые действия рядом → факты (долг,
// придержанное, где играет, бронь) → секции. Презентационная: данные и запись денег живут в
// оркестраторе, здесь только колбэки. Чего нельзя по правам — не рисуется вовсе.
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
  topUpBlockedReason = null,
  onChangeTopUpAmount,
  onTopUp,
  onOpenDcTopUp,
  canPayDebt,
  onOpenPayDebt,
  canManageClient,
  canCorrect,
  canCreateReservation,
  canSellPackage,
  canStartSession,
  onCorrect,
  onCreateReservation,
  onSellPackage,
  onStartSession,
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
  // Пополнение принимают в открытой смене; пока её нет — причина словами рядом с кнопкой.
  topUpBlockedReason?: string | null;
  onChangeTopUpAmount: (value: string) => void;
  onTopUp: () => void;
  onOpenDcTopUp: () => void;
  canPayDebt: boolean;
  onOpenPayDebt: () => void;
  canManageClient: boolean;
  canCorrect: boolean;
  canCreateReservation: boolean;
  canSellPackage: boolean;
  canStartSession: boolean;
  onCorrect: () => void;
  onCreateReservation: () => void;
  onSellPackage: () => void;
  onStartSession: () => void;
  onEditProfile: () => void;
  onToggleActive: () => void;
  onOpenFullHistory: () => void;
  onClose: () => void;
  // Репутацию в сети спрашивает оркестратор — карточка только рисует ответ и кнопку.
  reputation: ReputationController;
}) {
  const { t, locale } = useI18n();
  const isInactive = !client.isActive;
  const birthday = client.birthDate ? describeBirthday(client.birthDate, locale) : null;
  const session = liveContext.session;
  const booking = liveContext.nextBooking;

  const facts: Fact[] = [];
  if (debtMinorUnits > 0) {
    facts.push({ label: t('op.players.wallet.debtLabel'), value: <Money minorUnits={debtMinorUnits} currencyCode={currencyCode} className="client-fact-debt" /> });
  }
  // Придержанное — только когда оно есть: у большинства клиентов это вечный ноль, и нулевая
  // строка заставляла бы каждый раз гадать, что она значит.
  if (heldMinorUnits > 0) {
    facts.push({ label: t('op.players.wallet.heldLabel'), value: <Money minorUnits={heldMinorUnits} currencyCode={currencyCode} /> });
  }
  if (session !== null) {
    facts.push({
      label: t('op.players.table.col.now'),
      value: `${session.seatName} · ${session.untilLabel ? t('op.players.context.until', { time: session.untilLabel }) : t('op.players.context.openTab')}`,
    });
  }
  if (booking !== null) {
    facts.push({ label: t('op.players.fact.booking'), value: booking.seatName ? `${booking.timeLabel} · ${booking.seatName}` : booking.timeLabel });
  }
  if (birthday) {
    facts.push({ label: t('op.players.fact.birthday'), value: t('op.players.birthday', { date: birthday.label, age: birthday.age }) });
  }

  // Частые действия — ровным рядом под главной. Посадить того, кто уже сидит, некуда.
  const payDebt = debtMinorUnits > 0 && canPayDebt ? <Button onClick={onOpenPayDebt}>{t('op.players.actions.payDebtBtn')}</Button> : null;
  const dcTopUp = canTopUp ? <Button onClick={onOpenDcTopUp}>{t('op.dc.topup.open')}</Button> : null;
  const seat = canStartSession && session === null ? <Button onClick={onStartSession}>{t('op.players.session.start')}</Button> : null;
  const hasSecondary = payDebt !== null || dcTopUp !== null || seat !== null;

  return (
    <Inspector
      className="client-inspector"
      title={client.name}
      status={(
        <>
          {isInactive && <StatusBadge tone="neutral">{playerStatusLabel('inactive', t)}</StatusBadge>}
          {birthday?.isToday && (
            <StatusBadge tone="success"><Cake size={12} aria-hidden="true" />{t('op.players.birthdayToday')}</StatusBadge>
          )}
          {/* Откуда взялась карточка. Стойке это меняет разговор: человека, который завёл себя
              сам из приложения, здесь никто не видел и документов его не сверял. */}
          {client.createdFromApp && (
            <StatusBadge tone="neutral" title={t('op.players.createdFromApp.hint')}>
              <Smartphone size={12} aria-hidden="true" />{t('op.players.createdFromApp')}
            </StatusBadge>
          )}
        </>
      )}
      subtitle={client.phoneNumber ? displayPhone(client.phoneNumber) : t('op.pos.cart.clientNoPhone')}
      menu={{
        label: t('op.players.menu.open'),
        actions: clientMenuActions(t, {
          isActive: client.isActive,
          canManageClient,
          onEditProfile,
          onToggleActive,
          canCreateReservation,
          onCreateReservation,
          canSellPackage,
          onSellPackage,
          canCorrect,
          onCorrect,
        }),
      }}
      close={{ label: t('common.close'), onClose }}
      figure={{ label: t('op.players.wallet.balanceLabel'), value: <Money minorUnits={balanceMinorUnits} currencyCode={currencyCode} /> }}
    >
      {/* Неактивному деньги не проводят: ни пополнения, ни долга — только «Активировать» в «⋯». */}
      {!isInactive && (canTopUp || hasSecondary) && (
        <Inspector.Actions
          primary={canTopUp ? <WalletZone topUpAmount={topUpAmount} onChangeTopUpAmount={onChangeTopUpAmount} onTopUp={onTopUp} blockedReason={topUpBlockedReason} /> : undefined}
          secondary={hasSecondary ? [payDebt, dcTopUp, seat] : []}
        />
      )}

      {facts.length > 0 && <Inspector.Facts items={facts} />}

      <ReputationCard controller={reputation} />

      <PackagesSection
        packages={packages}
        loading={packagesLoading}
        errorDetail={packagesErrorDetail}
        canSellPackage={canSellPackage}
        onSellPackage={onSellPackage}
      />

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
    </Inspector>
  );
}
