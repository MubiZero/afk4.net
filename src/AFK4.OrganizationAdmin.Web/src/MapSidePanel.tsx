import { useCallback, useEffect, useRef, useState, type ReactElement } from 'react';
import { BellRing, Check, Loader2, Plus, TriangleAlert, Wifi, WifiOff } from 'lucide-react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { Button, Inspector, Money, StatusBadge, useBlockedReason, type Fact, type RowAction, type StatusTone } from '@afk4/ui/react';
import { projectOperatorError } from './apiErrors';
import { formatBilledDuration } from './checkoutState';
import { type PaymentPartDto, type PlayerSearchResultDto, type SessionCheckoutQuoteResponse } from './operatorApiClients';
import type {
  Feedback,
  OperatorBackendContext,
  PcControlActionId,
  PcControlActionOptions,
  PcControlActionResult,
  SeatActionRequest,
  SeatActionResult,
  SessionBillingSelection,
} from './operatorTypes';
import type { SeatSummary, SeatTone } from './operatorData';
import { hasPermission, permissionNames } from './operatorPermissions';
import {
  appVersionsLabel,
  commandTypeLabel,
  commandLabel,
  createAuthenticatedOperatorClients,
  emptyFeedback,
  formatMinorUnits,
  guestBillingSelection,
  projectOperatorFacingError,
  type PlayerClientItem,
  projectPlayerClient,
  zoneLabel
} from './operatorHelpers';
import { CriticalActionConfirmation } from './operatorPrimitives';
import { PanelModal } from './PanelModal';
import { PaymentDialog, type PaymentBillLine } from './PaymentDialog';
import { PanelSelect } from './PanelSelect';
import { PcCommandDialog } from './pc/PcCommandDialog';
import { planBulk, type BulkPlan } from './pc/pcBulk';
import { DANGEROUS_PC_COMMANDS, PC_COMMAND_CONFIRM } from './pc/pcCommandCopy';
import type { PcCommandId } from './pc/pcCommandOptions';
import { buildSeatMenu, type SeatMenuItem } from './seatMenu';
import { seatTileLead } from './seatTilePresentation';
import { formatDurationCompact, isPendingSeatCommand } from './floorMapState';
import { createSessionStartSelection, SessionStartForm, type SessionStartClient, type SessionStartSelection } from './session/SessionStartForm';

// Местное настенное время HH:MM начала сессии — оператор читает зал в локальном времени.
function formatSessionClock(iso: string | null | undefined): string | null {
  if (!iso) {
    return null;
  }

  const at = new Date(iso);
  if (Number.isNaN(at.getTime())) {
    return null;
  }

  return `${String(at.getHours()).padStart(2, '0')}:${String(at.getMinutes()).padStart(2, '0')}`;
}

/**
 * Отклик на действие одной строкой под кнопками: пока команда в полёте — спиннер, при успехе —
 * галочка (гаснет сама), ошибку оставляем текстом: её надо прочитать (#34).
 */
function ActionFeedback({ feedback }: { feedback: Feedback }) {
  const { t } = useI18n();
  if (feedback.state === 'idle') {
    return null;
  }
  if (feedback.state === 'pending') {
    return (
      <div className="action-feedback pending" role="status" aria-live="polite">
        <Loader2 size={15} className="ui-spinner" aria-hidden="true" />
        <span>{feedback.label}</span>
      </div>
    );
  }
  if (feedback.state === 'confirmed') {
    return (
      <div className="action-feedback done" role="status" aria-live="polite">
        <span className="action-feedback-check" aria-hidden="true"><Check size={14} strokeWidth={3} /></span>
        <span>{t('op.map.panel.actionDone')}</span>
      </div>
    );
  }
  return (
    <div className="action-feedback failed" role="alert">
      <TriangleAlert size={15} aria-hidden="true" />
      <span>{feedback.detail || t('op.helper.feedback.failed', { label: feedback.label })}</span>
    </div>
  );
}

/**
 * "Завершить и принять оплату": fetches the read-only checkout quote, shows the
 * unified bill (Наиграно • Время • Снеки • Итого), and lets the operator settle
 * it with one or more split-payment parts (cash / card / deposit) before the PC
 * is locked. Confirm routes the parts through the shared seat-action handler.
 */
