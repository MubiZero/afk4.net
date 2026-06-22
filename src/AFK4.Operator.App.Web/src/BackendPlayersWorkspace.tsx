import { useEffect, useState } from 'react';
import { UserRoundPlus } from 'lucide-react';
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
  requireBackend,
  workspaceLoadStatusLabel
} from './operatorHelpers';
import { fixturePlayers, playerStatusLabel, projectPlayerClient, buildClientSegments, matchesSegment, type PlayerClientItem, type ClientSegmentId } from './players/playersModel';
import { FeedbackNotice, StateFlag } from './operatorPrimitives';
import { useDeferredFlag } from './useDeferredFlag';
import { ClientList } from './players/ClientList';
import { ClientDetail, type ClientDetailTab } from './players/ClientDetail';
import { NewClientModal } from './players/NewClientModal';

type PlayerActionId = 'topUp' | 'writeOffDebt' | 'buyPackage' | 'booking' | 'newCard';

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
  const [ledgerCursor, setLedgerCursor] = useState<string | null>(null);
  const [ledgerFilter, setLedgerFilter] = useState<string | null>(null);
  const [ledgerLoading, setLedgerLoading] = useState(false);

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
        const players = await apiClients.players.searchPlayers(backend.branchId, clientSearch, 25);
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
    canViewLedger
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

  const canPurchasePackage = backend !== null
    && selectedClient !== null
    && selectedClient.source === 'backend'
    && Boolean(selectedClient.playerAccountId)
    && hasPermission(backend.session, permissionNames.purchasePackage);
  const canTopUpWallet = backend !== null
    && selectedClient !== null
    && selectedClient.source === 'backend'
    && Boolean(selectedClient.playerAccountId)
    && hasPermission(backend.session, permissionNames.topUpWallet);
  const canPayDebt = backend !== null
    && selectedClient !== null
    && selectedClient.source === 'backend'
    && Boolean(selectedClient.playerAccountId)
    && debt > 0
    && hasPermission(backend.session, permissionNames.payDebt);
  const canCreatePlayer = backend !== null && hasPermission(backend.session, permissionNames.createPlayerAccount);
  const canCreateClientReservation = backend !== null
    && selectedClient !== null
    && selectedClient.source === 'backend'
    && Boolean(selectedClient.playerAccountId)
    && hasPermission(backend.session, permissionNames.manageReservations);

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

  // Смена фильтра: сброс журнала и курсора — эффект перезагрузит первую страницу (ledgerFilter в deps).
  const changeLedgerFilter = (entryType: string | null) => {
    setLedgerEntries([]);
    setLedgerCursor(null);
    setLedgerFilter(entryType);
  };

  return (
    <main className="workspace-screen clients-screen">
      <section className="screen-head clients-head">
        <div>
          <span>{t('op.players.title')}</span>
          <h1>{t('op.players.heading')}</h1>
        </div>
        <div className="screen-actions clients-head-actions">
          <span className={`map-load-state ${loadStatus === 'backend' ? 'ready' : loadStatus}`}>{workspaceLoadStatusLabel(loadStatus, t('op.pos.platformConnected'), t)}</span>
          <StateFlag label={t('op.players.strip.clients')} value={String(clients.length)} />
          <StateFlag label={t('op.players.strip.platform')} value={String(clients.filter((client) => client.source === 'backend').length)} critical={loadStatus !== 'backend'} />
          <button type="button" className="clients-new-client-btn" disabled={!canCreatePlayer} onClick={() => setNewClientOpen(true)}>
            <UserRoundPlus size={15} aria-hidden="true" />{t('op.players.newClient.openBtn')}
          </button>
        </div>
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
    </main>
  );
}
