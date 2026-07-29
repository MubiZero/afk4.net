import { useEffect, useState } from 'react';
import { CircleDollarSign, MonitorCheck, UserRoundPlus, Wifi, Wrench } from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { projectOperatorError } from './apiErrors';
import { SettingsProfileSection } from './settings/SettingsProfileSection';
import { SettingsGoodsSection } from './settings/SettingsGoodsSection';
import { SettingsIntegrationsSection } from './settings/SettingsIntegrationsSection';
import { SettingsLayoutSection } from './settings/SettingsLayoutSection';
import { SettingsStaffSection } from './settings/SettingsStaffSection';
import { SettingsTariffsSection } from './settings/SettingsTariffsSection';
import type {
  BranchDiagnosticsDto,
  BranchProfileDto,
  DeviceCommandStatusDto,
  DeviceInventoryItemDto,
  PackageOptionDto,
  PosProductDto,
  StaffUserDto,
  TariffOptionDto,
  UpdateRolloutStatusDto,
  ZoneDto
} from './operatorApiClients';
import type { Feedback, LoadStatus, OperatorBackendContext } from './operatorTypes';
import { hasPermission, permissionNames } from './operatorPermissions';
import {
  createAuthenticatedOperatorClients,
  emptyFeedback,
  isRecord,
  readArray,
  readNullableString,
  readNumber,
  readString,
  requireBackend,
  triggerFeedback,
  workspaceLoadStatusLabel
} from './operatorHelpers';
import { normalizeWorkingHours } from './settings/club/workingHours';
import { useFeedbackToasts } from './useFeedbackToasts';