function CheckoutDialog({
  seat,
  backend,
  disabled,
  onCancel,
  onConfirm,
  onEndWithoutPayment
}: {
  seat: SeatSummary;
  backend: OperatorBackendContext;
  disabled: boolean;
  onCancel: () => void;
  onConfirm: (payments: PaymentPartDto[]) => void;
  onEndWithoutPayment: () => void;
}) {
  const { t } = useI18n();
  const [quote, setQuote] = useState<SessionCheckoutQuoteResponse | null>(null);
  const [status, setStatus] = useState<'loading' | 'ready' | 'failed'>('loading');
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let disposed = false;
    const sessionId = seat.activeSessionId;
    if (!sessionId) {
      setStatus('failed');
      setError(t('op.map.panel.noActiveSession'));
      return undefined;
    }

    setStatus('loading');
    setError(null);
    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    clients.sessions.getCheckoutQuote(sessionId)
      .then((result) => {
        if (disposed) {
          return;
        }

        setQuote(result);
        setStatus('ready');
      })
      .catch((fetchError) => {
        if (disposed) {
          return;
        }

        setStatus('failed');
        setError(projectOperatorError(fetchError, t).detail);
      });

    return () => {
      disposed = true;
    };
  }, [seat.activeSessionId, backend.config.platformBaseUrl, backend.session.accessToken]);

  const currencyCode = quote?.grandTotal?.currencyCode ?? '';
  const grandTotal = quote?.grandTotal?.minorUnits ?? 0;
  const walletBalance = quote?.walletBalance?.minorUnits ?? null;

  // Шапка-контекст: тариф и время старта — данные, что уже на руках (#34).
  const startedClock = formatSessionClock(seat.sessionStartedAtUtc);
  const contextParts = [
    seat.tariffName,
    startedClock ? t('op.checkout.startedAt', { time: startedClock }) : null
  ].filter((part): part is string => Boolean(part));

  // Окно говорит правду и про предоплаченную сессию: сколько сыграно, сколько уплачено вперёд и что
  // вернётся. «Время · 0м, 0 с.» у всех выглядело так, будто время не считалось вовсе (приёмка
  // 30.09.2026). Предоплаченное время — не строка счёта: к оплате по нему ноль, оно уже оплачено.
  const prepaid = quote?.prepaidCharged?.minorUnits ?? 0;
  const isGuestPrepaid = prepaid > 0 && quote?.playerAccountId == null;
  const notes: string[] = quote
    ? [
        t('op.checkout.played', { duration: formatBilledDuration(quote.playedSeconds ?? 0, t) }),
        ...(prepaid > 0
          ? [t(isGuestPrepaid ? 'op.checkout.prepaidCash' : 'op.checkout.prepaidWallet', { amount: formatMinorUnits(prepaid, currencyCode) })]
          : []),
        ...(prepaid > 0
          ? [(quote.prepaidRefund?.minorUnits ?? 0) > 0
            ? t('op.checkout.refundWallet', { amount: formatMinorUnits(quote.prepaidRefund?.minorUnits ?? 0, currencyCode) })
            : t(isGuestPrepaid ? 'op.checkout.refundNoneGuest' : 'op.checkout.refundNone')]
          : [])
      ]
    : [];

  // Позиции чека сессии: время (по наигранным секундам) + снеки/POS, если есть.
  const lines: PaymentBillLine[] = quote
    ? [
        ...(prepaid > 0 && (quote.timeCharge?.minorUnits ?? 0) === 0
          ? []
          : [{
            label: `${t('op.checkout.lineTime')} · ${formatBilledDuration(quote.billableSeconds ?? 0, t)}`,
            amountMinorUnits: quote.timeCharge?.minorUnits ?? 0
          }]),
        ...((quote.posTotal?.minorUnits ?? 0) > 0
          ? [{ label: t('op.map.panel.billableSnacks'), amountMinorUnits: quote.posTotal?.minorUnits ?? 0 }]
          : [])
      ]
    : [];

  return (
    <PanelModal
      title={t('op.map.panel.checkoutLabel')}
      subtitle={`${seat.name} · ${seat.playerDisplayName?.trim() || t('op.floor.player.guest')}`}
      onClose={onCancel}
      tone="warning"
    >
      <p className="checkout-subtitle">{t('op.map.panel.checkoutSubtitle')}</p>

      {status === 'loading' && <p className="checkout-loading">{t('op.map.panel.checkoutLoading')}</p>}
      {status === 'failed' && <p className="checkout-error">{error ?? t('op.map.panel.checkoutFailed')}</p>}

      {status === 'ready' && quote && (
        <PaymentDialog
          contextLine={contextParts.length > 0 ? contextParts.join(' · ') : undefined}
          notes={notes}
          lines={lines}
          dueLabel={t('op.map.panel.checkoutDue')}
          grandTotalMinorUnits={grandTotal}
          currencyCode={currencyCode}
          walletBalanceMinorUnits={walletBalance}
          allowSplit
          disabled={disabled}
          confirmVariant="accent"
          endWithoutPayment={{ label: t('op.map.panel.endWithoutPay'), onEnd: onEndWithoutPayment }}
          onCancel={onCancel}
          onConfirm={onConfirm}
        />
      )}
    </PanelModal>
  );
}

/**
 * «+15 / +30 мин» у гостя, заплатившего наличными: продление — это новая оплата у стойки, поэтому
 * до нажатия сервер называет точную сумму по тарифу самой сессии, и оператор берёт именно её.
 * Открытой смены нет — причина видна сразу, а не после нажатия.
 */
