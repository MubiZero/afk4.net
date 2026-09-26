import { useCallback, useEffect, useState, type Dispatch, type SetStateAction } from 'react';
import { useI18n } from '@afk4/i18n';
import type { OperatorAuthSession } from '../authClient';
import { projectOperatorError } from '../apiErrors';
import { createAuthenticatedOperatorClients, emptyFeedback } from '../operatorHelpers';
import { hasPermission, permissionNames } from '../operatorPermissions';
import { useFeedbackToasts } from '../useFeedbackToasts';
import type {
  DeviceInventoryItemDto,
  PackageOptionDto,
  PosProductDto,
  StaffUserDto,
  TariffOptionDto,
  ZoneDto
} from '../operatorApiClients';
import type { Feedback, OperatorBackendContext } from '../operatorTypes';
import type { ResourceState } from './destinations/types';
import { CriticalActionConfirmation, EmptyState } from '../operatorPrimitives';
import { allowedManagementDestinations, type ManagementDestinationId } from './managementNav';
import { useUnsavedGuard } from './useUnsavedGuard';
import { ClubDestination } from './destinations/ClubDestination';
import { BookingIntakeDestination } from './destinations/booking/BookingIntakeDestination';
import { ProtectionDestination } from './destinations/protection/ProtectionDestination';
import { GoodsDestination } from './destinations/GoodsDestination';
import { HallsDevicesDestination } from './destinations/HallsDevicesDestination';
import { GamesDestination } from './destinations/games/GamesDestination';
import { ReviewsDestination } from './destinations/reviews/ReviewsDestination';
import { NewsDestination } from './destinations/NewsDestination';
import { EventsDestination } from './destinations/EventsDestination';
import { PaymentsLoyaltyDestination } from './destinations/PaymentsLoyaltyDestination';
import { StaffRolesDestination } from './destinations/StaffRolesDestination';
import { TariffsPackagesDestination } from './destinations/TariffsPackagesDestination';

type Clients = ReturnType<typeof createAuthenticatedOperatorClients>;

