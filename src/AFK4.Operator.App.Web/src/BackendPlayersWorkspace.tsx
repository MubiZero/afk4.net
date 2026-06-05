import { useEffect, useState } from 'react';
import { CalendarClock, CircleDollarSign, ReceiptText, Search, TimerReset, UserRoundPlus } from 'lucide-react';
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

export function BackendPlayersWorkspace({ currencyCode, backend }: { currencyCode: string; backend: OperatorBackendContext | null }) {
  const [clientSearch, setClientSearch] = useState('');
  const [activeSegment, setActiveSegment] = useState('Все');
  const [selectedClientId, setSelectedClientId] = useState<string | null>(null);
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const [loadStatus, setLoadStatus] = useState<LoadStatus>(backend === null ? 'fixture' : 'loading');
  const [clients, setClients] = useState<PlayerClientItem[]>(() => backend === null ? fixturePlayers(currencyCode) : []);
  const [walletSummary, setWalletSummary] = useState<WalletSummaryDto | null>(null);
  const [packageOptions, setPackageOptions] = useState<PackageOptionDto[]>([]);
  const [selectedPackageDefinitionId, setSelectedPackageDefinitionId] = useState('');
  const [selectedClientPackages, setSelectedClientPackages] = useState<PlayerPackageDto[]>([]);
  const [walletTopUpAmount, setWalletTopUpAmount] = useState('100.00');
  const [walletTopUpReason, setWalletTopUpReason] = useState('пополнение через кассу');
  const [debtPaymentAmount, setDebtPaymentAmount] = useState('');
  const [debtPaymentReason, setDebtPaymentReason] = useState('оплата долга через кассу');
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
          setFeedback({ label: 'Клиенты', state: 'failed', detail: projectOperatorError(error).detail });
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

  const visibleClients = clients.filter((client) => {
    const segmentMatches = activeSegment === 'Все'
      || (activeSegment === 'VIP' && client.tone === 'vip')
      || (activeSegment === 'Есть долг' && client.debtMinorUnits > 0)
      || (activeSegment === 'Новые' && client.source === 'backend')
      || (activeSegment === 'Спящие' && client.status === 'Неактивен');
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
      throw new Error('Выберите игрока платформы перед операцией.');
    }

    return selectedClient as PlayerClientItem & { playerAccountId: string; source: 'backend' };
  };

  const runClientAction = async (label: string) => {
    setFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);

      if (label === 'Пополнить депозит') {
        if (!hasPermission(nextBackend.session, permissionNames.topUpWallet)) {
          throw new Error('Нет прав на пополнение депозита.');
        }

        const backendClient = requireSelectedBackendClient();

        const topUpMinorUnits = parseMoneyInputMinorUnits(walletTopUpAmount);
        const reason = walletTopUpReason.trim();
        if (topUpMinorUnits === null || !reason) {
          throw new Error('Заполните сумму и причину пополнения депозита.');
        }

        const wallet = await apiClients.players.topUpWallet(backendClient.playerAccountId, {
          organizationId: nextBackend.session.organizationId,
          amount: { currencyCode, minorUnits: topUpMinorUnits },
          reason,
          idempotencyKey: createIdempotencyKey('wallet-top-up')
        });
        setWalletSummary(wallet);
      } else if (label === 'Списать долг') {
        if (!hasPermission(nextBackend.session, permissionNames.payDebt)) {
          throw new Error('Нет прав на списание долга.');
        }

        const backendClient = requireSelectedBackendClient();

        const debtPaymentMinorUnits = parseMoneyInputMinorUnits(debtPaymentAmount);
        const reason = debtPaymentReason.trim();
        if (debtPaymentMinorUnits === null || !reason || debtPaymentMinorUnits > debt) {
          throw new Error('Заполните сумму долга не больше текущего долга и причину оплаты.');
        }

        const wallet = await apiClients.players.payDebt(backendClient.playerAccountId, {
          organizationId: nextBackend.session.organizationId,
          amount: { currencyCode, minorUnits: debtPaymentMinorUnits },
          reason,
          idempotencyKey: createIdempotencyKey('debt-payment')
        });
        setWalletSummary(wallet);
      } else if (label === 'Новая карта') {
        if (!hasPermission(nextBackend.session, permissionNames.createPlayerAccount)) {
          throw new Error('Нет прав на создание игрока.');
        }

        const displayName = newPlayerName.trim() || clientSearch.trim();
        if (!displayName) {
          throw new Error('Заполните имя нового клиента.');
        }

        const created = await apiClients.players.createPlayer(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          displayName,
          phoneNumber: newPlayerPhone.trim() || null,
          idempotencyKey: createIdempotencyKey('player-create')
        });
        const createdClient = projectPlayerClient({
          playerAccountId: readString(created, 'playerAccountId'),
          displayName: readString(created, 'displayName', 'Новый клиент'),
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
      } else if (label === 'Купить пакет') {
        if (!hasPermission(nextBackend.session, permissionNames.purchasePackage)) {
          throw new Error('Нет прав на покупку пакетов.');
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
          throw new Error('Нет доступного пакета платформы для покупки.');
        }

        const packagePriceMinorUnits = readNumber(packageOption, 'priceMinorUnits', 0);
        if (packagePriceMinorUnits > balance) {
          throw new Error('Недостаточно депозита для выбранного пакета.');
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
      } else if (label === 'Создать бронь') {
        if (!hasPermission(nextBackend.session, permissionNames.manageReservations)) {
          throw new Error('Нет прав на создание брони.');
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
          note: 'Создано из карточки клиента'
        });
      } else {
        throw new Error('Операция пока не подключена к платформе.');
      }

      setFeedback({ label, state: 'confirmed' });
    } catch (error) {
      setFeedback({ label, state: 'failed', detail: projectOperatorError(error).detail });
    }
  };

  return (
    <main className="workspace-screen clients-screen">
      <section className="screen-head clients-head">
        <div>
          <span>Клиенты</span>
          <h1>Клиенты · поиск, депозит и долги</h1>
        </div>
        <div className="screen-actions">
          <span className={`map-load-state ${loadStatus === 'backend' ? 'ready' : loadStatus}`}>{workspaceLoadStatusLabel(loadStatus, 'Платформа подключена')}</span>
        </div>
      </section>

      <section className="state-strip clients-state-strip" aria-label="Сводка клиентов">
        <StateFlag label="Клиенты" value={String(clients.length)} />
        <StateFlag label="Платформа" value={String(clients.filter((client) => client.source === 'backend').length)} critical={loadStatus !== 'backend'} />
        <StateFlag label="Депозит" value={formatMinorUnits(balance, currencyCode)} />
        <StateFlag label="Долг" value={formatMinorUnits(debt, currencyCode)} critical={debt > 0} />
        <StateFlag label="Пакеты" value={String(selectedClientPackageCount)} />
        <StateFlag label="Записи" value={String(recentEntries.length)} />
      </section>

      <section className="clients-layout">
        <section className="clients-panel clients-list-panel">
          <header className="clients-panel-title">
            <span>Список клиентов</span>
            <strong>поиск по имени, телефону или карте</strong>
          </header>
          <label className="clients-search">
            <Search size={14} />
            <input
              placeholder="Игрок, телефон, карта"
              value={clientSearch}
              onChange={(event) => setClientSearch(event.currentTarget.value)}
            />
          </label>
          <div className="clients-list">
            {visibleClients.length === 0 ? (
              <div className="clients-empty-state">
                <strong>Клиенты не найдены</strong>
                <span>{loadStatus === 'backend' ? 'По текущему поиску клиентов нет.' : 'Подключитесь к платформе, чтобы загрузить клиентов.'}</span>
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
            <span>Карточка клиента</span>
            <strong>выбранный игрок</strong>
          </header>
          {selectedClient === null ? (
            <div className="client-profile-card empty">
              <div className="client-avatar">--</div>
              <div>
                <span>Нет выбранного клиента</span>
                <strong>Выберите клиента из списка</strong>
                <em>Пустой ответ платформы не подменяется локальной карточкой</em>
              </div>
            </div>
          ) : (
            <div className="client-profile-card">
              <div className="client-avatar">{selectedClient.name.split(' ').map((part) => part[0]).join('').slice(0, 2).toUpperCase()}</div>
              <div>
                <span>{selectedClient.status}</span>
                <strong>{selectedClient.name}</strong>
                <em>{selectedClient.phoneNumber || 'без телефона'} · {dataSourceLabel(selectedClient.source)}</em>
              </div>
            </div>
          )}
          <div className="client-metrics-grid">
            <div><span>Депозит</span><strong>{formatMinorUnits(balance, currencyCode)}</strong></div>
            <div><span>Долг</span><strong>{formatMinorUnits(debt, currencyCode)}</strong></div>
            <div><span>Пакеты</span><strong>{selectedClientPackageCount}</strong></div>
            <div><span>Источник</span><strong>{selectedClient === null ? 'нет клиента' : dataSourceLabel(selectedClient.source)}</strong></div>
          </div>
          <div className="client-package-list" aria-label="Пакеты клиента">
            {selectedClientPackages.slice(0, 3).map((playerPackage) => (
              <article key={readString(playerPackage, 'playerPackageId')} className="client-package-row">
                <strong>{readString(playerPackage, 'name', 'Пакет')}</strong>
                <span>{playerPackageLabel(playerPackage)}</span>
                <b>{readString(playerPackage, 'state', 'active')}</b>
              </article>
            ))}
            {selectedClientPackages.length === 0 && (
              <article className="client-package-row">
                <strong>Нет активных пакетов</strong>
                <span>платформа</span>
                <b>0</b>
              </article>
            )}
          </div>
        </section>

        <section className="clients-panel clients-actions-panel">
          <header className="clients-panel-title">
            <span>Операции</span>
            <strong>денежные операции выполняет платформа</strong>
          </header>
          <div className="clients-money-form">
            <label>Сумма пополнения<input inputMode="decimal" value={walletTopUpAmount} disabled={!canTopUpWallet} onChange={(event) => setWalletTopUpAmount(event.currentTarget.value)} /></label>
            <label>Причина пополнения<input value={walletTopUpReason} disabled={!canTopUpWallet} onChange={(event) => setWalletTopUpReason(event.currentTarget.value)} /></label>
            <label>Сумма долга<input inputMode="decimal" value={debtPaymentAmount} disabled={!canPayDebt} onChange={(event) => setDebtPaymentAmount(event.currentTarget.value)} /></label>
            <label>Причина долга<input value={debtPaymentReason} disabled={!canPayDebt} onChange={(event) => setDebtPaymentReason(event.currentTarget.value)} /></label>
            <label>Имя нового клиента<input value={newPlayerName} disabled={!canCreatePlayer} onChange={(event) => setNewPlayerName(event.currentTarget.value)} /></label>
            <label>Телефон нового клиента<input value={newPlayerPhone} disabled={!canCreatePlayer} onChange={(event) => setNewPlayerPhone(event.currentTarget.value)} /></label>
          </div>
          <div className="clients-package-form">
            <label>
              Пакет для покупки
              <select
                value={selectedPackageOption === null ? '' : readString(selectedPackageOption, 'packageDefinitionId')}
                disabled={!canPurchasePackage || packageOptions.length === 0}
                onChange={(event) => setSelectedPackageDefinitionId(event.currentTarget.value)}
              >
                {packageOptions.length === 0 && <option value="">Нет активных пакетов</option>}
                {packageOptions.map((option) => (
                  <option key={readString(option, 'packageDefinitionId')} value={readString(option, 'packageDefinitionId')}>
                    {packageOptionLabel(option, currencyCode)}
                  </option>
                ))}
              </select>
            </label>
            <div className="clients-package-preview" aria-label="Пакет к покупке">
              <span><strong>Цена</strong><b>{formatMinorUnits(selectedPackagePriceMinorUnits, selectedPackageCurrencyCode)}</b></span>
              <span><strong>Минуты</strong><b>{selectedPackageTotalMinutes}</b></span>
              <span><strong>Бонус</strong><b>{selectedPackageBonusMinutes}</b></span>
              <span><strong>Срок</strong><b>{selectedPackageExpiresDays > 0 ? `${selectedPackageExpiresDays} дн.` : 'без срока'}</b></span>
              <span className={canAffordSelectedPackage ? undefined : 'attention'}><strong>Депозит</strong><b>{canAffordSelectedPackage ? 'достаточно' : 'пополнить'}</b></span>
            </div>
          </div>
          <div className="clients-action-grid">
            {[
              ['Пополнить депозит', `${walletTopUpAmount || '0'} ${currencyCode}`, CircleDollarSign],
              ['Списать долг', debtPaymentAmount ? `${debtPaymentAmount} ${currencyCode}` : 'нет долга', ReceiptText],
              ['Купить пакет', selectedPackageOption ? packageOptionLabel(selectedPackageOption, currencyCode) : 'нет пакетов', TimerReset],
              ['Создать бронь', 'бронь из карточки', CalendarClock],
              ['Новая карта', newPlayerName || 'создать игрока', UserRoundPlus]
            ].map(([label, detail, Icon]) => (
              <button
                key={label as string}
                type="button"
                className="clients-action-card"
                disabled={((label as string) === 'Пополнить депозит' && !canTopUpWallet)
                  || ((label as string) === 'Списать долг' && !canPayDebt)
                  || ((label as string) === 'Купить пакет' && (!canPurchasePackage || packageOptions.length === 0 || !canAffordSelectedPackage))
                  || ((label as string) === 'Создать бронь' && !canCreateClientReservation)
                  || ((label as string) === 'Новая карта' && !canCreatePlayer)}
                onClick={() => runClientAction(label as string)}
              >
                <Icon size={17} />
                <strong>{label as string}</strong>
                <span>{detail as string}</span>
              </button>
            ))}
          </div>
          <FeedbackNotice feedback={feedback} />
        </section>

        <section className="clients-panel clients-segments-panel">
          <header className="clients-panel-title">
            <span>Сегменты</span>
            <strong>фильтр по клиентам платформы</strong>
          </header>
          <div className="clients-segment-grid">
            {[
              ['Все', `${clients.length} клиентов`],
              ['VIP', `${clients.filter((client) => client.tone === 'vip').length} клиентов`],
              ['Есть долг', `${clients.filter((client) => client.debtMinorUnits > 0).length} клиентов`],
              ['Спящие', 'неактивные'],
              ['Новые', 'из поиска платформы']
            ].map(([label, detail]) => (
              <button
                key={label}
                type="button"
                className={activeSegment === label ? 'active' : undefined}
                onClick={() => setActiveSegment(label)}
              >
                <strong>{label}</strong>
                <span>{detail}</span>
              </button>
            ))}
          </div>
        </section>

        <section className="clients-panel clients-history-panel">
          <header className="clients-panel-title">
            <span>История клиента</span>
            <strong>последние операции</strong>
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
                <strong>Операций нет</strong>
                <b>0 {currencyCode}</b>
              </article>
            )}
          </div>
        </section>
      </section>
    </main>
  );
}
