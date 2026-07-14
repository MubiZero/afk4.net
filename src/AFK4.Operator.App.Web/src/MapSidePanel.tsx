import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react';
import { ArrowRightLeft, Check, Loader2, Lock, MonitorCheck, Plus, ReceiptText, TriangleAlert, Unlock, Wifi, WifiOff, Wrench } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { projectOperatorError } from './apiErrors';
import { formatBilledDuration } from './checkoutState';
import {
  type PaymentPartDto,
  type PlayerPackageDto,
  type PlayerSearchResultDto,
  type SessionCheckoutQuoteResponse,
  type TariffOptionDto
} from './operatorApiClients';
import type {
  Feedback,
  LoadStatus,
  OperatorBackendContext,
  PcControlActionId,
  PcControlActionResult,
  SeatActionRequest,
  SeatActionResult,
  SessionBillingModeId,
  SessionBillingSelection,
  SessionStartDurationMode
} from './operatorTypes';
import type { SeatSummary } from './operatorData';
import { hasPermission, permissionNames } from './operatorPermissions';
import {
  appVersionsLabel,
  billingModeOptions,
  commandLabel,
  createAuthenticatedOperatorClients,
  defaultTariffRuleVersionId,
  guestBillingSelection,
  emptyFeedback,
  formatMinorUnits,
  isPendingSeatCommand,
  playerPackageLabel,
  projectOperatorFacingError,
  readNumber,
  readString,
  tariffOptionLabel,
  toneLabel,
  type PlayerClientItem,
  projectPlayerClient,
  zoneLabel
} from './operatorHelpers';
import { CriticalActionConfirmation } from './operatorPrimitives';
import { PanelModal } from './PanelModal';
import { PaymentDialog, type PaymentBillLine } from './PaymentDialog';
import { PanelSelect } from './PanelSelect';
import { seatTileLead } from './seatTilePresentation';
import { formatDurationCompact } from './floorMapState';
import { ClientPicker } from './booking/ClientPicker';

