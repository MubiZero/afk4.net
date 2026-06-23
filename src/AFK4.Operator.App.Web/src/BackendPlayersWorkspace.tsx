import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { projectOperatorError } from './apiErrors';
import type { LedgerEntryDto, PackageOptionDto, PlayerPackageDto, WalletSummaryDto } from './operatorApiClients';
import type { Feedback, LoadStatus, OperatorBackendContext } from './operatorTypes';
import { hasPermission, permissionNames } from './operatorPermissions';
import {
  createAuthenticatedOperatorClients,
  createIdempotencyKey,
  emptyFeedback,
  formatMoneyInputMinorUnits,
  parseMoneyInputMinorUnits,
  readMoney,
  readNumber,
  readString,
  requireBackend
} from './operatorHelpers';
import { fixturePlayers, playerStatusLabel, projectPlayerClient, buildClientSegments, matchesSegment, type PlayerClientItem, type ClientSegmentId } from './players/playersModel';
import { FeedbackNotice } from './operatorPrimitives';
import { useDeferredFlag } from './useDeferredFlag';
import { ClientList } from './players/ClientList';
import { ClientDetail, type ClientDetailTab } from './players/ClientDetail';
import { NewClientModal } from './players/NewClientModal';
import { CorrectionModal, type CorrectionAccount, type CorrectionDirection } from './players/CorrectionModal';
import { RefundModal } from './players/RefundModal';
import { PinModal } from './players/PinModal';
import { EditProfileModal } from './players/EditProfileModal';
import { ActiveStateConfirmModal } from './players/ActiveStateConfirmModal';

type PlayerActionId = 'topUp' | 'writeOffDebt' | 'buyPackage' | 'booking' | 'newCard' | 'correction' | 'refund' | 'setPin' | 'updateProfile' | 'toggleActive';