function GuestExtendDialog({
  seat,
  backend,
  minutes,
  disabled,
  onCancel,
  onConfirm
}: {
  seat: SeatSummary;
  backend: OperatorBackendContext;
  minutes: number;
  disabled: boolean;
  onCancel: () => void;
  onConfirm: (chargeMinorUnits: number) => void;
}) {
  const { t } = useI18n();
  const [quote, setQuote] = useState<{ minorUnits: number; currencyCode: string } | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let disposed = false;
    const sessionId = seat.activeSessionId;
    if (!sessionId) {
      setError(t('op.map.panel.noActiveSession'));
      return undefined;
    }
    setQuote(null);
    setError(null);
    createAuthenticatedOperatorClients(backend.config, backend.session).sessions.getExtendQuote(sessionId, minutes)
      .then((result) => { if (!disposed) setQuote({ minorUnits: result.charge.minorUnits, currencyCode: result.charge.currencyCode }); })
      .catch((fetchError) => { if (!disposed) setError(projectOperatorError(fetchError, t).detail); });
    return () => { disposed = true; };
  }, [seat.activeSessionId, minutes, backend.config.platformBaseUrl, backend.session.accessToken]);

  return (
    <PanelModal title={t('op.map.panel.guestExtendTitle')} subtitle={seat.name} onClose={onCancel} tone="warning">
      <p className="checkout-subtitle">{t('op.map.panel.guestExtendBody', { minutes })}</p>
      {quote === null && error === null && <p className="checkout-loading">{t('op.map.panel.checkoutLoading')}</p>}
      {error !== null && <p className="checkout-error" role="alert">{error}</p>}
      {quote !== null && (
        <p className="start-price start-cash" role="status">{t('op.map.panel.guestCashDue', { amount: formatMinorUnits(quote.minorUnits, quote.currencyCode) })}</p>
      )}
      <div className="critical-confirmation-actions">
        <Button onClick={onCancel} disabled={disabled}>{t('common.cancel')}</Button>
        <Button
          variant="primary"
          disabled={disabled || quote === null}
          onClick={() => quote !== null && onConfirm(quote.minorUnits)}
        >
          {quote === null ? t('op.checkout.finish') : t('op.checkout.confirmAmount', { amount: formatMinorUnits(quote.minorUnits, quote.currencyCode) })}
        </Button>
      </div>
    </PanelModal>
  );
}

/**
 * В каком положении место — от этого зависит одна главная кнопка панели (таблица состояний из
 * дизайн-прохода 29.09): свободное сажают, идущую сессию завершают и рассчитывают, открытый счёт
 * закрывают приёмом денег, паузу снимают, ожидающее ждёт, выключенный ПК будят, обслуживание
 * возвращают в зал. Тон карты огрубляет состояние (пауза и игра одинаково «занято»), поэтому
 * панель решает по своему, а не по тону.
 */
export type SeatPanelState = 'free' | 'session' | 'open-tab' | 'paused' | 'pending' | 'failed' | 'offline' | 'maintenance' | 'closed';

export function seatPanelState(seat: SeatSummary): SeatPanelState {
  if (isPendingSeatCommand(seat)) return 'pending';
  const hasSession = Boolean(seat.activeSessionId) || seat.hasActiveSession === true || seat.tone === 'active';
  if (hasSession) {
    if (seat.sessionState === 'Paused') return 'paused';
    return seatTileLead(seat).kind === 'postpaid' ? 'open-tab' : 'session';
  }
  if (seat.tone === 'ready') return 'free';
  if (seat.tone === 'failed') return 'failed';
  if (seat.tone === 'offline') return 'offline';
  // Серый «сервис» — и обслуживание, и ПК сверх тарифа, и ПК без киоска. У двух последних своя
  // строка-причина; вернуть в зал можно только уведённый туда клубом (maintenanceSinceUtc), а не
  // неодобренный ПК.
  return seat.isOutsidePlan || seat.isKioskAbsent ? 'closed' : 'maintenance';
}

const STATUS_TONE: Record<SeatTone, StatusTone> = {
  ready: 'success',
  active: 'accent',
  pending: 'warning',
  // Сбой команды — не авария: ПК на связи и принимает гостей. Красного на карте нет.
  failed: 'warning',
  offline: 'neutral',
  service: 'neutral'
};

// Эти пункты «Ещё» уже стоят на панели кнопкой — в меню их не дублируем.
const SHOWN_ON_PANEL = new Set(['start-guest', 'extend-15', 'extend-30', 'resolve-assistance']);

// Что повторяет «Повторить …» по упавшей команде (SeatStatusDto.lastFailedCommandType). Сервер
// называет только те команды, что администратор шлёт сам и может повторить той же кнопкой.
const RETRY: Partial<Record<string, { action: 'lock' | 'unlock' | Exclude<PcCommandId, 'wake' | 'message'>; label: MessageKey }>> = {
  lock: { action: 'lock', label: 'op.map.panel.retry.lock' },
  unlock: { action: 'unlock', label: 'op.map.panel.retry.unlock' },
  reboot: { action: 'reboot', label: 'op.map.panel.retry.reboot' },
  shutdown: { action: 'shutdown', label: 'op.map.panel.retry.shutdown' },
  'sign-out': { action: 'sign-out', label: 'op.map.panel.retry.signOut' },
  'maintenance-on': { action: 'maintenance-on', label: 'op.map.panel.retry.maintenanceOn' },
  'maintenance-off': { action: 'maintenance-off', label: 'op.map.panel.retry.maintenanceOff' }
};

