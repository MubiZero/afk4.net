import { useEffect, useState } from 'react';
import { CalendarClock, CircleDollarSign, ReceiptText, Search, TimerReset, UserRoundPlus } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { projectOperatorError } from './apiErrors';
import type { PackageOptionDto, PlayerPackageDto, WalletSummaryDto } from './operatorApiClients';
import type { Feedback, LoadStatus, OperatorBackendContext } from './operatorTypes';
import { hasPermission, permissionNames } from './operatorPermissions';
import {
  createAuthenticatedOperatorClients,
  createIdempotencyKey,
  dataSourceLabel,
  emptyFeedback,
  fixturePlayers,
  formatMinorUnits,
  formatMoney,
  formatTime,
  packageOptionLabel,
  parseMoneyInputMinorUnits,
  formatMoneyInputMinorUnits,
  playerPackageLabel,
  type PlayerClientItem,
  projectPlayerClient,
  readArray,
  readMoney,
  readNumber,
  readString,
  requireBackend,
  workspaceLoadStatusLabel
} from './operatorHelpers';
import { FeedbackNotice, StateFlag } from './operatorPrimitives';

type PlayerActionId = 'topUp' | 'writeOffDebt' | 'buyPackage' | 'booking' | 'newCard';

export function BackendPlayersWorkspace({ currencyCode, backend }: { currencyCode: string; backend: OperatorBackendContext | null }) {
  const { t } = useI18n();
  const [clientSearch, setClientSearch] = useState('');
  const [activeSegment, setActiveSegment] = useState<string>(() => t('op.players.segments.all'));
  const [selectedClientId, setSelectedClientId] = useState<string | null>(null);
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const [loadStatus, setLoadStatus] = useState<LoadStatus>(backend === null ? 'fixture' : 'loading');
  const [clients, setClients] = useState<PlayerClientItem[]>(() => backend === null ? fixturePlayers(currencyCode) : []);
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

  useEffect(() => {
    if (backend === null) {
      setLoadStatus('fixture');
      setClients(fixturePlayers(currencyCode));
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

        const nextClients = Array.isArray(players) ? players.map(projectPlayerClient) : [];
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
          setFeedback({ label: t('op.players.error.loadFailed'), state: 'failed', detail: projectOperatorError(error).detail });
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
          setFeedback({ label: client.name, state: 'failed', detail: projectOperatorError(error).detail });
          setSelectedClientPackages([]);
        }
      }
    };

    void loadWallet();
    return () => {
      disposed = true;
    };
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, selectedClient?.playerAccountId, selectedClient?.source]);

  const segmentAll = t('op.players.segments.all');
  const segmentVip = t('op.players.segments.vip');
  const segmentDebt = t('op.players.segments.debt');
  const segmentNew = t('op.players.segments.new');
  const segmentSleeping = t('op.players.segments.sleeping');

  const visibleClients = clients.filter((client) => {
    const segmentMatches = activeSegment === segmentAll
      || (activeSegment === segmentVip && client.tone === 'vip')
      || (activeSegment === segmentDebt && client.debtMinorUnits > 0)
      || (activeSegment === segmentNew && client.source === 'backend')
      || (activeSegment === segmentSleeping && client.status === 'Неактивен');
    const searchMatches = `${client.name} ${client.status} ${client.detail} ${client.last}`.toLowerCase().includes(clientSearch.trim().toLowerCase());
    return segmentMatches && searchMatches;
  });
  const balance = readMoney(walletSummary, 'walletBalance')?.minorUnits ?? selectedClient?.balanceMinorUnits ?? 0;
  const debt = readMoney(walletSummary, 'debtBalance')?.minorUnits ?? selectedClient?.debtMinorUnits ?? 0;
  const recentEntries = readArray(walletSummary, 'recentEntries');
  const selectedClientPackageCount = selectedClientPackages.length || Number.parseInt(selectedClient?.last ?? '', 10) || 0;
  const selectedPackageOption = packageOptions.find((option) => readString(option, 'packageDefinitionId') === selectedPackageDefinitionId)
    ?? packageOptions[0]
    ?? null;
  const selectedPackagePriceMinorUnits = selectedPackageOption === null ? 0 : readNumber(selectedPackageOption, 'priceMinorUnits', 0);
  const selectedPackageCurrencyCode = selectedPackageOption === null ? currencyCode : readString(selectedPackageOption, 'currencyCode', currencyCode);
  const selectedPackageIncludedMinutes = selectedPackageOption === null ? 0 : Math.floor(readNumber(selectedPackageOption, 'includedSeconds', 0) / 60);
  const selectedPackageBonusMinutes = selectedPackageOption === null ? 0 : Math.floor(readNumber(selectedPackageOption, 'bonusSeconds', 0) / 60);
  const selectedPackageTotalMinutes = selectedPackageIncludedMinutes + selectedPackageBonusMinutes;
  const selectedPackageExpiresDays = selectedPackageOption === null ? 0 : readNumber(selectedPackageOption, 'expiresAfterDays', 0);
  const canAffordSelectedPackage = selectedPackageOption !== null && balance >= selectedPackagePriceMinorUnits;
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
      const nextBackend = requireBackend(backend);
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
        });
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
          // technical note sent to the API, not displayed to the user
          note: 'Создано из карточки клиента'
        });
      } else {
        throw new Error(t('op.players.error.actionNotConnected'));
      }

      setFeedback({ label, state: 'confirmed' });
    } catch (error) {
      setFeedback({ label, state: 'failed', detail: projectOperatorError(error).detail });
    }
  };

  type PlayerAction = {
    id: PlayerActionId;
    label: string;
    detail: string;
    Icon: React.ComponentType<{ size: number }>;
    disabled: boolean;
  };

  const playerActions: PlayerAction[] = [
    {
      id: 'topUp',
      label: t('op.players.actions.topUpBtn'),
      detail: `${walletTopUpAmount || '0'} ${currencyCode}`,
      Icon: CircleDollarSign,
      disabled: !canTopUpWallet
    },
    {
      id: 'writeOffDebt',
      label: t('op.players.actions.writeOffDebtBtn'),
      detail: debtPaymentAmount ? `${debtPaymentAmount} ${currencyCode}` : t('op.players.actions.writeOffDebtNone'),
      Icon: ReceiptText,
      disabled: !canPayDebt
    },
    {
      id: 'buyPackage',
      label: t('op.players.actions.buyPackageBtn'),
      detail: selectedPackageOption ? packageOptionLabel(selectedPackageOption, currencyCode) : t('op.players.actions.buyPackageNone'),
      Icon: TimerReset,
      disabled: !canPurchasePackage || packageOptions.length === 0 || !canAffordSelectedPackage
    },
    {
      id: 'booking',
      label: t('op.players.actions.bookingBtn'),
      detail: t('op.players.actions.bookingDetail'),
      Icon: CalendarClock,
      disabled: !canCreateClientReservation
    },
    {
      id: 'newCard',
      label: t('op.pos.cart.newCardLabel'),
      detail: newPlayerName || t('op.players.actions.newCardDetail'),
      Icon: UserRoundPlus,
      disabled: !canCreatePlayer
    }
  ];

  const segments: Array<{ id: string; label: string; detail: string }> = [
    {
      id: segmentAll,
      label: segmentAll,
      detail: t('op.players.segments.clients', { count: clients.length })
    },
    {
      id: segmentVip,
      label: segmentVip,
      detail: t('op.players.segments.clients', { count: clients.filter((c) => c.tone === 'vip').length })
    },
    {
      id: segmentDebt,
      label: segmentDebt,
      detail: t('op.players.segments.clients', { count: clients.filter((c) => c.debtMinorUnits > 0).length })
    },
    {
      id: segmentSleeping,
      label: segmentSleeping,
      detail: t('op.players.segments.inactive')
    },
    {
      id: segmentNew,
      label: segmentNew,
      detail: t('op.players.segments.fromSearch')
    }
  ];

  return (
    <main className="workspace-screen clients-screen">
      <section className="screen-head clients-head">
        <div>
          <span>{t('op.players.title')}</span>
          <h1>{t('op.players.heading')}</h1>
        </div>
        <div className="screen-actions">
          <span className={`map-load-state ${loadStatus === 'backend' ? 'ready' : loadStatus}`}>{workspaceLoadStatusLabel(loadStatus, t('op.pos.platformConnected'))}</span>
        </div>
      </section>

      <section className="state-strip clients-state-strip" aria-label={t('op.players.strip.label')}>
        <StateFlag label={t('op.players.strip.clients')} value={String(clients.length)} />
        <StateFlag label={t('op.players.strip.platform')} value={String(clients.filter((client) => client.source === 'backend').length)} critical={loadStatus !== 'backend'} />
        <StateFlag label={t('op.pos.payment.methodDeposit')} value={formatMinorUnits(balance, currencyCode)} />
        <StateFlag label={t('clients.col.debt')} value={formatMinorUnits(debt, currencyCode)} critical={debt > 0} />
        <StateFlag label={t('clients.col.packages')} value={String(selectedClientPackageCount)} />
        <StateFlag label={t('op.players.strip.entries')} value={String(recentEntries.length)} />
      </section>

      <section className="clients-layout">
        <section className="clients-panel clients-list-panel">
          <header className="clients-panel-title">
            <span>{t('op.players.list.title')}</span>
            <strong>{t('op.players.list.subtitle')}</strong>
          </header>
          <label className="clients-search">
            <Search size={14} />
            <input
              placeholder={t('op.players.list.searchPlaceholder')}
              value={clientSearch}
              onChange={(event) => setClientSearch(event.currentTarget.value)}
            />
          </label>
          <div className="clients-list">
            {visibleClients.length === 0 ? (
              <div className="clients-empty-state">
                <strong>{t('op.players.list.emptyTitle')}</strong>
                <span>{loadStatus === 'backend' ? t('op.players.list.emptyBackend') : t('op.players.list.emptyConnect')}</span>
              </div>
            ) : (
              visibleClients.map((client) => (
                <button
                  key={client.playerAccountId ?? client.name}
                  type="button"
                  className={`client-row ${client.tone}${client.playerAccountId === selectedClient?.playerAccountId ? ' selected' : ''}`}
                  onClick={() => setSelectedClientId(client.playerAccountId ?? null)}
                >
                  <span>{client.status}</span>
                  <div>
                    <strong>{client.name}</strong>
                    <em>{client.detail}</em>
                  </div>
                  <b>{formatMinorUnits(client.balanceMinorUnits, currencyCode)}</b>
                  <small>{client.last}</small>
                </button>
              ))
            )}
          </div>
        </section>

        <section className="clients-panel clients-profile-panel">
          <header className="clients-panel-title">
            <span>{t('op.players.profile.title')}</span>
            <strong>{t('op.players.profile.subtitle')}</strong>
          </header>
          {selectedClient === null ? (
            <div className="client-profile-card empty">
              <div className="client-avatar">--</div>
              <div>
                <span>{t('op.players.profile.empty')}</span>
                <strong>{t('op.players.profile.emptyHint')}</strong>
                <em>{t('op.players.profile.emptyNote')}</em>
              </div>
            </div>
          ) : (
            <div className="client-profile-card">
              <div className="client-avatar">{selectedClient.name.split(' ').map((part) => part[0]).join('').slice(0, 2).toUpperCase()}</div>
              <div>
                <span>{selectedClient.status}</span>
                <strong>{selectedClient.name}</strong>
                <em>{selectedClient.phoneNumber || t('op.pos.cart.clientNoPhone')} · {dataSourceLabel(selectedClient.source)}</em>
              </div>
            </div>
          )}
          <div className="client-metrics-grid">
            <div><span>{t('op.pos.payment.methodDeposit')}</span><strong>{formatMinorUnits(balance, currencyCode)}</strong></div>
            <div><span>{t('clients.col.debt')}</span><strong>{formatMinorUnits(debt, currencyCode)}</strong></div>
            <div><span>{t('clients.col.packages')}</span><strong>{selectedClientPackageCount}</strong></div>
            <div><span>{t('op.players.profile.source')}</span><strong>{selectedClient === null ? t('op.players.profile.noClient') : dataSourceLabel(selectedClient.source)}</strong></div>
          </div>
          <div className="client-package-list" aria-label={t('op.players.profile.packagesLabel')}>
            {selectedClientPackages.slice(0, 3).map((playerPackage) => (
              <article key={readString(playerPackage, 'playerPackageId')} className="client-package-row">
                <strong>{readString(playerPackage, 'name', t('op.players.profile.packageFallback'))}</strong>
                <span>{playerPackageLabel(playerPackage)}</span>
                <b>{readString(playerPackage, 'state', 'active')}</b>
              </article>
            ))}
            {selectedClientPackages.length === 0 && (
              <article className="client-package-row">
                <strong>{t('op.map.panel.noPackages')}</strong>
                <span>{t('op.players.profile.platformSource')}</span>
                <b>0</b>
              </article>
            )}
          </div>
        </section>

        <section className="clients-panel clients-actions-panel">
          <header className="clients-panel-title">
            <span>{t('op.players.actions.title')}</span>
            <strong>{t('op.players.actions.subtitle')}</strong>
          </header>
          <div className="clients-money-form">
            <label>{t('op.players.actions.topUpAmountLabel')}<input inputMode="decimal" value={walletTopUpAmount} disabled={!canTopUpWallet} onChange={(event) => setWalletTopUpAmount(event.currentTarget.value)} /></label>
            <label>{t('op.players.actions.topUpReasonLabel')}<input value={walletTopUpReason} disabled={!canTopUpWallet} onChange={(event) => setWalletTopUpReason(event.currentTarget.value)} /></label>
            <label>{t('op.players.actions.debtAmountLabel')}<input inputMode="decimal" value={debtPaymentAmount} disabled={!canPayDebt} onChange={(event) => setDebtPaymentAmount(event.currentTarget.value)} /></label>
            <label>{t('op.players.actions.debtReasonLabel')}<input value={debtPaymentReason} disabled={!canPayDebt} onChange={(event) => setDebtPaymentReason(event.currentTarget.value)} /></label>
            <label>{t('op.players.actions.newNameLabel')}<input value={newPlayerName} disabled={!canCreatePlayer} onChange={(event) => setNewPlayerName(event.currentTarget.value)} /></label>
            <label>{t('op.players.actions.newPhoneLabel')}<input value={newPlayerPhone} disabled={!canCreatePlayer} onChange={(event) => setNewPlayerPhone(event.currentTarget.value)} /></label>
          </div>
          <div className="clients-package-form">
            <label>
              {t('op.players.actions.packageSelectLabel')}
              <select
                value={selectedPackageOption === null ? '' : readString(selectedPackageOption, 'packageDefinitionId')}
                disabled={!canPurchasePackage || packageOptions.length === 0}
                onChange={(event) => setSelectedPackageDefinitionId(event.currentTarget.value)}
              >
                {packageOptions.length === 0 && <option value="">{t('op.map.panel.noPackages')}</option>}
                {packageOptions.map((option) => (
                  <option key={readString(option, 'packageDefinitionId')} value={readString(option, 'packageDefinitionId')}>
                    {packageOptionLabel(option, currencyCode)}
                  </option>
                ))}
              </select>
            </label>
            <div className="clients-package-preview" aria-label={t('op.players.actions.packagePreviewLabel')}>
              <span><strong>{t('op.players.actions.packagePrice')}</strong><b>{formatMinorUnits(selectedPackagePriceMinorUnits, selectedPackageCurrencyCode)}</b></span>
              <span><strong>{t('op.players.actions.packageMinutes')}</strong><b>{selectedPackageTotalMinutes}</b></span>
              <span><strong>{t('op.players.actions.packageBonus')}</strong><b>{selectedPackageBonusMinutes}</b></span>
              <span><strong>{t('op.players.actions.packageExpiry')}</strong><b>{selectedPackageExpiresDays > 0 ? t('op.players.actions.packageExpiryDays', { count: selectedPackageExpiresDays }) : t('op.players.actions.packageNoExpiry')}</b></span>
              <span className={canAffordSelectedPackage ? undefined : 'attention'}><strong>{t('op.pos.payment.methodDeposit')}</strong><b>{canAffordSelectedPackage ? t('op.players.actions.depositOk') : t('op.players.actions.depositLow')}</b></span>
            </div>
          </div>
          <div className="clients-action-grid">
            {playerActions.map(({ id, label, detail, Icon, disabled }) => (
              <button
                key={id}
                type="button"
                className="clients-action-card"
                disabled={disabled}
                onClick={() => runClientAction(id, label)}
              >
                <Icon size={17} />
                <strong>{label}</strong>
                <span>{detail}</span>
              </button>
            ))}
          </div>
          <FeedbackNotice feedback={feedback} />
        </section>

        <section className="clients-panel clients-segments-panel">
          <header className="clients-panel-title">
            <span>{t('op.players.segments.title')}</span>
            <strong>{t('op.players.segments.subtitle')}</strong>
          </header>
          <div className="clients-segment-grid">
            {segments.map(({ id, label, detail }) => (
              <button
                key={id}
                type="button"
                className={activeSegment === id ? 'active' : undefined}
                onClick={() => setActiveSegment(id)}
              >
                <strong>{label}</strong>
                <span>{detail}</span>
              </button>
            ))}
          </div>
        </section>

        <section className="clients-panel clients-history-panel">
          <header className="clients-panel-title">
            <span>{t('op.players.history.title')}</span>
            <strong>{t('op.players.history.subtitle')}</strong>
          </header>
          <div className="clients-history-list">
            {recentEntries.slice(0, 4).map((entry) => (
              <article key={readString(entry, 'ledgerEntryId')} className="client-history-row">
                <span>{formatTime(readString(entry, 'createdAtUtc'))}</span>
                <strong>{readString(entry, 'entryType', 'ledger')}</strong>
                <b>{formatMoney(readMoney(entry, 'amount'), currencyCode)}</b>
              </article>
            ))}
            {recentEntries.length === 0 && (
              <article className="client-history-row">
                <span>—</span>
                <strong>{t('op.players.history.empty')}</strong>
                <b>0 {currencyCode}</b>
              </article>
            )}
          </div>
        </section>
      </section>
    </main>
  );
}