export function BackendPlayersWorkspace({ currencyCode, backend }: { currencyCode: string; backend: OperatorBackendContext | null }) {
  const { t } = useI18n();
  const [clientSearch, setClientSearch] = useState('');
  const [activeSegment, setActiveSegment] = useState<ClientSegmentId>('all');
  const [activeTab, setActiveTab] = useState<ClientDetailTab>('wallet');
  const [newClientOpen, setNewClientOpen] = useState(false);
  const [selectedClientId, setSelectedClientId] = useState<string | null>(null);
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const [loadStatus, setLoadStatus] = useState<LoadStatus>(backend === null ? 'fixture' : 'loading');
  const [clients, setClients] = useState<PlayerClientItem[]>(() => backend === null ? fixturePlayers(currencyCode, t) : []);
  const [walletSummary, setWalletSummary] = useState<WalletSummaryDto | null>(null);
  const [packageOptions, setPackageOptions] = useState<PackageOptionDto[]>([]);
  const [selectedPackageDefinitionId, setSelectedPackageDefinitionId] = useState('');
  const [selectedClientPackages, setSelectedClientPackages] = useState<PlayerPackageDto[]>([]);
  const [walletTopUpAmount, setWalletTopUpAmount] = useState('100.00');
  const [walletTopUpReason, setWalletTopUpReason] = useState(() => t('op.players.actions.topUpDefault'));
  const [debtPaymentAmount, setDebtPaymentAmount] = useState('');
  const [debtPaymentReason, setDebtPaymentReason] = useState(() => t('op.players.actions.writeOffDebtDefault'));
  const [newPlayerName, setNewPlayerName] = useState('');
  const [newPlayerPhone, setNewPlayerPhone] = useState('');
  const [ledgerEntries, setLedgerEntries] = useState<LedgerEntryDto[]>([]);
  const [recentEntries, setRecentEntries] = useState<LedgerEntryDto[]>([]);
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
  const [pinOpen, setPinOpen] = useState(false);
  const [pinValue, setPinValue] = useState('');
  const [editOpen, setEditOpen] = useState(false);
  const [editName, setEditName] = useState('');
  const [editPhone, setEditPhone] = useState('');
  const [activeStateOpen, setActiveStateOpen] = useState(false);
  const [ledgerReloadNonce, setLedgerReloadNonce] = useState(0);

  useEffect(() => {
    if (backend === null) {
      setLoadStatus('fixture');
      setClients(fixturePlayers(currencyCode, t));
      setPackageOptions([]);
      setSelectedPackageDefinitionId('');
      return undefined;
    }

    let disposed = false;
    const loadPlayers = async () => {
      setLoadStatus('loading');
      try {
        const apiClients = createAuthenticatedOperatorClients(backend.config, backend.session);
        const players = await apiClients.players.searchPlayers(backend.branchId, clientSearch, 25, true);
        const nextPackageOptions = hasPermission(backend.session, permissionNames.viewPackages) || hasPermission(backend.session, permissionNames.purchasePackage)
          ? await apiClients.settings.getPackageOptions(backend.branchId).catch(() => [])
          : [];
        if (disposed) {
          return;
        }

        const nextClients = Array.isArray(players) ? players.map((p) => projectPlayerClient(p, t)) : [];
        const nextOptions = Array.isArray(nextPackageOptions) ? nextPackageOptions : [];
        setClients(nextClients.length > 0 ? nextClients : []);
        setPackageOptions(nextOptions);
        setSelectedPackageDefinitionId((current) => current && nextOptions.some((option) => readString(option, 'packageDefinitionId') === current)
          ? current
          : readString(nextOptions[0], 'packageDefinitionId'));
        setSelectedClientId((current) => current && nextClients.some((client) => client.playerAccountId === current)
          ? current
          : nextClients[0]?.playerAccountId ?? null);
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

  const selectedClient = clients.find((client) => client.playerAccountId === selectedClientId)
    ?? clients[0]
    ?? null;

  const canViewLedger = backend !== null
    && selectedClient !== null
    && selectedClient.source === 'backend'
    && Boolean(selectedClient.playerAccountId)
    && hasPermission(backend.session, permissionNames.viewBilling);

  useEffect(() => {
    if (backend === null || selectedClient === null || !selectedClient.playerAccountId || selectedClient.source !== 'backend') {
      setWalletSummary(null);
      setSelectedClientPackages([]);
      return undefined;
    }

    const client = selectedClient as PlayerClientItem & { playerAccountId: string; source: 'backend' };
    let disposed = false;
    const loadWallet = async () => {
      try {
        const apiClients = createAuthenticatedOperatorClients(backend.config, backend.session);
        const [wallet, packages] = await Promise.all([
          apiClients.players.getWalletSummary(client.playerAccountId),
          hasPermission(backend.session, permissionNames.viewPackages) || hasPermission(backend.session, permissionNames.purchasePackage)
            ? apiClients.players.getPlayerPackages(client.playerAccountId).catch(() => [])
            : Promise.resolve([])
        ]);
        if (!disposed) {
          setWalletSummary(wallet);
          setSelectedClientPackages(Array.isArray(packages) ? packages : []);
        }
      } catch (error) {
        if (!disposed) {
          setFeedback({ label: client.name, state: 'failed', detail: projectOperatorError(error, t).detail });
          setSelectedClientPackages([]);
        }
      }
    };

    void loadWallet();
    return () => {
      disposed = true;
    };
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, selectedClient?.playerAccountId, selectedClient?.source]);

  // Журнал истории: серверный источник (paged ledger-эндпоинт), отдельно от wallet-summary.
  // Грузим первую страницу при входе на таб «История» / смене клиента / смене фильтра.
  useEffect(() => {
    if (!canViewLedger || activeTab !== 'history' || selectedClient === null || !selectedClient.playerAccountId) {
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
    activeTab,
    selectedClient?.playerAccountId,
    selectedClient?.source,
    ledgerFilter,
    canViewLedger,
    ledgerReloadNonce
  ]);

  // Мини-лента последних операций для вкладки «Кошелёк» — отдельно от фильтруемого журнала
  // вкладки «История»: всегда последние 5 без фильтра. Обновляется после денежных действий (nonce).
  useEffect(() => {
    if (!canViewLedger || selectedClient === null || !selectedClient.playerAccountId) {
      setRecentEntries([]);
      return undefined;
    }

    const nextBackend = backend;
    if (nextBackend === null) {
      return undefined;
    }

    const playerAccountId = selectedClient.playerAccountId;
    let disposed = false;
    const loadRecent = async () => {
      try {
        const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
        const page = await apiClients.players.getLedger(playerAccountId, { limit: 5 });
        if (!disposed) {
          setRecentEntries(page.items.slice(0, 5));
        }
      } catch {
        if (!disposed) {
          setRecentEntries([]);
        }
      }
    };

    void loadRecent();
    return () => {
      disposed = true;
    };
  }, [
    backend?.branchId,
    backend?.config.platformBaseUrl,
    backend?.session.accessToken,
    selectedClient?.playerAccountId,
    canViewLedger,
    ledgerReloadNonce
  ]);

  const segments = buildClientSegments(clients, t);
  const visibleClients = clients.filter((client) => {
    const searchMatches = `${client.name} ${playerStatusLabel(client.status, t)} ${client.detail} ${client.last}`
      .toLowerCase()
      .includes(clientSearch.trim().toLowerCase());
    return matchesSegment(client, activeSegment) && searchMatches;
  });

  const balance = readMoney(walletSummary, 'walletBalance')?.minorUnits ?? selectedClient?.balanceMinorUnits ?? 0;
  const debt = readMoney(walletSummary, 'debtBalance')?.minorUnits ?? selectedClient?.debtMinorUnits ?? 0;
  const selectedClientPackageCount = selectedClientPackages.length || Number.parseInt(selectedClient?.last ?? '', 10) || 0;
  const selectedPackageOption = packageOptions.find((option) => readString(option, 'packageDefinitionId') === selectedPackageDefinitionId)
    ?? packageOptions[0]
    ?? null;

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

  const isSelectedInactive = selectedClient !== null && selectedClient.status === 'inactive';
  const canPurchasePackage = backend !== null
    && selectedClient !== null
    && selectedClient.source === 'backend'
    && Boolean(selectedClient.playerAccountId)
    && !isSelectedInactive
    && hasPermission(backend.session, permissionNames.purchasePackage);
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
  const canSetClientPin = backend !== null
    && selectedClient !== null
    && selectedClient.source === 'backend'
    && Boolean(selectedClient.playerAccountId)
    && !isSelectedInactive
    && hasPermission(backend.session, permissionNames.createPlayerAccount);
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

  const runClientAction = async (id: PlayerActionId, label: string) => {
    setFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);

      if (id === 'topUp') {
        if (!hasPermission(nextBackend.session, permissionNames.topUpWallet)) {
          throw new Error(t('op.players.error.noPermTopUp'));
        }

        const backendClient = requireSelectedBackendClient();

        const topUpMinorUnits = parseMoneyInputMinorUnits(walletTopUpAmount);
        const reason = walletTopUpReason.trim();
        if (topUpMinorUnits === null || !reason) {
          throw new Error(t('op.players.error.topUpInvalid'));
        }

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
        const reason = debtPaymentReason.trim();
        if (debtPaymentMinorUnits === null || !reason || debtPaymentMinorUnits > debt) {
          throw new Error(t('op.players.error.debtInvalid'));
        }

        const wallet = await apiClients.players.payDebt(backendClient.playerAccountId, {
          organizationId: nextBackend.session.organizationId,
          amount: { currencyCode, minorUnits: debtPaymentMinorUnits },
          reason,
          idempotencyKey: createIdempotencyKey('debt-payment')
        });
        setWalletSummary(wallet);
        bumpLedger();
      } else if (id === 'newCard') {
        if (!hasPermission(nextBackend.session, permissionNames.createPlayerAccount)) {
          throw new Error(t('op.players.error.noPermCreate'));
        }

        const displayName = newPlayerName.trim() || clientSearch.trim();
        if (!displayName) {
          throw new Error(t('op.players.error.createNameRequired'));
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
        setSelectedClientId(createdClient.playerAccountId ?? null);
        setNewPlayerName('');
        setNewPlayerPhone('');
      } else if (id === 'buyPackage') {
        if (!hasPermission(nextBackend.session, permissionNames.purchasePackage)) {
          throw new Error(t('op.players.error.noPermPackage'));
        }

        const backendClient = requireSelectedBackendClient();

        let packageOption: PackageOptionDto | null = selectedPackageOption;
        if (packageOption === null) {
          const options = await apiClients.settings.getPackageOptions(nextBackend.branchId);
          const nextOptions = Array.isArray(options) ? options : [];
          setPackageOptions(nextOptions);
          setSelectedPackageDefinitionId(readString(nextOptions[0], 'packageDefinitionId'));
          packageOption = Array.isArray(options) ? options[0] ?? null : null;
        }

        const packageDefinitionId = readString(packageOption, 'packageDefinitionId');
        if (!packageDefinitionId) {
          throw new Error(t('op.players.error.noPackageAvailable'));
        }

        const packagePriceMinorUnits = readNumber(packageOption, 'priceMinorUnits', 0);
        if (packagePriceMinorUnits > balance) {
          throw new Error(t('op.players.error.insufficientDeposit'));
        }

        const purchasedPackage = await apiClients.players.purchasePackage(backendClient.playerAccountId, {
          organizationId: nextBackend.session.organizationId,
          packageDefinitionId,
          idempotencyKey: createIdempotencyKey('package-purchase')
        });
        const [wallet, packages] = await Promise.all([
          apiClients.players.getWalletSummary(backendClient.playerAccountId),
          apiClients.players.getPlayerPackages(backendClient.playerAccountId).catch(() => [purchasedPackage])
        ]);
        setWalletSummary(wallet);
        setSelectedClientPackages(Array.isArray(packages) ? packages : [purchasedPackage]);
        bumpLedger();
      } else if (id === 'booking') {
        if (!hasPermission(nextBackend.session, permissionNames.manageReservations)) {
          throw new Error(t('op.players.error.noPermBooking'));
        }

        const backendClient = requireSelectedBackendClient();

        await apiClients.reservations.create(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          playerAccountId: backendClient.playerAccountId,
          seatId: null,
          customerName: backendClient.name,
          phoneNumber: backendClient.phoneNumber || null,
          startsAtUtc: new Date(Date.now() + 30 * 60_000).toISOString(),
          durationMinutes: 60,
          source: 'operator',
          // note sent to the API; surfaces in the audit log shown to operators
          note: t('op.players.note.createdFromCard')
        });
      } else if (id === 'correction') {
        if (!hasPermission(nextBackend.session, permissionNames.manualCorrection)) {
          throw new Error(t('op.players.error.noPermCorrection'));
        }

        const backendClient = requireSelectedBackendClient();

        const magnitude = parseMoneyInputMinorUnits(correctionAmount);
        const reason = correctionReason.trim();
        if (magnitude === null || magnitude <= 0 || !reason) {
          throw new Error(t('op.players.error.correctionInvalid'));
        }

        const signed = correctionDirection === 'debit' ? -magnitude : magnitude;
        const wallet = await apiClients.players.manualCorrection(backendClient.playerAccountId, {
          organizationId: nextBackend.session.organizationId,
          accountType: correctionAccount,
          amount: { currencyCode, minorUnits: signed },
          quantitySeconds: 0,
          reason,
          idempotencyKey: createIdempotencyKey('manual-correction')
        });
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
        await apiClients.players.refundLedgerEntry(backendClient.playerAccountId, refundTarget.ledgerEntryId, {
          organizationId: nextBackend.session.organizationId,
          ledgerEntryId: refundTarget.ledgerEntryId,
          amount: { currencyCode, minorUnits: Math.abs(refundTarget.amount.minorUnits) },
          reason,
          idempotencyKey: createIdempotencyKey('ledger-refund')
        });
        const wallet = await apiClients.players.getWalletSummary(backendClient.playerAccountId);
        setWalletSummary(wallet);
        bumpLedger();
        setRefundTarget(null);
      } else if (id === 'setPin') {
        if (!hasPermission(nextBackend.session, permissionNames.createPlayerAccount)) {
          throw new Error(t('op.players.error.noPermPin'));
        }

        const backendClient = requireSelectedBackendClient();
        const pin = pinValue.trim();
        if (pin.length < 4) {
          throw new Error(t('op.players.error.pinInvalid'));
        }

        await apiClients.players.setPlayerPin(nextBackend.branchId, backendClient.playerAccountId, { pin });
        setPinValue('');
        setPinOpen(false);
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
        const nextActive = backendClient.status === 'inactive';
        const updated = await apiClients.players.setActiveState(nextBackend.branchId, backendClient.playerAccountId, {
          organizationId: nextBackend.session.organizationId,
          isActive: nextActive
        });
        setClients((items) => items.map((c) => c.playerAccountId === backendClient.playerAccountId
          ? { ...c, status: updated.isActive ? 'active' : 'inactive', tone: updated.isActive ? 'active' : 'regular' }
          : c));
        setActiveStateOpen(false);
      } else {
        throw new Error(t('op.players.error.actionNotConnected'));
      }

      setFeedback({ label, state: 'confirmed' });
    } catch (error) {
      setFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    }
  };

  const showSkeleton = useDeferredFlag(loadStatus === 'loading');
  const emptyDescription = loadStatus === 'backend' ? t('op.players.list.emptyBackend') : t('op.players.list.emptyConnect');

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

  return (
    <main className="workspace-screen clients-screen">
      <section className="clients-head">
        <h1>
          <strong className="clients-head-name">{t('op.players.title')}</strong>
          {' · '}
          <span className="clients-head-tagline">{t('op.players.tagline')}</span>
        </h1>
      </section>

      <FeedbackNotice feedback={feedback} />

      <section className="clients-layout">
        <ClientList
          clients={visibleClients}
          segments={segments}
          activeSegment={activeSegment}
          selectedClientId={selectedClient?.playerAccountId ?? null}
          search={clientSearch}
          showSkeleton={showSkeleton}
          emptyDescription={emptyDescription}
          currencyCode={currencyCode}
          canCreatePlayer={canCreatePlayer}
          onNewClient={() => setNewClientOpen(true)}
          onSearchChange={setClientSearch}
          onSelectSegment={setActiveSegment}
          onSelectClient={setSelectedClientId}
        />

        <ClientDetail
          client={selectedClient}
          activeTab={activeTab}
          balanceMinorUnits={balance}
          debtMinorUnits={debt}
          packageCount={selectedClientPackageCount}
          currencyCode={currencyCode}
          packages={selectedClientPackages}
          options={packageOptions}
          ledgerEntries={ledgerEntries}
          recentEntries={recentEntries}
          ledgerFilter={ledgerFilter}
          ledgerHasMore={ledgerCursor !== null}
          ledgerLoading={ledgerLoading}
          onLedgerFilterChange={changeLedgerFilter}
          onLedgerLoadMore={() => void loadMoreLedger()}
          selectedPackageDefinitionId={selectedPackageDefinitionId}
          topUpAmount={walletTopUpAmount}
          topUpReason={walletTopUpReason}
          debtAmount={debtPaymentAmount}
          debtReason={debtPaymentReason}
          canTopUp={canTopUpWallet}
          canPayDebt={canPayDebt}
          canPurchase={canPurchasePackage}
          canCreateReservation={canCreateClientReservation}
          onSelectTab={setActiveTab}
          onChangeTopUpAmount={setWalletTopUpAmount}
          onChangeTopUpReason={setWalletTopUpReason}
          onChangeDebtAmount={setDebtPaymentAmount}
          onChangeDebtReason={setDebtPaymentReason}
          onTopUp={() => runClientAction('topUp', t('op.players.actions.topUpBtn'))}
          onPayDebt={() => runClientAction('writeOffDebt', t('op.players.actions.writeOffDebtBtn'))}
          onSelectOption={setSelectedPackageDefinitionId}
          onBuy={() => runClientAction('buyPackage', t('op.players.actions.buyPackageBtn'))}
          onCreateReservation={() => runClientAction('booking', t('op.players.actions.bookingBtn'))}
          canManageClient={canManageClient}
          onSetPin={() => setPinOpen(true)}
          onEditProfile={openEditProfile}
          onToggleActive={() => setActiveStateOpen(true)}
          canCorrect={canManualCorrect}
          onCorrect={() => setCorrectionOpen(true)}
          canRefund={canRefundLedger}
          onRefund={(entry) => setRefundTarget(entry)}
        />
      </section>

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

      {refundTarget !== null && (
        <RefundModal
          entry={refundTarget}
          currencyCode={currencyCode}
          reason={refundReason}
          onChangeReason={setRefundReason}
          onClose={() => setRefundTarget(null)}
          onConfirm={() => void runClientAction('refund', t('op.players.actions.refundLabel'))}
          busy={feedback.state === 'pending'}
        />
      )}

      {pinOpen && (
        <PinModal
          pin={pinValue}
          onChangePin={setPinValue}
          onClose={() => setPinOpen(false)}
          onSubmit={() => void runClientAction('setPin', t('op.players.actions.pinLabel'))}
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
    </main>
  );
}
