import { useEffect, useState, type ReactNode } from 'react';
import { ArrowRightLeft, Banknote, CircleDollarSign, Clock3, Plus, ReceiptText, Square, TimerReset, X } from 'lucide-react';
import { projectOperatorError } from './apiErrors';
import {
  buildCheckoutPayments,
  checkoutMethods,
  checkoutMethodLabels,
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
  billingLabel,
  billingModeLabel,
  billingModeOptions,
  commandLabel,
  createAuthenticatedOperatorClients,
  defaultTariffRuleVersionId,
  deviceStatusLabel,
  emptyFeedback,
  feedbackText,
  formatMinorUnits,
  isPendingSeatCommand,
  mapSeatStatus,
  playerPackageLabel,
  projectOperatorFacingError,
  readString,
  tariffOptionLabel,
  toneLabels,
  type PlayerClientItem,
  projectPlayerClient,
  zoneLabel
} from './operatorHelpers';
import { CriticalActionConfirmation, FeedbackNotice } from './operatorPrimitives';

const checkoutMethodIcons: Record<CheckoutMethod, ReactNode> = {
  cash: <Banknote size={14} />,
  card_manual: <CircleDollarSign size={14} />,
  wallet: <ReceiptText size={14} />
};

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
  onConfirm
}: {
  seat: SeatSummary;
  backend: OperatorBackendContext;
  disabled: boolean;
  onCancel: () => void;
  onConfirm: (payments: PaymentPartDto[]) => void;
}) {
  const [quote, setQuote] = useState<SessionCheckoutQuoteResponse | null>(null);
  const [status, setStatus] = useState<'loading' | 'ready' | 'failed'>('loading');
  const [error, setError] = useState<string | null>(null);
  const [drafts, setDrafts] = useState<CheckoutPaymentDraft[]>([{ method: 'cash', amountText: '' }]);

  useEffect(() => {
    let disposed = false;
    const sessionId = seat.activeSessionId;
    if (!sessionId) {
      setStatus('failed');
      setError('На выбранном ПК нет активной сессии.');
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
        setDrafts(initialCheckoutDrafts(result.grandTotal.minorUnits));
        setStatus('ready');
      })
      .catch((fetchError) => {
        if (disposed) {
          return;
        }

        setStatus('failed');
        setError(projectOperatorError(fetchError).detail);
      });

    return () => {
      disposed = true;
    };
  }, [seat.activeSessionId, backend.config.platformBaseUrl, backend.session.accessToken]);

  const currencyCode = quote?.grandTotal.currencyCode ?? '';
  const grandTotal = quote?.grandTotal.minorUnits ?? 0;
  const walletBalance = quote?.walletBalance?.minorUnits ?? null;
  const validation = validateCheckoutPayments(drafts, grandTotal, walletBalance);
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
    <section className="critical-confirmation warning checkout-dialog" role="alertdialog" aria-label="Завершить и принять оплату">
      <div>
        <strong>Завершить и принять оплату</strong>
        <span>{seat.name} · {seat.player}</span>
        <em>После оплаты сессия закрывается, платформа блокирует ПК.</em>
      </div>

      {status === 'loading' && <p className="checkout-loading">Расчёт счёта…</p>}
      {status === 'failed' && <p className="checkout-error">{error ?? 'Не удалось получить счёт.'}</p>}

      {status === 'ready' && quote && (
        <>
          <dl className="checkout-breakdown">
            <div><dt>Наиграно</dt><dd>{formatBilledDuration(quote.billableSeconds)}</dd></div>
            <div><dt>Время</dt><dd>{formatMinorUnits(quote.timeCharge.minorUnits, currencyCode)}</dd></div>
            <div><dt>Снеки</dt><dd>{formatMinorUnits(quote.posTotal.minorUnits, currencyCode)}</dd></div>
            <div className="checkout-breakdown-total"><dt>Итого</dt><dd>{formatMinorUnits(grandTotal, currencyCode)}</dd></div>
          </dl>

          <div className="checkout-payments">
            {drafts.map((draft, index) => (
              <div className="checkout-payment-row" key={index}>
                <select
                  aria-label="Способ оплаты"
                  value={draft.method}
                  disabled={disabled}
                  onChange={(event) => updateDraft(index, { method: event.currentTarget.value as CheckoutMethod })}
                >
                  {checkoutMethods.map((method) => (
                    <option key={method} value={method}>{checkoutMethodLabels[method]}</option>
                  ))}
                </select>
                <span className="checkout-payment-icon">{checkoutMethodIcons[draft.method]}</span>
                <input
                  type="text"
                  inputMode="decimal"
                  aria-label="Сумма"
                  placeholder="0.00"
                  value={draft.amountText}
                  disabled={disabled}
                  onChange={(event) => updateDraft(index, { amountText: event.currentTarget.value })}
                />
                <button
                  type="button"
                  className="checkout-payment-remove"
                  aria-label="Убрать строку"
                  disabled={disabled || drafts.length <= 1}
                  onClick={() => removeDraft(index)}
                >
                  <X size={14} />
                </button>
              </div>
            ))}
            <div className="checkout-payment-controls">
              <button type="button" disabled={disabled} onClick={addDraft}><Plus size={14} />Ещё способ</button>
              {walletBalance !== null && walletBalance > 0 && (
                <button type="button" disabled={disabled} onClick={suggestWallet}>
                  <ReceiptText size={14} />Депозит ({formatMinorUnits(walletBalance, currencyCode)})
                </button>
              )}
            </div>
          </div>

          {validation.error
            ? <p className="checkout-error">{validation.error}</p>
            : <p className="checkout-balanced">Сумма совпадает</p>}
        </>
      )}

      <div className="critical-confirmation-actions">
        <button type="button" onClick={onCancel} disabled={disabled}>Отмена</button>
        <button
          type="button"
          className="danger"
          disabled={!canConfirm}
          onClick={() => onConfirm(buildCheckoutPayments(drafts, currencyCode))}
        >
          Принять оплату
        </button>
      </div>
    </section>
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
  const session = backend?.session ?? null;
  const status = mapSeatStatus(seat);
  const activeBilling = billingLabel(seat.billing);
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
    ? {
        mode: 'guest',
        tariffRuleVersionId: defaultTariffRuleVersionId,
        playerAccountId: null,
        tariffVersionId: null,
        playerPackageId: null
      }
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
    ? 'выберите игрока'
    : (billingMode === 'prepaid_wallet' || billingMode === 'postpaid_debt') && !billingSelection.tariffVersionId
      ? 'выберите тариф'
      : billingMode === 'package' && !billingSelection.playerPackageId
        ? 'выберите пакет игрока'
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
  const confirmationText = !actionsEnabled
    ? 'Карта платформы недоступна'
    : !hasAnySessionActionPermission
      ? 'Нет прав на действия с сессией'
      : !billingReady
        ? `Биллинг: ${billingMissing}`
      : hasPendingSessionCommand
        ? 'Команда уже отправлена. Дождитесь подтверждения ПК.'
      : feedback.state === 'idle'
        ? 'Ждём платформу'
        : feedbackText(feedback);
  const billingLoadText = billingStatus === 'backend'
    ? 'данные платформы'
    : billingStatus === 'loading'
      ? 'загрузка'
      : billingStatus === 'failed'
        ? billingError ?? 'ошибка загрузки'
        : 'ожидает платформу';

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
      setBillingError(backend === null ? 'Сессия оператора платформы недоступна.' : null);
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
        setBillingError(projectOperatorError(error).detail);
      });

    return () => {
      disposed = true;
    };
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken]);

  useEffect(() => {
    setCriticalAction(null);
  }, [seat.id, seat.activeSessionId]);

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

        const projected = players.map(projectPlayerClient);
        setBillingPlayers(projected);
        setSelectedPlayerId((current) => current || (projected[0]?.playerAccountId ?? ''));
      })
      .catch((error) => {
        if (disposed) {
          return;
        }

        setBillingPlayers([]);
        setSelectedPlayerId('');
        setBillingError(projectOperatorError(error).detail);
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
        setBillingError(projectOperatorError(error).detail);
      });

    return () => {
      disposed = true;
    };
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, selectedPlayerId]);

  const runSeatAction = async (label: string, request: SeatActionRequest) => {
    setCriticalAction(null);
    setFeedback({ label, state: 'pending' });

    try {
      const result = await onSeatAction(request);
      setFeedback({ label, state: 'confirmed', detail: result.detail });
    } catch (error) {
      setFeedback({
        label,
        state: 'failed',
        detail: projectOperatorFacingError(error)
      });
    }
  };

  return (
    <aside className="context-panel">
      <header className="context-head">
        <div>
          <span>{zoneLabel(seat.zone)}</span>
          <h2>{seat.name}</h2>
        </div>
        <span className={`state-chip state-${seat.tone}`}>{toneLabels[seat.tone]}</span>
      </header>

      <section className={`context-status-row state-${seat.tone}`}>
        <span>{status.label}</span>
        <strong>{status.value}</strong>
      </section>

      <section className="action-grid context-actions" aria-label="Быстрые действия">
        {hasActiveSession ? (
          <>
            <button type="button" disabled={!canExtendSession || isBusy} onClick={() => runSeatAction('+15 мин', { type: 'extend', seat, minutes: 15, billing: billingSelection })}><Plus size={15} />15 мин</button>
            <button type="button" disabled={!canExtendSession || isBusy} onClick={() => runSeatAction('+30 мин', { type: 'extend', seat, minutes: 30, billing: billingSelection })}><TimerReset size={15} />30 мин</button>
            <button type="button" disabled={!canTransferSession || isBusy} onClick={() => runSeatAction('Перенос', { type: 'transfer', seat, targetSeatId })}><ArrowRightLeft size={15} />Перенос</button>
            <button type="button" className="danger" disabled={!canEndSession || isBusy} onClick={() => setCriticalAction('end-session')}><Square size={15} />Стоп</button>
          </>
        ) : (
          <>
            <button type="button" className="start-action" disabled={!canStartSession || isBusy} onClick={() => runSeatAction(effectiveDurationMode === 'open' ? 'Старт (открытый счёт)' : 'Старт 60 мин', { type: 'start', seat, billing: billingSelection, durationMode: effectiveDurationMode })}><Plus size={15} />{effectiveDurationMode === 'open' ? 'Старт · открытый счёт' : 'Старт 60 мин'}</button>
            <button type="button" disabled><TimerReset size={15} />Нет сессии</button>
          </>
        )}
      </section>
      {hasActiveSession && backend !== null && (
        <button
          type="button"
          className="checkout-action"
          disabled={!canEndSession || isBusy}
          onClick={() => setCriticalAction('checkout')}
        >
          <ReceiptText size={15} />Завершить и принять оплату
        </button>
      )}
      {hasActiveSession && (
        <label className="context-transfer-target">
          <span>Перенести на</span>
          <select value={targetSeatId} disabled={!actionsEnabled || !canTransferPermission || hasPendingSessionCommand || isBusy || transferCandidates.length === 0} onChange={(event) => setTargetSeatId(event.currentTarget.value)}>
            {transferCandidates.length === 0 && <option value="">Нет свободных ПК</option>}
            {transferCandidates.map((candidate) => (
              <option key={candidate.id} value={candidate.id}>{candidate.name}</option>
            ))}
          </select>
        </label>
      )}
      {criticalAction === 'end-session' && (
        <CriticalActionConfirmation
          title="Подтвердите остановку сессии"
          detail={`${seat.name} · ${seat.remaining} · ${activeBilling}`}
          impact="Сессия будет завершена, платформа отправит команду блокировки ПК."
          confirmLabel="Подтвердить стоп"
          disabled={isBusy}
          onCancel={() => setCriticalAction(null)}
          onConfirm={() => void runSeatAction('Стоп', { type: 'end', seat })}
        />
      )}
      {criticalAction === 'checkout' && backend !== null && (
        <CheckoutDialog
          seat={seat}
          backend={backend}
          disabled={isBusy}
          onCancel={() => setCriticalAction(null)}
          onConfirm={(payments) => void runSeatAction('Оплата', { type: 'checkout', seat, payments })}
        />
      )}
      <FeedbackNotice feedback={feedback} />

      <section className="context-section">
        <div className="session-timer">
          <Clock3 size={17} />
          <div>
            <span>Активная сессия</span>
            <strong>{seat.remaining}</strong>
          </div>
        </div>
        <div className="detail-row">
          <span>Игрок</span>
          <strong>{seat.player}</strong>
        </div>
        <div className="detail-row">
          <span>Биллинг</span>
          <strong>{activeBilling} · {currencyCode}</strong>
        </div>
      </section>

      <section className="context-section">
        <div className="detail-row">
          <span>Устройство</span>
          <strong>{deviceStatusLabel(seat.device)}</strong>
        </div>
        <div className="detail-row">
          <span>Команда</span>
          <strong>{commandLabel(seat.command)}</strong>
        </div>
        <div className="detail-row">
          <span>Подтверждение</span>
          <strong>{confirmationText}</strong>
        </div>
      </section>

      <section className="context-section billing-selection-panel" aria-label="Настройка биллинга">
        <div className="billing-panel-head">
          <span>Биллинг сессии</span>
          <strong>{billingModeLabel(billingMode)}</strong>
          <em>{billingLoadText}</em>
        </div>
        <div className="billing-mode" aria-label="Режим биллинга">
          {billingModeOptions.map((option) => {
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
          <div className="billing-mode duration-mode" aria-label="Длительность сессии">
            <button
              type="button"
              className={effectiveDurationMode === 'fixed' ? 'active' : undefined}
              disabled={!actionsEnabled || isBusy}
              title="Фиксированные 60 минут"
              onClick={() => setDurationMode('fixed')}
            >
              <span>60 мин</span>
              <small>фиксировано</small>
            </button>
            <button
              type="button"
              className={effectiveDurationMode === 'open' ? 'active' : undefined}
              disabled={!actionsEnabled || isBusy || !openTabAllowed}
              title={openTabAllowed ? 'Время идёт, оплата при завершении' : 'Доступно для гостя или постоплаты'}
              onClick={() => setDurationMode('open')}
            >
              <span>Открытый счёт</span>
              <small>{openTabAllowed ? 'оплата при завершении' : 'гость / постоплата'}</small>
            </button>
          </div>
        )}
        {billingMode !== 'guest' && (
          <>
            <label className="context-transfer-target billing-input-row">
              <span>Игрок</span>
              <input
                aria-label="Игрок для биллинга"
                value={playerSearch}
                disabled={!actionsEnabled || isBusy || !hasPermission(session, permissionNames.viewPlayers)}
                placeholder="имя или телефон"
                onChange={(event) => setPlayerSearch(event.currentTarget.value)}
              />
            </label>
            <div className="billing-candidate-list" aria-label="Найденные игроки">
              {billingPlayers.map((player) => (
                <button
                  key={player.playerAccountId ?? player.name}
                  type="button"
                  className={player.playerAccountId === selectedPlayerId ? 'active' : undefined}
                  disabled={!player.playerAccountId || isBusy}
                  onClick={() => setSelectedPlayerId(player.playerAccountId ?? '')}
                >
                  <strong>{player.name}</strong>
                  <span>{formatMinorUnits(player.balanceMinorUnits, currencyCode)} · долг {formatMinorUnits(player.debtMinorUnits, currencyCode)}</span>
                </button>
              ))}
              {playerSearch.trim().length > 1 && billingPlayers.length === 0 && (
                <p>Игрок не найден</p>
              )}
            </div>
            {(billingMode === 'prepaid_wallet' || billingMode === 'postpaid_debt') && (
              <label className="context-transfer-target billing-input-row">
                <span>Тариф</span>
                <select
                  aria-label="Тариф для сессии"
                  value={selectedTariffVersionId}
                  disabled={!actionsEnabled || isBusy || tariffOptions.length === 0}
                  onChange={(event) => setSelectedTariffVersionId(event.currentTarget.value)}
                >
                  {tariffOptions.length === 0 && <option value="">Нет тарифов</option>}
                  {tariffOptions.map((tariff) => (
                    <option key={readString(tariff, 'tariffVersionId')} value={readString(tariff, 'tariffVersionId')}>
                      {tariffOptionLabel(tariff, currencyCode)}
                    </option>
                  ))}
                </select>
              </label>
            )}
            {billingMode === 'package' && (
              <label className="context-transfer-target billing-input-row">
                <span>Пакет</span>
                <select
                  aria-label="Пакет для сессии"
                  value={selectedPlayerPackageId}
                  disabled={!actionsEnabled || isBusy || !selectedPlayer || playerPackages.length === 0}
                  onChange={(event) => setSelectedPlayerPackageId(event.currentTarget.value)}
                >
                  {playerPackages.length === 0 && <option value="">Нет активных пакетов</option>}
                  {playerPackages.map((playerPackage) => (
                    <option key={readString(playerPackage, 'playerPackageId')} value={readString(playerPackage, 'playerPackageId')}>
                      {playerPackageLabel(playerPackage)}
                    </option>
                  ))}
                </select>
              </label>
            )}
            <div className="detail-row billing-meta">
              <span>Выбор</span>
              <strong>{billingMissing ?? `${billingModeLabel(billingMode)} готов`}</strong>
            </div>
          </>
        )}
      </section>
    </aside>
  );
}