// Управление: левый рейл разделов, доступных сессии, + активный экран раздела справа. Все
// разделы (Клуб, Приём броней, Залы, Тарифы, Сотрудники, Товары, Оплата и лояльность, Новости) маршрутизируют
// на реальные компоненты — заглушек-скелетов больше нет.
//
// Task 2.1: settings-domain data (zones/staff/catalog/tariffs/packages/device lists) is loaded
// once here — via createAuthenticatedOperatorClients, same contract as the retired
// Прежний монолит настроек грузил то же самое минус diagnostics/rollouts/updates (Integrations was
// dropped from this redesign) — so it's ready to hand to the halls/tariffs/staff/goods
// destination wrappers landing in Task 2.2-2.6. Club/Loyalty/News load/save independently and
// ignore all of it.
export function ManagementWorkspace({
  backend,
  session,
  currencyCode
}: {
  backend: OperatorBackendContext | null;
  session: OperatorAuthSession | null;
  currencyCode: string;
}) {
  const { t } = useI18n();
  const destinations = allowedManagementDestinations(session);
  const [active, setActive] = useState<ManagementDestinationId | null>(destinations[0]?.id ?? null);
  const [dirty, setDirty] = useState(false);

  const navigate = useCallback((target: ManagementDestinationId) => {
    setActive(target);
    setDirty(false);
  }, []);

  const guard = useUnsavedGuard({
    isDirty: dirty,
    onNavigate: navigate,
    onDiscard: () => setDirty(false)
  });

  const [settingsFeedback, setSettingsFeedback] = useState<Feedback>(emptyFeedback);
  useFeedbackToasts(settingsFeedback);
  // Каждый список — своя загрузка со своей причиной отказа. Раньше все пять грузились одним
  // ожиданием, и отказ любого (например, у менеджера тарифов нет права видеть сотрудников) гасил
  // сразу «Залы», «Тарифы», «Сотрудников» и «Товары», хотя их данные уже пришли.
  const [zones, setZones] = useState<ResourceState<ZoneDto[]>>({ status: 'fixture', data: [] });
  const [staffUsers, setStaffUsers] = useState<ResourceState<StaffUserDto[]>>({ status: 'fixture', data: [] });
  const [catalog, setCatalog] = useState<ResourceState<PosProductDto[]>>({ status: 'fixture', data: [] });
  const [tariffs, setTariffs] = useState<ResourceState<TariffOptionDto[]>>({ status: 'fixture', data: [] });
  const [packageState, setPackageState] = useState<ResourceState<PackageOptionDto[]>>({ status: 'fixture', data: [] });
  const [devices, setDevices] = useState<ResourceState<DeviceInventoryItemDto[]>>({ status: 'fixture', data: [] });

  // Списки, которые грузятся вместе, делят один набор клиентов: у каждого набора свой продлеватель
  // сессии, и шесть наборов на истёкшем токене ушли бы продлевать его шесть раз наперегонки.
  const clientsFor = (nextBackend: OperatorBackendContext | null): Clients | undefined =>
    nextBackend === null ? undefined : createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);

  const loadResource = async <T,>(
    setState: Dispatch<SetStateAction<ResourceState<T[]>>>,
    fetchRows: (clients: Clients, branchId: string) => Promise<unknown>,
    nextBackend: OperatorBackendContext | null,
    shared?: Clients
  ) => {
    if (nextBackend === null) {
      setState((state) => ({ status: 'fixture', data: state.data }));
      return;
    }
    setState((state) => ({ status: 'loading', data: state.data }));
    try {
      const rows = await fetchRows(shared ?? createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session), nextBackend.branchId);
      setState({ status: 'backend', data: Array.isArray(rows) ? rows as T[] : [] });
    } catch (error) {
      setState((state) => ({ status: 'failed', data: state.data, failure: projectOperatorError(error, t) }));
    }
  };

  // Профиль филиала здесь НЕ грузим: им владеет ClubDestination (свой load/save). Тянуть его
  // сюда — лишний дубль-запрос на getBranchProfile при заходе на «Клуб». Грузим ровно то, что
  // нужно потребителям слайса 2 (Залы/Тарифы/Сотрудники/Товары).
  const loadZones = (nextBackend = backend, shared?: Clients) => loadResource(setZones, (clients, branchId) => clients.settings.getLayoutZones(branchId), nextBackend, shared);
  const loadStaff = (nextBackend = backend, shared?: Clients) => loadResource(setStaffUsers, (clients, branchId) => clients.settings.getStaffUsers(branchId), nextBackend, shared);
  const loadCatalog = (nextBackend = backend, shared?: Clients) => loadResource(setCatalog, (clients, branchId) => clients.pos.getCatalog(branchId), nextBackend, shared);
  const loadTariffs = (nextBackend = backend, shared?: Clients) => loadResource(setTariffs, (clients, branchId) => clients.settings.getTariffOptions(branchId), nextBackend, shared);
  const loadPackageOptions = (nextBackend = backend, shared?: Clients) => loadResource(setPackageState, (clients, branchId) => clients.settings.getPackageOptions(branchId), nextBackend, shared);
  const loadDevices = async (nextBackend = backend, shared?: Clients) => {
    // Без права на карточку устройства список не спрашиваем вовсе: это не отказ, а «не положено».
    if (nextBackend !== null && !hasPermission(nextBackend.session, permissionNames.viewDeviceDetail)) {
      setDevices({ status: 'backend', data: [] });
      return;
    }
    await loadResource(setDevices, (clients, branchId) => clients.devices.listDevices(branchId), nextBackend, shared);
  };

  useEffect(() => {
    const shared = clientsFor(backend);
    void loadZones(backend, shared);
    void loadStaff(backend, shared);
    void loadCatalog(backend, shared);
    void loadTariffs(backend, shared);
    void loadDevices(backend, shared);
    void loadPackageOptions(backend, shared);
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, currencyCode]);

  if (destinations.length === 0) {
    return (
      <main className="workspace-screen">
        <EmptyState title={t('op.management.noAccess')} next={{ kind: 'denied', hint: t('op.error.accessHint') }} />
      </main>
    );
  }

  // Сессия могла лишиться прав между рендерами (обновлённые permissions) — активный раздел
  // мог перестать быть доступным. Падаем на первый разрешённый вместо пустого экрана.
  const currentId = destinations.some((destination) => destination.id === active) ? (active as ManagementDestinationId) : destinations[0].id;

  // Загрузчики выше не бросают — отказ оседает в состоянии своего списка, — поэтому общее
  // ожидание здесь не гасит один список отказом другого.
  const reloadHalls = async (nextBackend = backend) => {
    const shared = clientsFor(nextBackend);
    await Promise.all([loadZones(nextBackend, shared), loadDevices(nextBackend, shared)]);
  };
  const reloadTariffsAndPackages = async (nextBackend = backend) => {
    const shared = clientsFor(nextBackend);
    await Promise.all([loadTariffs(nextBackend, shared), loadPackageOptions(nextBackend, shared)]);
  };

  const renderActiveDestination = () => {
    if (currentId === 'club') {
      return <ClubDestination backend={backend} session={session} currencyCode={currencyCode} onDirtyChange={setDirty} />;
    }
    if (currentId === 'booking') {
      return <BookingIntakeDestination backend={backend} session={session} currencyCode={currencyCode} onDirtyChange={setDirty} />;
    }
    if (currentId === 'protection') {
      return <ProtectionDestination backend={backend} session={session} currencyCode={currencyCode} onDirtyChange={setDirty} />;
    }
    if (currentId === 'reviews') {
      return <ReviewsDestination backend={backend} session={session} currencyCode={currencyCode} onDirtyChange={setDirty} />;
    }
    if (currentId === 'games') {
      return <GamesDestination backend={backend} session={session} currencyCode={currencyCode} onDirtyChange={setDirty} />;
    }
    if (currentId === 'news') {
      return <NewsDestination backend={backend} session={session} currencyCode={currencyCode} onDirtyChange={setDirty} />;
    }
    if (currentId === 'events') {
      return <EventsDestination backend={backend} session={session} currencyCode={currencyCode} onDirtyChange={setDirty} />;
    }
    if (currentId === 'halls') {
      return (
        <HallsDevicesDestination
          backend={backend}
          session={session}
          currencyCode={currencyCode}
          onDirtyChange={setDirty}
          zones={zones.data}
          deviceInventory={devices.data}
          deviceState={devices}
          onDeviceInventoryChange={(rows) => setDevices((state) => ({ ...state, data: rows }))}
          onReload={reloadHalls}
          onFeedback={setSettingsFeedback}
          loadStatus={zones.status}
          failure={zones.failure}
          onRetry={() => void loadZones()}
          onRetryDevices={() => void loadDevices()}
        />
      );
    }
    if (currentId === 'tariffs') {
      return (
        <TariffsPackagesDestination
          backend={backend}
          session={session}
          currencyCode={currencyCode}
          onDirtyChange={setDirty}
          tariffs={tariffs.data}
          packageOptions={packageState.data}
          packageState={packageState}
          onReload={reloadTariffsAndPackages}
          onFeedback={setSettingsFeedback}
          loadStatus={tariffs.status}
          failure={tariffs.failure}
          onRetry={() => void loadTariffs()}
          onRetryPackages={() => void loadPackageOptions()}
        />
      );
    }

    if (currentId === 'staff') {
      return (
        <StaffRolesDestination
          backend={backend}
          session={session}
          currencyCode={currencyCode}
          onDirtyChange={setDirty}
          staffUsers={staffUsers.data}
          onStaffUsersChange={(rows) => setStaffUsers((state) => ({ ...state, data: rows }))}
          onFeedback={setSettingsFeedback}
          loadStatus={staffUsers.status}
          failure={staffUsers.failure}
          onRetry={() => void loadStaff()}
        />
      );
    }

    if (currentId === 'goods') {
      return (
        <GoodsDestination
          backend={backend}
          session={session}
          currencyCode={currencyCode}
          onDirtyChange={setDirty}
          catalog={catalog.data}
          onCatalogChange={(rows) => setCatalog((state) => ({ ...state, data: rows }))}
          onReload={loadCatalog}
          onFeedback={setSettingsFeedback}
          loadStatus={catalog.status}
          failure={catalog.failure}
          onRetry={() => void loadCatalog()}
        />
      );
    }

    // payments: merged «Платежи и лояльность» — last remaining destination id.
    return <PaymentsLoyaltyDestination backend={backend} session={session} currencyCode={currencyCode} onDirtyChange={setDirty} />;
  };

  return (
    <div className="management-layout">
      <nav className="management-nav">
        {destinations.map((destination) => {
          const Icon = destination.Icon;
          return (
            <button
              key={destination.id}
              type="button"
              className={destination.id === currentId ? 'active' : undefined}
              onClick={() => guard.requestNavigate(destination.id)}
            >
              <Icon size={16} aria-hidden="true" />
              <span>{t(destination.labelKey)}</span>
            </button>
          );
        })}
      </nav>

      <div className="management-active-pane">
        {guard.pendingTarget !== null ? (
          <CriticalActionConfirmation
            title={t('op.management.unsaved.title')}
            detail={t('op.management.unsaved.body')}
            impact=""
            confirmLabel={t('op.management.unsaved.confirm')}
            cancelLabel={t('op.management.unsaved.cancel')}
            tone="warning"
            onConfirm={guard.confirm}
            onCancel={guard.cancel}
          />
        ) : (
          renderActiveDestination()
        )}
      </div>
    </div>
  );
}
