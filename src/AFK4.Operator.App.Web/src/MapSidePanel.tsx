import { useEffect, useState, type ReactNode } from 'react';
import { ArrowRightLeft, Banknote, Check, CircleDollarSign, Loader2, Lock, MonitorCheck, Plus, ReceiptText, TimerReset, TriangleAlert, Unlock, Wifi, WifiOff, X } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { currencySymbol } from '@afk4/money';
import { projectOperatorError } from './apiErrors';
import {
  buildCheckoutPayments,
  checkoutMethods,
  checkoutMethodLabel,
  formatBilledDuration,
  formatCheckoutAmount,
  initialCheckoutDrafts,
  validateCheckoutPayments,
  type CheckoutMethod,
  type CheckoutPaymentDraft
} from './checkoutState';
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
  billingModeLabel,
  billingModeOptions,
  commandLabel,
  createAuthenticatedOperatorClients,
  defaultTariffRuleVersionId,
  guestBillingSelection,
  emptyFeedback,
  formatMinorUnits,
  isPendingSeatCommand,
  mapSeatStatus,
  playerPackageLabel,
  projectOperatorFacingError,
  readString,
  tariffOptionLabel,
  toneLabel,
  type PlayerClientItem,
  projectPlayerClient,
  zoneLabel
} from './operatorHelpers';
import { CriticalActionConfirmation } from './operatorPrimitives';
import { PanelModal } from './PanelModal';
import { isAttentionTone, seatTileLead } from './seatTilePresentation';
import { formatDurationCompact } from './floorMapState';

const checkoutMethodIcons: Record<CheckoutMethod, ReactNode> = {
  cash: <Banknote size={14} />,
  card_manual: <CircleDollarSign size={14} />,
  wallet: <ReceiptText size={14} />
};

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
  const [drafts, setDrafts] = useState<CheckoutPaymentDraft[]>([{ method: 'cash', amountText: '' }]);

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
        setDrafts(initialCheckoutDrafts(result.grandTotal?.minorUnits ?? 0));
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

  // Защищаемся от неполной котировки: карточка вне error-boundary, любой бросок здесь гасит весь экран.
  const currencyCode = quote?.grandTotal?.currencyCode ?? '';
  const grandTotal = quote?.grandTotal?.minorUnits ?? 0;
  const walletBalance = quote?.walletBalance?.minorUnits ?? null;
  const validation = validateCheckoutPayments(drafts, grandTotal, walletBalance, t);
  const canConfirm = status === 'ready' && !disabled && validation.canSubmit;

  const updateDraft = (index: number, patch: Partial<CheckoutPaymentDraft>) => {
    setDrafts((current) => current.map((draft, position) => (position === index ? { ...draft, ...patch } : draft)));
  };
  const addDraft = () => {
    setDrafts((current) => [...current, { method: 'cash', amountText: '' }]);
  };
  const removeDraft = (index: number) => {
    setDrafts((current) => (current.length <= 1 ? current : current.filter((_, position) => position !== index)));
  };
  const suggestWallet = () => {
    if (walletBalance === null) {
      return;
    }

    const fromWallet = Math.min(walletBalance, grandTotal);
    const remainder = grandTotal - fromWallet;
    const next: CheckoutPaymentDraft[] = [{ method: 'wallet', amountText: formatCheckoutAmount(fromWallet) }];
    if (remainder > 0) {
      next.push({ method: 'cash', amountText: formatCheckoutAmount(remainder) });
    }

    setDrafts(next);
  };

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
        <>
          <dl className="checkout-breakdown">
            <div><dt>{t('op.map.panel.billableTime')}</dt><dd>{formatBilledDuration(quote.billableSeconds ?? 0, t)}</dd></div>
            <div><dt>{t('op.map.panel.billableTimeCharge')}</dt><dd>{formatMinorUnits(quote.timeCharge?.minorUnits ?? 0, currencyCode)}</dd></div>
            <div><dt>{t('op.map.panel.billableSnacks')}</dt><dd>{formatMinorUnits(quote.posTotal?.minorUnits ?? 0, currencyCode)}</dd></div>
            <div className="checkout-breakdown-total"><dt>{t('op.map.panel.billableTotal')}</dt><dd>{formatMinorUnits(grandTotal, currencyCode)}</dd></div>
          </dl>

          <div className="checkout-payments">
            {drafts.map((draft, index) => (
              <div className="checkout-payment-row" key={index}>
                <select
                  aria-label={t('op.map.panel.paymentMethod')}
                  value={draft.method}
                  disabled={disabled}
                  onChange={(event) => updateDraft(index, { method: event.currentTarget.value as CheckoutMethod })}
                >
                  {checkoutMethods.map((method) => (
                    <option key={method} value={method}>{checkoutMethodLabel(method, t)}</option>
                  ))}
                </select>
                <span className="checkout-payment-icon">{checkoutMethodIcons[draft.method]}</span>
                <input
                  type="text"
                  inputMode="decimal"
                  aria-label={t('op.map.panel.paymentAmount')}
                  placeholder="0.00"
                  value={draft.amountText}
                  disabled={disabled}
                  onChange={(event) => updateDraft(index, { amountText: event.currentTarget.value })}
                />
                <button
                  type="button"
                  className="checkout-payment-remove"
                  aria-label={t('op.map.panel.removePaymentRow')}
                  disabled={disabled || drafts.length <= 1}
                  onClick={() => removeDraft(index)}
                >
                  <X size={14} />
                </button>
              </div>
            ))}
            <div className="checkout-payment-controls">
              <button type="button" disabled={disabled} onClick={addDraft}><Plus size={14} />{t('op.map.panel.addPaymentMethod')}</button>
              {walletBalance !== null && walletBalance > 0 && (
                <button type="button" disabled={disabled} onClick={suggestWallet}>
                  <ReceiptText size={14} />{t('op.map.panel.walletSuggest', { amount: formatMinorUnits(walletBalance, currencyCode) })}
                </button>
              )}
            </div>
          </div>

          {validation.error
            ? <p className="checkout-error">{validation.error}</p>
            : <p className="checkout-balanced">{t('op.map.panel.checkoutBalanced')}</p>}
        </>
      )}

      <button type="button" className="checkout-end-plain" onClick={onEndWithoutPayment} disabled={disabled}>
        {t('op.map.panel.endWithoutPay')}
      </button>

      <div className="critical-confirmation-actions">
        <button type="button" onClick={onCancel} disabled={disabled}>{t('common.cancel')}</button>
        <button
          type="button"
          className="danger"
          disabled={!canConfirm}
          onClick={() => onConfirm(buildCheckoutPayments(drafts, currencyCode))}
        >
          {t('op.map.panel.confirmPayment')}
        </button>
      </div>
    </PanelModal>
  );
}