export function BackendSettingsWorkspace({ currencyCode, backend }: { currencyCode: string; backend: OperatorBackendContext | null }) {
  const { t } = useI18n();

  // --- Sentinel constants (displayed AND compared) ---
  const sectionProfileKey = t('op.settings.section.profile');
  const sectionLayoutKey = t('op.settings.section.layout');
  const sectionTariffsKey = t('op.settings.section.tariffs');
  const sectionStaffKey = t('op.settings.section.staff');
  const sectionGoodsKey = t('op.settings.section.goods');
  const sectionIntegrationsKey = t('op.settings.section.integrations');

  const checkDevicesActionKey = t('op.settings.action.checkDevices');
  const addSeatActionKey = t('op.settings.action.addSeat');
  const createTariffActionKey = t('op.settings.action.createTariff');
  const inviteStaffActionKey = t('op.settings.action.inviteStaff');
  const updateStaffProfileActionKey = t('op.settings.action.updateStaffProfile');

  const [selectedSection, setSelectedSection] = useState(() => t('op.settings.section.profile'));
  const [clubName, setClubName] = useState('AFK4');
  const [city, setCity] = useState('Dushanbe');
  const [rawProfile, setRawProfile] = useState<BranchProfileDto | null>(null);
  const [settingsDirty, setSettingsDirty] = useState(false);
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  useFeedbackToasts(feedback);
  const [loadStatus, setLoadStatus] = useState<LoadStatus>('fixture');
  const [staffUsers, setStaffUsers] = useState<StaffUserDto[]>([]);
  const [zones, setZones] = useState<ZoneDto[]>([]);
  const [catalog, setCatalog] = useState<PosProductDto[]>([]);
  const [diagnostics, setDiagnostics] = useState<BranchDiagnosticsDto | null>(null);
  const [rollouts, setRollouts] = useState<UpdateRolloutStatusDto[]>([]);
  const [tariffs, setTariffs] = useState<TariffOptionDto[]>([]);
  const [packageOptions, setPackageOptions] = useState<PackageOptionDto[]>([]);
  const [deviceInventory, setDeviceInventory] = useState<DeviceInventoryItemDto[]>([]);
  const [branchDeviceCommandHistory, setBranchDeviceCommandHistory] = useState<DeviceCommandStatusDto[]>([]);

  const loadSettings = async (nextBackend = backend) => {
    if (nextBackend === null) {
      setLoadStatus('fixture');
      return;
    }

    setLoadStatus('loading');
    try {
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const [branchProfile, staff, layoutZones, products, branchDiagnostics, rolloutStatuses, tariffOptions, packageOptionRows, deviceRows, branchDeviceCommands] = await Promise.all([
        apiClients.settings.getBranchProfile(nextBackend.branchId),
        apiClients.settings.getStaffUsers(nextBackend.branchId),
        apiClients.settings.getLayoutZones(nextBackend.branchId),
        apiClients.pos.getCatalog(nextBackend.branchId),
        apiClients.diagnostics.getDiagnostics(nextBackend.branchId),
        apiClients.updates.getRolloutStatuses(nextBackend.branchId),
        apiClients.settings.getTariffOptions(nextBackend.branchId),
        apiClients.settings.getPackageOptions(nextBackend.branchId).catch(() => []),
        hasPermission(nextBackend.session, permissionNames.viewDeviceDetail)
          ? apiClients.devices.listDevices(nextBackend.branchId).catch(() => [])
          : Promise.resolve([]),
        hasPermission(nextBackend.session, permissionNames.viewDeviceCommandStatus)
          ? apiClients.devices.listBranchDeviceCommands(nextBackend.branchId, { limit: 50 }).catch(() => [])
          : Promise.resolve([])
      ]);
      const productRows = Array.isArray(products) ? products : [];
      setStaffUsers(Array.isArray(staff) ? staff : []);
      const zoneRows = Array.isArray(layoutZones) ? layoutZones : [];
      setZones(zoneRows);
      setCatalog(productRows);
      setDiagnostics(branchDiagnostics);
      const rolloutRows = Array.isArray(rolloutStatuses) ? rolloutStatuses : [];
      setRollouts(rolloutRows);
      const tariffRows = Array.isArray(tariffOptions) ? tariffOptions : [];
      setTariffs(tariffRows);
      const packageRows = Array.isArray(packageOptionRows) ? packageOptionRows : [];
      setPackageOptions(packageRows);
      const nextDeviceInventory = Array.isArray(deviceRows) ? deviceRows : [];
      setDeviceInventory(nextDeviceInventory);
      setBranchDeviceCommandHistory(Array.isArray(branchDeviceCommands) ? branchDeviceCommands : []);
      setClubName(readString(branchProfile, 'name', 'AFK4'));
      setCity(readString(branchProfile, 'city', 'Dushanbe'));
      setRawProfile(branchProfile);
      setSettingsDirty(false);
      setLoadStatus('backend');
    } catch (error) {
      setLoadStatus('failed');
      setFeedback({ label: t('op.settings.profile.loadFeedbackLabel'), state: 'failed', detail: projectOperatorError(error, t).detail });
    }
  };

  useEffect(() => {
    void loadSettings();
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, currencyCode]);

  const sections = [
    [sectionProfileKey, t('op.settings.section.profile.detail')],
    [sectionLayoutKey, t('op.settings.section.layout.detail')],
    [sectionTariffsKey, t('op.settings.section.tariffs.detail')],
    [sectionStaffKey, t('op.settings.section.staff.detail')],
    [sectionGoodsKey, t('op.settings.section.goods.detail')],
    [sectionIntegrationsKey, t('op.settings.section.integrations.detail')]
  ];
  const selectedSectionDetail = sections.find(([name]) => name === selectedSection)?.[1] ?? '';
  const deviceSummary = isRecord(diagnostics) ? diagnostics.deviceSummary : null;
  const updateSummary = isRecord(diagnostics) ? diagnostics.updateSummary : null;
  const canManageLayout = backend !== null && hasPermission(backend.session, permissionNames.manageLayout);
  const canManageBranchStaff = backend !== null && hasPermission(backend.session, permissionNames.manageBranchStaff);
  const canManageRoles = backend !== null && hasPermission(backend.session, permissionNames.manageRoles);
  const canManagePosCatalog = backend !== null && hasPermission(backend.session, permissionNames.managePosCatalog);
  const canManageInventoryStock = backend !== null && hasPermission(backend.session, permissionNames.manageInventoryStock);
  const canManageTariffs = backend !== null && hasPermission(backend.session, permissionNames.manageTariffs);
  const canManagePackages = backend !== null && hasPermission(backend.session, permissionNames.managePackages);
  const canCreateDeviceEnrollmentCode = backend !== null && hasPermission(backend.session, permissionNames.createDeviceEnrollmentCode);
  const canAssignDeviceSeat = backend !== null && hasPermission(backend.session, permissionNames.assignDeviceSeat);
  const canViewDeviceDetail = backend !== null && hasPermission(backend.session, permissionNames.viewDeviceDetail);
  const canViewDeviceCommandStatus = backend !== null && hasPermission(backend.session, permissionNames.viewDeviceCommandStatus);
  const canDispatchDeviceCommand = backend !== null && hasPermission(backend.session, permissionNames.dispatchDeviceCommand);
  const canRotateDeviceCredential = backend !== null && hasPermission(backend.session, permissionNames.rotateDeviceCredential);
  const canRevokeDeviceCredential = backend !== null && hasPermission(backend.session, permissionNames.revokeDeviceCredential);
  const readiness = [
    [t('op.settings.readiness.profile'), `${clubName} · ${city}`],
    [t('op.settings.readiness.layout'), t('op.settings.readiness.layoutSeats', { count: zones.reduce((sum, zone) => sum + readArray(zone, 'seats').length, 0) })],
    [t('op.settings.readiness.staff'), t('op.settings.readiness.staffCount', { count: staffUsers.length })],
    [t('op.settings.readiness.pos'), t('op.settings.readiness.posDetail', { currencyCode })],
    [t('op.settings.readiness.devices'), t('op.settings.readiness.devicesOnline', { online: readNumber(deviceSummary, 'onlineDevices', 0), total: readNumber(deviceSummary, 'totalDevices', 0) })]
  ];
  // Быстрые действия = ярлыки к разделам (клик переключает вкладку, а не выполняет действие
  // со скрытой формой). Исключение — checkDevices: это read-only обновление диагностики, оно
  // питает readiness-панель, поэтому выполняется на месте (section: null).
  const actions: Array<{ key: string; detail: string; Icon: LucideIcon; section: string | null }> = [
    { key: addSeatActionKey, detail: t('op.settings.action.addSeat.detail'), Icon: MonitorCheck, section: sectionLayoutKey },
    { key: createTariffActionKey, detail: t('op.settings.action.createTariff.detail'), Icon: CircleDollarSign, section: sectionTariffsKey },
    { key: inviteStaffActionKey, detail: canManageBranchStaff ? t('op.settings.action.inviteStaff.detailAllowed') : t('op.settings.action.inviteStaff.detailDenied'), Icon: UserRoundPlus, section: sectionStaffKey },
    { key: updateStaffProfileActionKey, detail: canManageBranchStaff ? t('op.settings.action.updateStaffProfile.detailAllowed') : t('op.settings.action.updateStaffProfile.detailDenied'), Icon: Wrench, section: sectionStaffKey },
    { key: checkDevicesActionKey, detail: t('op.settings.action.checkDevices.detail'), Icon: Wifi, section: null }
  ];

  const runSettingsAction = async (label: string) => {
    setFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      if (label === checkDevicesActionKey) {
        setDiagnostics(await apiClients.diagnostics.getDiagnostics(nextBackend.branchId));
      } else {
        throw new Error(t('op.settings.generic.error.notConnected'));
      }

      setFeedback({ label, state: 'confirmed' });
    } catch (error) {
      setFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    }
  };

  const saveSettings = async () => {
    if (!clubName.trim() || !city.trim()) {
      triggerFeedback(setFeedback, t('op.settings.profile.feedbackLabel'), 'failed', t('op.settings.profile.errorRequiredFields'));
      return;
    }

    setFeedback({ label: t('op.settings.profile.feedbackLabel'), state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const branchProfile: BranchProfileDto = await apiClients.settings.updateBranchProfile(nextBackend.branchId, {
        organizationId: nextBackend.session.organizationId,
        name: clubName.trim(),
        city: city.trim(),
        description: readNullableString(rawProfile, 'description'),
        address: readNullableString(rawProfile, 'address'),
        phone: readNullableString(rawProfile, 'phone'),
        telegram: readNullableString(rawProfile, 'telegram'),
        website: readNullableString(rawProfile, 'website'),
        instagram: readNullableString(rawProfile, 'instagram'),
        logoUrl: readNullableString(rawProfile, 'logoUrl'),
        logoMediaId: readNullableString(rawProfile, 'logoMediaId'),
        timeZone: readString(rawProfile, 'timeZone', 'Asia/Dushanbe'),
        locale: readString(rawProfile, 'locale', 'ru'),
        workingHours: normalizeWorkingHours(rawProfile?.workingHours)
      });
      setClubName(readString(branchProfile, 'name', clubName.trim()));
      setCity(readString(branchProfile, 'city', city.trim()));
      setRawProfile(branchProfile);
      setSettingsDirty(false);
      setFeedback({ label: t('op.settings.profile.feedbackLabel'), state: 'confirmed' });
    } catch (error) {
      setFeedback({ label: t('op.settings.profile.feedbackLabel'), state: 'failed', detail: projectOperatorError(error, t).detail });
    }
  };

  const renderSettingsContent = () => {
    if (selectedSection === sectionLayoutKey) {
      return (
        <SettingsLayoutSection
          zones={zones}
          deviceInventory={deviceInventory}
          branchDeviceCommandHistory={branchDeviceCommandHistory}
          backend={backend}
          canManageLayout={canManageLayout}
          canCreateDeviceEnrollmentCode={canCreateDeviceEnrollmentCode}
          canAssignDeviceSeat={canAssignDeviceSeat}
          canViewDeviceDetail={canViewDeviceDetail}
          canViewDeviceCommandStatus={canViewDeviceCommandStatus}
          canDispatchDeviceCommand={canDispatchDeviceCommand}
          canRotateDeviceCredential={canRotateDeviceCredential}
          canRevokeDeviceCredential={canRevokeDeviceCredential}
          onBranchDeviceCommandHistoryChange={setBranchDeviceCommandHistory}
          onDeviceInventoryChange={setDeviceInventory}
          onReload={loadSettings}
          onFeedback={setFeedback}
        />
      );
    }

    if (selectedSection === sectionTariffsKey) {
      return (
        <SettingsTariffsSection
          tariffs={tariffs}
          packageOptions={packageOptions}
          currencyCode={currencyCode}
          backend={backend}
          canManageTariffs={canManageTariffs}
          canManagePackages={canManagePackages}
          onReload={loadSettings}
          onFeedback={setFeedback}
        />
      );
    }

    if (selectedSection === sectionStaffKey) {
      return (
        <SettingsStaffSection
          staffUsers={staffUsers}
          backend={backend}
          canManageBranchStaff={canManageBranchStaff}
          canManageRoles={canManageRoles}
          onStaffUsersChange={setStaffUsers}
          onFeedback={setFeedback}
        />
      );
    }

    if (selectedSection === sectionGoodsKey) {
      return (
        <SettingsGoodsSection
          catalog={catalog}
          currencyCode={currencyCode}
          backend={backend}
          canManagePosCatalog={canManagePosCatalog}
          canManageInventoryStock={canManageInventoryStock}
          onCatalogChange={setCatalog}
          onReload={loadSettings}
          onFeedback={setFeedback}
        />
      );
    }

    if (selectedSection === sectionIntegrationsKey) {
      return (
        <SettingsIntegrationsSection
          rollouts={rollouts}
          updateSummary={updateSummary as Record<string, unknown> | null}
        />
      );
    }


    return (
      <SettingsProfileSection
        clubName={clubName}
        city={city}
        currencyCode={currencyCode}
        hasBackend={backend !== null}
        settingsDirty={settingsDirty}
        onClubNameChange={(value) => { setClubName(value); setSettingsDirty(true); }}
        onCityChange={(value) => { setCity(value); setSettingsDirty(true); }}
        onSave={saveSettings}
      />
    );
  };

  return (
    <main className="workspace-screen settings-screen">
      <section className="screen-head settings-head">
        <div>
          <span>{t('op.settings.title')}</span>
          <h1>{t('op.settings.heading')}</h1>
        </div>
        <div className="screen-actions">
          <span className={`map-load-state ${loadStatus === 'backend' ? 'ready' : loadStatus}`}>{workspaceLoadStatusLabel(loadStatus, t('op.settings.profile.loadFeedbackLabel'), t)}</span>
        </div>
      </section>

      <section className="settings-layout">
        <aside className="settings-nav-panel">
          <span>{t('op.settings.sections.label')}</span>
          {sections.map(([name, detail]) => (
            <button
              key={name}
              type="button"
              className={selectedSection === name ? 'active' : undefined}
              onClick={() => setSelectedSection(name)}
            >
              <strong>{name}</strong>
              <em>{detail}</em>
            </button>
          ))}
        </aside>

        <section className="settings-main-panel">
          <header className="settings-panel-title">
            <span>{selectedSection}</span>
            <strong>{selectedSectionDetail}</strong>
          </header>
          {renderSettingsContent()}
        </section>

        <aside className="settings-side-panel">
          <section className="settings-card-panel">
            <header className="settings-panel-title">
              <span>{t('op.settings.readiness.title')}</span>
              <strong>{t('op.settings.readiness.detail')}</strong>
            </header>
            <div className="settings-readiness-list">
              {readiness.map(([name, detail]) => (
                <div key={name}>
                  <span>{name}</span>
                  <strong>{detail}</strong>
                </div>
              ))}
            </div>
          </section>

          <section className="settings-card-panel">
            <header className="settings-panel-title">
              <span>{t('op.settings.quickActions.title')}</span>
              <strong>{t('op.settings.quickActions.detail')}</strong>
            </header>
            <div className="settings-action-grid">
              {actions.map(({ key, detail, Icon, section }) => (
                <button key={key} type="button" className="settings-action-card" onClick={() => (section === null ? runSettingsAction(key) : setSelectedSection(section))}>
                  <Icon size={17} />
                  <strong>{key}</strong>
                  <span>{detail}</span>
                </button>
              ))}
            </div>
          </section>
        </aside>
      </section>
    </main>
  );
}