export function MapSidePanel({
  seat,
  seats: floorSeats,
  currencyCode,
  backend,
  actionsEnabled,
  canUsePcControl,
  startRequestToken,
  onSeatAction,
  onPcControlAction,
  onResolveAssistance
}: {
  seat: SeatSummary;
  seats: SeatSummary[];
  currencyCode: string;
  backend: OperatorBackendContext | null;
  actionsEnabled: boolean;
  canUsePcControl: boolean;
  startRequestToken?: number;
  onSeatAction: (request: SeatActionRequest) => Promise<SeatActionResult>;
  onPcControlAction: (seat: SeatSummary, action: PcControlActionId, options?: PcControlActionOptions) => Promise<PcControlActionResult>;
  onResolveAssistance?: (seat: SeatSummary) => Promise<PcControlActionResult>;
}) {
  const { t } = useI18n();
  const session = backend?.session ?? null;
  const state = seatPanelState(seat);
  const lead = seatTileLead(seat);
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const [startSelection, setStartSelection] = useState<SessionStartSelection>(() => createSessionStartSelection());
  const [startClient, setStartClient] = useState<SessionStartClient | null>(null);
  const [startFormValid, setStartFormValid] = useState(true);
  // Сколько гость отдаст наличными за выбранное время — её видит форма и называет кнопка.
  const [startCharge, setStartCharge] = useState<number | null>(null);
  const [dialog, setDialog] = useState<'start' | 'checkout' | 'end-session' | 'transfer' | 'extend-cash' | null>(null);
  const [cashExtendMinutes, setCashExtendMinutes] = useState<15 | 30>(15);
  const [commandPlan, setCommandPlan] = useState<BulkPlan | null>(null);
  // Тот же вопрос, что «можно ли сюда посадить гостя» (см. isSeatReadyForGuest в floorMapState) —
  // ПК со сбоем прошлой команды, но на связи, годится и для переноса сессии.
  const transferCandidates = floorSeats.filter((candidate) =>
    candidate.id !== seat.id &&
    (candidate.tone === 'ready' || candidate.tone === 'failed') &&
    !candidate.activeSessionId);
  const [targetSeatId, setTargetSeatId] = useState(transferCandidates[0]?.id ?? '');
  const isBusy = feedback.state === 'pending';

  // Права решают, есть ли кнопка вообще; состояние места и связь — активна ли она.
  const can = {
    start: hasPermission(session, permissionNames.startSession),
    extend: hasPermission(session, permissionNames.extendSession),
    transfer: hasPermission(session, permissionNames.transferSession),
    end: hasPermission(session, permissionNames.endSession),
    pause: hasPermission(session, permissionNames.pauseSession),
    resolve: hasPermission(session, permissionNames.resolveAssistanceRequest) && onResolveAssistance !== undefined,
    dispatch: canUsePcControl && hasPermission(session, permissionNames.dispatchDeviceCommand),
    maintain: canUsePcControl && hasPermission(session, permissionNames.maintainDevice)
  };
  const pcAccess = { canDispatch: can.dispatch, canMaintain: can.maintain };
  // Консоль — место без агента: команд ПК и строки о нём у неё нет.
  const hasDevice = !seat.isConsole && Boolean(seat.deviceId);

  const billingSelection: SessionBillingSelection = {
    mode: startSelection.billingMode as SessionBillingSelection['mode'],
    tariffRuleVersionId: startSelection.tariffRuleVersionId,
    playerAccountId: startClient?.playerAccountId ?? null,
    tariffVersionId: startSelection.tariffVersionId,
    playerPackageId: startSelection.playerPackageId
  };

  const searchBillingPlayers = useCallback(async (query: string): Promise<PlayerClientItem[]> => {
    if (backend === null || !hasPermission(backend.session, permissionNames.viewPlayers)) {
      return [];
    }
    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    const players: PlayerSearchResultDto[] = await clients.players.searchPlayers(backend.branchId, query, 8);
    return players.map((player) => projectPlayerClient(player, t));
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, backend?.session.permissions, t]);

  const loadStartTariffs = useCallback(async () => {
    if (backend === null || !hasPermission(backend.session, permissionNames.viewTariffs)) return [];
    return createAuthenticatedOperatorClients(backend.config, backend.session).settings.getTariffOptions(backend.branchId);
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, backend?.session.permissions]);

  const loadStartPackages = useCallback(async (playerAccountId: string) => {
    if (backend === null || !hasPermission(backend.session, permissionNames.viewBilling)) return [];
    return createAuthenticatedOperatorClients(backend.config, backend.session).players.getPlayerPackages(playerAccountId);
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, backend?.session.permissions]);

  useEffect(() => {
    if (targetSeatId.length > 0 && transferCandidates.some((candidate) => candidate.id === targetSeatId)) {
      return;
    }

    setTargetSeatId(transferCandidates[0]?.id ?? '');
  }, [seat.id, floorSeats]);

  useEffect(() => {
    setDialog(null);
    setCommandPlan(null);
  }, [seat.id, seat.activeSessionId]);

  // Клик по «+» свободной плитки (App растит startRequestToken) — открыть запуск сессии сразу,
  // если место свободно. Реагируем ТОЛЬКО на реальное изменение токена, а не на каждый mount:
  // иначе при возврате на карту с другого таба (remount с ненулевым токеном) окно бы само
  // открывалось. Ref инициализируется текущим значением → первый прогон/remount ничего не делает.
  const lastStartTokenRef = useRef(startRequestToken);
  useEffect(() => {
    if (startRequestToken === lastStartTokenRef.current) {
      return;
    }
    lastStartTokenRef.current = startRequestToken;
    if (startRequestToken && seat.tone === 'ready' && !seat.activeSessionId) {
      setDialog('start');
    }
  }, [startRequestToken]);

  // Успех показываем галочкой и сами гасим — оператору не нужно его закрывать.
  // Ошибку оставляем на экране: её надо прочитать и решить.
  useEffect(() => {
    if (feedback.state !== 'confirmed') {
      return;
    }
    const timer = window.setTimeout(() => setFeedback(emptyFeedback), 1600);
    return () => window.clearTimeout(timer);
  }, [feedback]);

  const run = async (label: string, action: () => Promise<{ detail?: string }>) => {
    setDialog(null);
    setCommandPlan(null);
    setFeedback({ label, state: 'pending' });
    try {
      const result = await action();
      setFeedback({ label, state: 'confirmed', detail: result.detail });
    } catch (error) {
      setFeedback({ label, state: 'failed', detail: projectOperatorFacingError(error, t) });
    }
  };
  const runSeatAction = (label: string, request: SeatActionRequest) => run(label, () => onSeatAction(request));
  const runPc = (label: string, action: PcControlActionId, options?: PcControlActionOptions) =>
    run(label, () => onPcControlAction(seat, action, options));

  // Команда ПК из «Ещё»: опасная или с текстом спрашивает — тем же окном, что меню места на карте.
  const askPcCommand = (command: PcCommandId, label: string) => {
    if (PC_COMMAND_CONFIRM[command] === undefined) {
      void runPc(label, command);
      return;
    }
    setCommandPlan(planBulk([seat], command, pcAccess));
  };

  const runMenuItem = (item: SeatMenuItem) => {
    const label = t(item.feedbackKey);
    const menuRun = item.run;
    if (menuRun.kind === 'pause' || menuRun.kind === 'resume') {
      void runSeatAction(label, { type: menuRun.kind, seat });
    } else if (menuRun.kind === 'pc') {
      void runPc(label, menuRun.action);
    } else if (menuRun.kind === 'pc-command') {
      askPcCommand(menuRun.command, label);
    }
  };

  const extend = (minutes: 15 | 30) => {
    // Продление не несёт условий оплаты: тариф, способ и клиента сервер берёт из самой сессии.
    // Прежде сюда уходил выбор из формы «Новая сессия» — игрок или тариф, отмеченные там для
    // другого ПК, попадали в продление чужой сессии.
    const label = t(minutes === 15 ? 'op.map.panel.extend15Action' : 'op.map.panel.extend30Action');
    // Гость, заплативший наличными, доплачивает за продление: сумма называется до нажатия.
    const paysCash = seat.sessionBillingMode === 'prepaid_cash' && backend !== null;
    return (
      <Button
        key={`extend-${minutes}`}
        disabled={!actionsEnabled || isBusy}
        onClick={() => {
          if (paysCash) {
            setCashExtendMinutes(minutes);
            setDialog('extend-cash');
            return;
          }
          void runSeatAction(label, { type: 'extend', seat, minutes, billing: guestBillingSelection });
        }}
      >
        <Plus size={14} aria-hidden="true" />{label}
      </Button>
    );
  };
  const transferButton = can.transfer ? (
    <Button key="transfer" disabled={!actionsEnabled || isBusy || transferCandidates.length === 0} onClick={() => setDialog('transfer')}>
      {t('op.map.panel.transferOpen')}
    </Button>
  ) : null;
  // Гость на фиксированное время платит наличными сразу: кнопка называет сумму, которую надо взять.
  const startCashDue = startSelection.billingMode === 'guest' && !startSelection.isComp && startSelection.durationMode === 'fixed'
    ? startCharge
    : null;
  const startLabel = startSelection.durationMode === 'open'
    ? t('op.map.panel.startOpen')
    : startCashDue !== null
      ? t('op.map.panel.startCashCta', { amount: formatMinorUnits(startCashDue, currencyCode) })
      : t('op.map.panel.startCta', { duration: formatDurationCompact((startSelection.durationMinutes ?? 60) * 60, t) });
  const endLabel = state === 'open-tab' && seat.accruedCostMinorUnits != null
    ? t('op.map.panel.finishAndTake', { amount: formatMinorUnits(seat.accruedCostMinorUnits, currencyCode) })
    : t('op.map.panel.finishLabel');
  const openEnd = () => setDialog(backend !== null ? 'checkout' : 'end-session');
  const startGuestButton = (primaryVariant: boolean) => can.start ? (
    <Button key="start" variant={primaryVariant ? 'primary' : 'secondary'} block={primaryVariant} disabled={!actionsEnabled || isBusy} onClick={() => setDialog('start')}>
      {t('op.map.seatInvite')}
    </Button>
  ) : null;

  // Упавшая команда — тем же путём, что из «Ещё»: с тем же ключом повтора на нажатие и, для
  // перезагрузки или выключения, с тем же «точно?».
  const retry = seat.lastFailedCommandType ? RETRY[seat.lastFailedCommandType] : undefined;
  const canRetry = retry !== undefined && hasDevice && seat.isDeviceOnline !== false
    && (retry.action === 'maintenance-on' || retry.action === 'maintenance-off' ? can.maintain : can.dispatch);
  const retryCommand = () => {
    if (retry === undefined) return;
    const label = t(retry.label);
    if (retry.action === 'lock' || retry.action === 'unlock') {
      void runPc(label, retry.action);
    } else {
      askPcCommand(retry.action, label);
    }
  };
  const retryButton = (primaryVariant: boolean) => canRetry && retry !== undefined ? (
    <Button key="retry" variant={primaryVariant ? 'primary' : 'secondary'} block={primaryVariant} size={primaryVariant ? 'md' : 'sm'} disabled={!actionsEnabled || isBusy} onClick={retryCommand}>
      {primaryVariant ? t(retry.label) : t('op.map.panel.retryShort')}
    </Button>
  ) : null;

  // Одна главная кнопка по положению места и до трёх второстепенных рядом.
  let primary: ReactElement | null = null;
  let secondary: (ReactElement | null)[] = [];
  const shownOnPanel = new Set(SHOWN_ON_PANEL);
  if (state === 'failed' && canRetry) {
    // Сбой команды: главное — повторить её, но ПК на связи, и гостя посадить тоже можно.
    primary = retryButton(true);
    secondary = [startGuestButton(false)];
  } else if (state === 'free' || state === 'failed') {
    primary = startGuestButton(true);
  } else if (state === 'session' || state === 'open-tab') {
    primary = can.end ? <Button variant="primary" block disabled={!actionsEnabled || isBusy} onClick={openEnd}>{endLabel}</Button> : null;
    secondary = [can.extend ? extend(15) : null, can.extend ? extend(30) : null, transferButton];
  } else if (state === 'paused') {
    shownOnPanel.add('session-resume');
    primary = can.pause
      ? <Button variant="primary" block disabled={!actionsEnabled || isBusy} onClick={() => void runSeatAction(t('op.map.actionResume'), { type: 'resume', seat })}>{t('op.map.panel.resume')}</Button>
      : null;
    secondary = [can.end ? <Button key="end" disabled={!actionsEnabled || isBusy} onClick={openEnd}>{t('op.map.panel.finishLabel')}</Button> : null, transferButton];
  } else if (state === 'offline' && can.dispatch && hasDevice) {
    shownOnPanel.add('pc-wake');
    primary = <Button variant="primary" block disabled={isBusy} onClick={() => void runPc(t('op.pc.wake'), 'wake')}>{t('op.pc.wake')}</Button>;
  } else if (state === 'maintenance' && can.maintain && hasDevice && seat.maintenanceSinceUtc) {
    shownOnPanel.add('pc-maintenance-off');
    primary = <Button variant="primary" block disabled={isBusy} onClick={() => void runPc(t('op.pc.maintenanceOff'), 'maintenance-off')}>{t('op.pc.maintenanceOff')}</Button>;
  }
  const secondaryButtons = secondary.filter((node): node is ReactElement => node !== null).slice(0, 3) as [] | [ReactElement] | [ReactElement, ReactElement] | [ReactElement, ReactElement, ReactElement];
  // Сервер недоступен — объясняет одна строка у главной, а не каждая кнопка. Ожидающему месту
  // объяснять нечего: вместо кнопки стоит строка «ждём ответа ПК».
  const blocked = useBlockedReason(primary !== null && !actionsEnabled ? t('op.map.panel.confirmStatusUnavailable') : null);

  // «Ещё» — то же, что правый клик по плитке, без уже стоящего на панели. Опасное — в конец,
  // за разделителем.
  const menuItems = buildSeatMenu(seat, {
    actionsEnabled,
    canStart: can.start,
    canExtend: can.extend,
    canLockUnlock: can.dispatch,
    canResolveAssistance: can.resolve,
    canPause: can.pause,
    canMaintain: can.maintain
  }).flatMap((section) => section.items).filter((item) => !shownOnPanel.has(item.id));
  const toAction = (item: SeatMenuItem): RowAction => ({
    id: item.id,
    label: t(item.labelKey),
    hint: item.hintKey ? t(item.hintKey) : undefined,
    disabled: item.disabled || isBusy,
    danger: item.run.kind === 'pc-command' && DANGEROUS_PC_COMMANDS.has(item.run.command),
    onSelect: () => runMenuItem(item)
  });
  const menuActions = [...menuItems.map(toAction).filter((action) => !action.danger), ...menuItems.map(toAction).filter((action) => action.danger)];

  const startedAtClock = formatSessionClock(seat.sessionStartedAtUtc);
  const facts = ([
    seat.playerDisplayName ? { label: t('op.map.panel.playerLabel'), value: seat.playerDisplayName } : null,
    seat.tariffName ? { label: t('op.map.panel.tariffLabel'), value: seat.tariffName } : null,
    startedAtClock ? { label: t('op.map.panel.startedLabel'), value: <span className="ui-num">{startedAtClock}</span> } : null
  ] as (Fact | null)[]).filter((fact): fact is Fact => fact !== null);

  // Одна главная цифра: сколько осталось или сколько набежало. У остальных положений её нет —
  // статус уже сказал всё.
  const overtimeSeconds = lead.kind === 'prepaid' && lead.expired && seat.remainingSeconds != null ? Math.max(0, -seat.remainingSeconds) : 0;
  const figure = lead.kind === 'prepaid' && seat.remainingSeconds != null
    ? {
        label: t('op.map.panel.timeLeft'),
        value: lead.expired ? t('op.floor.duration.expiredShort') : formatDurationCompact(seat.remainingSeconds, t),
        hint: (
          <>
            <span className={`seat-timebar${lead.expired ? ' seat-timebar--expired' : lead.low ? ' seat-timebar--low' : ''}`} aria-hidden="true">
              <i style={{ width: `${Math.round(lead.barRatio * 100)}%` }} />
            </span>
            {overtimeSeconds >= 60 ? t('op.map.expiredAgo', { duration: formatDurationCompact(overtimeSeconds, t) }) : null}
          </>
        )
      }
    : lead.kind === 'postpaid' && seat.accruedCostMinorUnits != null
      ? { label: t('op.map.panel.accrued'), value: <Money minorUnits={seat.accruedCostMinorUnits} currencyCode={currencyCode} /> }
      : undefined;

  const callingForSeconds = seat.assistanceRequestedAtUtc
    ? Math.max(0, Math.round((Date.now() - new Date(seat.assistanceRequestedAtUtc).getTime()) / 1000))
    : null;
  const statusLabel = state === 'paused' ? t('op.map.panel.pausedStatus') : seat.stateLabel;

  return (
    <Inspector
      className="seat-inspector"
      title={seat.name}
      status={<StatusBadge tone={state === 'paused' ? 'warning' : STATUS_TONE[seat.tone]}>{statusLabel}</StatusBadge>}
      subtitle={zoneLabel(seat.zone, t)}
      menu={{ label: t('op.map.panel.more'), actions: menuActions }}
      figure={figure}
    >
      {/* Игрок позвал: сколько ждёт и «Подошёл» прямо тут — к зовущему идут первым. */}
      {callingForSeconds !== null && (
        <div className="seat-calling-strip" role="status">
          <BellRing size={14} aria-hidden="true" />
          <span>{t('op.map.panel.calling', { duration: formatDurationCompact(callingForSeconds, t) })}</span>
          {can.resolve && (
            <Button size="sm" disabled={!actionsEnabled || isBusy} onClick={() => void run(t('op.map.actionResolveAssistance'), () => onResolveAssistance!(seat))}>
              {t('op.map.panel.resolveAssistance')}
            </Button>
          )}
        </div>
      )}

      {/* С игроком за ПК упавшая команда — не главная кнопка (главная — сессия), а строка с
          «Повторить»: так видно, что разблокировка при старте не прошла и игрок сидит у экрана. */}
      {seat.lastFailedCommandType && state !== 'failed' && state !== 'pending' && (
        <div className="seat-failed-strip" role="status">
          <TriangleAlert size={14} aria-hidden="true" />
          <span>{t('op.map.panel.commandFailed', { command: commandTypeLabel(seat.lastFailedCommandType, t) })}</span>
          {retryButton(false)}
        </div>
      )}

      {state === 'pending' ? (
        <p className="seat-pending-line" role="status">
          <Loader2 size={14} className="ui-spinner" aria-hidden="true" />
          {commandLabel(seat.command, t)}
        </p>
      ) : state === 'closed' ? (
        <p className="ui-blocked-reason">{t(seat.isKioskAbsent ? 'op.map.panel.kioskAbsentHint' : 'op.map.panel.outsidePlanHint')}</p>
      ) : (primary !== null || secondaryButtons.length > 0) && (
        <Inspector.Actions primary={primary} hint={blocked.hint} secondary={secondaryButtons} />
      )}

      <ActionFeedback feedback={feedback} />

      {facts.length > 0 && <Inspector.Facts items={facts} />}

      {hasDevice && canUsePcControl && (
        <p className="seat-pc-line">
          {seat.isDeviceOnline === false ? <WifiOff size={13} aria-hidden="true" /> : <Wifi size={13} aria-hidden="true" />}
          <span className={seat.isDeviceOnline === false ? 'is-offline' : undefined}>
            {seat.isDeviceOnline === true ? t('op.helper.deviceStatus.online') : seat.isDeviceOnline === false ? t('op.helper.deviceStatus.offline') : t('op.map.panel.unknown')}
          </span>
          <span>{seat.isDeviceLocked === true ? t('op.helper.deviceStatus.locked') : seat.isDeviceLocked === false ? t('op.helper.deviceStatus.unlocked') : t('op.helper.deviceStatus.lockUnknown')}</span>
          {seat.deviceName && seat.deviceName !== seat.name && <span>{seat.deviceName}</span>}
          <span>{appVersionsLabel(seat.app, t)}</span>
        </p>
      )}

      {dialog === 'end-session' && (
        <CriticalActionConfirmation
          title={t('op.map.panel.stopConfirmTitle')}
          detail={`${seat.name} · ${seat.remaining}`}
          impact={t('op.map.panel.stopConfirmImpact')}
          confirmLabel={t('op.map.panel.stopConfirmBtn')}
          disabled={isBusy}
          onCancel={() => setDialog(null)}
          onConfirm={() => void runSeatAction(t('op.map.panel.stopAction'), { type: 'end', seat })}
        />
      )}
      {dialog === 'checkout' && backend !== null && (
        <CheckoutDialog
          seat={seat}
          backend={backend}
          disabled={isBusy}
          onCancel={() => setDialog(null)}
          onConfirm={(payments) => void runSeatAction(t('op.map.panel.paymentAction'), { type: 'checkout', seat, payments })}
          onEndWithoutPayment={() => void runSeatAction(t('op.map.panel.stopAction'), { type: 'end', seat })}
        />
      )}
      {dialog === 'extend-cash' && backend !== null && (
        <GuestExtendDialog
          seat={seat}
          backend={backend}
          minutes={cashExtendMinutes}
          disabled={isBusy}
          onCancel={() => setDialog(null)}
          onConfirm={(chargeMinorUnits) => void runSeatAction(
            t(cashExtendMinutes === 15 ? 'op.map.panel.extend15Action' : 'op.map.panel.extend30Action'),
            { type: 'extend', seat, minutes: cashExtendMinutes, billing: guestBillingSelection, expectedChargeMinorUnits: chargeMinorUnits }
          )}
        />
      )}
      {dialog === 'transfer' && (
        <PanelModal title={t('op.map.panel.transferTitle')} subtitle={seat.name} onClose={() => setDialog(null)}>
          <PanelSelect
            ariaLabel={t('op.map.panel.transferTo')}
            value={targetSeatId}
            placeholder={t('op.map.panel.noFreePc')}
            options={transferCandidates.map((candidate) => ({ value: candidate.id, label: candidate.name }))}
            onChange={setTargetSeatId}
          />
          <div className="critical-confirmation-actions">
            <Button onClick={() => setDialog(null)}>{t('common.cancel')}</Button>
            <Button
              variant="primary"
              disabled={targetSeatId.length === 0 || isBusy}
              onClick={() => void runSeatAction(t('op.map.panel.transferAction'), { type: 'transfer', seat, targetSeatId })}
            >
              {t('op.map.panel.transferAction')}
            </Button>
          </div>
        </PanelModal>
      )}
      {commandPlan !== null && (
        <PcCommandDialog
          plan={commandPlan}
          onCancel={() => setCommandPlan(null)}
          onConfirm={(text) => void runPc(t(PC_COMMAND_CONFIRM[commandPlan.command]!.confirm), commandPlan.command as PcControlActionId, commandPlan.command === 'message' ? { text } : undefined)}
        />
      )}

      {dialog === 'start' && (
      <PanelModal
        title={t('op.map.panel.startSessionTitle')}
        subtitle={seat.name}
        onClose={() => setDialog(null)}
      >
        <SessionStartForm
          seatName={seat.name}
          currencyCode={currencyCode}
          disabled={!actionsEnabled || isBusy}
          value={startSelection}
          onChange={setStartSelection}
          selectedClient={startClient}
          onSelectedClientChange={setStartClient}
          searchClients={searchBillingPlayers}
          loadTariffs={loadStartTariffs}
          loadPackages={loadStartPackages}
          onValidityChange={(valid) => setStartFormValid(valid)}
          onChargeChange={setStartCharge}
        />
        <div className="critical-confirmation-actions">
          <Button onClick={() => setDialog(null)} disabled={isBusy}>{t('common.cancel')}</Button>
          <Button
            variant="primary"
            disabled={!actionsEnabled || !can.start || !startFormValid || isBusy}
            onClick={() => void runSeatAction(startLabel, {
              type: 'start', seat, billing: billingSelection,
              durationMode: startSelection.durationMode === 'open' ? 'open' : 'fixed',
              durationMinutes: startSelection.durationMinutes,
              isComp: startSelection.isComp,
              compReason: startSelection.compReason,
              expectedChargeMinorUnits: startCashDue
            })}
          >
            {startLabel}
          </Button>
        </div>
      </PanelModal>
      )}
    </Inspector>
  );
}