export function MapSidePanel({
  seat,
  seats: floorSeats,
  currencyCode,
  backend,
  actionsEnabled,
  onSeatAction
}: {
  seat: SeatSummary;
  seats: SeatSummary[];
  currencyCode: string;
  backend: OperatorBackendContext | null;
  actionsEnabled: boolean;
  onSeatAction: (request: SeatActionRequest) => Promise<SeatActionResult>;
}) {
  const { t } = useI18n();
  const session = backend?.session ?? null;
  const status = mapSeatStatus(seat, t);
  const lead = seatTileLead(seat);
  // Подпись над герой-значением: что это за число (осталось / открытый счёт / статус проблемы).
  const heroLabel = lead.kind === 'prepaid'
    ? t('op.map.seatLeft')
    : lead.kind === 'postpaid'
      ? t('op.map.seatOpenTab')
      : lead.kind === 'plain'
        ? status.label
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
  const [billingMode, setBillingMode] = useState<SessionBillingModeId>('guest');
  const [durationMode, setDurationMode] = useState<SessionStartDurationMode>('fixed');
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
  const selectedPlayerPackage = playerPackages.find((playerPackage) => readString(playerPackage, 'playerPackageId') === selectedPlayerPackageId) ??
    playerPackages[0] ??
    null;
  const hasStoredSession = Boolean(seat.activeSessionId);
  const hasPendingSessionCommand = hasStoredSession && isPendingSeatCommand(seat);
  const hasActionableSession = hasStoredSession && !hasPendingSessionCommand;
  const hasActiveSession = hasStoredSession || seat.hasActiveSession === true || seat.tone === 'active';
  // Технические детали ПК (команда, версии) показываем только когда они actionable —
  // место требует внимания или команда в полёте; для здоровой сессии это шум.
  const showPcDetail = isAttentionTone(seat.tone) || hasPendingSessionCommand;
  const isBusy = feedback.state === 'pending';
  const canStartPermission = hasPermission(session, permissionNames.startSession);
  const canExtendPermission = hasPermission(session, permissionNames.extendSession);
  const canTransferPermission = hasPermission(session, permissionNames.transferSession);
  const canEndPermission = hasPermission(session, permissionNames.endSession);
  const hasAnySessionActionPermission = canStartPermission ||
    canExtendPermission ||
    canTransferPermission ||
    canEndPermission;
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
  // An open tab (no fixed duration, settled later at checkout) is only valid for
  // an unbilled guest or a postpaid-debt player; other modes must be fixed.
  const openTabAllowed = billingMode === 'guest' || billingMode === 'postpaid_debt';
  const effectiveDurationMode: SessionStartDurationMode = openTabAllowed ? durationMode : 'fixed';
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
  const billingLoadText = billingStatus === 'backend'
    ? t('op.map.panel.billingLoadData')
    : billingStatus === 'loading'
      ? t('op.map.panel.billingLoadLoading')
      : billingStatus === 'failed'
        ? billingError ?? t('op.map.panel.billingLoadError')
        : t('op.map.panel.billingLoadWaiting');

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
  }, [seat.id, seat.activeSessionId]);

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
    const query = playerSearch.trim();

    if (backend === null || query.length < 2 || !hasPermission(session, permissionNames.viewPlayers)) {
      setBillingPlayers([]);
      setSelectedPlayerId('');
      return undefined;
    }

    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    clients.players.searchPlayers(backend.branchId, query, 8)
      .then((players: PlayerSearchResultDto[]) => {
        if (disposed) {
          return;
        }

        const projected = players.map((p) => projectPlayerClient(p, t));
        setBillingPlayers(projected);
        setSelectedPlayerId((current) => current || (projected[0]?.playerAccountId ?? ''));
      })
      .catch((error) => {
        if (disposed) {
          return;
        }

        setBillingPlayers([]);
        setSelectedPlayerId('');
        setBillingError(projectOperatorError(error, t).detail);
      });

    return () => {
      disposed = true;
    };
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, playerSearch]);

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
        {/* Крупное число-герой только когда оно есть (время/счёт). Для свободного места
            ничего не дублируем — чип «Готов» и кнопка «Посадить гостя» уже всё сказали;
            для проблемных статусов показываем строку скромным размером, не гигантом. */}
        {lead.kind !== 'free' && (
          <div className="seat-hero-metric">
            {heroLabel && <span className="seat-hero-label">{heroLabel}</span>}
            <strong className={`seat-hero-value${lead.kind === 'postpaid' ? ' is-money' : ''}${lead.kind === 'plain' ? ' is-text' : ''}`}>
              {lead.kind === 'prepaid' && seat.remainingSeconds != null
                ? formatDurationCompact(seat.remainingSeconds, t)
                : seat.remaining}
            </strong>
          </div>
        )}
        {lead.kind === 'prepaid' && (
          <span className={`seat-timebar${lead.low ? ' seat-timebar--low' : ''}`} aria-hidden="true">
            <i style={{ width: `${Math.round(lead.barRatio * 100)}%` }} />
          </span>
        )}
      </header>

      {/* Действия: главная CTA доминирует, быстрые продления сегментом, перенос/стоп тише. */}
      <section className="panel-actions" aria-label={t('op.map.panel.quickActions')}>
        <div className="context-section-head"><span>{t('op.map.panel.actionsHead')}</span></div>
        {hasActiveSession ? (
          <>
            <div className="quick-extend">
              <button type="button" disabled={!canExtendSession || isBusy} onClick={() => runSeatAction(t('op.map.panel.extend15Action'), { type: 'extend', seat, minutes: 15, billing: billingSelection })}>{actionGlyph(t('op.map.panel.extend15Action'), <Plus size={14} />)}{t('op.map.panel.extend15Action')}</button>
              <button type="button" disabled={!canExtendSession || isBusy} onClick={() => runSeatAction(t('op.map.panel.extend30Action'), { type: 'extend', seat, minutes: 30, billing: billingSelection })}>{actionGlyph(t('op.map.panel.extend30Action'), <TimerReset size={14} />)}{t('op.map.panel.extend30Action')}</button>
            </div>
            {/* Одна кнопка «Завершить»: онлайн ведёт в расчёт (с опцией «без оплаты»),
                офлайн — в простое подтверждение завершения. Отдельный «Стоп» убран. */}
            <button type="button" className="cta-primary" disabled={!canEndSession || isBusy} onClick={() => setCriticalAction(backend !== null ? 'checkout' : 'end-session')}>
              <ReceiptText size={16} />{t('op.map.panel.finishLabel')}
            </button>
            <div className="transfer-row">
              <span className="transfer-row-label"><ArrowRightLeft size={13} aria-hidden="true" />{t('op.map.panel.transferTo')}</span>
              <div className="transfer-row-controls">
                <select aria-label={t('op.map.panel.transferTo')} value={targetSeatId} disabled={!actionsEnabled || !canTransferPermission || hasPendingSessionCommand || isBusy || transferCandidates.length === 0} onChange={(event) => setTargetSeatId(event.currentTarget.value)}>
                  {transferCandidates.length === 0 && <option value="">{t('op.map.panel.noFreePc')}</option>}
                  {transferCandidates.map((candidate) => (
                    <option key={candidate.id} value={candidate.id}>{candidate.name}</option>
                  ))}
                </select>
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

      {hasActiveSession && seat.remainingSeconds == null && seat.accruedCostMinorUnits != null && (
        // Открытый счёт: набежавшая сумма — настоящие деньги, которые уже на руках (#34).
        <section className="context-section">
          <div className="detail-row">
            <span>{t('op.map.panel.accrued')}</span>
            <strong className="detail-value-money">{formatMinorUnits(seat.accruedCostMinorUnits, currencyCode)}</strong>
          </div>
        </section>
      )}

      {/* Диагностика ПК — конкретика, которая уже на руках (#34). Здоровье — компактным рядом
          статус-пилюль, чтобы связь/блокировка читались за полсекунды, а не в столбце строк. */}
      <section className="context-section">
        <div className="context-section-head">
          <MonitorCheck size={13} aria-hidden="true" />
          <span>{t('op.map.panel.diagnostics')}</span>
        </div>
        {!hasDevice ? (
          <div className="detail-row">
            <span>{t('op.map.colDevice')}</span>
            <strong>{t('op.helper.deviceStatus.unassigned')}</strong>
          </div>
        ) : (
          <>
            {/* Имя устройства показываем, только если оно отличается от имени места (иначе дубль героя). */}
            {seat.deviceName && seat.deviceName !== seat.name && (
              <div className="detail-row">
                <span>{t('op.map.colDevice')}</span>
                <strong>{seat.deviceName}</strong>
              </div>
            )}
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
            {showPcDetail && (
              <div className="detail-row">
                <span>{t('op.map.panel.versions')}</span>
                <strong>{appVersionsLabel(seat.app, t)}</strong>
              </div>
            )}
          </>
        )}
        {showPcDetail && (
          <div className="detail-row">
            <span>{t('op.map.colCommand')}</span>
            <strong>{commandLabel(seat.command, t)}</strong>
          </div>
        )}
        {!isHealthyIdle && (
          <div className="detail-row">
            <span>{t('op.map.panel.confirmationLabel')}</span>
            <strong>{confirmationText}</strong>
          </div>
        )}
      </section>

      {startDialogOpen && (
      <PanelModal
        title={t('op.map.panel.startSessionTitle')}
        subtitle={`${seat.name} · ${currencySymbol(currencyCode)}`}
        onClose={() => setStartDialogOpen(false)}
      >
        <div className="billing-selection-panel start-dialog-body">
        <div className="billing-panel-head">
          <strong>{billingModeLabel(billingMode, t)}</strong>
          <em>{billingLoadText}</em>
        </div>
        <div className="billing-mode" aria-label={t('op.map.panel.billingModeLabel')}>
          {billingModeOptions(t).map((option) => {
            const isBilledMode = option.id !== 'guest';
            return (
              <button
                key={option.id}
                type="button"
                className={option.id === billingMode ? 'active' : undefined}
                disabled={!actionsEnabled || isBusy || (isBilledMode && !hasPermission(session, permissionNames.viewBilling))}
                title={option.detail}
                onClick={() => setBillingMode(option.id)}
              >
                <span>{option.label}</span>
                <small>{option.detail}</small>
              </button>
            );
          })}
        </div>
        {!hasActiveSession && (
          <div className="billing-mode duration-mode" aria-label={t('op.map.panel.sessionDuration')}>
            <button
              type="button"
              className={effectiveDurationMode === 'fixed' ? 'active' : undefined}
              disabled={!actionsEnabled || isBusy}
              title={t('op.map.panel.duration60Title')}
              onClick={() => setDurationMode('fixed')}
            >
              <span>{t('op.map.panel.duration60')}</span>
              <small>{t('op.map.panel.duration60Detail')}</small>
            </button>
            <button
              type="button"
              className={effectiveDurationMode === 'open' ? 'active' : undefined}
              disabled={!actionsEnabled || isBusy || !openTabAllowed}
              title={openTabAllowed ? t('op.map.panel.openTabAllowedTitle') : t('op.map.panel.openTabDisabledTitle')}
              onClick={() => setDurationMode('open')}
            >
              <span>{t('op.map.panel.openTab')}</span>
              <small>{openTabAllowed ? t('op.map.panel.openTabAllowed') : t('op.map.panel.openTabDisabled')}</small>
            </button>
          </div>
        )}
        {billingMode !== 'guest' && (
          <>
            <label className="context-transfer-target billing-input-row">
              <span>{t('op.map.colPlayer')}</span>
              <input
                aria-label={t('op.map.panel.playerInput')}
                value={playerSearch}
                disabled={!actionsEnabled || isBusy || !hasPermission(session, permissionNames.viewPlayers)}
                placeholder={t('op.map.panel.playerSearch')}
                onChange={(event) => setPlayerSearch(event.currentTarget.value)}
              />
            </label>
            <div className="billing-candidate-list" aria-label={t('op.map.panel.playersFoundLabel')}>
              {billingPlayers.map((player) => (
                <button
                  key={player.playerAccountId ?? player.name}
                  type="button"
                  className={player.playerAccountId === selectedPlayerId ? 'active' : undefined}
                  disabled={!player.playerAccountId || isBusy}
                  onClick={() => setSelectedPlayerId(player.playerAccountId ?? '')}
                >
                  <strong>{player.name}</strong>
                  <span>{formatMinorUnits(player.balanceMinorUnits, currencyCode)} · {t('op.map.panel.playerDebt', { amount: formatMinorUnits(player.debtMinorUnits, currencyCode) })}</span>
                </button>
              ))}
              {playerSearch.trim().length > 1 && billingPlayers.length === 0 && (
                <p>{t('op.map.panel.playerNotFound')}</p>
              )}
            </div>
            {(billingMode === 'prepaid_wallet' || billingMode === 'postpaid_debt') && (
              <label className="context-transfer-target billing-input-row">
                <span>{t('op.map.panel.tariffLabel')}</span>
                <select
                  aria-label={t('op.map.panel.tariffSession')}
                  value={selectedTariffVersionId}
                  disabled={!actionsEnabled || isBusy || tariffOptions.length === 0}
                  onChange={(event) => setSelectedTariffVersionId(event.currentTarget.value)}
                >
                  {tariffOptions.length === 0 && <option value="">{t('op.map.panel.noTariffs')}</option>}
                  {tariffOptions.map((tariff) => (
                    <option key={readString(tariff, 'tariffVersionId')} value={readString(tariff, 'tariffVersionId')}>
                      {tariffOptionLabel(tariff, currencyCode, t)}
                    </option>
                  ))}
                </select>
              </label>
            )}
            {billingMode === 'package' && (
              <label className="context-transfer-target billing-input-row">
                <span>{t('op.map.panel.packageLabel')}</span>
                <select
                  aria-label={t('op.map.panel.packageSession')}
                  value={selectedPlayerPackageId}
                  disabled={!actionsEnabled || isBusy || !selectedPlayer || playerPackages.length === 0}
                  onChange={(event) => setSelectedPlayerPackageId(event.currentTarget.value)}
                >
                  {playerPackages.length === 0 && <option value="">{t('op.map.panel.noPackages')}</option>}
                  {playerPackages.map((playerPackage) => (
                    <option key={readString(playerPackage, 'playerPackageId')} value={readString(playerPackage, 'playerPackageId')}>
                      {playerPackageLabel(playerPackage, t)}
                    </option>
                  ))}
                </select>
              </label>
            )}
            <div className="detail-row billing-meta">
              <span>{t('op.map.panel.billingChoice')}</span>
              <strong>{billingMissing ?? t('op.map.panel.billingReady', { mode: billingModeLabel(billingMode, t) })}</strong>
            </div>
          </>
        )}
        <div className="critical-confirmation-actions">
          <button type="button" onClick={() => setStartDialogOpen(false)} disabled={isBusy}>{t('common.cancel')}</button>
          <button
            type="button"
            className="cta-primary"
            disabled={!canStartSession || isBusy}
            onClick={() => void runSeatAction(effectiveDurationMode === 'open' ? t('op.map.panel.startOpenAction') : t('op.map.panel.startFixed'), { type: 'start', seat, billing: billingSelection, durationMode: effectiveDurationMode })}
          >
            <Plus size={16} />{effectiveDurationMode === 'open' ? t('op.map.panel.startOpen') : t('op.map.panel.startFixed')}
          </button>
        </div>
        </div>
      </PanelModal>
      )}
    </aside>
  );
}
