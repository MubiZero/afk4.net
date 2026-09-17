import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { projectOperatorError, requiresManagerApproval } from './apiErrors';
import type { LedgerEntryDto, PlayerPackageDto, SessionTimelineItemDto, WalletSummaryDto, ReservationDto } from './operatorApiClients';
import type { Feedback, LoadStatus, OperatorBackendContext } from './operatorTypes';
import { hasPermission, permissionNames } from './operatorPermissions';
import {
  createAuthenticatedOperatorClients,
  createIdempotencyKey,
  emptyFeedback,
  formatMinorUnits,
  formatMoneyInputMinorUnits,
  parseMoneyInputMinorUnits,
  readArray,
  readMoney,
  readString,
  requireBackend,
  resolveReasonInput,
  toDateTimeInputValue
} from './operatorHelpers';
import { fixturePlayers, playerStatusLabel, projectPlayerClient, buildClientSegments, buildClientOverview, buildClientContextMap, matchesSegment, type PlayerClientItem, type ClientSegmentId, type ClientLiveContext } from './players/playersModel';
import { fetchPlayersData, playersSnapshotCache } from './players/playersSnapshot';
import { StateFlag } from './operatorPrimitives';
import { useFeedbackToasts } from './useFeedbackToasts';
import { ClientsTable } from './players/ClientsTable';
import { ClientDrawer } from './players/ClientDrawer';
import { useReputation } from './players/useReputation';
import { HistorySection } from './players/HistorySection';
import { fullPhoneDigits } from './phoneFormat';
import { PanelModal } from './PanelModal';
import { NewClientModal } from './players/NewClientModal';
import { ClientBookingModal, type ClientBookingDraft } from './players/ClientBookingModal';
import { CorrectionModal, correctionQuantities, type CorrectionAccount, type CorrectionDirection } from './players/CorrectionModal';
import { RefundModal } from './players/RefundModal';
import { EditProfileModal } from './players/EditProfileModal';
import { ActiveStateConfirmModal } from './players/ActiveStateConfirmModal';
import { ApprovalRequestModal } from './players/ApprovalRequestModal';
import type { MoneyActionSubmitRequest } from './api/clients/moneyActions';
import { correctionApprovalRequest, refundApprovalRequest } from './players/approvalDraft';
import { PayDebtModal } from './players/PayDebtModal';
import { DcTopUpDialog } from './players/DcTopUpDialog';

type PlayerActionId = 'topUp' | 'writeOffDebt' | 'booking' | 'newCard' | 'correction' | 'refund' | 'updateProfile' | 'toggleActive';

