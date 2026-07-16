import { useCallback, useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import type { OperatorAuthSession } from '../authClient';
import { projectOperatorError } from '../apiErrors';
import { createAuthenticatedOperatorClients, emptyFeedback } from '../operatorHelpers';
import { hasPermission, permissionNames } from '../operatorPermissions';
import { useFeedbackToasts } from '../useFeedbackToasts';
import type {
  DeviceCommandStatusDto,
  DeviceInventoryItemDto,
  PackageOptionDto,
  PosProductDto,
  StaffUserDto,
  TariffOptionDto,
  ZoneDto
} from '../operatorApiClients';
import type { Feedback, LoadStatus, OperatorBackendContext } from '../operatorTypes';
import { CriticalActionConfirmation, EmptyState } from '../operatorPrimitives';
import { allowedManagementDestinations, type ManagementDestinationId } from './managementNav';
import { useUnsavedGuard } from './useUnsavedGuard';
import { ClubDestination } from './destinations/ClubDestination';
import { GoodsDestination } from './destinations/GoodsDestination';
import { HallsDevicesDestination } from './destinations/HallsDevicesDestination';
import { LoyaltyDestination } from './destinations/LoyaltyDestination';
import { NewsDestination } from './destinations/NewsDestination';
import { PaymentDestination } from './destinations/PaymentDestination';
import { StaffRolesDestination } from './destinations/StaffRolesDestination';
import { TariffsPackagesDestination } from './destinations/TariffsPackagesDestination';

// Управление: левый рейл разделов, доступных сессии, + активный экран раздела справа. Все 8
// разделов (Клуб, Залы, Тарифы, Сотрудники, Товары, Оплата, Лояльность, Новости) маршрутизируют
// на реальные компоненты — заглушек-скелетов больше нет.
//
// Task 2.1: settings-domain data (zones/staff/catalog/tariffs/packages/device lists) is loaded
// once here — via createAuthenticatedOperatorClients, same contract as the retired
// BackendSettingsWorkspace.loadSettings minus diagnostics/rollouts/updates (Integrations was
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
  const [packageOptions, setPackageOptions] = useState<PackageOptionDto[]>([]);
  const [deviceInventory, setDeviceInventory] = useState<DeviceInventoryItemDto[]>([]);
  const [branchDeviceCommandHistory, setBranchDeviceCommandHistory] = useState<DeviceCommandStatusDto[]>([]);

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
      const [staff, layoutZones, products, tariffOptions, packageOptionRows, deviceRows, branchDeviceCommands] = await Promise.all([
        apiClients.settings.getStaffUsers(nextBackend.branchId),
        apiClients.settings.getLayoutZones(nextBackend.branchId),
        apiClients.pos.getCatalog(nextBackend.branchId),
        apiClients.settings.getTariffOptions(nextBackend.branchId),
        apiClients.settings.getPackageOptions(nextBackend.branchId).catch(() => []),
        hasPermission(nextBackend.session, permissionNames.viewDeviceDetail)
          ? apiClients.devices.listDevices(nextBackend.branchId).catch(() => [])
          : Promise.resolve([]),
        hasPermission(nextBackend.session, permissionNames.viewDeviceCommandStatus)
          ? apiClients.devices.listBranchDeviceCommands(nextBackend.branchId, { limit: 50 }).catch(() => [])
          : Promise.resolve([])
      ]);
      setStaffUsers(Array.isArray(staff) ? staff : []);
      setZones(Array.isArray(layoutZones) ? layoutZones : []);
      setCatalog(Array.isArray(products) ? products : []);
      setTariffs(Array.isArray(tariffOptions) ? tariffOptions : []);
      setPackageOptions(Array.isArray(packageOptionRows) ? packageOptionRows : []);
      setDeviceInventory(Array.isArray(deviceRows) ? deviceRows : []);
      setBranchDeviceCommandHistory(Array.isArray(branchDeviceCommands) ? branchDeviceCommands : []);
      setSettingsLoadStatus('backend');
    } catch (error) {
      setSettingsLoadStatus('failed');
      setSettingsFeedback({ label: t('op.settings.profile.loadFeedbackLabel'), state: 'failed', detail: projectOperatorError(error, t).detail });
    }
  };

  useEffect(() => {
    void loadSettings();
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

  const renderActiveDestination = () => {
    if (currentId === 'club') {
      return <ClubDestination backend={backend} session={session} currencyCode={currencyCode} onDirtyChange={setDirty} />;
    }
    if (currentId === 'loyalty') {
      return <LoyaltyDestination backend={backend} session={session} currencyCode={currencyCode} onDirtyChange={setDirty} />;
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
          branchDeviceCommandHistory={branchDeviceCommandHistory}
          onDeviceInventoryChange={setDeviceInventory}
          onBranchDeviceCommandHistoryChange={setBranchDeviceCommandHistory}
          onReload={loadSettings}
          onFeedback={setSettingsFeedback}
          loadStatus={settingsLoadStatus}
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
          packageOptions={packageOptions}
          onReload={loadSettings}
          onFeedback={setSettingsFeedback}
          loadStatus={settingsLoadStatus}
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
        />
      );
    }

    // payment: last remaining destination id — all 8 sections now route to a real wrapper.
    return <PaymentDestination backend={backend} session={session} currencyCode={currencyCode} onDirtyChange={setDirty} />;
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