// Чипы длительности старта сессии (минуты). «Открытый счёт» добавляется отдельной кнопкой.
const START_DURATIONS = [30, 60, 120, 180] as const;

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
 * Визуальный отклик на действие вместо строки текста: пока команда в полёте — спиннер,
 * при успехе — зелёная галочка (сама гаснет через эффект выше). Ошибку показываем текстом:
 * её надо прочитать (#34). Так подтверждение читается «языком», а не абзацем.
 */
function ActionFeedback({ feedback }: { feedback: Feedback }) {
  const { t } = useI18n();
  if (feedback.state === 'idle') {
    return null;
  }
  if (feedback.state === 'pending') {
    return (
      <div className="action-feedback pending" role="status" aria-live="polite">
        <Loader2 size={15} className="spin" aria-hidden="true" />
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

  // Позиции чека сессии: время (по наигранным секундам) + снеки/POS, если есть.
  const lines: PaymentBillLine[] = quote
    ? [
        {
          label: `${t('op.checkout.lineTime')} · ${formatBilledDuration(quote.billableSeconds ?? 0, t)}`,
          amountMinorUnits: quote.timeCharge?.minorUnits ?? 0
        },
        ...((quote.posTotal?.minorUnits ?? 0) > 0
          ? [{ label: t('op.map.panel.billableSnacks'), amountMinorUnits: quote.posTotal?.minorUnits ?? 0 }]
          : [])
      ]
    : [];

  return (
    <PanelModal
      title={t('op.map.panel.checkoutLabel')}
      subtitle={`${seat.name} · ${seat.player}`}
      onClose={onCancel}
      tone="warning"
    >
      <p className="checkout-subtitle">{t('op.map.panel.checkoutSubtitle')}</p>

      {status === 'loading' && <p className="checkout-loading">{t('op.map.panel.checkoutLoading')}</p>}
      {status === 'failed' && <p className="checkout-error">{error ?? t('op.map.panel.checkoutFailed')}</p>}

      {status === 'ready' && quote && (
        <PaymentDialog
          contextLine={contextParts.length > 0 ? contextParts.join(' · ') : undefined}
          lines={lines}
          dueLabel={t('op.map.panel.checkoutDue')}
          grandTotalMinorUnits={grandTotal}
          currencyCode={currencyCode}
          walletBalanceMinorUnits={walletBalance}
          allowSplit
          disabled={disabled}
          confirmVariant="danger"
          endWithoutPayment={{ label: t('op.map.panel.endWithoutPay'), onEnd: onEndWithoutPayment }}
          onCancel={onCancel}
          onConfirm={onConfirm}
        />
      )}
    </PanelModal>
  );
}

export function MapSidePanel({
  seat,
  seats: floorSeats,
  currencyCode,
  backend,
  actionsEnabled,
  canUsePcControl,
  startRequestToken,
  onSeatAction,
  onPcControlAction
}: {
  seat: SeatSummary;
  seats: SeatSummary[];
  currencyCode: string;
  backend: OperatorBackendContext | null;
  actionsEnabled: boolean;
  canUsePcControl: boolean;
  startRequestToken?: number;
  onSeatAction: (request: SeatActionRequest) => Promise<SeatActionResult>;
  onPcControlAction: (seat: SeatSummary, action: PcControlActionId) => Promise<PcControlActionResult>;
}) {
  const { t } = useI18n();
  const session = backend?.session ?? null;
  const lead = seatTileLead(seat);
  // Сессия с оплаченным временем дотикала до нуля: «осталось» больше не уместно — это «Время вышло».
  const isExpired = lead.kind === 'prepaid' && lead.expired === true;
  // Просрочка в секундах (сколько ПК уже сидит занятым после нуля) — для строки «вышло N назад».
  const overtimeSeconds = isExpired && seat.remainingSeconds != null ? Math.max(0, -seat.remainingSeconds) : 0;
  // Plain-статус (нет связи / обслуживание / ожидание) уже назван чипом справа. Значение-герой
  // показываем, только если оно добавляет конкретику сверх чипа (как «Нет связи с ПК» к «Нет
  // связи»); если совпадает с чипом (как «Обслуживание»), это дубль — прячем.
  const plainStatusRedundant = lead.kind === 'plain' && seat.remaining === toneLabel(seat.tone, t);
  // Подпись над герой-значением — только для чисел (осталось / открытый счёт); у plain-статуса
  // и истёкшей сессии подписи нет (чип/значение говорят сами за себя).
  const heroLabel = lead.kind === 'prepaid'
    ? (lead.expired ? null : t('op.map.seatLeft'))
    : lead.kind === 'postpaid'
      ? t('op.map.seatOpenTab')
      : null;
  const hasDevice = Boolean(seat.deviceId) || Boolean(seat.deviceName);
  const connectionLabel = seat.isDeviceOnline === true
    ? t('op.helper.deviceStatus.online')
    : seat.isDeviceOnline === false
      ? t('op.helper.deviceStatus.offline')
      : t('op.map.panel.unknown');
  const lockLabel = seat.isDeviceLocked === true
    ? t('op.helper.deviceStatus.locked')
    : seat.isDeviceLocked === false
      ? t('op.helper.deviceStatus.unlocked')
      : t('op.helper.deviceStatus.lockUnknown');
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  // Отклик команд блокировки/разблокировки ПК: спиннер→галочка на самой кнопке.
  const [pcFeedback, setPcFeedback] = useState<Feedback>(emptyFeedback);
  const [billingMode, setBillingMode] = useState<SessionBillingModeId>('guest');
  // Выбор длительности: число минут (фикс) или 'open' (открытый счёт). Чипы старт-диалога.
  // По умолчанию — открытый счёт (гость чаще играет «пока не уйдёт», оплата по факту на кассе).
  const [durationChoice, setDurationChoice] = useState<number | 'open'>('open');
  const [playerSearch, setPlayerSearch] = useState('');
  const [billingPlayers, setBillingPlayers] = useState<PlayerClientItem[]>([]);
  const [selectedPlayerId, setSelectedPlayerId] = useState('');
  const [tariffOptions, setTariffOptions] = useState<TariffOptionDto[]>([]);
  const [selectedTariffVersionId, setSelectedTariffVersionId] = useState('');
  const [playerPackages, setPlayerPackages] = useState<PlayerPackageDto[]>([]);
  const [selectedPlayerPackageId, setSelectedPlayerPackageId] = useState('');
  const [billingStatus, setBillingStatus] = useState<LoadStatus>('fixture');
  const [billingError, setBillingError] = useState<string | null>(null);
  const [criticalAction, setCriticalAction] = useState<'end-session' | 'checkout' | null>(null);
  const [startDialogOpen, setStartDialogOpen] = useState(false);
  const transferCandidates = floorSeats.filter((candidate) =>
    candidate.id !== seat.id &&
    candidate.tone === 'ready' &&
    !candidate.activeSessionId);
  const [targetSeatId, setTargetSeatId] = useState(transferCandidates[0]?.id ?? '');
  const selectedPlayer = billingPlayers.find((player) => player.playerAccountId === selectedPlayerId) ?? null;
  const selectedTariff = tariffOptions.find((tariff) => readString(tariff, 'tariffVersionId') === selectedTariffVersionId) ??
    tariffOptions[0] ??
    null;
  const selectedPlayerPackage = playerPackages.find((playerPackage) => playerPackage.playerPackageId === selectedPlayerPackageId) ??
    playerPackages[0] ??
    null;
  const hasStoredSession = Boolean(seat.activeSessionId);
  const hasPendingSessionCommand = hasStoredSession && isPendingSeatCommand(seat);
  const hasActionableSession = hasStoredSession && !hasPendingSessionCommand;
  const hasActiveSession = hasStoredSession || seat.hasActiveSession === true || seat.tone === 'active';
  const startedAtClock = formatSessionClock(seat.sessionStartedAtUtc);
  const hasSessionIdentity = hasActiveSession && Boolean(seat.playerDisplayName || seat.tariffName || startedAtClock);
  const isBusy = feedback.state === 'pending';
  const canStartPermission = hasPermission(session, permissionNames.startSession);
  const canExtendPermission = hasPermission(session, permissionNames.extendSession);
  const canTransferPermission = hasPermission(session, permissionNames.transferSession);
  const canEndPermission = hasPermission(session, permissionNames.endSession);
  const hasAnySessionActionPermission = canStartPermission ||
    canExtendPermission ||
    canTransferPermission ||
    canEndPermission;
  // Гость — manual-тариф (оплата по факту на кассе), как и было; пакет несёт своё время; депозит/
  // постоплата — по явно выбранному тарифу (ставке), чтобы показать цену заранее.
  const billingSelection: SessionBillingSelection = billingMode === 'guest'
    ? guestBillingSelection
    : billingMode === 'package'
      ? {
          mode: 'package',
          tariffRuleVersionId: defaultTariffRuleVersionId,
          playerAccountId: selectedPlayerId || null,
          playerPackageId: selectedPlayerPackage === null ? null : readString(selectedPlayerPackage, 'playerPackageId')
        }
      : {
          mode: billingMode,
          tariffRuleVersionId: selectedTariff === null
            ? defaultTariffRuleVersionId
            : readString(selectedTariff, 'tariffRuleVersionId', defaultTariffRuleVersionId),
          playerAccountId: selectedPlayerId || null,
          tariffVersionId: selectedTariff === null ? null : readString(selectedTariff, 'tariffVersionId')
        };
  const billingMissing = billingMode !== 'guest' && !selectedPlayerId
    ? t('op.map.panel.billingMissingPlayer')
    : (billingMode === 'prepaid_wallet' || billingMode === 'postpaid_debt') && !billingSelection.tariffVersionId
      ? t('op.map.panel.billingMissingTariff')
      : billingMode === 'package' && !billingSelection.playerPackageId
        ? t('op.map.panel.billingMissingPackage')
        : null;
  const billingReady = billingMissing === null;
  const isGuest = billingMode === 'guest';
  // Открытый счёт (оплата при завершении) валиден только для гостя и постоплаты; остальное — фикс.
  const openTabAllowed = billingMode === 'guest' || billingMode === 'postpaid_debt';
  const effectiveDurationMode: SessionStartDurationMode = durationChoice === 'open' && openTabAllowed ? 'open' : 'fixed';
  const effectiveDurationMinutes = effectiveDurationMode === 'open'
    ? null
    : (typeof durationChoice === 'number' ? durationChoice : 60);
  const durationLabel = formatDurationCompact((effectiveDurationMinutes ?? 60) * 60, t);
  // Цена-превью и покрытие баланса — концретика, что уже на руках (#34): ставка тарифа × минуты,
  // и на сколько хватит депозита игрока.
  // Цена-превью только для платных режимов с выбранным тарифом (гость платит по факту на кассе,
  // его ставка не предопределена — показывать цену было бы враньём, #37).
  const tariffPricePerMin = !isGuest && selectedTariff ? readNumber(selectedTariff, 'pricePerMinuteMinorUnits', 0) : 0;
  const estimatedCostMinor = effectiveDurationMode === 'fixed' && billingMode !== 'package' && tariffPricePerMin > 0 && effectiveDurationMinutes != null
    ? tariffPricePerMin * effectiveDurationMinutes
    : null;
  const coverageMinutes = billingMode === 'prepaid_wallet' && selectedPlayer && tariffPricePerMin > 0
    ? Math.floor((selectedPlayer.balanceMinorUnits ?? 0) / tariffPricePerMin)
    : null;
  const canStartSession = actionsEnabled && canStartPermission && billingReady && !hasActionableSession && seat.tone === 'ready';
  const canExtendSession = actionsEnabled && canExtendPermission && billingReady && hasActionableSession;
  const canEndSession = actionsEnabled && canEndPermission && hasActionableSession;
  const canTransferSession = actionsEnabled && canTransferPermission && hasActionableSession && targetSeatId.length > 0;
  // Строка отражает только готовность (можно ли действовать и почему нет), а не результат
  // последнего действия — результат теперь показывает визуальный ActionFeedback (галочка/спиннер).
  const confirmationText = !actionsEnabled
    ? t('op.map.panel.confirmStatusUnavailable')
    : !hasAnySessionActionPermission
      ? t('op.map.panel.confirmStatusNoPermission')
      : !billingReady
        ? t('op.map.panel.confirmStatusBilling', { missing: billingMissing ?? '' })
      : hasPendingSessionCommand
        ? t('op.map.panel.confirmStatusPending')
        : t('op.map.panel.confirmStatusWaiting');
  // Строку готовности показываем только когда есть реальное препятствие: нет бэка / прав /
  // биллинга или команда в полёте. Здоровое «готов к работе» — это шум, прячем.
  const isHealthyIdle = actionsEnabled && hasAnySessionActionPermission && billingReady
    && !hasPendingSessionCommand;
  // Превью «что сейчас запустим» — кто · тариф/пакет · сколько · цена. Решение одной строкой
  // перед стартом.
  const startPlanWho = isGuest ? t('op.helper.billing.guest') : (selectedPlayer?.name ?? '');
  const startPlanRate = billingMode === 'package'
    ? (selectedPlayerPackage ? readString(selectedPlayerPackage, 'name') : '')
    : (selectedTariff ? readString(selectedTariff, 'name') : '');
  const startPlanDuration = billingMode === 'package'
    ? ''
    : effectiveDurationMode === 'open'
      ? t('op.map.panel.openTab')
      : durationLabel;
  const startPlanPrice = estimatedCostMinor != null ? formatMinorUnits(estimatedCostMinor, currencyCode) : '';
  const startPlan = [startPlanWho, startPlanRate, startPlanDuration, startPlanPrice].filter(Boolean).join(' · ');

  const clearPlayerSelection = useCallback(() => {
    setPlayerSearch('');
    setBillingPlayers([]);
    setSelectedPlayerId('');
    setSelectedPlayerPackageId('');
  }, []);

  // ClientPicker владеет debounce/open/loading; Map остаётся владельцем
  // backend-поиска и сохраняет проекции для баланса/пакетов в старт-форме.
  const searchBillingPlayers = useCallback(async (query: string): Promise<PlayerClientItem[]> => {
    if (backend === null || !hasPermission(backend.session, permissionNames.viewPlayers)) {
      setBillingPlayers([]);
      return [];
    }

    try {
      const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
      const players: PlayerSearchResultDto[] = await clients.players.searchPlayers(backend.branchId, query, 8);
      const projected = players.map((player) => projectPlayerClient(player, t));
      setBillingPlayers(projected);
      return projected;
    } catch (error) {
      setBillingPlayers([]);
      setBillingError(projectOperatorError(error, t).detail);
      throw error;
    }
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, backend?.session.permissions, t]);

  useEffect(() => {
    if (targetSeatId.length > 0 && transferCandidates.some((candidate) => candidate.id === targetSeatId)) {
      return;
    }

    setTargetSeatId(transferCandidates[0]?.id ?? '');
  }, [seat.id, floorSeats]);

  useEffect(() => {
    let disposed = false;

    if (backend === null || !hasPermission(session, permissionNames.viewTariffs)) {
      setTariffOptions([]);
      setSelectedTariffVersionId('');
      setBillingStatus(backend === null ? 'failed' : 'fixture');
      setBillingError(backend === null ? t('op.map.panel.sessionUnavailable') : null);
      return undefined;
    }

    setBillingStatus('loading');
    setBillingError(null);
    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    clients.settings.getTariffOptions(backend.branchId)
      .then((options) => {
        if (disposed) {
          return;
        }

        setTariffOptions(options);
        setSelectedTariffVersionId((current) => current || readString(options[0], 'tariffVersionId'));
        setBillingStatus('backend');
      })
      .catch((error) => {
        if (disposed) {
          return;
        }

        setTariffOptions([]);
        setSelectedTariffVersionId('');
        setBillingStatus('failed');
        setBillingError(projectOperatorError(error, t).detail);
      });

    return () => {
      disposed = true;
    };
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken]);

  useEffect(() => {
    setCriticalAction(null);
    setStartDialogOpen(false);
    setPcFeedback(emptyFeedback);
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
      setStartDialogOpen(true);
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

  useEffect(() => {
    let disposed = false;

    if (backend === null || !selectedPlayerId || !hasPermission(session, permissionNames.viewBilling)) {
      setPlayerPackages([]);
      setSelectedPlayerPackageId('');
      return undefined;
    }

    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    clients.players.getPlayerPackages(selectedPlayerId)
      .then((packages: PlayerPackageDto[]) => {
        if (disposed) {
          return;
        }

        setPlayerPackages(packages);
        setSelectedPlayerPackageId((current) => current || readString(packages[0], 'playerPackageId'));
      })
      .catch((error) => {
        if (disposed) {
          return;
        }

        setPlayerPackages([]);
        setSelectedPlayerPackageId('');
        setBillingError(projectOperatorError(error, t).detail);
      });

    return () => {
      disposed = true;
    };
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, selectedPlayerId]);

  const runSeatAction = async (label: string, request: SeatActionRequest) => {
    setCriticalAction(null);
    setStartDialogOpen(false);
    setFeedback({ label, state: 'pending' });

    try {
      const result = await onSeatAction(request);
      setFeedback({ label, state: 'confirmed', detail: result.detail });
    } catch (error) {
      setFeedback({
        label,
        state: 'failed',
        detail: projectOperatorFacingError(error, t)
      });
    }
  };

  // Иконка на самой кнопке: пока её действие в полёте — спиннер, при успехе — галочка,
  // иначе обычная иконка. Так подтверждение читается на кнопке, без отдельной строки текста.
  const actionGlyph = (label: string, fallback: ReactNode): ReactNode => {
    if (feedback.label !== label) {
      return fallback;
    }
    if (feedback.state === 'pending') {
      return <Loader2 size={14} className="spin" aria-hidden="true" />;
    }
    if (feedback.state === 'confirmed') {
      return <Check size={14} aria-hidden="true" />;
    }
    return fallback;
  };

  const pcBusy = pcFeedback.state === 'pending';
  // Блокировка/разблокировка: успех — галочка на кнопке, ошибку показываем текстом (#34).
  const runPcControl = async (label: string, action: PcControlActionId) => {
    setPcFeedback({ label, state: 'pending' });
    try {
      await onPcControlAction(seat, action);
      setPcFeedback({ label, state: 'confirmed' });
    } catch (error) {
      setPcFeedback({ label, state: 'failed', detail: projectOperatorFacingError(error, t) });
    }
  };
  const pcGlyph = (label: string, fallback: ReactNode): ReactNode => {
    if (pcFeedback.label !== label) {
      return fallback;
    }
    if (pcFeedback.state === 'pending') {
      return <Loader2 size={14} className="spin" aria-hidden="true" />;
    }
    if (pcFeedback.state === 'confirmed') {
      return <Check size={14} aria-hidden="true" />;
    }
    return fallback;
  };

  return (
    <aside className="context-panel">
      {/* Герой места: что я смотрю + главное число (остаток/счёт/статус) одним блоком наверху. */}
      <header className={`seat-hero state-${seat.tone}`}>
        {/* Надзаголовок «зона» вплотную над именем места — один титульный блок, а не два
            конкурирующих заголовка; чип статуса прижат справа на уровне имени. */}
        <div className="seat-hero-head">
          <div className="seat-hero-id">
            <span className="seat-hero-zone">{zoneLabel(seat.zone, t)}</span>
            <h2 className="seat-hero-name">{seat.name}</h2>
          </div>
          <span className={`state-chip state-${seat.tone}`}>{toneLabel(seat.tone, t)}</span>
        </div>
        {/* Крупное число-герой только когда оно есть (время/счёт). Для свободного места и для
            статуса, дублирующего чип (напр. «Обслуживание»), ничего не показываем — чип уже всё
            сказал; для остальных проблемных статусов даём строку скромным размером. */}
        {lead.kind !== 'free' && !plainStatusRedundant && (
          <div className="seat-hero-metric">
            {heroLabel && <span className="seat-hero-label">{heroLabel}</span>}
            <strong className={`seat-hero-value${lead.kind === 'postpaid' ? ' is-money' : ''}${lead.kind === 'plain' ? ' is-text' : ''}${isExpired ? ' is-expired' : ''}`}>
              {lead.kind === 'prepaid' && seat.remainingSeconds != null
                ? formatDurationCompact(seat.remainingSeconds, t)
                : seat.remaining}
            </strong>
          </div>
        )}
        {/* Сколько ПК уже сидит занятым после нуля — оператор видит, что место «зависло». */}
        {isExpired && overtimeSeconds >= 60 && (
          <span className="seat-hero-sub">{t('op.map.expiredAgo', { duration: formatDurationCompact(overtimeSeconds, t) })}</span>
        )}
        {lead.kind === 'prepaid' && (
          <span className={`seat-timebar${lead.expired ? ' seat-timebar--expired' : lead.low ? ' seat-timebar--low' : ''}`} aria-hidden="true">
            <i style={{ width: `${Math.round(lead.barRatio * 100)}%` }} />
          </span>
        )}
      </header>

      {/* Действия: главная CTA доминирует, быстрые продления сегментом, перенос/стоп тише. */}
      <section className="panel-actions" aria-label={t('op.map.panel.quickActions')}>
        <div className="context-section-head"><span>{t('op.map.panel.actionsHead')}</span></div>
        {hasActiveSession ? (
          <>
            {/* Время вышло: спокойная подсказка, что у оператора два пути — продлить или завершить. */}
            {isExpired && <p className="panel-expired-hint">{t('op.map.panel.expiredHint')}</p>}
            <div className="quick-extend">
              <button type="button" disabled={!canExtendSession || isBusy} onClick={() => runSeatAction(t('op.map.panel.extend15Action'), { type: 'extend', seat, minutes: 15, billing: billingSelection })}>{actionGlyph(t('op.map.panel.extend15Action'), <Plus size={14} />)}{t('op.map.panel.extend15Action')}</button>
              <button type="button" disabled={!canExtendSession || isBusy} onClick={() => runSeatAction(t('op.map.panel.extend30Action'), { type: 'extend', seat, minutes: 30, billing: billingSelection })}>{actionGlyph(t('op.map.panel.extend30Action'), <Plus size={14} />)}{t('op.map.panel.extend30Action')}</button>
            </div>
            {/* Одна кнопка «Завершить»: онлайн ведёт в расчёт (с опцией «без оплаты»),
                офлайн — в простое подтверждение завершения. Отдельный «Стоп» убран. */}
            <button type="button" className="cta-primary" disabled={!canEndSession || isBusy} onClick={() => setCriticalAction(backend !== null ? 'checkout' : 'end-session')}>
              <ReceiptText size={16} />{t('op.map.panel.finishLabel')}
            </button>
            <div className="transfer-row">
              <span className="transfer-row-label"><ArrowRightLeft size={13} aria-hidden="true" />{t('op.map.panel.transferTo')}</span>
              <div className="transfer-row-controls">
                <PanelSelect
                  className="transfer-select"
                  ariaLabel={t('op.map.panel.transferTo')}
                  value={targetSeatId}
                  disabled={!actionsEnabled || !canTransferPermission || hasPendingSessionCommand || isBusy || transferCandidates.length === 0}
                  placeholder={t('op.map.panel.noFreePc')}
                  options={transferCandidates.map((candidate) => ({ value: candidate.id, label: candidate.name }))}
                  onChange={setTargetSeatId}
                />
                <button type="button" className="transfer-go" disabled={!canTransferSession || isBusy} onClick={() => runSeatAction(t('op.map.panel.transferAction'), { type: 'transfer', seat, targetSeatId })}>{actionGlyph(t('op.map.panel.transferAction'), null)}{t('op.map.panel.transferAction')}</button>
              </div>
            </div>
          </>
        ) : (
          <button type="button" className="cta-primary start-action" disabled={!actionsEnabled || !canStartPermission || isBusy || seat.tone !== 'ready'} onClick={() => setStartDialogOpen(true)}>
            <Plus size={16} />{t('op.map.seatInvite')}
          </button>
        )}
      </section>

      {criticalAction === 'end-session' && (
        <CriticalActionConfirmation
          title={t('op.map.panel.stopConfirmTitle')}
          detail={`${seat.name} · ${seat.remaining}`}
          impact={t('op.map.panel.stopConfirmImpact')}
          confirmLabel={t('op.map.panel.stopConfirmBtn')}
          disabled={isBusy}
          onCancel={() => setCriticalAction(null)}
          onConfirm={() => void runSeatAction(t('op.map.panel.stopAction'), { type: 'end', seat })}
        />
      )}
      {criticalAction === 'checkout' && backend !== null && (
        <CheckoutDialog
          seat={seat}
          backend={backend}
          disabled={isBusy}
          onCancel={() => setCriticalAction(null)}
          onConfirm={(payments) => void runSeatAction(t('op.map.panel.paymentAction'), { type: 'checkout', seat, payments })}
          onEndWithoutPayment={() => void runSeatAction(t('op.map.panel.stopAction'), { type: 'end', seat })}
        />
      )}
      <ActionFeedback feedback={feedback} />

      {/* Готовность к действиям (нет прав / биллинг не готов / команда в полёте) — это про
          ДЕЙСТВИЯ, не про статус ПК, поэтому показываем здесь и всегда, не пряча за «Статус». */}
      {!isHealthyIdle && (
        <div className="detail-row panel-readiness">
          <span>{t('op.map.panel.confirmationLabel')}</span>
          <strong>{confirmationText}</strong>
        </div>
      )}

      {hasActiveSession && seat.remainingSeconds == null && seat.accruedCostMinorUnits != null && (
        // Открытый счёт: набежавшая сумма — настоящие деньги, которые уже на руках (#34).
        <section className="context-section">
          <div className="detail-row">
            <span>{t('op.map.panel.accrued')}</span>
            <strong className="detail-value-money">{formatMinorUnits(seat.accruedCostMinorUnits, currencyCode)}</strong>
          </div>
        </section>
      )}

      {hasSessionIdentity && (
        // Кто на месте и по какому тарифу — реальные данные сессии из floor-map DTO (B1),
        // а не плейсхолдеры. Каждую строку показываем только когда бэкенд её знает (#34).
        <section className="context-section">
          {seat.playerDisplayName && (
            <div className="detail-row"><span>{t('op.map.panel.playerLabel')}</span><strong>{seat.playerDisplayName}</strong></div>
          )}
          {seat.tariffName && (
            <div className="detail-row"><span>{t('op.map.panel.tariffLabel')}</span><strong>{seat.tariffName}</strong></div>
          )}
          {startedAtClock && (
            <div className="detail-row"><span>{t('op.map.panel.startedLabel')}</span><strong>{startedAtClock}</strong></div>
          )}
        </section>
      )}

      {/* Управление ПК: команды блокировки/разблокировки для выбранного места. */}
      {canUsePcControl && hasDevice && (
        <section className="context-section">
          <div className="context-section-head">
            <Wrench size={13} aria-hidden="true" />
            <span>{t('op.map.pcControlLabel')}</span>
          </div>
          <div className="pc-control-actions panel-pc-actions">
            <button type="button" disabled={pcBusy || !seat.deviceId} onClick={() => void runPcControl(t('op.map.actionLock'), 'lock')}>
              {pcGlyph(t('op.map.actionLock'), <Lock size={14} />)}<span>{t('op.map.actionLockBtn')}</span>
            </button>
            <button
              type="button"
              disabled={pcBusy || !seat.deviceId || !hasActiveSession}
              title={hasActiveSession ? t('op.map.unlockActiveTitle') : t('op.map.unlockNoSessionTitle')}
              onClick={() => void runPcControl(t('op.map.actionUnlock'), 'unlock')}
            >
              {pcGlyph(t('op.map.actionUnlock'), <Unlock size={14} />)}<span>{t('op.map.actionUnlockBtn')}</span>
            </button>
          </div>
          {pcFeedback.state === 'failed' && pcFeedback.detail && (
            <p className="pc-control-result failed" role="alert">{pcFeedback.detail}</p>
          )}
        </section>
      )}

      {/* Статус ПК — единый блок состояния (связь, блокировка, полезные детали) внизу панели,
          всегда виден. Данные берём из карты — обновляются с floor-map, без отдельного опроса. */}
      {canUsePcControl && hasDevice && (
        <section className="context-section pc-status-section">
          <div className="context-section-head">
            <MonitorCheck size={13} aria-hidden="true" />
            <span>{t('op.map.actionStatus')}</span>
          </div>
          <div className="pc-status-readout">
            <div className="pc-health">
              <span className={`status-pill ${seat.isDeviceOnline === true ? 'ok' : seat.isDeviceOnline === false ? 'bad' : 'neutral'}`}>
                {seat.isDeviceOnline === false ? <WifiOff size={12} aria-hidden="true" /> : <Wifi size={12} aria-hidden="true" />}
                {connectionLabel}
              </span>
              <span className="status-pill neutral">
                {seat.isDeviceLocked === true ? <Lock size={12} aria-hidden="true" /> : <Unlock size={12} aria-hidden="true" />}
                {lockLabel}
              </span>
            </div>
            {seat.deviceName && seat.deviceName !== seat.name && (
              <div className="detail-row">
                <span>{t('op.map.panel.deviceLabel')}</span>
                <strong>{seat.deviceName}</strong>
              </div>
            )}
            {/* Версия ПО показывается всегда — оператору важно знать билд агента/оболочки
                независимо от того, на связи ПК сейчас или нет. */}
            <div className="detail-row">
              <span>{t('op.map.panel.versions')}</span>
              <strong>{appVersionsLabel(seat.app, t)}</strong>
            </div>
            {/* Команда несёт смысл только в проблемных состояниях (ожидание/сбой/нет связи) —
                для активной/свободной сессии она дублирует чип статуса, поэтому её прячем. */}
            {(seat.tone === 'pending' || seat.tone === 'offline') && (
              <div className="detail-row">
                <span>{t('op.map.panel.commandLabel')}</span>
                <strong>{commandLabel(seat.command, t)}</strong>
              </div>
            )}
          </div>
        </section>
      )}

      {startDialogOpen && (
      <PanelModal
        title={t('op.map.panel.startSessionTitle')}
        subtitle={seat.name}
        onClose={() => setStartDialogOpen(false)}
      >
        <div className="billing-selection-panel start-dialog-body">
        {/* КТО ИГРАЕТ: гость (walk-in) vs клиент клуба (по аккаунту). */}
        <div className="start-section-head">{t('op.map.panel.startWhoHead')}</div>
        <div className="start-segment" role="tablist" aria-label={t('op.map.panel.startWhoHead')}>
          <button
            type="button"
            role="tab"
            aria-selected={isGuest}
            className={isGuest ? 'active' : undefined}
            disabled={!actionsEnabled || isBusy}
            onClick={() => {
              setBillingMode('guest');
              clearPlayerSelection();
            }}
          >
            {t('op.helper.billing.guest')}
          </button>
          <button
            type="button"
            role="tab"
            aria-selected={!isGuest}
            className={!isGuest ? 'active' : undefined}
            disabled={!actionsEnabled || isBusy || !hasPermission(session, permissionNames.viewBilling)}
            onClick={() => { if (isGuest) { setBillingMode('prepaid_wallet'); } }}
          >
            {t('op.map.panel.startMember')}
          </button>
        </div>

        {!isGuest && (
          <>
            <div className="start-section-head">{t('op.map.panel.startPlayerHead')}</div>
            <ClientPicker
              value={playerSearch}
              linked={selectedPlayerId !== ''}
              disabled={!actionsEnabled || isBusy || !hasPermission(session, permissionNames.viewPlayers)}
              ariaLabel={t('op.map.panel.playerInput')}
              search={searchBillingPlayers}
              onQueryChange={(value) => {
                setPlayerSearch(value);
                setSelectedPlayerId('');
                setSelectedPlayerPackageId('');
              }}
              onPick={(pick) => {
                setPlayerSearch(pick.name);
                setSelectedPlayerId(pick.playerAccountId);
              }}
              onClear={clearPlayerSelection}
            />
            <div className="start-section-head">{t('op.map.panel.startChargeHead')}</div>
            <div className="start-segment three" role="tablist" aria-label={t('op.map.panel.startChargeHead')}>
              {billingModeOptions(t).filter((option) => option.id !== 'guest').map((option) => (
                <button
                  key={option.id}
                  type="button"
                  role="tab"
                  aria-selected={billingMode === option.id}
                  className={billingMode === option.id ? 'active' : undefined}
                  disabled={!actionsEnabled || isBusy}
                  title={option.detail}
                  onClick={() => setBillingMode(option.id)}
                >
                  {option.label}
                </button>
              ))}
            </div>
            {coverageMinutes != null && selectedPlayer && (
              <p className="start-coverage">
                {t('op.map.panel.startBalanceCoverage', {
                  balance: formatMinorUnits(selectedPlayer.balanceMinorUnits, currencyCode),
                  duration: formatDurationCompact(coverageMinutes * 60, t)
                })}
              </p>
            )}
          </>
        )}

        {/* ТАРИФ (ставка) — для депозита/постоплаты; гость платит по факту, пакет несёт своё время. */}
        {!isGuest && billingMode !== 'package' && (
          <>
            <div className="start-section-head">{t('op.map.panel.tariffLabel')}</div>
            <PanelSelect
              className="start-select"
              ariaLabel={t('op.map.panel.tariffSession')}
              value={selectedTariffVersionId}
              disabled={!actionsEnabled || isBusy || tariffOptions.length === 0}
              placeholder={t('op.map.panel.noTariffs')}
              options={tariffOptions.map((tariff) => ({
                value: readString(tariff, 'tariffVersionId'),
                label: tariffOptionLabel(tariff, currencyCode, t)
              }))}
              onChange={setSelectedTariffVersionId}
            />
          </>
        )}
        {billingMode === 'package' && (
          <>
            <div className="start-section-head">{t('op.map.panel.packageLabel')}</div>
            <PanelSelect
              className="start-select"
              ariaLabel={t('op.map.panel.packageSession')}
              value={selectedPlayerPackageId}
              disabled={!actionsEnabled || isBusy || !selectedPlayer || playerPackages.length === 0}
              placeholder={t('op.map.panel.noPackages')}
              options={playerPackages.map((playerPackage) => ({
                value: playerPackage.playerPackageId,
                label: playerPackageLabel(playerPackage, t)
              }))}
              onChange={setSelectedPlayerPackageId}
            />
          </>
        )}

        {/* СКОЛЬКО ВРЕМЕНИ: чипы длительности + открытый счёт; цена-превью под ними. */}
        {billingMode !== 'package' && (
          <>
            <div className="start-section-head">{t('op.map.panel.startTimeHead')}</div>
            <div className="start-duration-chips" role="group" aria-label={t('op.map.panel.sessionDuration')}>
              {START_DURATIONS.map((minutes) => (
                <button
                  key={minutes}
                  type="button"
                  className={effectiveDurationMode === 'fixed' && durationChoice === minutes ? 'active' : undefined}
                  disabled={!actionsEnabled || isBusy}
                  onClick={() => setDurationChoice(minutes)}
                >
                  {formatDurationCompact(minutes * 60, t)}
                </button>
              ))}
              <button
                type="button"
                className={effectiveDurationMode === 'open' ? 'active' : undefined}
                disabled={!actionsEnabled || isBusy || !openTabAllowed}
                title={openTabAllowed ? t('op.map.panel.openTabAllowedTitle') : t('op.map.panel.openTabDisabledTitle')}
                onClick={() => setDurationChoice('open')}
              >
                {t('op.map.panel.openTab')}
              </button>
            </div>
            {estimatedCostMinor != null && (
              <p className="start-price">
                {t('op.map.panel.startPriceEstimate', { duration: durationLabel, amount: formatMinorUnits(estimatedCostMinor, currencyCode) })}
              </p>
            )}
          </>
        )}

        {/* Реальную ошибку загрузки тарифов показываем как ошибку, а не прячем за dev-статусом (#34). */}
        {billingStatus === 'failed' && billingError && <p className="checkout-error">{billingError}</p>}
        {/* Превью решения целиком: либо что запустим, либо чего не хватает. */}
        <div className={`start-plan${billingMissing ? ' is-missing' : ''}`}>
          <span>{billingMissing ? t('op.map.panel.startNeed') : t('op.map.panel.startPlanLabel')}</span>
          <strong>{billingMissing ?? startPlan}</strong>
        </div>
        <div className="critical-confirmation-actions">
          <button type="button" onClick={() => setStartDialogOpen(false)} disabled={isBusy}>{t('common.cancel')}</button>
          <button
            type="button"
            className="cta-primary"
            disabled={!canStartSession || isBusy}
            onClick={() => void runSeatAction(effectiveDurationMode === 'open' ? t('op.map.panel.startOpen') : t('op.map.panel.startCta', { duration: durationLabel }), { type: 'start', seat, billing: billingSelection, durationMode: effectiveDurationMode, durationMinutes: effectiveDurationMinutes })}
          >
            <Plus size={16} />{effectiveDurationMode === 'open' ? t('op.map.panel.startOpen') : t('op.map.panel.startCta', { duration: durationLabel })}
          </button>
        </div>
        </div>
      </PanelModal>
      )}
    </aside>
  );
}