export function BackendPlayersWorkspace({ currencyCode, backend, openClient }: {
  currencyCode: string;
  backend: OperatorBackendContext | null;
  // Кого открыть сразу: выбор из командной палитры. Строка поиска приезжает вместе с ним —
  // раздел грузит список по ней, и без неё выбранного в списке могло бы не оказаться.
  openClient?: { playerAccountId: string; search: string } | null;
}) {
  const { t } = useI18n();
  // Снимок из кэша (если раздел уже открывали в этой сессии на этом филиале) → мгновенный возврат.
  const cacheKey = backend?.branchId ?? null;
  const cachedSnapshot = cacheKey !== null ? playersSnapshotCache.get(cacheKey) : undefined;
  const [clientSearch, setClientSearch] = useState(openClient?.search ?? '');
  const [activeSegment, setActiveSegment] = useState<ClientSegmentId>('all');
  const [newClientOpen, setNewClientOpen] = useState(false);
  const [payDebtOpen, setPayDebtOpen] = useState(false);
  const [selectedClientId, setSelectedClientId] = useState<string | null>(
    openClient?.playerAccountId ?? cachedSnapshot?.selectedId ?? null);
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  useFeedbackToasts(feedback);
  const [loadStatus, setLoadStatus] = useState<LoadStatus>(backend === null ? 'fixture' : cachedSnapshot ? 'backend' : 'loading');
  const [clients, setClients] = useState<PlayerClientItem[]>(() => backend === null ? fixturePlayers(currencyCode, t) : cachedSnapshot?.clients ?? []);
  const [walletSummary, setWalletSummary] = useState<WalletSummaryDto | null>(null);
  const [walletTopUpAmount, setWalletTopUpAmount] = useState('');
  const [walletTopUpReason] = useState('');
  const [debtPaymentAmount, setDebtPaymentAmount] = useState('');
  const [debtPaymentReason, setDebtPaymentReason] = useState('');
  const [newPlayerName, setNewPlayerName] = useState('');
  // Бронь из карточки клиента спрашивает время, длительность и место, а не создаёт их за
  // оператора: раньше один клик молча заводил бронь «через 30 минут, на час, без места».
  const [bookingDraft, setBookingDraft] = useState<ClientBookingDraft | null>(null);
  const [bookingSeats, setBookingSeats] = useState<{ seatId: string; label: string }[]>([]);
  const [bookingBusy, setBookingBusy] = useState(false);
  const [newPlayerPhone, setNewPlayerPhone] = useState('');
  const [ledgerEntries, setLedgerEntries] = useState<LedgerEntryDto[]>([]);
  const [playerPackages, setPlayerPackages] = useState<PlayerPackageDto[]>([]);
  const [packagesLoading, setPackagesLoading] = useState(false);
  const [packagesErrorDetail, setPackagesErrorDetail] = useState<string | undefined>();
  const [ledgerCursor, setLedgerCursor] = useState<string | null>(null);
  const [ledgerFilter, setLedgerFilter] = useState<string | null>(null);
  const [ledgerLoading, setLedgerLoading] = useState(false);
  const [correctionOpen, setCorrectionOpen] = useState(false);
  const [correctionAccount, setCorrectionAccount] = useState<CorrectionAccount>('wallet');
  const [correctionDirection, setCorrectionDirection] = useState<CorrectionDirection>('credit');
  const [correctionAmount, setCorrectionAmount] = useState('50.00');
  const [correctionReason, setCorrectionReason] = useState(() => t('op.players.correction.reasonDefault'));
  const [refundTarget, setRefundTarget] = useState<LedgerEntryDto | null>(null);
  const [refundReason, setRefundReason] = useState(() => t('op.players.refund.reasonDefault'));
  const [editOpen, setEditOpen] = useState(false);
  const [editName, setEditName] = useState('');
  const [editPhone, setEditPhone] = useState('');
  const [activeStateOpen, setActiveStateOpen] = useState(false);
  const [historyModalOpen, setHistoryModalOpen] = useState(false);
  const [dcTopUpOpen, setDcTopUpOpen] = useState(false);
  const [ledgerReloadNonce, setLedgerReloadNonce] = useState(0);
  // DC-пополнение подтверждается через отдельный wallet fulfil-эндпоинт, который отдаёт
  // PlayerTopUpIntentDto (не WalletSummaryDto) — значит, в отличие от счётчик-пополнения,
  // обновлённый баланс не приезжает в ответе. Тот же «рефетч», что после counter-пополнения,
  // здесь — явный ре-триггер эффекта загрузки кошелька через нонс (как ledgerReloadNonce ниже).
  const [walletReloadNonce, setWalletReloadNonce] = useState(0);

  // Кросс-контекст ВСЕХ загруженных клиентов (играет-сейчас/ближайшая-бронь), построенный за один
  // проход по branch-wide sessions+reservations — таблица подсвечивает «сейчас» в каждой строке,
  // drawer берёт контекст выбранного из этой же карты (без per-client рефетча).
  const [liveContextByClient, setLiveContextByClient] = useState<Map<string, ClientLiveContext>>(() => new Map());

  // Палитра может позвать другого человека, когда раздел уже открыт — тогда компонент не
  // пересоздаётся, и начальные значения выше не сработали бы.
  useEffect(() => {
    if (!openClient) return;
    setClientSearch(openClient.search);
    setSelectedClientId(openClient.playerAccountId);
  }, [openClient?.playerAccountId, openClient?.search]);

  useEffect(() => {
    if (backend === null) {
      setLoadStatus('fixture');
      setClients(fixturePlayers(currencyCode, t));
      return undefined;
    }

    let disposed = false;
    const loadPlayers = async () => {
      if (!cachedSnapshot) {
        // Холодный вход (данных ещё нет) → показываем скелетон. Тёплый возврат/поиск при уже
        // показанных данных ревалидируем молча, не сбрасывая экран в загрузку.
        setLoadStatus('loading');
      }
      try {
        const { clients: nextClients } = await fetchPlayersData(backend, t, clientSearch);
        if (disposed) {
          return;
        }

        setClients(nextClients);
        setSelectedClientId((current) => {
          const resolved = current && nextClients.some((client) => client.playerAccountId === current)
            ? current
            : nextClients[0]?.playerAccountId ?? null;
          // Кладём удачный снимок в кэш → следующий заход в раздел мгновенный (без ре-фетча с нуля).
          if (cacheKey !== null) {
            playersSnapshotCache.set(cacheKey, { clients: nextClients, selectedId: resolved });
          }
          return resolved;
        });
        setLoadStatus('backend');
      } catch (error) {
        if (!disposed) {
          setLoadStatus('failed');
          setFeedback({ label: t('op.players.error.loadFailed'), state: 'failed', detail: projectOperatorError(error, t).detail });
        }
      }
    };

    const timer = window.setTimeout(() => void loadPlayers(), 180);
    return () => {
      disposed = true;
      window.clearTimeout(timer);
    };
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, clientSearch, currencyCode]);

  const selectedClient = clients.find((client) => client.playerAccountId === selectedClientId) ?? null;
  const reputation = useReputation(backend, selectedClient?.phoneNumber ?? '', selectedClient?.platformPersonId ?? null);

  useEffect(() => {
    if (backend === null || selectedClient?.source !== 'backend' || !selectedClient.playerAccountId) {
      setPlayerPackages([]);
      setPackagesErrorDetail(undefined);
      return;
    }
    let disposed = false;
    setPackagesLoading(true);
    setPackagesErrorDetail(undefined);
    createAuthenticatedOperatorClients(backend.config, backend.session).players.getPlayerPackages(selectedClient.playerAccountId)
      .then((items) => { if (!disposed) setPlayerPackages(items); })
      .catch((error) => { if (!disposed) setPackagesErrorDetail(projectOperatorError(error, t).detail); })
      .finally(() => { if (!disposed) setPackagesLoading(false); });
    return () => { disposed = true; };
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, selectedClient?.playerAccountId, selectedClient?.source, walletReloadNonce, t]);

  const canViewLedger = backend !== null
    && selectedClient !== null
    && selectedClient.source === 'backend'
    && Boolean(selectedClient.playerAccountId)
    && hasPermission(backend.session, permissionNames.viewBilling);

  // Журнал живёт правой колонкой карточки без вкладок — грузим его всегда, когда он доступен.
  const ledgerPaneVisible = canViewLedger;

  useEffect(() => {
    if (backend === null || selectedClient === null || !selectedClient.playerAccountId || selectedClient.source !== 'backend') {
      setWalletSummary(null);
      return undefined;
    }

    const client = selectedClient as PlayerClientItem & { playerAccountId: string; source: 'backend' };
    let disposed = false;
    const loadWallet = async () => {
      try {
        const apiClients = createAuthenticatedOperatorClients(backend.config, backend.session);
        const wallet = await apiClients.players.getWalletSummary(client.playerAccountId);
        if (!disposed) {
          setWalletSummary(wallet);
        }
      } catch (error) {
        if (!disposed) {
          setFeedback({ label: client.name, state: 'failed', detail: projectOperatorError(error, t).detail });
        }
      }
    };

    void loadWallet();
    return () => {
      disposed = true;
    };
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, selectedClient?.playerAccountId, selectedClient?.source, walletReloadNonce]);

  // Журнал истории: серверный источник (paged ledger-эндпоинт), отдельно от wallet-summary.
  // Грузим первую страницу при входе на таб «История» / смене клиента / смене фильтра.
  useEffect(() => {
    if (!ledgerPaneVisible || selectedClient === null || !selectedClient.playerAccountId) {
      return undefined;
    }

    const nextBackend = backend;
    if (nextBackend === null) {
      return undefined;
    }

    const playerAccountId = selectedClient.playerAccountId;
    let disposed = false;
    const loadLedger = async () => {
      setLedgerLoading(true);
      try {
        const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
        const page = await apiClients.players.getLedger(playerAccountId, {
          entryType: ledgerFilter ?? undefined,
          limit: 50
        });
        if (!disposed) {
          setLedgerEntries(page.items);
          setLedgerCursor(page.nextCursor);
        }
      } catch (error) {
        if (!disposed) {
          setLedgerEntries([]);
          setLedgerCursor(null);
          setFeedback({ label: t('op.players.tabs.history'), state: 'failed', detail: projectOperatorError(error, t).detail });
        }
      } finally {
        if (!disposed) {
          setLedgerLoading(false);
        }
      }
    };

    void loadLedger();
    return () => {
      disposed = true;
    };
  }, [
    backend?.branchId,
    backend?.config.platformBaseUrl,
    backend?.session.accessToken,
    ledgerPaneVisible,
    selectedClient?.playerAccountId,
    selectedClient?.source,
    ledgerFilter,
    ledgerReloadNonce
  ]);

  // Кросс-контекст ВСЕХ клиентов: активная сессия (играет сейчас) и ближайшая бронь. Один branch-wide
  // fetch sessions+reservations раскладывается по playerAccountId (buildClientContextMap), а не рефетчится
  // на выбранного клиента — так «сейчас» видно в каждой строке таблицы, а не только в drawer. Брони
  // грузим branch-wide (без playerAccountId) — фильтрация по клиенту живёт внутри buildClientContext.
  // Best-effort: нет прав/ошибка → пустая карта, экран не страдает.
  const clientIdsKey = clients.map((client) => client.playerAccountId ?? '').join(',');
  useEffect(() => {
    const backendClients = clients.filter((client): client is PlayerClientItem & { playerAccountId: string } =>
      client.source === 'backend' && Boolean(client.playerAccountId));
    if (backend === null || backendClients.length === 0) {
      setLiveContextByClient(new Map());
      return undefined;
    }

    const nextBackend = backend;
    const canSessions = hasPermission(nextBackend.session, permissionNames.viewSessions);
    const canReservations = hasPermission(nextBackend.session, permissionNames.viewReservations);
    if (!canSessions && !canReservations) {
      setLiveContextByClient(new Map());
      return undefined;
    }

    let disposed = false;
    const loadContext = async () => {
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const nowIso = new Date().toISOString();
      const horizonIso = new Date(Date.now() + 14 * 24 * 60 * 60_000).toISOString();
      const [sessionsResult, reservationsResult] = await Promise.all([
        canSessions
          ? apiClients.sessions.timeline(nextBackend.branchId, { limit: null }).catch(() => null)
          : Promise.resolve(null),
        canReservations
          // branch-wide fetch для контекста всех строк — потолок = серверный EfReservationService.MaxLimit (100).
          ? apiClients.reservations.search(nextBackend.branchId, { fromUtc: nowIso, toUtc: horizonIso, limit: 100 }).catch(() => null)
          : Promise.resolve(null)
      ]);
      if (disposed) {
        return;
      }

      const sessions = readArray<SessionTimelineItemDto>(sessionsResult, 'sessions');
      const reservations = readArray<ReservationDto>(reservationsResult, 'reservations');
      setLiveContextByClient(buildClientContextMap(sessions, reservations, backendClients));
    };

    void loadContext();
    return () => {
      disposed = true;
    };
  }, [
    backend?.branchId,
    backend?.config.platformBaseUrl,
    backend?.session.accessToken,
    clientIdsKey,
    ledgerReloadNonce
  ]);

  const handleSelectClient = (id: string | null) => {
    setSelectedClientId(id);
  };

  const segments = buildClientSegments(clients, t);
  const overview = buildClientOverview(clients);
  const visibleClients = clients.filter((client) => {
    const searchMatches = `${client.name} ${playerStatusLabel(client.status, t)} ${client.detail} ${client.last}`
      .toLowerCase()
      .includes(clientSearch.trim().toLowerCase());
    return matchesSegment(client, activeSegment) && searchMatches;
  });

  const balance = readMoney(walletSummary, 'walletBalance')?.minorUnits ?? selectedClient?.balanceMinorUnits ?? 0;
  const debt = readMoney(walletSummary, 'debtBalance')?.minorUnits ?? selectedClient?.debtMinorUnits ?? 0;
  // Заморожено под брони. Ноль до загрузки сводки — в строке поиска этого числа нет, и
  // придумывать его из остатка нельзя: холд из остатка уже вычтен.
  const held = readMoney(walletSummary, 'heldBalance')?.minorUnits ?? 0;

  useEffect(() => {
    if (debt <= 0) {
      setDebtPaymentAmount('');
      return;
    }

    setDebtPaymentAmount((current) => {
      const parsed = parseMoneyInputMinorUnits(current);
      return parsed !== null && parsed > 0 && parsed <= debt ? current : formatMoneyInputMinorUnits(debt);
    });
  }, [debt, selectedClient?.playerAccountId]);

  const isSelectedInactive = selectedClient !== null && !selectedClient.isActive;
  const canTopUpWallet = backend !== null
    && selectedClient !== null
    && selectedClient.source === 'backend'
    && Boolean(selectedClient.playerAccountId)
    && !isSelectedInactive
    && hasPermission(backend.session, permissionNames.topUpWallet);
  const canPayDebt = backend !== null
    && selectedClient !== null
    && selectedClient.source === 'backend'
    && Boolean(selectedClient.playerAccountId)
    && debt > 0
    && !isSelectedInactive
    && hasPermission(backend.session, permissionNames.payDebt);
  const canCreatePlayer = backend !== null && hasPermission(backend.session, permissionNames.createPlayerAccount);
  const canCreateClientReservation = backend !== null
    && selectedClient !== null
    && selectedClient.source === 'backend'
    && Boolean(selectedClient.playerAccountId)
    && !isSelectedInactive
    && hasPermission(backend.session, permissionNames.manageReservations);
  const canManualCorrect = backend !== null
    && selectedClient !== null
    && selectedClient.source === 'backend'
    && Boolean(selectedClient.playerAccountId)
    && !isSelectedInactive
    && hasPermission(backend.session, permissionNames.manualCorrection);
  const canRefundLedger = backend !== null
    && selectedClient !== null
    && selectedClient.source === 'backend'
    && Boolean(selectedClient.playerAccountId)
    && !isSelectedInactive
    && hasPermission(backend.session, permissionNames.refundLedgerEntry);
  const canManageClient = backend !== null
    && selectedClient !== null
    && selectedClient.source === 'backend'
    && Boolean(selectedClient.playerAccountId)
    && hasPermission(backend.session, permissionNames.createPlayerAccount);

  const requireSelectedBackendClient = (): PlayerClientItem & { playerAccountId: string; source: 'backend' } => {
    if (selectedClient === null || selectedClient.source !== 'backend' || !selectedClient.playerAccountId) {
      throw new Error(t('op.players.error.selectPlayer'));
    }

    return selectedClient as PlayerClientItem & { playerAccountId: string; source: 'backend' };
  };

  // Операция, упёршаяся в порог сотрудника: не проведена, но может уйти старшему на одобрение.
  // Держим её целиком, а не «что хотели сделать»: одобренную заявку сервер исполняет сам, и
  // пересобирать payload в момент одобрения было бы вторым источником правды.
  const [approvalDraft, setApprovalDraft] = useState<
    { request: MoneyActionSubmitRequest; amountLabel: string; label: string } | null
  >(null);
  const [approvalBusy, setApprovalBusy] = useState(false);

  // Отправка заявки. Отдельно от runClientAction: это не «повтор того же действия», а другое
  // действие с другим исходом — операция не выполняется, а встаёт в очередь.
  const submitApprovalDraft = async () => {
    if (approvalDraft === null) return;
    setApprovalBusy(true);
    try {
      const nextBackend = requireBackend(backend, t);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      await apiClients.moneyActions.submit(nextBackend.branchId, approvalDraft.request);
      setFeedback({ label: approvalDraft.label, state: 'confirmed', detail: t('op.players.approval.sent') });
      setApprovalDraft(null);
      setCorrectionOpen(false);
      setRefundTarget(null);
    } catch (error) {
      setFeedback({
        label: approvalDraft.label,
        state: 'failed',
        detail: projectOperatorError(error, t).detail
      });
    } finally {
      setApprovalBusy(false);
    }
  };

  const runClientAction = async (id: PlayerActionId, label: string, options?: { topUpMinorUnits?: number; refundMinorUnits?: number }) => {
    setFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);

      if (id === 'topUp') {
        if (!hasPermission(nextBackend.session, permissionNames.topUpWallet)) {
          throw new Error(t('op.players.error.noPermTopUp'));
        }

        const backendClient = requireSelectedBackendClient();

        // Пресет-чипы (+100/+200/+500) передают сумму напрямую аргументом — стейт поля пополнения
        // асинхронный, читать его сразу после setState нельзя. Кнопка «Пополнить» без аргумента
        // берёт введённую в поле сумму как раньше.
        const topUpMinorUnits = options?.topUpMinorUnits ?? parseMoneyInputMinorUnits(walletTopUpAmount);
        if (topUpMinorUnits === null) {
          throw new Error(t('op.players.error.topUpInvalid'));
        }
        // §7.5: поле причины пустое с плейсхолдером — пустой ввод сабмитится тем же
        // дефолтом, что и раньше был значением поля (единая аудиторская строка).
        const reason = resolveReasonInput(walletTopUpReason, t('op.players.actions.topUpDefault'));

        const wallet = await apiClients.players.topUpWallet(backendClient.playerAccountId, {
          organizationId: nextBackend.session.organizationId,
          amount: { currencyCode, minorUnits: topUpMinorUnits },
          reason,
          idempotencyKey: createIdempotencyKey('wallet-top-up')
        });
        setWalletSummary(wallet);
        bumpLedger();
      } else if (id === 'writeOffDebt') {
        if (!hasPermission(nextBackend.session, permissionNames.payDebt)) {
          throw new Error(t('op.players.error.noPermDebt'));
        }

        const backendClient = requireSelectedBackendClient();

        const debtPaymentMinorUnits = parseMoneyInputMinorUnits(debtPaymentAmount);
        if (debtPaymentMinorUnits === null || debtPaymentMinorUnits > debt) {
          throw new Error(t('op.players.error.debtInvalid'));
        }
        // §7.5: та же сабмит-время подстановка дефолта для пустого поля причины.
        const reason = resolveReasonInput(debtPaymentReason, t('op.players.actions.writeOffDebtDefault'));

        const wallet = await apiClients.players.payDebt(backendClient.playerAccountId, {
          organizationId: nextBackend.session.organizationId,
          amount: { currencyCode, minorUnits: debtPaymentMinorUnits },
          reason,
          idempotencyKey: createIdempotencyKey('debt-payment')
        });
        setWalletSummary(wallet);
        bumpLedger();
        setPayDebtOpen(false);
      } else if (id === 'newCard') {
        if (!hasPermission(nextBackend.session, permissionNames.createPlayerAccount)) {
          throw new Error(t('op.players.error.noPermCreate'));
        }

        const displayName = newPlayerName.trim() || clientSearch.trim();
        if (!displayName) {
          throw new Error(t('op.players.error.createNameRequired'));
        }

        // Тот же человек со вторым номером-двойником — это разошедшиеся баланс, долг и история.
        // Поиск по номеру здесь дешевле, чем потом сводить две карточки руками.
        const phone = newPlayerPhone.trim();
        if (phone.length > 0) {
          const digits = fullPhoneDigits(phone);
          const sameNumber = (await apiClients.players.searchPlayers(nextBackend.branchId, phone, 5))
            .find((candidate) => fullPhoneDigits(readString(candidate, 'phoneNumber')) === digits);
          if (sameNumber) {
            setNewClientOpen(false);
            handleSelectClient(readString(sameNumber, 'playerAccountId'));
            throw new Error(t('op.players.error.phoneTaken', { name: readString(sameNumber, 'displayName') }));
          }
        }

        const created = await apiClients.players.createPlayer(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          displayName,
          phoneNumber: newPlayerPhone.trim() || null,
          idempotencyKey: createIdempotencyKey('player-create')
        });
        const createdClient = projectPlayerClient({
          playerAccountId: readString(created, 'playerAccountId'),
          displayName: readString(created, 'displayName', t('op.players.newClient')),
          phoneNumber: readString(created, 'phoneNumber'),
          walletBalanceMinorUnits: 0,
          debtBalanceMinorUnits: 0,
          activePackageCount: 0,
          isActive: true
        }, t);
        setClients((items) => [createdClient, ...items]);
        handleSelectClient(createdClient.playerAccountId ?? null);
        setNewPlayerName('');
        setNewPlayerPhone('');
      } else if (id === 'booking') {
        if (!hasPermission(nextBackend.session, permissionNames.manageReservations)) {
          throw new Error(t('op.players.error.noPermBooking'));
        }

        requireSelectedBackendClient();
        openBookingModal();
      } else if (id === 'correction') {
        if (!hasPermission(nextBackend.session, permissionNames.manualCorrection)) {
          throw new Error(t('op.players.error.noPermCorrection'));
        }

        const backendClient = requireSelectedBackendClient();

        const quantities = correctionQuantities(correctionAccount, correctionDirection, correctionAmount);
        const reason = correctionReason.trim();
        if (quantities === null || !reason) {
          throw new Error(t('op.players.error.correctionInvalid'));
        }

        const idempotencyKey = createIdempotencyKey('manual-correction');
        let wallet: WalletSummaryDto;
        try {
          wallet = await apiClients.players.manualCorrection(backendClient.playerAccountId, {
            organizationId: nextBackend.session.organizationId,
            accountType: correctionAccount,
            amount: { currencyCode, minorUnits: quantities.minorUnits },
            quantitySeconds: quantities.quantitySeconds,
            reason,
            idempotencyKey
          });
        } catch (error) {
          if (!requiresManagerApproval(error)) throw error;
          setApprovalDraft({
            label,
            amountLabel: formatMinorUnits(Math.abs(quantities.minorUnits), currencyCode),
            request: correctionApprovalRequest({
              organizationId: nextBackend.session.organizationId,
              playerAccountId: backendClient.playerAccountId,
              accountType: correctionAccount,
              signedAmountMinorUnits: quantities.minorUnits,
              quantitySeconds: quantities.quantitySeconds,
              currencyCode,
              reason,
              idempotencyKey
            })
          });
          return;
        }
        setWalletSummary(wallet);
        bumpLedger();
        setCorrectionOpen(false);
      } else if (id === 'refund') {
        if (!hasPermission(nextBackend.session, permissionNames.refundLedgerEntry)) {
          throw new Error(t('op.players.error.noPermRefund'));
        }

        const backendClient = requireSelectedBackendClient();
        if (refundTarget === null || refundTarget.reversesLedgerEntryId !== null) {
          throw new Error(t('op.players.error.refundInvalid'));
        }

        const reason = refundReason.trim();
        const originalMinorUnits = Math.abs(refundTarget.amount.minorUnits);
        const refundMinorUnits = options?.refundMinorUnits;
        if (!reason || refundMinorUnits === undefined || refundMinorUnits <= 0 || refundMinorUnits > originalMinorUnits) {
          throw new Error(t('op.players.error.refundInvalid'));
        }
        const refundIdempotencyKey = createIdempotencyKey('ledger-refund');
        try {
          await apiClients.players.refundLedgerEntry(backendClient.playerAccountId, refundTarget.ledgerEntryId, {
            organizationId: nextBackend.session.organizationId,
            ledgerEntryId: refundTarget.ledgerEntryId,
            amount: { currencyCode, minorUnits: refundMinorUnits },
            reason,
            idempotencyKey: refundIdempotencyKey
          });
        } catch (error) {
          if (!requiresManagerApproval(error)) throw error;
          setApprovalDraft({
            label,
            amountLabel: formatMinorUnits(refundMinorUnits, currencyCode),
            request: refundApprovalRequest({
              organizationId: nextBackend.session.organizationId,
              playerAccountId: backendClient.playerAccountId,
              ledgerEntryId: refundTarget.ledgerEntryId,
              accountType: refundTarget.accountType,
              originalSignedMinorUnits: refundTarget.amount.minorUnits,
              refundMinorUnits,
              currencyCode,
              reason,
              idempotencyKey: refundIdempotencyKey
            })
          });
          return;
        }
        const wallet = await apiClients.players.getWalletSummary(backendClient.playerAccountId);
        setWalletSummary(wallet);
        bumpLedger();
        setRefundTarget(null);
      } else if (id === 'updateProfile') {
        if (!hasPermission(nextBackend.session, permissionNames.createPlayerAccount)) {
          throw new Error(t('op.players.error.noPermEditProfile'));
        }

        const backendClient = requireSelectedBackendClient();
        const displayName = editName.trim();
        if (!displayName) {
          throw new Error(t('op.players.error.editNameRequired'));
        }

        const updated = await apiClients.players.updateProfile(nextBackend.branchId, backendClient.playerAccountId, {
          organizationId: nextBackend.session.organizationId,
          displayName,
          phoneNumber: editPhone.trim() || null
        });
        setClients((items) => items.map((c) => c.playerAccountId === backendClient.playerAccountId
          ? { ...c, name: updated.displayName, phoneNumber: updated.phoneNumber ?? '' }
          : c));
        setEditOpen(false);
      } else if (id === 'toggleActive') {
        if (!hasPermission(nextBackend.session, permissionNames.createPlayerAccount)) {
          throw new Error(t('op.players.error.noPermActiveState'));
        }

        const backendClient = requireSelectedBackendClient();
        const nextActive = !backendClient.isActive;
        const updated = await apiClients.players.setActiveState(nextBackend.branchId, backendClient.playerAccountId, {
          organizationId: nextBackend.session.organizationId,
          isActive: nextActive
        });
        setClients((items) => items.map((c) => {
          if (c.playerAccountId !== backendClient.playerAccountId) return c;
          const isActive = updated.isActive;
          return {
            ...c,
            isActive,
            status: !isActive ? 'inactive' : c.debtMinorUnits > 0 ? 'debt' : 'active',
            tone: !isActive ? 'regular' : c.debtMinorUnits > 0 ? 'debt' : 'active'
          };
        }));
        setActiveStateOpen(false);
      } else {
        throw new Error(t('op.players.error.actionNotConnected'));
      }

      setFeedback({ label, state: 'confirmed' });
    } catch (error) {
      setFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    }
  };

  // Скелетон — только на холодном входе (данных ещё нет). Тёплый возврат/поиск держит текущий список,
  // поэтому отложенный анти-флэш здесь не нужен: показываем скелетон сразу, без пустого экрана.
  const showSkeleton = loadStatus === 'loading' && clients.length === 0;
  const emptyDescription = loadStatus === 'backend' ? t('op.players.list.emptyBackend') : t('op.players.list.emptyConnect');

  // Ближайшая четверть часа через 15 минут — тот же старт по умолчанию, что и в «Бронях»:
  // один и тот же смысл не должен считаться по-разному на двух экранах.
  const nextQuarterHour = () => {
    const at = new Date(Date.now() + 15 * 60_000);
    at.setMinutes(Math.ceil(at.getMinutes() / 15) * 15, 0, 0);
    return toDateTimeInputValue(at);
  };

  const openBookingModal = () => {
    setBookingDraft({ startsAt: nextQuarterHour(), durationMinutes: 60, seatId: '' });
    const nextBackend = backend;
    if (nextBackend === null) return;
    // Свободные места подтягиваем рядом с диалогом: без них бронь получалась «без места», и её
    // приходилось дозаполнять в другом разделе.
    void createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session).floorMap
      .getFloorMap(nextBackend.branchId)
      .then((map) => setBookingSeats(map.seats
        .filter((seat) => seat.state === 'free' || seat.state === 'ready')
        .map((seat) => ({ seatId: seat.seatId, label: seat.seatName }))))
      .catch(() => setBookingSeats([]));
  };

  const submitBooking = async () => {
    const draft = bookingDraft;
    if (draft === null) return;
    const label = t('op.players.actions.bookingBtn');
    setBookingBusy(true);
    setFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      const backendClient = requireSelectedBackendClient();
      const startsAt = new Date(draft.startsAt);
      if (Number.isNaN(startsAt.getTime())) throw new Error(t('op.booking.error.invalidStart'));
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      await apiClients.reservations.create(nextBackend.branchId, {
        organizationId: nextBackend.session.organizationId,
        playerAccountId: backendClient.playerAccountId,
        seatId: draft.seatId || null,
        customerName: backendClient.name,
        phoneNumber: backendClient.phoneNumber || null,
        startsAtUtc: startsAt.toISOString(),
        durationMinutes: draft.durationMinutes,
        source: 'operator',
        // note sent to the API; surfaces in the audit log shown to operators
        note: t('op.players.note.createdFromCard')
      });
      setBookingDraft(null);
      setFeedback({ label, state: 'confirmed' });
      // Обновляем кросс-контекст профиля, чтобы новая бронь сразу появилась полосой «ближайшая бронь».
      bumpLedger();
    } catch (error) {
      setFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setBookingBusy(false);
    }
  };

  const submitNewClient = async () => {
    await runClientAction('newCard', t('op.pos.cart.newCardLabel'));
    setNewClientOpen(false);
  };

  const loadMoreLedger = async () => {
    // ledgerLoading в guard: повторный клик «Показать ещё» в полёте иначе задвоил бы страницу (аппенд дважды).
    if (backend === null || selectedClient === null || !selectedClient.playerAccountId || ledgerCursor === null || ledgerLoading) {
      return;
    }

    const playerAccountId = selectedClient.playerAccountId;
    setLedgerLoading(true);
    try {
      const apiClients = createAuthenticatedOperatorClients(backend.config, backend.session);
      const page = await apiClients.players.getLedger(playerAccountId, {
        entryType: ledgerFilter ?? undefined,
        cursor: ledgerCursor,
        limit: 50
      });
      setLedgerEntries((current) => [...current, ...page.items]);
      setLedgerCursor(page.nextCursor);
    } catch (error) {
      setFeedback({ label: t('op.players.tabs.history'), state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setLedgerLoading(false);
    }
  };

  const bumpLedger = () => setLedgerReloadNonce((n) => n + 1);
  const bumpWallet = () => setWalletReloadNonce((n) => n + 1);

  const openEditProfile = () => {
    setEditName(selectedClient?.name ?? '');
    setEditPhone(selectedClient?.phoneNumber ?? '');
    setEditOpen(true);
  };

  // Смена фильтра: сброс журнала и курсора — эффект перезагрузит первую страницу (ledgerFilter в deps).
  const changeLedgerFilter = (entryType: string | null) => {
    setLedgerEntries([]);
    setLedgerCursor(null);
    setLedgerFilter(entryType);
  };

  // Единая «текущая» отметка на рендер — метки «Новый»/относительный визит в таблице детерминируемы
  // от одного значения (не по Date.now() в каждой ячейке).
  const nowMs = Date.now();

  return (
    <main className="workspace-screen clients-screen">
      <section className="clients-head">
        <h1>
          <strong className="clients-head-name">{t('op.players.title')}</strong>
          {' · '}
          <span className="clients-head-tagline">{t('op.players.tagline')}</span>
        </h1>
        <div className="clients-head-metrics">
          <StateFlag label={t('op.players.overview.clients')} value={String(overview.count)} />
          <StateFlag label={t('op.players.overview.deposits')} value={formatMinorUnits(overview.depositMinorUnits, currencyCode)} />
          <StateFlag
            label={t('op.players.overview.debts')}
            value={formatMinorUnits(overview.debtMinorUnits, currencyCode)}
            tone={overview.debtMinorUnits > 0 ? 'warning' : undefined}
          />
        </div>
      </section>

      <div className="clients-grid">
        <ClientsTable
          clients={visibleClients}
          segments={segments}
          activeSegment={activeSegment}
          selectedClientId={selectedClient?.playerAccountId ?? null}
          search={clientSearch}
          showSkeleton={showSkeleton}
          isLoading={loadStatus === 'loading'}
          emptyDescription={emptyDescription}
          currencyCode={currencyCode}
          canCreatePlayer={canCreatePlayer}
          liveContextByClient={liveContextByClient}
          nowMs={nowMs}
          onNewClient={() => setNewClientOpen(true)}
          onSearchChange={setClientSearch}
          onSelectSegment={setActiveSegment}
          onSelectClient={handleSelectClient}
        />

        {selectedClient !== null && (
          <ClientDrawer
            client={selectedClient}
            liveContext={liveContextByClient.get(selectedClient.playerAccountId ?? '') ?? { session: null, nextBooking: null }}
            balanceMinorUnits={balance}
            heldMinorUnits={held}
            debtMinorUnits={debt}
            currencyCode={currencyCode}
            recentEntries={ledgerEntries}
            packages={playerPackages}
            packagesLoading={packagesLoading}
            packagesErrorDetail={packagesErrorDetail}
            topUpAmount={walletTopUpAmount}
            canTopUp={canTopUpWallet}
            onChangeTopUpAmount={setWalletTopUpAmount}
            onTopUp={() => runClientAction('topUp', t('op.players.actions.topUpBtn'))}
            onOpenDcTopUp={() => setDcTopUpOpen(true)}
            canPayDebt={canPayDebt}
            onOpenPayDebt={() => setPayDebtOpen(true)}
            canManageClient={canManageClient}
            canCorrect={canManualCorrect}
            canCreateReservation={canCreateClientReservation}
            onCorrect={() => setCorrectionOpen(true)}
            onCreateReservation={() => runClientAction('booking', t('op.players.actions.bookingBtn'))}
            onEditProfile={openEditProfile}
            onToggleActive={() => setActiveStateOpen(true)}
            reputation={reputation}
            onOpenFullHistory={() => setHistoryModalOpen(true)}
            onClose={() => handleSelectClient(null)}
          />
        )}
      </div>

      {historyModalOpen && selectedClient !== null && (
        <PanelModal title={t('op.players.wallet.allHistory')} subtitle={selectedClient.name} onClose={() => setHistoryModalOpen(false)}>
          <HistorySection
            entries={ledgerEntries}
            currencyCode={currencyCode}
            activeFilter={ledgerFilter}
            onFilterChange={changeLedgerFilter}
            hasMore={ledgerCursor !== null}
            onLoadMore={() => void loadMoreLedger()}
            loading={ledgerLoading}
            canRefund={canRefundLedger}
            onRefund={(entry) => setRefundTarget(entry)}
          />
        </PanelModal>
      )}

      {bookingDraft !== null && selectedClient !== null && (
        <ClientBookingModal
          clientName={selectedClient.name}
          draft={bookingDraft}
          seats={bookingSeats}
          busy={bookingBusy}
          onChange={setBookingDraft}
          onClose={() => setBookingDraft(null)}
          onSubmit={() => void submitBooking()}
        />
      )}

      {newClientOpen && (
        <NewClientModal
          name={newPlayerName}
          phone={newPlayerPhone}
          onChangeName={setNewPlayerName}
          onChangePhone={setNewPlayerPhone}
          onClose={() => setNewClientOpen(false)}
          onSubmit={() => void submitNewClient()}
        />
      )}

      {correctionOpen && (
        <CorrectionModal
          account={correctionAccount}
          direction={correctionDirection}
          amount={correctionAmount}
          reason={correctionReason}
          onChangeAccount={setCorrectionAccount}
          onChangeDirection={setCorrectionDirection}
          onChangeAmount={setCorrectionAmount}
          onChangeReason={setCorrectionReason}
          onClose={() => setCorrectionOpen(false)}
          onSubmit={() => void runClientAction('correction', t('op.players.actions.correctionLabel'))}
          busy={feedback.state === 'pending'}
        />
      )}

      {payDebtOpen && (
        <PayDebtModal
          amount={debtPaymentAmount}
          reason={debtPaymentReason}
          onChangeAmount={setDebtPaymentAmount}
          onChangeReason={setDebtPaymentReason}
          onClose={() => setPayDebtOpen(false)}
          onSubmit={() => void runClientAction('writeOffDebt', t('op.players.actions.writeOffDebtBtn'))}
          busy={feedback.state === 'pending'}
        />
      )}

      {dcTopUpOpen && backend !== null && selectedClient !== null && selectedClient.source === 'backend' && selectedClient.playerAccountId && (
        <DcTopUpDialog
          backend={{ dcTopUps: createAuthenticatedOperatorClients(backend.config, backend.session).dcTopUps }}
          branchId={backend.branchId}
          playerAccountId={selectedClient.playerAccountId}
          onClose={() => setDcTopUpOpen(false)}
          onCredited={() => { bumpLedger(); bumpWallet(); }}
        />
      )}

      {refundTarget !== null && (
        <RefundModal
          entry={refundTarget}
          currencyCode={currencyCode}
          reason={refundReason}
          onChangeReason={setRefundReason}
          onClose={() => setRefundTarget(null)}
          onConfirm={(refundMinorUnits) => void runClientAction('refund', t('op.players.actions.refundLabel'), { refundMinorUnits })}
          busy={feedback.state === 'pending'}
        />
      )}

      {editOpen && (
        <EditProfileModal
          name={editName}
          phone={editPhone}
          onChangeName={setEditName}
          onChangePhone={setEditPhone}
          onClose={() => setEditOpen(false)}
          onSubmit={() => void runClientAction('updateProfile', t('op.players.actions.editProfileLabel'))}
          busy={feedback.state === 'pending'}
        />
      )}

      {activeStateOpen && (
        <ActiveStateConfirmModal
          mode={isSelectedInactive ? 'reactivate' : 'deactivate'}
          onClose={() => setActiveStateOpen(false)}
          onConfirm={() => void runClientAction('toggleActive', isSelectedInactive ? t('op.players.actions.reactivateLabel') : t('op.players.actions.deactivateLabel'))}
          busy={feedback.state === 'pending'}
        />
      )}

      {approvalDraft !== null && (
        <ApprovalRequestModal
          amountLabel={approvalDraft.amountLabel}
          onClose={() => setApprovalDraft(null)}
          onConfirm={() => void submitApprovalDraft()}
          busy={approvalBusy}
        />
      )}
    </main>
  );
}
