import { useCallback, useEffect, useState } from 'react';
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
import type { Feedback, LoadStatus, OperatorBackendContext } from '../operatorTypes';
import type { ResourceState } from './destinations/types';
import { CriticalActionConfirmation, EmptyState } from '../operatorPrimitives';
import { allowedManagementDestinations, type ManagementDestinationId } from './managementNav';
import { useUnsavedGuard } from './useUnsavedGuard';
import { ClubDestination } from './destinations/ClubDestination';
import { GoodsDestination } from './destinations/GoodsDestination';
import { HallsDevicesDestination } from './destinations/HallsDevicesDestination';
import { NewsDestination } from './destinations/NewsDestination';
import { PaymentsLoyaltyDestination } from './destinations/PaymentsLoyaltyDestination';
import { StaffRolesDestination } from './destinations/StaffRolesDestination';
import { TariffsPackagesDestination } from './destinations/TariffsPackagesDestination';

// Управление: левый рейл разделов, доступных сессии, + активный экран раздела справа. Все 8
// разделов (Клуб, Залы, Тарифы, Сотрудники, Товары, Оплата, Лояльность, Новости) маршрутизируют
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

  const [settingsLoadStatus, setSettingsLoadStatus] = useState<LoadStatus>('fixture');
  const [settingsFeedback, setSettingsFeedback] = useState<Feedback>(emptyFeedback);
  useFeedbackToasts(settingsFeedback);
  const [zones, setZones] = useState<ZoneDto[]>([]);
  const [staffUsers, setStaffUsers] = useState<StaffUserDto[]>([]);
  const [catalog, setCatalog] = useState<PosProductDto[]>([]);
  const [tariffs, setTariffs] = useState<TariffOptionDto[]>([]);
  const [packageState, setPackageState] = useState<ResourceState<PackageOptionDto[]>>({ status: 'fixture', data: [] });
  const [deviceInventory, setDeviceInventory] = useState<DeviceInventoryItemDto[]>([]);

  const loadPackageOptions = async (nextBackend = backend) => {
    if (nextBackend === null) {
      setPackageState((state) => ({ status: 'fixture', data: state.data }));
      return;
    }
    setPackageState((state) => ({ status: 'loading', data: state.data }));
    try {
      const rows = await createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session).settings.getPackageOptions(nextBackend.branchId);
      setPackageState({ status: 'backend', data: Array.isArray(rows) ? rows : [] });
    } catch (error) {
      setPackageState((state) => ({ status: 'failed', data: state.data, errorDetail: projectOperatorError(error, t).detail }));
    }
  };

  const loadSettings = async (nextBackend = backend) => {
    if (nextBackend === null) {
      setSettingsLoadStatus('fixture');
      return;
    }

    setSettingsLoadStatus('loading');
    try {
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      // Профиль филиала здесь НЕ грузим: им владеет ClubDestination (свой load/save). Тянуть его
      // сюда — лишний дубль-запрос на getBranchProfile при заходе на «Клуб». Грузим ровно то,
      // что нужно потребителям слайса 2 (Залы/Тарифы/Сотрудники/Товары).
      const [staff, layoutZones, products, tariffOptions, deviceRows] = await Promise.all([
        apiClients.settings.getStaffUsers(nextBackend.branchId),
        apiClients.settings.getLayoutZones(nextBackend.branchId),
        apiClients.pos.getCatalog(nextBackend.branchId),
        apiClients.settings.getTariffOptions(nextBackend.branchId),
        hasPermission(nextBackend.session, permissionNames.viewDeviceDetail)
          ? apiClients.devices.listDevices(nextBackend.branchId).catch(() => [])
          : Promise.resolve([])
      ]);
      setStaffUsers(Array.isArray(staff) ? staff : []);
      setZones(Array.isArray(layoutZones) ? layoutZones : []);
      setCatalog(Array.isArray(products) ? products : []);
      setTariffs(Array.isArray(tariffOptions) ? tariffOptions : []);
      setDeviceInventory(Array.isArray(deviceRows) ? deviceRows : []);
      setSettingsLoadStatus('backend');
    } catch (error) {
      setSettingsLoadStatus('failed');
      setSettingsFeedback({ label: t('op.settings.profile.loadFeedbackLabel'), state: 'failed', detail: projectOperatorError(error, t).detail });
    }
  };

  useEffect(() => {
    void loadSettings();
    void loadPackageOptions();
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, currencyCode]);

  if (destinations.length === 0) {
    return (
      <main className="workspace-screen">
        <EmptyState title={t('op.management.noAccess')} />
      </main>
    );
  }

  // Сессия могла лишиться прав между рендерами (обновлённые permissions) — активный раздел
  // мог перестать быть доступным. Падаем на первый разрешённый вместо пустого экрана.
  const currentId = destinations.some((destination) => destination.id === active) ? (active as ManagementDestinationId) : destinations[0].id;

  const settingsErrorDetail = settingsFeedback.state === 'failed' ? settingsFeedback.detail : undefined;
  const retrySettings = () => void loadSettings();
  const retryPackages = () => void loadPackageOptions();
  const reloadTariffsAndPackages = async (nextBackend = backend) => {
    await Promise.all([loadSettings(nextBackend), loadPackageOptions(nextBackend)]);
  };

  const renderActiveDestination = () => {
    if (currentId === 'club') {
      return <ClubDestination backend={backend} session={session} currencyCode={currencyCode} onDirtyChange={setDirty} />;
    }
    if (currentId === 'news') {
      return <NewsDestination backend={backend} session={session} currencyCode={currencyCode} onDirtyChange={setDirty} />;
    }
    if (currentId === 'halls') {
      return (
        <HallsDevicesDestination
          backend={backend}
          session={session}
          currencyCode={currencyCode}
          onDirtyChange={setDirty}
          zones={zones}
          deviceInventory={deviceInventory}
          onDeviceInventoryChange={setDeviceInventory}
          onReload={loadSettings}
          onFeedback={setSettingsFeedback}
          loadStatus={settingsLoadStatus}
          errorDetail={settingsErrorDetail}
          onRetry={retrySettings}
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
          tariffs={tariffs}
          packageOptions={packageState.data}
          packageState={packageState}
          onReload={reloadTariffsAndPackages}
          onFeedback={setSettingsFeedback}
          loadStatus={settingsLoadStatus}
          errorDetail={settingsErrorDetail}
          onRetry={retrySettings}
          onRetryPackages={retryPackages}
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
          staffUsers={staffUsers}
          onStaffUsersChange={setStaffUsers}
          onFeedback={setSettingsFeedback}
          loadStatus={settingsLoadStatus}
          errorDetail={settingsErrorDetail}
          onRetry={retrySettings}
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
          catalog={catalog}
          onCatalogChange={setCatalog}
          onReload={loadSettings}
          onFeedback={setSettingsFeedback}
          loadStatus={settingsLoadStatus}
          errorDetail={settingsErrorDetail}
          onRetry={retrySettings}
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
