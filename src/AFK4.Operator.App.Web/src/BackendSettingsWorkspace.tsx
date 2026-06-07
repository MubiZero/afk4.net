import { useEffect, useState } from 'react';
import { CircleDollarSign, MonitorCheck, UserRoundPlus, Wifi, Wrench } from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { projectOperatorError } from './apiErrors';
import type {
  BranchDiagnosticsDto,
  BranchProfileDto,
  DeviceCommandStatusDto,
  DeviceInventoryItemDto,
  PackageOptionDto,
  PosProductDto,
  StaffUserDto,
  StockMovementDto,
  TariffOptionDto,
  UpdatePackageDto,
  UpdateRolloutStatusDto,
  ZoneDto
} from './operatorApiClients';
import type { Feedback, LoadStatus, OperatorBackendContext } from './operatorTypes';
import { hasPermission, permissionNames, staffRoleOptions } from './operatorPermissions';
import {
  commandStatusLabel,
  commandStatusMessageLabel,
  commandTypeLabel,
  createAuthenticatedOperatorClients,
  createIdempotencyKey,
  emptyFeedback,
  formatMinorUnits,
  formatMoney,
  formatMoneyInputMinorUnits,
  formatTime,
  isGuid,
  isRecord,
  operatorDisplayNameLabel,
  parseMoneyInputMinorUnits,
  parseNonNegativeMoneyInputMinorUnits,
  readArray,
  readBoolean,
  readMoney,
  readNumber,
  readString,
  requireBackend,
  staffRoleLabel,
  stockMovementTypeLabel,
  triggerFeedback,
  updateChannelLabel,
  updateComponentLabel,
  updateDeviceMessageLabel,
  updateDeviceStatusLabel,
  updatePackageStateLabel,
  updateRolloutStateLabel,
  updateTargetKindLabel,
  workspaceLoadStatusLabel
} from './operatorHelpers';
import { CriticalActionConfirmation, FeedbackNotice } from './operatorPrimitives';

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
  const refreshCommandHistoryActionKey = t('op.settings.action.refreshCommandHistory');
  const createEnrollmentCodeActionKey = t('op.settings.action.createEnrollmentCode');
  const assignDeviceActionKey = t('op.settings.action.assignDevice');
  const openDeviceCardActionKey = t('op.settings.action.openDeviceCard');
  const sendCommandActionKey = t('op.settings.action.sendCommand');
  const rotateKeyActionKey = t('op.settings.action.rotateKey');
  const revokeKeyActionKey = t('op.settings.action.revokeKey');
  const createZoneActionKey = t('op.settings.action.createZone');
  const updateZoneActionKey = t('op.settings.action.updateZone');
  const deleteZoneActionKey = t('op.settings.action.deleteZone');
  const addSeatActionKey = t('op.settings.action.addSeat');
  const updateSeatActionKey = t('op.settings.action.updateSeat');
  const deleteSeatActionKey = t('op.settings.action.deleteSeat');
  const createTariffActionKey = t('op.settings.action.createTariff');
  const updateTariffActionKey = t('op.settings.action.updateTariff');
  const retireTariffActionKey = t('op.settings.action.retireTariff');
  const createPackageActionKey = t('op.settings.action.createPackage');
  const updatePackageActionKey = t('op.settings.action.updatePackage');
  const retirePackageActionKey = t('op.settings.action.retirePackage');
  const inviteStaffActionKey = t('op.settings.action.inviteStaff');
  const updateStaffProfileActionKey = t('op.settings.action.updateStaffProfile');
  const updateRoleActionKey = t('op.settings.action.updateRole');
  const disableStaffActionKey = t('op.settings.action.disableStaff');
  const enableStaffActionKey = t('op.settings.action.enableStaff');
  const resetPasswordActionKey = t('op.settings.action.resetPassword');
  const createProductActionKey = t('op.settings.action.createProduct');
  const updateProductActionKey = t('op.settings.action.updateProduct');
  const delistProductActionKey = t('op.settings.action.delistProduct');
  const recordMovementActionKey = t('op.settings.action.recordMovement');
  const addUpdatePackageActionKey = t('op.settings.action.addUpdatePackage');
  const createRolloutActionKey = t('op.settings.action.createRollout');
  const changePackageStateActionKey = t('op.settings.action.changePackageState');
  const changeRolloutStateActionKey = t('op.settings.action.changeRolloutState');

  const [selectedSection, setSelectedSection] = useState(() => t('op.settings.section.profile'));
  const [clubName, setClubName] = useState('AFK4');
  const [city, setCity] = useState('Dushanbe');
  const [settingsDirty, setSettingsDirty] = useState(false);
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const [loadStatus, setLoadStatus] = useState<LoadStatus>('fixture');
  const [staffUsers, setStaffUsers] = useState<StaffUserDto[]>([]);
  const [zones, setZones] = useState<ZoneDto[]>([]);
  const [catalog, setCatalog] = useState<PosProductDto[]>([]);
  const [stockMovements, setStockMovements] = useState<StockMovementDto[]>([]);
  const [diagnostics, setDiagnostics] = useState<BranchDiagnosticsDto | null>(null);
  const [rollouts, setRollouts] = useState<UpdateRolloutStatusDto[]>([]);
  const [registeredUpdatePackages, setRegisteredUpdatePackages] = useState<UpdatePackageDto[]>([]);
  const [tariffs, setTariffs] = useState<TariffOptionDto[]>([]);
  const [packageOptions, setPackageOptions] = useState<PackageOptionDto[]>([]);
  const [deviceInventory, setDeviceInventory] = useState<DeviceInventoryItemDto[]>([]);
  const [deviceAssignmentDeviceId, setDeviceAssignmentDeviceId] = useState('');
  const [deviceAssignmentSeatId, setDeviceAssignmentSeatId] = useState('');
  const [enrollmentExpiresMinutes, setEnrollmentExpiresMinutes] = useState('15');
  const [enrollmentCode, setEnrollmentCode] = useState<Record<string, unknown> | null>(null);
  const [deviceDetail, setDeviceDetail] = useState<Record<string, unknown> | null>(null);
  const [deviceCommandHistory, setDeviceCommandHistory] = useState<DeviceCommandStatusDto[]>([]);
  const [branchDeviceCommandHistory, setBranchDeviceCommandHistory] = useState<DeviceCommandStatusDto[]>([]);
  const [credentialIdToRevoke, setCredentialIdToRevoke] = useState('');
  const [rotatedCredential, setRotatedCredential] = useState<Record<string, unknown> | null>(null);
  const [criticalAction, setCriticalAction] = useState<
    'credential-revoke' |
    'layout-zone-delete' |
    'layout-seat-delete' |
    'package-state-change' |
    'rollout-state-change' |
    null
  >(null);
  const [deviceCommandType, setDeviceCommandType] = useState('lock');
  const [deviceCommandReason, setDeviceCommandReason] = useState('Проверка оператором'); // default prefill, not user-facing copy
  const [lastDeviceCommand, setLastDeviceCommand] = useState<Record<string, unknown> | null>(null);
  const [layoutZoneName, setLayoutZoneName] = useState('Основной зал'); // default prefill
  const [layoutZoneSortOrder, setLayoutZoneSortOrder] = useState('10');
  const [layoutSeatZoneId, setLayoutSeatZoneId] = useState('');
  const [layoutSeatName, setLayoutSeatName] = useState('PC-01');
  const [layoutSeatSortOrder, setLayoutSeatSortOrder] = useState('10');
  const [selectedLayoutZoneId, setSelectedLayoutZoneId] = useState('');
  const [selectedLayoutSeatId, setSelectedLayoutSeatId] = useState('');
  const [inviteUserName, setInviteUserName] = useState('operator');
  const [inviteDisplayName, setInviteDisplayName] = useState('Новый оператор'); // default prefill
  const [inviteEmail, setInviteEmail] = useState('');
  const [inviteCode, setInviteCode] = useState<string | null>(null);
  const [resetPassword, setResetPassword] = useState('');
  const [inviteRoleName, setInviteRoleName] = useState('cashier_operator');
  const [selectedStaffUserId, setSelectedStaffUserId] = useState('');
  const [staffProfileUserName, setStaffProfileUserName] = useState('');
  const [staffProfileDisplayName, setStaffProfileDisplayName] = useState('');
  const [staffRoleName, setStaffRoleName] = useState('cashier_operator');
  const [productCategoryName, setProductCategoryName] = useState('Категория 1'); // default prefill
  const [productName, setProductName] = useState('Товар 1'); // default prefill
  const [productSku, setProductSku] = useState('SKU-001');
  const [productPrice, setProductPrice] = useState('12.00');
  const [productTrackStock, setProductTrackStock] = useState(true);
  const [productAllowNegativeStock, setProductAllowNegativeStock] = useState(false);
  const [selectedProductId, setSelectedProductId] = useState('');
  const [stockProductId, setStockProductId] = useState('');
  const [stockMovementType, setStockMovementType] = useState('purchase');
  const [stockQuantityDelta, setStockQuantityDelta] = useState('10');
  const [stockUnitCost, setStockUnitCost] = useState('0.00');
  const [stockReason, setStockReason] = useState('Первичное поступление'); // default prefill
  const [tariffName, setTariffName] = useState('Дневной тариф'); // default prefill
  const [tariffPricePerHour, setTariffPricePerHour] = useState('90.00');
  const [tariffMinimumMinutes, setTariffMinimumMinutes] = useState('15');
  const [tariffRoundingMinutes, setTariffRoundingMinutes] = useState('5');
  const [tariffEffectiveFromUtc, setTariffEffectiveFromUtc] = useState(() => new Date().toISOString());
  const [selectedTariffVersionId, setSelectedTariffVersionId] = useState('');
  const [packageName, setPackageName] = useState('Ночной пакет 5ч'); // default prefill
  const [packagePrice, setPackagePrice] = useState('250.00');
  const [packageMinutes, setPackageMinutes] = useState('300');
  const [packageBonusMinutes, setPackageBonusMinutes] = useState('30');
  const [packageExpiresDays, setPackageExpiresDays] = useState('30');
  const [selectedPackageDefinitionId, setSelectedPackageDefinitionId] = useState('');
  const [updateComponent, setUpdateComponent] = useState('operator-app');
  const [updateVersion, setUpdateVersion] = useState('0.1.0');
  const [updateChannel, setUpdateChannel] = useState('internal');
  const [updateArtifactUri, setUpdateArtifactUri] = useState('https://updates.afk4.staging.mubi.dev/operator-app/0.1.0/operator-app.msi');
  const [updateSha256, setUpdateSha256] = useState('0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef');
  const [updateSignature, setUpdateSignature] = useState('signed-update-package');
  const [updateSignatureAlgorithm, setUpdateSignatureAlgorithm] = useState('ECDSA-P256-SHA256-IEEE-P1363');
  const [updateSizeKilobytes, setUpdateSizeKilobytes] = useState('1024');
  const [updateReleaseNotes, setUpdateReleaseNotes] = useState('Пакет обновления приложения оператора.'); // default prefill
  const [rolloutPackageId, setRolloutPackageId] = useState('');
  const [rolloutChannel, setRolloutChannel] = useState('internal');
  const [rolloutTargetKind, setRolloutTargetKind] = useState('branch');
  const [rolloutTargetDeviceIds, setRolloutTargetDeviceIds] = useState('');
  const [rolloutBatchPercent, setRolloutBatchPercent] = useState('100');
  const [rolloutStartsAtUtc, setRolloutStartsAtUtc] = useState(() => new Date(Date.now() + 60 * 60 * 1000).toISOString());
  const [rolloutReason, setRolloutReason] = useState('Публикация обновления оператором.'); // default prefill
  const [packageStatePackageId, setPackageStatePackageId] = useState('');
  const [packageState, setPackageState] = useState('validated');
  const [packageStateReason, setPackageStateReason] = useState('Подпись проверена.'); // default prefill
  const [rolloutStateRolloutId, setRolloutStateRolloutId] = useState('');
  const [rolloutState, setRolloutState] = useState('paused');
  const [rolloutStateReason, setRolloutStateReason] = useState('Изменение состояния оператором.'); // default prefill

  const loadSettings = async (nextBackend = backend) => {
    if (nextBackend === null) {
      setLoadStatus('fixture');
      return;
    }

    setLoadStatus('loading');
    try {
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const [branchProfile, staff, layoutZones, products, stockMovementRows, branchDiagnostics, rolloutStatuses, tariffOptions, packageOptionRows, deviceRows, branchDeviceCommands] = await Promise.all([
        apiClients.settings.getBranchProfile(nextBackend.branchId),
        apiClients.settings.getStaffUsers(nextBackend.branchId),
        apiClients.settings.getLayoutZones(nextBackend.branchId),
        apiClients.pos.getCatalog(nextBackend.branchId),
        apiClients.inventory.getStockMovements(nextBackend.branchId, { limit: 8 }).catch(() => []),
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
      const staffRows = Array.isArray(staff) ? staff : [];
      const selectedStaff = staffRows.find((user) => readString(user, 'staffUserId') === selectedStaffUserId) ?? staffRows[0];
      const selectedStaffRole = readArray<string>(selectedStaff, 'roleNames')
        .find((role) => staffRoleOptions.includes(role as (typeof staffRoleOptions)[number]));
      setStaffUsers(staffRows);
      setSelectedStaffUserId(readString(selectedStaff, 'staffUserId'));
      setStaffProfileUserName(readString(selectedStaff, 'userName'));
      setStaffProfileDisplayName(operatorDisplayNameLabel(readString(selectedStaff, 'displayName')));
      setStaffRoleName(selectedStaffRole ?? 'cashier_operator');
      const zoneRows = Array.isArray(layoutZones) ? layoutZones : [];
      setZones(zoneRows);
      const firstZoneId = readString(zoneRows[0], 'zoneId');
      setLayoutSeatZoneId((current) => zoneRows.some((zone) => readString(zone, 'zoneId') === current) ? current : firstZoneId);
      setSelectedLayoutZoneId((current) => zoneRows.some((zone) => readString(zone, 'zoneId') === current) ? current : '');
      const firstSeatId = zoneRows.flatMap((zone) => readArray<Record<string, unknown>>(zone, 'seats')).map((seat) => readString(seat, 'seatId')).find(Boolean) ?? '';
      setSelectedLayoutSeatId((current) => zoneRows.some((zone) => readArray<Record<string, unknown>>(zone, 'seats').some((seat) => readString(seat, 'seatId') === current)) ? current : '');
      setDeviceAssignmentSeatId((current) => isGuid(current) ? current : firstSeatId);
      setCatalog(productRows);
      setSelectedProductId((current) => productRows.some((product) => readString(product, 'productId') === current) ? current : '');
      setStockMovements(Array.isArray(stockMovementRows) ? stockMovementRows : []);
      setStockProductId((current) => productRows.some((product) => readString(product, 'productId') === current && readBoolean(product, 'trackStock'))
        ? current
        : readString(productRows.find((product) => readBoolean(product, 'trackStock')), 'productId'));
      setDiagnostics(branchDiagnostics);
      const rolloutRows = Array.isArray(rolloutStatuses) ? rolloutStatuses : [];
      setRollouts(rolloutRows);
      setRolloutPackageId((current) => rolloutRows.some((rollout) => readString(rollout, 'updatePackageId') === current)
        ? current
        : readString(rolloutRows[0], 'updatePackageId'));
      setPackageStatePackageId((current) => rolloutRows.some((rollout) => readString(rollout, 'updatePackageId') === current)
        ? current
        : readString(rolloutRows[0], 'updatePackageId'));
      setRolloutStateRolloutId((current) => rolloutRows.some((rollout) => readString(rollout, 'updateRolloutId') === current)
        ? current
        : readString(rolloutRows[0], 'updateRolloutId'));
      const tariffRows = Array.isArray(tariffOptions) ? tariffOptions : [];
      setTariffs(tariffRows);
      setSelectedTariffVersionId((current) => tariffRows.some((tariff) => readString(tariff, 'tariffVersionId') === current) ? current : '');
      const packageRows = Array.isArray(packageOptionRows) ? packageOptionRows : [];
      setPackageOptions(packageRows);
      setSelectedPackageDefinitionId((current) => packageRows.some((option) => readString(option, 'packageDefinitionId') === current) ? current : '');
      const nextDeviceInventory = Array.isArray(deviceRows) ? deviceRows : [];
      setDeviceInventory(nextDeviceInventory);
      setBranchDeviceCommandHistory(Array.isArray(branchDeviceCommands) ? branchDeviceCommands : []);
      setDeviceAssignmentDeviceId((current) => nextDeviceInventory.some((device) => readString(device, 'deviceId') === current)
        ? current
        : readString(nextDeviceInventory[0], 'deviceId'));
      setClubName(readString(branchProfile, 'name', 'AFK4'));
      setCity(readString(branchProfile, 'city', 'Dushanbe'));
      setSettingsDirty(false);
      setLoadStatus('backend');
    } catch (error) {
      setLoadStatus('failed');
      setFeedback({ label: t('op.settings.profile.loadFeedbackLabel'), state: 'failed', detail: projectOperatorError(error).detail });
    }
  };

  useEffect(() => {
    void loadSettings();
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, currencyCode]);

  useEffect(() => {
    setCriticalAction(null);
  }, [deviceAssignmentDeviceId]);

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
  const canManageUpdatePackages = backend !== null && hasPermission(backend.session, permissionNames.manageUpdatePackages);
  const canManageUpdateRollouts = backend !== null && hasPermission(backend.session, permissionNames.manageUpdateRollouts);
  const canCreateDeviceEnrollmentCode = backend !== null && hasPermission(backend.session, permissionNames.createDeviceEnrollmentCode);
  const canAssignDeviceSeat = backend !== null && hasPermission(backend.session, permissionNames.assignDeviceSeat);
  const canViewDeviceDetail = backend !== null && hasPermission(backend.session, permissionNames.viewDeviceDetail);
  const canViewDeviceCommandStatus = backend !== null && hasPermission(backend.session, permissionNames.viewDeviceCommandStatus);
  const canDispatchDeviceCommand = backend !== null && hasPermission(backend.session, permissionNames.dispatchDeviceCommand);
  const canRotateDeviceCredential = backend !== null && hasPermission(backend.session, permissionNames.rotateDeviceCredential);
  const canRevokeDeviceCredential = backend !== null && hasPermission(backend.session, permissionNames.revokeDeviceCredential);
  const selectedStaffUser = staffUsers.find((user) => readString(user, 'staffUserId') === selectedStaffUserId);
  const selectedStaffIsActive = readBoolean(selectedStaffUser, 'isActive', true);
  const selectedLayoutZone = zones.find((zone) => readString(zone, 'zoneId') === selectedLayoutZoneId) ?? null;
  const selectedLayoutSeat = zones
    .flatMap((zone) => readArray<Record<string, unknown>>(zone, 'seats'))
    .find((seat) => readString(seat, 'seatId') === selectedLayoutSeatId) ?? null;
  const layoutSeatOptions = zones.flatMap((zone) => readArray<Record<string, unknown>>(zone, 'seats').map((seat) => ({
    seatId: readString(seat, 'seatId'),
    label: `${readString(zone, 'name', t('op.settings.layout.zoneFallback'))} · ${readString(seat, 'name', t('op.settings.layout.seatFallback'))}`
  }))).filter((seat) => isGuid(seat.seatId));
  const trackedCatalog = catalog.filter((product) => readBoolean(product, 'trackStock'));
  const deviceRecentCommands = deviceCommandHistory.length > 0
    ? deviceCommandHistory
    : readArray<Record<string, unknown>>(deviceDetail, 'recentCommands');
  const getDeviceInventoryName = (deviceId: string) =>
    readString(deviceInventory.find((device) => readString(device, 'deviceId') === deviceId), 'machineName', t('op.settings.devices.deviceFallback'));
  const selectedRollout = rollouts.find((rollout) => readString(rollout, 'updateRolloutId') === rolloutStateRolloutId) ?? rollouts[0] ?? null;
  const selectedRolloutDeviceStatuses = readArray<Record<string, unknown>>(selectedRollout, 'deviceStatuses');
  const deviceOptions = deviceInventory
    .map((device) => ({
      id: readString(device, 'deviceId'),
      label: `${readString(device, 'machineName', t('op.settings.devices.deviceFallback'))} · ${readString(device, 'zoneName', t('op.settings.devices.zoneFallback'))} · ${readString(device, 'seatName', t('op.settings.devices.seatFallback'))}`
    }))
    .filter((device) => isGuid(device.id));
  const selectedDeviceLabel = getDeviceInventoryName(deviceAssignmentDeviceId);
  const updatePackageOptions = Array.from(new Map([
    ...rollouts.map((rollout) => ({
      id: readString(rollout, 'updatePackageId'),
      label: `${updateComponentLabel(readString(rollout, 'component'))} ${readString(rollout, 'version', t('op.settings.updates.versionFallback'))} · ${updateChannelLabel(readString(rollout, 'channel'))}`
    })),
    ...registeredUpdatePackages.map((updatePackage) => ({
      id: readString(updatePackage, 'updatePackageId'),
      label: `${updateComponentLabel(readString(updatePackage, 'component'))} ${readString(updatePackage, 'version', t('op.settings.updates.versionFallback'))} · ${updateChannelLabel(readString(updatePackage, 'channel'))} · ${updatePackageStateLabel(readString(updatePackage, 'state', 'registered'))}`
    }))
  ].filter((option) => isGuid(option.id)).map((option) => [option.id, option])).values());
  const rolloutOptions = rollouts
    .map((rollout) => ({
      id: readString(rollout, 'updateRolloutId'),
      label: `${updateComponentLabel(readString(rollout, 'component'))} ${readString(rollout, 'version', t('op.settings.updates.versionFallback'))} · ${updateRolloutStateLabel(readString(rollout, 'state'))}`
    }))
    .filter((rollout) => isGuid(rollout.id));
  const rolloutTargetDeviceIdSet = new Set(rolloutTargetDeviceIds.split(/[\s,;]+/).map((value) => value.trim()).filter(Boolean));
  const rotatedCredentialId = readString(rotatedCredential, 'credentialId');
  const rotatedCredentialLabel = rotatedCredentialId
    ? t('op.settings.devices.credentialRotated', { deviceName: selectedDeviceLabel })
    : t('op.settings.devices.credentialEmpty');
  const selectLayoutZone = (zone: Record<string, unknown>) => {
    const zoneId = readString(zone, 'zoneId');
    setSelectedLayoutZoneId(zoneId);
    setLayoutSeatZoneId(zoneId);
    setLayoutZoneName(readString(zone, 'name', layoutZoneName));
    setLayoutZoneSortOrder(String(readNumber(zone, 'sortOrder', Number(layoutZoneSortOrder))));
    triggerFeedback(setFeedback, readString(zone, 'name', t('op.settings.layout.zoneFallback')), 'confirmed');
  };
  const selectLayoutSeat = (zone: Record<string, unknown>, seat: Record<string, unknown>) => {
    setSelectedLayoutSeatId(readString(seat, 'seatId'));
    setLayoutSeatZoneId(readString(zone, 'zoneId'));
    setLayoutSeatName(readString(seat, 'name', layoutSeatName));
    setLayoutSeatSortOrder(String(readNumber(seat, 'sortOrder', Number(layoutSeatSortOrder))));
    triggerFeedback(setFeedback, readString(seat, 'name', t('op.settings.layout.seatFallback')), 'confirmed');
  };
  const selectCatalogProduct = (product: PosProductDto) => {
    const productId = readString(product, 'productId');
    const price = readMoney(product, 'price');
    setSelectedProductId(productId);
    setProductName(readString(product, 'name', productName));
    setProductSku(readString(product, 'sku', productSku));
    setProductPrice(price ? formatMoneyInputMinorUnits(price.minorUnits) : productPrice);
    setProductTrackStock(readBoolean(product, 'trackStock', true));
    setProductAllowNegativeStock(readBoolean(product, 'allowNegativeStock'));
    triggerFeedback(setFeedback, readString(product, 'name', t('op.settings.pos.productFallback')), 'confirmed');
  };
  const selectTariffOption = (option: TariffOptionDto) => {
    setSelectedTariffVersionId(readString(option, 'tariffVersionId'));
    setTariffName(readString(option, 'name', tariffName));
    setTariffPricePerHour(formatMoneyInputMinorUnits(readNumber(option, 'pricePerMinuteMinorUnits', 0) * 60));
    setTariffMinimumMinutes(String(readNumber(option, 'minimumBillableMinutes', Number(tariffMinimumMinutes))));
    setTariffRoundingMinutes(String(readNumber(option, 'roundingIncrementMinutes', Number(tariffRoundingMinutes))));
    setTariffEffectiveFromUtc(readString(option, 'effectiveFromUtc', new Date().toISOString()));
    triggerFeedback(setFeedback, readString(option, 'name', t('op.settings.tariffs.tariffFallback')), 'confirmed');
  };
  const selectPackageOption = (option: PackageOptionDto) => {
    setSelectedPackageDefinitionId(readString(option, 'packageDefinitionId'));
    setPackageName(readString(option, 'name', packageName));
    setPackagePrice(formatMoneyInputMinorUnits(readNumber(option, 'priceMinorUnits', 0)));
    setPackageMinutes(String(Math.round(readNumber(option, 'includedSeconds', 0) / 60)));
    setPackageBonusMinutes(String(Math.round(readNumber(option, 'bonusSeconds', 0) / 60)));
    setPackageExpiresDays(String(readNumber(option, 'expiresAfterDays', 30)));
    triggerFeedback(setFeedback, readString(option, 'name', t('op.settings.packages.packageFallback')), 'confirmed');
  };
  const selectDeviceInventoryItem = (device: DeviceInventoryItemDto) => {
    const deviceId = readString(device, 'deviceId');
    const seatId = readString(device, 'seatId');
    setDeviceAssignmentDeviceId(deviceId);
    if (isGuid(seatId)) {
      setDeviceAssignmentSeatId(seatId);
    }
    setDeviceDetail(null);
    setDeviceCommandHistory([]);
    setRotatedCredential(null);
    setCredentialIdToRevoke('');
    triggerFeedback(setFeedback, readString(device, 'machineName', t('op.settings.devices.deviceFallback')), 'confirmed');
  };
  const readiness = [
    [t('op.settings.readiness.profile'), `${clubName} · ${city}`],
    [t('op.settings.readiness.layout'), t('op.settings.readiness.layoutSeats', { count: zones.reduce((sum, zone) => sum + readArray(zone, 'seats').length, 0) })],
    [t('op.settings.readiness.staff'), t('op.settings.readiness.staffCount', { count: staffUsers.length })],
    [t('op.settings.readiness.pos'), t('op.settings.readiness.posDetail', { currencyCode })],
    [t('op.settings.readiness.devices'), t('op.settings.readiness.devicesOnline', { online: readNumber(deviceSummary, 'onlineDevices', 0), total: readNumber(deviceSummary, 'totalDevices', 0) })]
  ];
  const actions: Array<[string, string, LucideIcon]> = [
    [addSeatActionKey, t('op.settings.action.addSeat.detail'), MonitorCheck],
    [createTariffActionKey, t('op.settings.action.createTariff.detail'), CircleDollarSign],
    [inviteStaffActionKey, canManageBranchStaff ? t('op.settings.action.inviteStaff.detailAllowed') : t('op.settings.action.inviteStaff.detailDenied'), UserRoundPlus],
    [updateStaffProfileActionKey, canManageBranchStaff ? t('op.settings.action.updateStaffProfile.detailAllowed') : t('op.settings.action.updateStaffProfile.detailDenied'), Wrench],
    [checkDevicesActionKey, t('op.settings.action.checkDevices.detail'), Wifi]
  ];

  const runSettingsAction = async (label: string) => {
    setCriticalAction(null);
    setFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      if (label === checkDevicesActionKey) {
        setDiagnostics(await apiClients.diagnostics.getDiagnostics(nextBackend.branchId));
      } else if (label === refreshCommandHistoryActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.viewDeviceCommandStatus)) {
          throw new Error(t('op.settings.devices.error.noPermCommandHistory'));
        }

        const commands = await apiClients.devices.listBranchDeviceCommands(nextBackend.branchId, { limit: 50 });
        setBranchDeviceCommandHistory(Array.isArray(commands) ? commands : []);
      } else if (label === createEnrollmentCodeActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.createDeviceEnrollmentCode)) {
          throw new Error(t('op.settings.devices.error.noPermEnrollment'));
        }

        const expiresInMinutes = Number(enrollmentExpiresMinutes);
        if (!Number.isInteger(expiresInMinutes) || expiresInMinutes < 1 || expiresInMinutes > 1440) {
          throw new Error(t('op.settings.devices.error.enrollmentRange'));
        }

        const expiresInSeconds = expiresInMinutes * 60;
        const code = await apiClients.devices.createEnrollmentCode(nextBackend.branchId, nextBackend.session.organizationId, expiresInSeconds);
        setEnrollmentCode(code);
      } else if (label === assignDeviceActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.assignDeviceSeat)) {
          throw new Error(t('op.settings.devices.error.noPermAssign'));
        }

        const deviceId = deviceAssignmentDeviceId.trim();
        const seatId = deviceAssignmentSeatId.trim();
        if (!isGuid(deviceId) || !isGuid(seatId)) {
          throw new Error(t('op.settings.devices.error.selectDeviceAndSeat'));
        }

        await apiClients.settings.assignDeviceSeat(nextBackend.branchId, deviceId, {
          organizationId: nextBackend.session.organizationId,
          seatId
        });
        if (hasPermission(nextBackend.session, permissionNames.viewDeviceDetail)) {
          const [detail, commands] = await Promise.all([
            apiClients.devices.getDeviceDetail(deviceId),
            hasPermission(nextBackend.session, permissionNames.viewDeviceCommandStatus)
              ? apiClients.devices.listDeviceCommands(deviceId, { limit: 25 }).catch(() => [])
              : Promise.resolve([])
          ]);
          setDeviceDetail(detail);
          setDeviceCommandHistory(Array.isArray(commands) ? commands : []);
        }
        await loadSettings(nextBackend);
      } else if (label === openDeviceCardActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.viewDeviceDetail)) {
          throw new Error(t('op.settings.devices.error.noPermViewDetail'));
        }

        const deviceId = deviceAssignmentDeviceId.trim();
        if (!isGuid(deviceId)) {
          throw new Error(t('op.settings.devices.error.selectDevice'));
        }

        const [detail, commands] = await Promise.all([
          apiClients.devices.getDeviceDetail(deviceId),
          hasPermission(nextBackend.session, permissionNames.viewDeviceCommandStatus)
            ? apiClients.devices.listDeviceCommands(deviceId, { limit: 25 }).catch(() => [])
            : Promise.resolve([])
        ]);
        setDeviceDetail(detail);
        setDeviceCommandHistory(Array.isArray(commands) ? commands : []);
      } else if (label === sendCommandActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.dispatchDeviceCommand)) {
          throw new Error(t('op.settings.devices.error.noPermSendCommand'));
        }

        const deviceId = deviceAssignmentDeviceId.trim();
        const type = deviceCommandType.trim();
        const reason = deviceCommandReason.trim();
        if (!isGuid(deviceId) || !type || !reason) {
          throw new Error(t('op.settings.devices.error.fillCommandFields'));
        }

        const command = await apiClients.devices.dispatchDeviceCommand(deviceId, {
          type,
          payload: {
            reason,
            source: 'operator-settings'
          }
        });
        setLastDeviceCommand(command);
        if (hasPermission(nextBackend.session, permissionNames.viewDeviceDetail)) {
          setDeviceInventory(await apiClients.devices.listDevices(nextBackend.branchId).catch(() => deviceInventory));
        }
        if (hasPermission(nextBackend.session, permissionNames.viewDeviceCommandStatus)) {
          const [selectedCommands, branchCommands] = await Promise.all([
            apiClients.devices.listDeviceCommands(deviceId, { limit: 25 }).catch(() => deviceCommandHistory),
            apiClients.devices.listBranchDeviceCommands(nextBackend.branchId, { limit: 50 }).catch(() => branchDeviceCommandHistory)
          ]);
          setDeviceCommandHistory(Array.isArray(selectedCommands) ? selectedCommands : []);
          setBranchDeviceCommandHistory(Array.isArray(branchCommands) ? branchCommands : []);
        }
      } else if (label === rotateKeyActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.rotateDeviceCredential)) {
          throw new Error(t('op.settings.devices.error.noPermRotateKey'));
        }

        const deviceId = deviceAssignmentDeviceId.trim();
        if (!isGuid(deviceId)) {
          throw new Error(t('op.settings.devices.error.selectDevice'));
        }

        const rotated = await apiClients.devices.rotateDeviceCredential(deviceId);
        setRotatedCredential(rotated);
        setCredentialIdToRevoke(readString(rotated, 'credentialId'));
        if (hasPermission(nextBackend.session, permissionNames.viewDeviceDetail)) {
          setDeviceInventory(await apiClients.devices.listDevices(nextBackend.branchId).catch(() => deviceInventory));
        }
      } else if (label === revokeKeyActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.revokeDeviceCredential)) {
          throw new Error(t('op.settings.devices.error.noPermRevokeKey'));
        }

        const deviceId = deviceAssignmentDeviceId.trim();
        const credentialId = credentialIdToRevoke.trim();
        if (!isGuid(deviceId) || !isGuid(credentialId)) {
          throw new Error(t('op.settings.devices.error.selectDeviceAndKey'));
        }

        await apiClients.devices.revokeDeviceCredential(deviceId, credentialId);
        setRotatedCredential(null);
        if (hasPermission(nextBackend.session, permissionNames.viewDeviceDetail)) {
          setDeviceInventory(await apiClients.devices.listDevices(nextBackend.branchId).catch(() => deviceInventory));
        }
      } else if (label === createZoneActionKey || label === updateZoneActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.manageLayout)) {
          throw new Error(t('op.settings.layout.error.noPermLayout'));
        }

        const name = layoutZoneName.trim();
        const sortOrder = Number(layoutZoneSortOrder);
        if (!name || !Number.isInteger(sortOrder)) {
          throw new Error(t('op.settings.layout.error.fillZone'));
        }
        if (label === updateZoneActionKey && !isGuid(selectedLayoutZoneId)) {
          throw new Error(t('op.settings.layout.error.selectZone'));
        }

        const zone = label === updateZoneActionKey
          ? await apiClients.settings.updateZone(nextBackend.branchId, selectedLayoutZoneId, {
            organizationId: nextBackend.session.organizationId,
            name,
            sortOrder
          })
          : await apiClients.settings.createZone(nextBackend.branchId, {
            organizationId: nextBackend.session.organizationId,
            name,
            sortOrder
          });
        const zoneId = readString(zone, 'zoneId', selectedLayoutZoneId);
        setSelectedLayoutZoneId(zoneId);
        setLayoutSeatZoneId(zoneId);
        if (label === createZoneActionKey) {
          setLayoutZoneName(`Zone ${zones.length + 2}`);
          setLayoutZoneSortOrder(String(sortOrder + 10));
        }
        await loadSettings(nextBackend);
      } else if (label === deleteZoneActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.manageLayout)) {
          throw new Error(t('op.settings.layout.error.noPermLayout'));
        }

        const zoneId = selectedLayoutZoneId.trim();
        if (!isGuid(zoneId)) {
          throw new Error(t('op.settings.layout.error.selectZoneDelete'));
        }

        await apiClients.settings.deleteZone(nextBackend.branchId, zoneId, nextBackend.session.organizationId);
        setSelectedLayoutZoneId('');
        setLayoutSeatZoneId('');
        await loadSettings(nextBackend);
      } else if (label === addSeatActionKey || label === updateSeatActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.manageLayout)) {
          throw new Error(t('op.settings.layout.error.noPermLayout'));
        }

        const zoneId = layoutSeatZoneId.trim();
        if (!zoneId) {
          throw new Error(t('op.settings.layout.error.noZoneForSeat'));
        }

        const name = layoutSeatName.trim();
        const sortOrder = Number(layoutSeatSortOrder);
        if (!name || !Number.isInteger(sortOrder)) {
          throw new Error(t('op.settings.layout.error.fillSeat'));
        }
        if (label === updateSeatActionKey && !isGuid(selectedLayoutSeatId)) {
          throw new Error(t('op.settings.layout.error.selectSeat'));
        }

        const seat = label === updateSeatActionKey
          ? await apiClients.settings.updateSeat(nextBackend.branchId, selectedLayoutSeatId, {
            organizationId: nextBackend.session.organizationId,
            zoneId,
            name,
            sortOrder
          })
          : await apiClients.settings.createSeat(nextBackend.branchId, {
            organizationId: nextBackend.session.organizationId,
            zoneId,
            name,
            sortOrder
          });
        setSelectedLayoutSeatId(readString(seat, 'seatId', selectedLayoutSeatId));
        if (label === addSeatActionKey) {
          setLayoutSeatName(`PC-${zones.reduce((sum, zone) => sum + readArray(zone, 'seats').length, 0) + 2}`);
          setLayoutSeatSortOrder(String(sortOrder + 10));
        }
        await loadSettings(nextBackend);
      } else if (label === deleteSeatActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.manageLayout)) {
          throw new Error(t('op.settings.layout.error.noPermLayout'));
        }

        const seatId = selectedLayoutSeatId.trim();
        if (!isGuid(seatId)) {
          throw new Error(t('op.settings.layout.error.selectSeatDelete'));
        }

        await apiClients.settings.deleteSeat(nextBackend.branchId, seatId, nextBackend.session.organizationId);
        setSelectedLayoutSeatId('');
        setDeviceAssignmentSeatId('');
        await loadSettings(nextBackend);
      } else if (label === createTariffActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.manageTariffs)) {
          throw new Error(t('op.settings.tariffs.error.noPerm'));
        }

        const name = tariffName.trim();
        const pricePerHourMinorUnits = parseMoneyInputMinorUnits(tariffPricePerHour);
        const minimumBillableMinutes = Number(tariffMinimumMinutes);
        const roundingIncrementMinutes = Number(tariffRoundingMinutes);
        if (!name || pricePerHourMinorUnits === null
          || !Number.isInteger(minimumBillableMinutes) || minimumBillableMinutes <= 0
          || !Number.isInteger(roundingIncrementMinutes) || roundingIncrementMinutes <= 0) {
          throw new Error(t('op.settings.tariffs.error.fillCreate'));
        }

        const tariff = await apiClients.settings.createTariff(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          name,
          idempotencyKey: createIdempotencyKey('tariff-create')
        });
        const tariffId = readString(tariff, 'tariffId');
        if (tariffId) {
          await apiClients.settings.createTariffVersion(nextBackend.branchId, tariffId, {
            organizationId: nextBackend.session.organizationId,
            tariffId,
            currencyCode,
            pricePerMinuteMinorUnits: Math.max(1, Math.round(pricePerHourMinorUnits / 60)),
            minimumBillableMinutes,
            roundingIncrementMinutes,
            effectiveFromUtc: new Date().toISOString(),
            idempotencyKey: createIdempotencyKey('tariff-version-create')
          });
        }
        setTariffName(`Тариф ${tariffs.length + 2}`); // default prefill
        setTariffEffectiveFromUtc(new Date().toISOString());
        await loadSettings(nextBackend);
      } else if (label === updateTariffActionKey || label === retireTariffActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.manageTariffs)) {
          throw new Error(t('op.settings.tariffs.error.noPerm'));
        }

        const tariffOption = tariffs.find((tariff) => readString(tariff, 'tariffVersionId') === selectedTariffVersionId);
        const tariffId = readString(tariffOption, 'tariffId');
        const tariffVersionId = readString(tariffOption, 'tariffVersionId');
        const name = tariffName.trim();
        const pricePerHourMinorUnits = parseMoneyInputMinorUnits(tariffPricePerHour);
        const minimumBillableMinutes = Number(tariffMinimumMinutes);
        const roundingIncrementMinutes = Number(tariffRoundingMinutes);
        if (!isGuid(tariffId) || !isGuid(tariffVersionId) || !name || pricePerHourMinorUnits === null
          || !Number.isInteger(minimumBillableMinutes) || minimumBillableMinutes <= 0
          || !Number.isInteger(roundingIncrementMinutes) || roundingIncrementMinutes <= 0) {
          throw new Error(t('op.settings.tariffs.error.fillUpdate'));
        }

        const isActive = label !== retireTariffActionKey;
        await apiClients.settings.updateTariff(nextBackend.branchId, tariffId, {
          organizationId: nextBackend.session.organizationId,
          name,
          isActive
        });
        await apiClients.settings.updateTariffVersion(nextBackend.branchId, tariffId, tariffVersionId, {
          organizationId: nextBackend.session.organizationId,
          currencyCode,
          pricePerMinuteMinorUnits: Math.max(1, Math.round(pricePerHourMinorUnits / 60)),
          minimumBillableMinutes,
          roundingIncrementMinutes,
          effectiveFromUtc: tariffEffectiveFromUtc,
          isActive
        });
        if (!isActive) {
          setSelectedTariffVersionId('');
        }
        await loadSettings(nextBackend);
      } else if (label === createPackageActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.managePackages)) {
          throw new Error(t('op.settings.packages.error.noPerm'));
        }

        const name = packageName.trim();
        const priceMinorUnits = parseMoneyInputMinorUnits(packagePrice);
        const includedMinutes = Number(packageMinutes);
        const bonusMinutes = Number(packageBonusMinutes);
        const expiresDays = Number(packageExpiresDays);
        if (!name || priceMinorUnits === null || !Number.isInteger(includedMinutes) || includedMinutes <= 0
          || !Number.isInteger(bonusMinutes) || bonusMinutes < 0
          || !Number.isInteger(expiresDays) || expiresDays <= 0) {
          throw new Error(t('op.settings.packages.error.fillCreate'));
        }

        await apiClients.settings.createPackageDefinition(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          name,
          price: { currencyCode, minorUnits: priceMinorUnits },
          includedSeconds: includedMinutes * 60,
          bonusSeconds: bonusMinutes * 60,
          expiresAfterDays: expiresDays,
          idempotencyKey: createIdempotencyKey('package-definition-create')
        });
        await loadSettings(nextBackend);
      } else if (label === updatePackageActionKey || label === retirePackageActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.managePackages)) {
          throw new Error(t('op.settings.packages.error.noPerm'));
        }

        const packageDefinitionId = selectedPackageDefinitionId.trim();
        const name = packageName.trim();
        const priceMinorUnits = parseMoneyInputMinorUnits(packagePrice);
        const includedMinutes = Number(packageMinutes);
        const bonusMinutes = Number(packageBonusMinutes);
        const expiresDays = Number(packageExpiresDays);
        if (!isGuid(packageDefinitionId) || !name || priceMinorUnits === null || !Number.isInteger(includedMinutes) || includedMinutes <= 0
          || !Number.isInteger(bonusMinutes) || bonusMinutes < 0
          || !Number.isInteger(expiresDays) || expiresDays <= 0) {
          throw new Error(t('op.settings.packages.error.fillUpdate'));
        }

        await apiClients.settings.updatePackageDefinition(nextBackend.branchId, packageDefinitionId, {
          organizationId: nextBackend.session.organizationId,
          name,
          price: { currencyCode, minorUnits: priceMinorUnits },
          includedSeconds: includedMinutes * 60,
          bonusSeconds: bonusMinutes * 60,
          expiresAfterDays: expiresDays,
          isActive: label !== retirePackageActionKey
        });
        if (label === retirePackageActionKey) {
          setSelectedPackageDefinitionId('');
        }
        await loadSettings(nextBackend);
      } else if (label === inviteStaffActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.manageBranchStaff)) {
          throw new Error(t('op.settings.staff.error.noPerm'));
        }

        const userName = inviteUserName.trim();
        const displayName = inviteDisplayName.trim();
        const email = inviteEmail.trim();
        if (!userName || !displayName || !email) {
          throw new Error(t('op.settings.staff.error.fillInvite'));
        }

        const invite = await apiClients.settings.createStaffInvite(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          userName,
          displayName,
          email,
          roleNames: [inviteRoleName || 'cashier_operator']
        });
        // The invitee appears in the staff list only after they accept and set a password.
        setInviteCode(invite.code);
        setInviteUserName(`operator${staffUsers.length + 2}`);
        setInviteDisplayName('Новый оператор'); // default prefill
        setInviteEmail('');
      } else if (label === updateStaffProfileActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.manageBranchStaff)) {
          throw new Error(t('op.settings.staff.error.noPerm'));
        }

        const staffUserId = selectedStaffUserId.trim();
        const userName = staffProfileUserName.trim();
        const displayName = staffProfileDisplayName.trim();
        if (!isGuid(staffUserId) || !userName || !displayName) {
          throw new Error(t('op.settings.staff.error.fillProfile'));
        }

        const staffUser = await apiClients.settings.updateStaffUserProfile(nextBackend.branchId, staffUserId, {
          organizationId: nextBackend.session.organizationId,
          userName,
          displayName
        });
        setStaffUsers((items) => items.map((item) => readString(item, 'staffUserId') === staffUserId ? staffUser : item));
        setSelectedStaffUserId(readString(staffUser, 'staffUserId'));
        setStaffProfileUserName(readString(staffUser, 'userName'));
        setStaffProfileDisplayName(operatorDisplayNameLabel(readString(staffUser, 'displayName')));
        setStaffRoleName(readArray<string>(staffUser, 'roleNames')[0] ?? staffRoleName);
      } else if (label === updateRoleActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.manageRoles)) {
          throw new Error(t('op.settings.staff.error.noPermRoles'));
        }

        const staffUserId = selectedStaffUserId.trim();
        const roleName = staffRoleName.trim();
        if (!isGuid(staffUserId) || !staffRoleOptions.includes(roleName as (typeof staffRoleOptions)[number])) {
          throw new Error(t('op.settings.staff.error.selectStaffAndRole'));
        }

        const staffUser = await apiClients.settings.updateStaffUserRoles(nextBackend.branchId, staffUserId, {
          organizationId: nextBackend.session.organizationId,
          roleNames: [roleName]
        });
        setStaffUsers((items) => items.map((item) => readString(item, 'staffUserId') === staffUserId ? staffUser : item));
        setSelectedStaffUserId(readString(staffUser, 'staffUserId'));
        setStaffRoleName(readArray<string>(staffUser, 'roleNames')[0] ?? roleName);
      } else if (label === disableStaffActionKey || label === enableStaffActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.manageBranchStaff)) {
          throw new Error(t('op.settings.staff.error.noPerm'));
        }

        const staffUserId = selectedStaffUserId.trim();
        if (!isGuid(staffUserId)) {
          throw new Error(t('op.settings.staff.error.selectStaff'));
        }

        const staffUser = await apiClients.settings.updateStaffUserState(nextBackend.branchId, staffUserId, {
          organizationId: nextBackend.session.organizationId,
          isActive: label === enableStaffActionKey
        });
        setStaffUsers((items) => items.map((item) => readString(item, 'staffUserId') === staffUserId ? staffUser : item));
        setSelectedStaffUserId(readString(staffUser, 'staffUserId'));
        setStaffRoleName(readArray<string>(staffUser, 'roleNames')[0] ?? staffRoleName);
      } else if (label === resetPasswordActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.manageBranchStaff)) {
          throw new Error(t('op.settings.staff.error.noPerm'));
        }

        const staffUserId = selectedStaffUserId.trim();
        const nextPassword = resetPassword.trim();
        if (!isGuid(staffUserId) || nextPassword.length < 8) {
          throw new Error(t('op.settings.staff.error.passwordTooShort'));
        }

        const staffUser = await apiClients.settings.resetStaffUserPassword(nextBackend.branchId, staffUserId, {
          organizationId: nextBackend.session.organizationId,
          newPassword: nextPassword
        });
        setStaffUsers((items) => items.map((item) => readString(item, 'staffUserId') === staffUserId ? staffUser : item));
        setSelectedStaffUserId(readString(staffUser, 'staffUserId'));
        setResetPassword('');
      } else if (label === createProductActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.managePosCatalog)) {
          throw new Error(t('op.settings.pos.error.noPerm'));
        }

        const categoryName = productCategoryName.trim();
        const nextProductName = productName.trim();
        const sku = productSku.trim();
        const priceMinorUnits = parseMoneyInputMinorUnits(productPrice);
        if (!categoryName || !nextProductName || !sku || priceMinorUnits === null) {
          throw new Error(t('op.settings.pos.error.fillCreate'));
        }

        const category = await apiClients.settings.createProductCategory(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          name: categoryName,
          idempotencyKey: createIdempotencyKey('pos-category-create')
        });
        const categoryId = readString(category, 'categoryId');
        if (!categoryId) {
          throw new Error(t('op.settings.pos.error.categoryNotConfirmed'));
        }

        const product = await apiClients.settings.createProduct(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          categoryId,
          name: nextProductName,
          sku,
          price: { currencyCode, minorUnits: priceMinorUnits },
          trackStock: productTrackStock,
          allowNegativeStock: productAllowNegativeStock,
          idempotencyKey: createIdempotencyKey('pos-product-create')
        });
        setCatalog((items) => [...items, product]);
        const nextIndex = catalog.length + 2;
        setProductCategoryName(`Категория ${nextIndex}`); // default prefill
        setProductName(`Товар ${nextIndex}`); // default prefill
        setProductSku(`SKU-${String(nextIndex).padStart(3, '0')}`);
        setProductPrice('12.00');
        setProductTrackStock(true);
        setProductAllowNegativeStock(false);
        setSelectedProductId(readString(product, 'productId'));
      } else if (label === updateProductActionKey || label === delistProductActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.managePosCatalog)) {
          throw new Error(t('op.settings.pos.error.noPerm'));
        }

        const selectedProduct = catalog.find((product) => readString(product, 'productId') === selectedProductId);
        const nextProductName = productName.trim();
        const sku = productSku.trim();
        const priceMinorUnits = parseMoneyInputMinorUnits(productPrice);
        if (!selectedProduct || !nextProductName || !sku || priceMinorUnits === null) {
          throw new Error(t('op.settings.pos.error.fillUpdate'));
        }

        await apiClients.settings.updateProduct(nextBackend.branchId, readString(selectedProduct, 'productId'), {
          organizationId: nextBackend.session.organizationId,
          categoryId: readString(selectedProduct, 'categoryId'),
          name: nextProductName,
          sku,
          price: { currencyCode, minorUnits: priceMinorUnits },
          trackStock: productTrackStock,
          allowNegativeStock: productAllowNegativeStock,
          isActive: label !== delistProductActionKey
        });
        if (label === delistProductActionKey) {
          setSelectedProductId('');
        }
        await loadSettings(nextBackend);
      } else if (label === recordMovementActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.manageInventoryStock)) {
          throw new Error(t('op.settings.stock.error.noPerm'));
        }

        const selectedProduct = trackedCatalog.find((product) => readString(product, 'productId') === stockProductId);
        const quantityDelta = Number(stockQuantityDelta);
        const unitCostMinorUnits = parseNonNegativeMoneyInputMinorUnits(stockUnitCost);
        const reason = stockReason.trim();
        if (!selectedProduct || !Number.isInteger(quantityDelta) || quantityDelta === 0 || unitCostMinorUnits === null || !reason) {
          throw new Error(t('op.settings.stock.error.fillFields'));
        }

        await apiClients.inventory.createStockMovement(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          productId: readString(selectedProduct, 'productId'),
          movementType: stockMovementType,
          quantityDelta,
          unitCost: { currencyCode, minorUnits: unitCostMinorUnits },
          reason,
          idempotencyKey: createIdempotencyKey('stock-movement-create')
        });
        await loadSettings(nextBackend);
      } else if (label === addUpdatePackageActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.manageUpdatePackages)) {
          throw new Error(t('op.settings.updates.error.noPerm'));
        }

        const component = updateComponent.trim();
        const version = updateVersion.trim();
        const channel = updateChannel.trim();
        const artifactUri = updateArtifactUri.trim();
        const sha256 = updateSha256.trim();
        const signature = updateSignature.trim();
        const signatureAlgorithm = updateSignatureAlgorithm.trim();
        const sizeKilobytes = Number(updateSizeKilobytes);
        const sizeBytes = sizeKilobytes * 1024;
        if (!component || !version || !channel || !artifactUri || !sha256 || !signature || !signatureAlgorithm
          || !Number.isInteger(sizeKilobytes) || sizeKilobytes <= 0) {
          throw new Error(t('op.settings.updates.error.fillFields'));
        }

        try {
          new URL(artifactUri);
        } catch {
          throw new Error(t('op.settings.updates.error.invalidUrl'));
        }

        const createdPackage = await apiClients.updates.registerPackage(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          component,
          version,
          channel,
          artifactUri,
          sha256,
          signature,
          signatureAlgorithm,
          sizeBytes,
          releaseNotes: updateReleaseNotes.trim()
        });
        const updatePackageId = readString(createdPackage, 'updatePackageId');
        if (!isGuid(updatePackageId)) {
          throw new Error(t('op.settings.updates.error.packageNotConfirmed'));
        }

        setRegisteredUpdatePackages((items) => [createdPackage, ...items.filter((item) => readString(item, 'updatePackageId') !== updatePackageId)]);
        setRolloutPackageId(updatePackageId);
        setPackageStatePackageId(updatePackageId);
      } else if (label === createRolloutActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.manageUpdateRollouts)) {
          throw new Error(t('op.settings.rollouts.error.noPermRollouts'));
        }

        const updatePackageId = rolloutPackageId.trim();
        const channel = rolloutChannel.trim();
        const targetKind = rolloutTargetKind.trim();
        const batchPercent = Number(rolloutBatchPercent);
        const startsAtText = rolloutStartsAtUtc.trim();
        const startsAt = new Date(startsAtText);
        const targetDeviceIds = targetKind === 'device'
          ? rolloutTargetDeviceIds.split(/[\s,;]+/).map((value) => value.trim()).filter(Boolean)
          : [];
        if (!isGuid(updatePackageId) || !channel || (targetKind !== 'branch' && targetKind !== 'device')
          || !Number.isInteger(batchPercent) || batchPercent < 1 || batchPercent > 100
          || Number.isNaN(startsAt.getTime()) || !rolloutReason.trim()
          || targetDeviceIds.some((deviceId) => !isGuid(deviceId))) {
          throw new Error(t('op.settings.rollouts.error.fillFields'));
        }

        const rollout = await apiClients.updates.createRollout(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          updatePackageId,
          channel,
          targetKind,
          targetDeviceIds,
          batchPercent,
          startsAtUtc: startsAt.toISOString(),
          reason: rolloutReason.trim()
        });
        const updateRolloutId = readString(rollout, 'updateRolloutId');
        if (updateRolloutId) {
          setRolloutStateRolloutId(updateRolloutId);
          setRollouts((items) => [rollout, ...items.filter((item) => readString(item, 'updateRolloutId') !== updateRolloutId)]);
        }
      } else if (label === changePackageStateActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.manageUpdatePackages)) {
          throw new Error(t('op.settings.packageState.error.noPermPackages'));
        }

        const updatePackageId = packageStatePackageId.trim();
        const state = packageState.trim();
        const reason = packageStateReason.trim();
        if (!isGuid(updatePackageId) || !state || !reason) {
          throw new Error(t('op.settings.packageState.error.fillPackageState'));
        }

        const updatePackage = await apiClients.updates.changePackageState(nextBackend.branchId, updatePackageId, {
          organizationId: nextBackend.session.organizationId,
          state,
          reason
        });
        setRegisteredUpdatePackages((items) => [updatePackage, ...items.filter((item) => readString(item, 'updatePackageId') !== updatePackageId)]);
      } else if (label === changeRolloutStateActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.manageUpdateRollouts)) {
          throw new Error(t('op.settings.rollouts.error.noPermRollouts'));
        }

        const updateRolloutId = rolloutStateRolloutId.trim();
        const state = rolloutState.trim();
        const reason = rolloutStateReason.trim();
        if (!isGuid(updateRolloutId) || !state || !reason) {
          throw new Error(t('op.settings.packageState.error.fillRolloutState'));
        }

        const rollout = await apiClients.updates.changeRolloutState(nextBackend.branchId, updateRolloutId, {
          organizationId: nextBackend.session.organizationId,
          state,
          reason
        });
        setRollouts((items) => items.map((item) => readString(item, 'updateRolloutId') === updateRolloutId ? rollout : item));
      } else {
        throw new Error(t('op.settings.generic.error.notConnected'));
      }

      setFeedback({ label, state: 'confirmed' });
    } catch (error) {
      setFeedback({ label, state: 'failed', detail: projectOperatorError(error).detail });
    }
  };

  const saveSettings = async () => {
    if (!clubName.trim() || !city.trim()) {
      triggerFeedback(setFeedback, t('op.settings.profile.feedbackLabel'), 'failed', t('op.settings.profile.errorRequiredFields'));
      return;
    }

    setFeedback({ label: t('op.settings.profile.feedbackLabel'), state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const branchProfile: BranchProfileDto = await apiClients.settings.updateBranchProfile(nextBackend.branchId, {
        organizationId: nextBackend.session.organizationId,
        name: clubName.trim(),
        city: city.trim()
      });
      setClubName(readString(branchProfile, 'name', clubName.trim()));
      setCity(readString(branchProfile, 'city', city.trim()));
      setSettingsDirty(false);
      setFeedback({ label: t('op.settings.profile.feedbackLabel'), state: 'confirmed' });
    } catch (error) {
      setFeedback({ label: t('op.settings.profile.feedbackLabel'), state: 'failed', detail: projectOperatorError(error).detail });
    }
  };

  const renderSettingsContent = () => {
    if (selectedSection === sectionLayoutKey) {
      return (
        <>
          <div className="settings-section-title">
            <span>{t('op.settings.layout.title')}</span>
            <div className="settings-section-actions">
              <button type="button" disabled={!canManageLayout} onClick={() => runSettingsAction(createZoneActionKey)}>{t('op.settings.layout.createZoneBtn')}</button>
              <button type="button" disabled={!canManageLayout || !selectedLayoutZoneId} onClick={() => runSettingsAction(updateZoneActionKey)}>{updateZoneActionKey}</button>
              <button
                type="button"
                disabled={!canManageLayout || !selectedLayoutZoneId}
                onClick={() => {
                  setFeedback(emptyFeedback);
                  setCriticalAction('layout-zone-delete');
                }}
              >
                {deleteZoneActionKey}
              </button>
            </div>
          </div>
          <div className="settings-form-grid settings-layout-form">
            <label>{t('op.settings.layout.zoneName')}<input value={layoutZoneName} disabled={!canManageLayout} onChange={(event) => setLayoutZoneName(event.currentTarget.value)} /></label>
            <label>{t('op.settings.layout.zoneOrder')}<input inputMode="numeric" value={layoutZoneSortOrder} disabled={!canManageLayout} onChange={(event) => setLayoutZoneSortOrder(event.currentTarget.value)} /></label>
            <label>{t('op.settings.layout.seatZone')}
              <select value={layoutSeatZoneId} disabled={!canManageLayout || zones.length === 0} onChange={(event) => setLayoutSeatZoneId(event.currentTarget.value)}>
                {zones.length === 0 && <option value="">{t('op.settings.layout.noZones')}</option>}
                {zones.map((zone) => (
                  <option key={readString(zone, 'zoneId')} value={readString(zone, 'zoneId')}>{readString(zone, 'name', t('op.settings.layout.zoneFallback'))}</option>
                ))}
              </select>
            </label>
            <label>{t('op.settings.layout.seatName')}<input value={layoutSeatName} disabled={!canManageLayout} onChange={(event) => setLayoutSeatName(event.currentTarget.value)} /></label>
            <label>{t('op.settings.layout.seatOrder')}<input inputMode="numeric" value={layoutSeatSortOrder} disabled={!canManageLayout} onChange={(event) => setLayoutSeatSortOrder(event.currentTarget.value)} /></label>
            <button type="button" disabled={!canManageLayout || !layoutSeatZoneId} onClick={() => runSettingsAction(addSeatActionKey)}>{t('op.settings.layout.addSeatBtn')}</button>
            <button type="button" disabled={!canManageLayout || !selectedLayoutSeatId || !layoutSeatZoneId} onClick={() => runSettingsAction(updateSeatActionKey)}>{updateSeatActionKey}</button>
            <button
              type="button"
              disabled={!canManageLayout || !selectedLayoutSeatId}
              onClick={() => {
                setFeedback(emptyFeedback);
                setCriticalAction('layout-seat-delete');
              }}
            >
              {deleteSeatActionKey}
            </button>
          </div>
          {criticalAction === 'layout-zone-delete' && (
            <CriticalActionConfirmation
              title={t('op.settings.layout.confirmDeleteZone.title')}
              detail={`${readString(selectedLayoutZone, 'name', layoutZoneName || t('op.settings.layout.zoneFallback'))} · ${t('op.settings.layout.seatCount', { count: readArray(selectedLayoutZone, 'seats').length })}`}
              impact={t('op.settings.layout.confirmDeleteZone.impact')}
              confirmLabel={t('op.settings.layout.confirmDeleteZone.confirm')}
              disabled={feedback.state === 'pending'}
              onCancel={() => setCriticalAction(null)}
              onConfirm={() => void runSettingsAction(deleteZoneActionKey)}
            />
          )}
          {criticalAction === 'layout-seat-delete' && (
            <CriticalActionConfirmation
              title={t('op.settings.layout.confirmDeleteSeat.title')}
              detail={readString(selectedLayoutSeat, 'name', layoutSeatName || t('op.settings.layout.seatFallback'))}
              impact={t('op.settings.layout.confirmDeleteSeat.impact')}
              confirmLabel={t('op.settings.layout.confirmDeleteSeat.confirm')}
              disabled={feedback.state === 'pending'}
              onCancel={() => setCriticalAction(null)}
              onConfirm={() => void runSettingsAction(deleteSeatActionKey)}
            />
          )}
          <div className="settings-room-grid">
            {zones.map((zone) => (
              <button key={readString(zone, 'zoneId')} type="button" className={`settings-room-card ${readString(zone, 'zoneId') === selectedLayoutZoneId ? 'active' : ''}`} onClick={() => selectLayoutZone(zone)}>
                <strong>{readString(zone, 'name', t('op.settings.layout.zoneFallback'))}</strong>
                <b>{t('op.settings.layout.seatCount', { count: readArray(zone, 'seats').length })}</b>
                <span>{t('op.settings.layout.sortOrder', { order: readNumber(zone, 'sortOrder', 0) })}</span>
              </button>
            ))}
          </div>
          <div className="settings-tariff-list">
            {zones.flatMap((zone) => readArray<Record<string, unknown>>(zone, 'seats').map((seat) => (
              <button key={readString(seat, 'seatId')} type="button" className={`settings-tariff-row ${readString(seat, 'seatId') === selectedLayoutSeatId ? 'active' : ''}`} onClick={() => selectLayoutSeat(zone, seat)}>
                <strong>{readString(seat, 'name', t('op.settings.layout.seatFallback'))}</strong>
                <b>{readString(zone, 'name', t('op.settings.layout.zoneFallback'))}</b>
                <span>{t('op.settings.layout.sortOrder', { order: readNumber(seat, 'sortOrder', 0) })}</span>
              </button>
            )))}
          </div>
          <div className="settings-section-title">
            <span>{t('op.settings.devices.title')}</span>
            <button type="button" disabled={!canCreateDeviceEnrollmentCode} onClick={() => runSettingsAction(createEnrollmentCodeActionKey)}>{createEnrollmentCodeActionKey}</button>
            <button type="button" disabled={!canAssignDeviceSeat || layoutSeatOptions.length === 0} onClick={() => runSettingsAction(assignDeviceActionKey)}>{assignDeviceActionKey}</button>
            <button type="button" disabled={!canViewDeviceCommandStatus} onClick={() => runSettingsAction(refreshCommandHistoryActionKey)}>{refreshCommandHistoryActionKey}</button>
          </div>
          <div className="settings-form-grid settings-device-form">
            <label>{t('op.settings.devices.enrollmentMinutes')}<input inputMode="numeric" value={enrollmentExpiresMinutes} disabled={!canCreateDeviceEnrollmentCode} onChange={(event) => setEnrollmentExpiresMinutes(event.currentTarget.value)} /></label>
            <label>{t('op.settings.devices.enrollmentCode')}<input value={readString(enrollmentCode, 'code', '—')} readOnly /></label>
            <label>{t('op.settings.devices.device')}
              <select value={deviceAssignmentDeviceId} disabled={deviceOptions.length === 0 || (!canAssignDeviceSeat && !canViewDeviceDetail)} onChange={(event) => setDeviceAssignmentDeviceId(event.currentTarget.value)}>
                {deviceOptions.length === 0 && <option value="">{t('op.settings.devices.noDevices')}</option>}
                {deviceAssignmentDeviceId && !deviceOptions.some((device) => device.id === deviceAssignmentDeviceId) && (
                  <option value={deviceAssignmentDeviceId}>{t('op.settings.devices.selectedDevice')}</option>
                )}
                {deviceOptions.map((device) => (
                  <option key={device.id} value={device.id}>{device.label}</option>
                ))}
              </select>
            </label>
            <label>{t('op.settings.devices.seat')}
              <select value={deviceAssignmentSeatId} disabled={!canAssignDeviceSeat || layoutSeatOptions.length === 0} onChange={(event) => setDeviceAssignmentSeatId(event.currentTarget.value)}>
                {layoutSeatOptions.length === 0 && <option value="">{t('op.settings.devices.noSeats')}</option>}
                {layoutSeatOptions.map((seat) => (
                  <option key={seat.seatId} value={seat.seatId}>{seat.label}</option>
                ))}
              </select>
            </label>
            <label>{t('op.settings.devices.deviceCard')}<input value={deviceDetail ? `${readString(deviceDetail, 'machineName', t('op.settings.devices.deviceFallback'))} · ${readString(deviceDetail, 'seatName', t('op.settings.devices.seatFallback'))}` : t('op.settings.devices.deviceCardNotOpen')} readOnly /></label>
            <button type="button" disabled={!canViewDeviceDetail || !isGuid(deviceAssignmentDeviceId)} onClick={() => runSettingsAction(openDeviceCardActionKey)}>{openDeviceCardActionKey}</button>
            <label>{t('op.settings.devices.commandType')}
              <select value={deviceCommandType} disabled={!canDispatchDeviceCommand} onChange={(event) => setDeviceCommandType(event.currentTarget.value)}>
                <option value="lock">{t('op.settings.devices.commandLock')}</option>
                <option value="unlock">{t('op.settings.devices.commandUnlock')}</option>
              </select>
            </label>
            <label>{t('op.settings.devices.commandReason')}<input value={deviceCommandReason} disabled={!canDispatchDeviceCommand} onChange={(event) => setDeviceCommandReason(event.currentTarget.value)} /></label>
            <label>{t('op.settings.devices.lastCommand')}<input value={lastDeviceCommand ? `${commandTypeLabel(readString(lastDeviceCommand, 'type', 'command'))} · ${t('op.settings.devices.lastCommandSent')}` : t('op.settings.devices.noCommand')} readOnly /></label>
            <button type="button" disabled={!canDispatchDeviceCommand || !isGuid(deviceAssignmentDeviceId)} onClick={() => runSettingsAction(sendCommandActionKey)}>{sendCommandActionKey}</button>
            <label>{t('op.settings.devices.credentialToRevoke')}<input value={rotatedCredentialLabel} readOnly /></label>
            <label>{t('op.settings.devices.newCredential')}<input value={rotatedCredentialId ? t('op.settings.devices.newCredentialCreated') : t('op.settings.devices.newCredentialEmpty')} readOnly /></label>
            <label className="settings-form-wide">{t('op.settings.devices.enrollmentSecret')}<input value={readString(rotatedCredential, 'credentialSecret', '—')} readOnly /></label>
            <button type="button" disabled={!canRotateDeviceCredential || !isGuid(deviceAssignmentDeviceId)} onClick={() => runSettingsAction(rotateKeyActionKey)}>{rotateKeyActionKey}</button>
            <button
              type="button"
              disabled={!canRevokeDeviceCredential || !isGuid(deviceAssignmentDeviceId) || !isGuid(credentialIdToRevoke) || feedback.state === 'pending'}
              onClick={() => {
                setFeedback(emptyFeedback);
                setCriticalAction('credential-revoke');
              }}
            >
              {revokeKeyActionKey}
            </button>
          </div>
          {criticalAction === 'credential-revoke' && (
            <CriticalActionConfirmation
              title={t('op.settings.devices.confirmRevokeKey.title')}
              detail={t('op.settings.devices.confirmRevokeKey.detail', { deviceName: selectedDeviceLabel })}
              impact={t('op.settings.devices.confirmRevokeKey.impact')}
              confirmLabel={t('op.settings.devices.confirmRevokeKey.confirm')}
              disabled={feedback.state === 'pending'}
              onCancel={() => setCriticalAction(null)}
              onConfirm={() => void runSettingsAction(revokeKeyActionKey)}
            />
          )}
          <div className="settings-device-inventory" aria-label={t('op.settings.devices.ariaInventory')}>
            {deviceInventory.map((device) => {
              const pendingCommands = readNumber(device, 'pendingCommandCount', 0);
              const failedCommands = readNumber(device, 'failedCommandCount', 0);
              return (
                <button
                  key={readString(device, 'deviceId')}
                  type="button"
                  aria-label={readString(device, 'machineName', t('op.settings.devices.deviceFallback'))}
                  className={`settings-device-row ${readString(device, 'deviceId') === deviceAssignmentDeviceId ? 'active' : ''}${failedCommands > 0 ? ' attention' : ''}`}
                  disabled={!canViewDeviceDetail}
                  onClick={() => selectDeviceInventoryItem(device)}
                >
                  <strong>{readString(device, 'machineName', t('op.settings.devices.deviceFallback'))}</strong>
                  <b>{readBoolean(device, 'isOnline') ? t('op.settings.devices.online') : t('op.settings.devices.offline')} · {readBoolean(device, 'isLocked') ? t('op.settings.devices.locked') : t('op.settings.devices.unlocked')}</b>
                  <span>{readString(device, 'zoneName', t('op.settings.devices.zoneFallback'))} · {readString(device, 'seatName', t('op.settings.devices.seatFallback'))}</span>
                  <em>{t('op.settings.devices.deviceSummary', { agentVersion: readString(device, 'agentVersion', '—'), appCount: readNumber(device, 'installedAppCount', 0), pending: pendingCommands, failed: failedCommands, lastHeartbeat: formatTime(readString(device, 'lastHeartbeatAtUtc')) })}</em>
                </button>
              );
            })}
            {deviceInventory.length === 0 && (
              <span className="settings-device-empty">{t('op.settings.devices.emptyInventory')}</span>
            )}
          </div>
          {deviceDetail && (
            <div className="settings-device-detail-grid">
              <span><strong>{t('op.settings.devices.detail.device')}</strong><b>{readString(deviceDetail, 'machineName', t('op.settings.devices.deviceFallback'))}</b></span>
              <span><strong>{t('op.settings.devices.detail.status')}</strong><b>{readBoolean(deviceDetail, 'isOnline') ? t('op.settings.devices.online') : t('op.settings.devices.offline')} · {readBoolean(deviceDetail, 'isLocked') ? t('op.settings.devices.locked') : t('op.settings.devices.unlocked')}</b></span>
              <span><strong>{t('op.settings.devices.detail.seat')}</strong><b>{readString(deviceDetail, 'zoneName', t('op.settings.devices.zoneFallback'))} · {readString(deviceDetail, 'seatName', t('op.settings.devices.seatFallback'))}</b></span>
              <span><strong>{t('op.settings.devices.detail.heartbeat')}</strong><b>{formatTime(readString(deviceDetail, 'lastHeartbeatAtUtc'))}</b></span>
              <span><strong>{t('op.settings.devices.detail.agent')}</strong><b>{readString(deviceDetail, 'agentVersion', '—')}</b></span>
              <span><strong>{t('op.settings.devices.detail.shell')}</strong><b>{readString(deviceDetail, 'shellVersion', '—')}</b></span>
              <span><strong>{t('op.settings.devices.detail.credentials')}</strong><b>{readNumber(deviceDetail, 'activeCredentialCount', 0)}</b></span>
              <span><strong>{t('op.settings.devices.detail.apps')}</strong><b>{readNumber(deviceDetail, 'installedAppCount', 0)}</b></span>
            </div>
          )}
          {deviceRecentCommands.length > 0 && (
            <div className="settings-command-history">
              {deviceRecentCommands.map((command) => (
                <span key={readString(command, 'commandId')}>
                  <strong>{commandTypeLabel(readString(command, 'type', 'command'))}</strong>
                  <b>{commandStatusLabel(readString(command, 'status', 'unknown'))}</b>
                  <em>{commandStatusMessageLabel(readString(command, 'message')) || formatTime(readString(command, 'updatedAtUtc'))}</em>
                </span>
              ))}
            </div>
          )}
          {branchDeviceCommandHistory.length > 0 && (
            <>
              <div className="settings-section-title">
                <span>{t('op.settings.devices.branchHistory')}</span>
                <strong>{t('op.settings.devices.branchHistoryCount', { count: branchDeviceCommandHistory.length })}</strong>
              </div>
              <div className="settings-command-history" aria-label={t('op.settings.devices.branchHistory')}>
                {branchDeviceCommandHistory.map((command) => {
                  const deviceId = readString(command, 'deviceId');
                  return (
                    <span key={`${deviceId}-${readString(command, 'commandId')}`}>
                      <strong>{getDeviceInventoryName(deviceId)}</strong>
                      <b>{commandTypeLabel(readString(command, 'type', 'command'))} · {commandStatusLabel(readString(command, 'status', 'unknown'))}</b>
                      <em>{commandStatusMessageLabel(readString(command, 'message')) || formatTime(readString(command, 'updatedAtUtc'))}</em>
                    </span>
                  );
                })}
              </div>
            </>
          )}
        </>
      );
    }

    if (selectedSection === sectionTariffsKey) {
      return (
        <>
          <div className="settings-section-title">
            <span>{t('op.settings.tariffs.title')}</span>
            <div className="settings-section-actions">
              <button type="button" disabled={!canManageTariffs} onClick={() => runSettingsAction(createTariffActionKey)}>{createTariffActionKey}</button>
              <button type="button" disabled={!canManageTariffs || !selectedTariffVersionId} onClick={() => runSettingsAction(updateTariffActionKey)}>{updateTariffActionKey}</button>
              <button type="button" disabled={!canManageTariffs || !selectedTariffVersionId} onClick={() => runSettingsAction(retireTariffActionKey)}>{retireTariffActionKey}</button>
            </div>
          </div>
          <div className="settings-form-grid settings-tariff-form">
            <label>{t('op.settings.tariffs.name')}<input value={tariffName} disabled={!canManageTariffs} onChange={(event) => setTariffName(event.currentTarget.value)} /></label>
            <label>{t('op.settings.tariffs.pricePerHour')}<input inputMode="decimal" value={tariffPricePerHour} disabled={!canManageTariffs} onChange={(event) => setTariffPricePerHour(event.currentTarget.value)} /></label>
            <label>{t('op.settings.tariffs.minMinutes')}<input inputMode="numeric" value={tariffMinimumMinutes} disabled={!canManageTariffs} onChange={(event) => setTariffMinimumMinutes(event.currentTarget.value)} /></label>
            <label>{t('op.settings.tariffs.rounding')}<input inputMode="numeric" value={tariffRoundingMinutes} disabled={!canManageTariffs} onChange={(event) => setTariffRoundingMinutes(event.currentTarget.value)} /></label>
          </div>
          <div className="settings-tariff-list">
            {tariffs.map((tariff) => (
              <button key={readString(tariff, 'tariffVersionId')} type="button" className={`settings-tariff-row ${readString(tariff, 'tariffVersionId') === selectedTariffVersionId ? 'active' : ''}`} onClick={() => selectTariffOption(tariff)}>
                <strong>{readString(tariff, 'name', t('op.settings.tariffs.tariffFallback'))}</strong>
                <b>{formatMinorUnits(readNumber(tariff, 'pricePerMinuteMinorUnits', 0) * 60, readString(tariff, 'currencyCode', currencyCode))} {t('op.settings.tariffs.perHour')}</b>
                <span>{t('op.settings.tariffs.minLabel', { min: readNumber(tariff, 'minimumBillableMinutes', 0), step: readNumber(tariff, 'roundingIncrementMinutes', 0), state: readBoolean(tariff, 'isActive', true) ? t('op.settings.tariffs.active') : t('op.settings.tariffs.inactive') })}</span>
              </button>
            ))}
          </div>
          <div className="settings-section-title">
            <span>{t('op.settings.packages.title')}</span>
            <div className="settings-section-actions">
              <button type="button" disabled={!canManagePackages} onClick={() => runSettingsAction(createPackageActionKey)}>{createPackageActionKey}</button>
              <button type="button" disabled={!canManagePackages || !selectedPackageDefinitionId} onClick={() => runSettingsAction(updatePackageActionKey)}>{updatePackageActionKey}</button>
              <button type="button" disabled={!canManagePackages || !selectedPackageDefinitionId} onClick={() => runSettingsAction(retirePackageActionKey)}>{retirePackageActionKey}</button>
            </div>
          </div>
          <div className="settings-tariff-list">
            {packageOptions.map((option) => (
              <button key={readString(option, 'packageDefinitionId')} type="button" className={`settings-tariff-row ${readString(option, 'packageDefinitionId') === selectedPackageDefinitionId ? 'active' : ''}`} onClick={() => selectPackageOption(option)}>
                <strong>{readString(option, 'name', t('op.settings.packages.packageFallback'))}</strong>
                <b>{formatMinorUnits(readNumber(option, 'priceMinorUnits', 0), readString(option, 'currencyCode', currencyCode))}</b>
                <span>{t('op.settings.packages.minutesDetail', { minutes: Math.round(readNumber(option, 'includedSeconds', 0) / 60), bonus: Math.round(readNumber(option, 'bonusSeconds', 0) / 60), days: readNumber(option, 'expiresAfterDays', 0) })}</span>
              </button>
            ))}
          </div>
          <div className="settings-form-grid settings-package-form">
            <label>{t('op.settings.packages.name')}<input value={packageName} disabled={!canManagePackages} onChange={(event) => setPackageName(event.currentTarget.value)} /></label>
            <label>{t('op.settings.packages.price')}<input inputMode="decimal" value={packagePrice} disabled={!canManagePackages} onChange={(event) => setPackagePrice(event.currentTarget.value)} /></label>
            <label>{t('op.settings.packages.minutes')}<input inputMode="numeric" value={packageMinutes} disabled={!canManagePackages} onChange={(event) => setPackageMinutes(event.currentTarget.value)} /></label>
            <label>{t('op.settings.packages.bonusMinutes')}<input inputMode="numeric" value={packageBonusMinutes} disabled={!canManagePackages} onChange={(event) => setPackageBonusMinutes(event.currentTarget.value)} /></label>
            <label>{t('op.settings.packages.expiresDays')}<input inputMode="numeric" value={packageExpiresDays} disabled={!canManagePackages} onChange={(event) => setPackageExpiresDays(event.currentTarget.value)} /></label>
          </div>
        </>
      );
    }

    if (selectedSection === sectionStaffKey) {
      return (
        <>
          <div className="settings-section-title">
            <span>{t('op.settings.staff.title')}</span>
            <div className="settings-section-actions">
              <button type="button" disabled={!canManageBranchStaff} onClick={() => runSettingsAction(inviteStaffActionKey)}>{inviteStaffActionKey}</button>
              <button type="button" disabled={!canManageBranchStaff || !selectedStaffUserId} onClick={() => runSettingsAction(selectedStaffIsActive ? disableStaffActionKey : enableStaffActionKey)}>
                {selectedStaffIsActive ? disableStaffActionKey : enableStaffActionKey}
              </button>
              <button type="button" disabled={!canManageBranchStaff || !selectedStaffUserId} onClick={() => runSettingsAction(resetPasswordActionKey)}>{resetPasswordActionKey}</button>
            </div>
          </div>
          <div className="settings-staff-layout">
            <div className="settings-config-grid">
              {staffUsers.map((user) => (
                <button
                  key={readString(user, 'staffUserId')}
                  type="button"
                  className={readString(user, 'staffUserId') === selectedStaffUserId ? 'active' : ''}
                  onClick={() => {
                    const roleName = readArray<string>(user, 'roleNames').find((role) => staffRoleOptions.includes(role as (typeof staffRoleOptions)[number]));
                    setSelectedStaffUserId(readString(user, 'staffUserId'));
                    setStaffProfileUserName(readString(user, 'userName'));
                    setStaffProfileDisplayName(operatorDisplayNameLabel(readString(user, 'displayName')));
                    setStaffRoleName(roleName ?? 'cashier_operator');
                    triggerFeedback(setFeedback, operatorDisplayNameLabel(readString(user, 'displayName', t('op.settings.staff.staffFallback'))), 'confirmed');
                  }}
                >
                  <strong>{operatorDisplayNameLabel(readString(user, 'displayName', t('op.settings.staff.staffFallback')))}</strong>
                  <span>{readString(user, 'userName', t('op.settings.staff.userFallback'))} · {readArray<string>(user, 'roleNames').map(staffRoleLabel).join(', ') || t('op.settings.staff.roleEmpty')} · {readBoolean(user, 'isActive', true) ? t('op.settings.staff.active') : t('op.settings.staff.inactive')}</span>
                </button>
              ))}
            </div>
            <div className="settings-form-grid settings-staff-form">
              <label>{t('op.settings.staff.loginLabel')}<input value={inviteUserName} disabled={!canManageBranchStaff} onChange={(event) => setInviteUserName(event.currentTarget.value)} /></label>
              <label>{t('op.settings.staff.displayName')}<input value={inviteDisplayName} disabled={!canManageBranchStaff} onChange={(event) => setInviteDisplayName(event.currentTarget.value)} /></label>
              <label>{t('op.settings.staff.email')}<input type="email" value={inviteEmail} disabled={!canManageBranchStaff} onChange={(event) => setInviteEmail(event.currentTarget.value)} /></label>
              {inviteCode && <label>{t('op.settings.staff.inviteCode')}<input readOnly value={inviteCode} onFocus={(event) => event.currentTarget.select()} /></label>}
              <label>{t('op.settings.staff.roleAccess')}
                <select value={inviteRoleName} disabled={!canManageBranchStaff} onChange={(event) => setInviteRoleName(event.currentTarget.value)}>
                  {staffRoleOptions.map((roleName) => <option key={roleName} value={roleName}>{staffRoleLabel(roleName)}</option>)}
                </select>
              </label>
              <label>{t('op.settings.staff.profileLogin')}<input value={staffProfileUserName} disabled={!canManageBranchStaff || !selectedStaffUserId} onChange={(event) => setStaffProfileUserName(event.currentTarget.value)} /></label>
              <label>{t('op.settings.staff.profileName')}<input value={staffProfileDisplayName} disabled={!canManageBranchStaff || !selectedStaffUserId} onChange={(event) => setStaffProfileDisplayName(event.currentTarget.value)} /></label>
              <button type="button" disabled={!canManageBranchStaff || !selectedStaffUserId} onClick={() => runSettingsAction(updateStaffProfileActionKey)}>{t('op.settings.staff.updateProfileBtn')}</button>
              <label>{t('op.settings.staff.newRole')}
                <select value={staffRoleName} disabled={!canManageRoles || !selectedStaffUserId} onChange={(event) => setStaffRoleName(event.currentTarget.value)}>
                  {staffRoleOptions.map((roleName) => <option key={roleName} value={roleName}>{staffRoleLabel(roleName)}</option>)}
                </select>
              </label>
              <label>{t('op.settings.staff.newPassword')}<input type="password" value={resetPassword} disabled={!canManageBranchStaff || !selectedStaffUserId} onChange={(event) => setResetPassword(event.currentTarget.value)} /></label>
              <button type="button" disabled={!canManageRoles || !selectedStaffUserId} onClick={() => runSettingsAction(updateRoleActionKey)}>{updateRoleActionKey}</button>
            </div>
          </div>
        </>
      );
    }

    if (selectedSection === sectionGoodsKey) {
      return (
        <>
          <div className="settings-section-title">
            <span>{t('op.settings.pos.title')}</span>
            <div className="settings-section-actions">
              <button type="button" disabled={!canManagePosCatalog} onClick={() => runSettingsAction(createProductActionKey)}>{createProductActionKey}</button>
              <button type="button" disabled={!canManagePosCatalog || !selectedProductId} onClick={() => runSettingsAction(updateProductActionKey)}>{updateProductActionKey}</button>
              <button type="button" disabled={!canManagePosCatalog || !selectedProductId} onClick={() => runSettingsAction(delistProductActionKey)}>{delistProductActionKey}</button>
            </div>
          </div>
          <div className="settings-config-grid">
            {catalog.slice(0, 8).map((product) => (
              <button key={readString(product, 'productId')} type="button" className={readString(product, 'productId') === selectedProductId ? 'active' : undefined} onClick={() => selectCatalogProduct(product)}>
                <strong>{readString(product, 'name', t('op.settings.pos.productFallback'))}</strong>
                <span>{formatMoney(readMoney(product, 'price'), currencyCode)} · {t('op.settings.pos.stockOnHand', { count: readNumber(product, 'stockOnHand', 0) })}</span>
              </button>
            ))}
          </div>
          <div className="settings-form-grid settings-pos-form">
            <label>{t('op.settings.pos.category')}<input value={productCategoryName} disabled={!canManagePosCatalog} onChange={(event) => setProductCategoryName(event.currentTarget.value)} /></label>
            <label>{t('op.settings.pos.productName')}<input value={productName} disabled={!canManagePosCatalog} onChange={(event) => setProductName(event.currentTarget.value)} /></label>
            <label>{t('op.settings.pos.sku')}<input value={productSku} disabled={!canManagePosCatalog} onChange={(event) => setProductSku(event.currentTarget.value)} /></label>
            <label>{t('op.settings.pos.price')}<input inputMode="decimal" value={productPrice} disabled={!canManagePosCatalog} onChange={(event) => setProductPrice(event.currentTarget.value)} /></label>
            <label>{t('op.settings.pos.trackStock')}
              <select value={productTrackStock ? 'yes' : 'no'} disabled={!canManagePosCatalog} onChange={(event) => setProductTrackStock(event.currentTarget.value === 'yes')}>
                <option value="yes">{t('op.settings.pos.yes')}</option>
                <option value="no">{t('op.settings.pos.no')}</option>
              </select>
            </label>
            <label>{t('op.settings.pos.allowNegative')}
              <select value={productAllowNegativeStock ? 'yes' : 'no'} disabled={!canManagePosCatalog} onChange={(event) => setProductAllowNegativeStock(event.currentTarget.value === 'yes')}>
                <option value="no">{t('op.settings.pos.no')}</option>
                <option value="yes">{t('op.settings.pos.yes')}</option>
              </select>
            </label>
          </div>
          <div className="settings-section-title">
            <span>{t('op.settings.stock.title')}</span>
            <button type="button" disabled={!canManageInventoryStock || trackedCatalog.length === 0} onClick={() => runSettingsAction(recordMovementActionKey)}>{t('op.settings.stock.recordBtn')}</button>
          </div>
          <div className="settings-form-grid settings-stock-form">
            <label>{t('op.settings.stock.product')}
              <select value={stockProductId} disabled={!canManageInventoryStock || trackedCatalog.length === 0} onChange={(event) => setStockProductId(event.currentTarget.value)}>
                {trackedCatalog.length === 0 && <option value="">{t('op.settings.stock.noTrackedProducts')}</option>}
                {trackedCatalog.map((product) => (
                  <option key={readString(product, 'productId')} value={readString(product, 'productId')}>{readString(product, 'name', t('op.settings.pos.productFallback'))} · {t('op.settings.pos.stockOnHand', { count: readNumber(product, 'stockOnHand', 0) })}</option>
                ))}
              </select>
            </label>
            <label>{t('op.settings.stock.type')}
              <select value={stockMovementType} disabled={!canManageInventoryStock} onChange={(event) => setStockMovementType(event.currentTarget.value)}>
                <option value="purchase">{t('op.settings.stock.typePurchase')}</option>
                <option value="adjustment">{t('op.settings.stock.typeAdjustment')}</option>
              </select>
            </label>
            <label>{t('op.settings.stock.quantity')}<input inputMode="numeric" value={stockQuantityDelta} disabled={!canManageInventoryStock} onChange={(event) => setStockQuantityDelta(event.currentTarget.value)} /></label>
            <label>{t('op.settings.stock.unitCost')}<input inputMode="decimal" value={stockUnitCost} disabled={!canManageInventoryStock} onChange={(event) => setStockUnitCost(event.currentTarget.value)} /></label>
            <label>{t('op.settings.stock.reason')}<input value={stockReason} disabled={!canManageInventoryStock} onChange={(event) => setStockReason(event.currentTarget.value)} /></label>
          </div>
          <div className="settings-section-title">
            <span>{t('op.settings.stock.history.title')}</span>
            <strong>{t('op.settings.stock.history.count', { count: stockMovements.length })}</strong>
          </div>
          <div className="settings-config-grid settings-stock-history">
            {stockMovements.length === 0 && (
              <button type="button" disabled>
                <strong>{t('op.settings.stock.history.empty')}</strong>
                <span>{t('op.settings.stock.history.emptyDetail')}</span>
              </button>
            )}
            {stockMovements.map((movement) => {
              const productId = readString(movement, 'productId');
              const productName = readString(
                catalog.find((product) => readString(product, 'productId') === productId),
                'name',
                t('op.settings.pos.productFallback'));
              const quantityDelta = readNumber(movement, 'quantityDelta', 0);
              const reason = readString(movement, 'reason', t('op.settings.stock.reasonFallback'));
              return (
                <button key={readString(movement, 'stockMovementId')} type="button" onClick={() => triggerFeedback(setFeedback, productName, 'confirmed')}>
                  <strong>{productName} · {stockMovementTypeLabel(readString(movement, 'movementType'))}</strong>
                  <span>{quantityDelta > 0 ? '+' : ''}{quantityDelta} · {formatMoney(readMoney(movement, 'unitCost'), currencyCode)} · {reason}</span>
                </button>
              );
            })}
          </div>
        </>
      );
    }

    if (selectedSection === sectionIntegrationsKey) {
      return (
        <>
          <div className="settings-config-grid">
            {[
              [t('op.settings.integrations.payments'), t('op.settings.integrations.payments.detail')],
              [t('op.settings.integrations.updates'), t('op.settings.integrations.rolloutCount', { count: rollouts.length })],
              [t('op.settings.integrations.errors'), t('op.settings.integrations.errors.detail', { count: readNumber(updateSummary, 'failedDevices', 0) })],
              [t('op.settings.integrations.connection'), backend ? t('op.settings.integrations.connectionOk') : t('op.settings.integrations.connectionLocal')]
            ].map(([name, detail]) => (
              <button key={name} type="button" onClick={() => triggerFeedback(setFeedback, name, 'confirmed')}>
                <strong>{name}</strong>
                <span>{detail}</span>
              </button>
            ))}
          </div>

          <div className="settings-section-title">
            <span>{t('op.settings.updates.packagesTitle')}</span>
            <button type="button" disabled={!canManageUpdatePackages} onClick={() => runSettingsAction(addUpdatePackageActionKey)}>{t('op.settings.updates.addBtn')}</button>
          </div>
          <div className="settings-form-grid settings-update-form">
            <label>{t('op.settings.updates.component')}
              <select value={updateComponent} disabled={!canManageUpdatePackages} onChange={(event) => setUpdateComponent(event.currentTarget.value)}>
                <option value="operator-app">{updateComponentLabel('operator-app')}</option>
                <option value="agent-service">{updateComponentLabel('agent-service')}</option>
                <option value="player-shell">{updateComponentLabel('player-shell')}</option>
              </select>
            </label>
            <label>{t('op.settings.updates.version')}<input value={updateVersion} disabled={!canManageUpdatePackages} onChange={(event) => setUpdateVersion(event.currentTarget.value)} /></label>
            <label>{t('op.settings.updates.channel')}
              <select value={updateChannel} disabled={!canManageUpdatePackages} onChange={(event) => setUpdateChannel(event.currentTarget.value)}>
                <option value="internal">{updateChannelLabel('internal')}</option>
                <option value="beta">{updateChannelLabel('beta')}</option>
                <option value="stable">{updateChannelLabel('stable')}</option>
              </select>
            </label>
            <label className="settings-form-wide">{t('op.settings.updates.installerFile')}<input value={updateArtifactUri} disabled={!canManageUpdatePackages} onChange={(event) => setUpdateArtifactUri(event.currentTarget.value)} /></label>
            <label className="settings-form-wide">{t('op.settings.updates.checksum')}<input value={updateSha256} disabled={!canManageUpdatePackages} onChange={(event) => setUpdateSha256(event.currentTarget.value)} /></label>
            <label>{t('op.settings.updates.signature')}<input value={updateSignature} disabled={!canManageUpdatePackages} onChange={(event) => setUpdateSignature(event.currentTarget.value)} /></label>
            <label>{t('op.settings.updates.signatureAlgorithm')}<input value={updateSignatureAlgorithm} disabled={!canManageUpdatePackages} onChange={(event) => setUpdateSignatureAlgorithm(event.currentTarget.value)} /></label>
            <label>{t('op.settings.updates.fileSizeKb')}<input inputMode="numeric" value={updateSizeKilobytes} disabled={!canManageUpdatePackages} onChange={(event) => setUpdateSizeKilobytes(event.currentTarget.value)} /></label>
            <label className="settings-form-wide">{t('op.settings.updates.releaseNotes')}<input value={updateReleaseNotes} disabled={!canManageUpdatePackages} onChange={(event) => setUpdateReleaseNotes(event.currentTarget.value)} /></label>
          </div>

          <div className="settings-section-title">
            <span>{t('op.settings.rollouts.title')}</span>
            <button type="button" disabled={!canManageUpdateRollouts} onClick={() => runSettingsAction(createRolloutActionKey)}>{t('op.settings.rollouts.createBtn')}</button>
          </div>
          <div className="settings-tariff-list">
            {rollouts.map((rollout) => (
              <button
                key={readString(rollout, 'updateRolloutId')}
                type="button"
                className={`settings-tariff-row ${readString(rollout, 'updateRolloutId') === readString(selectedRollout, 'updateRolloutId') ? 'active' : ''}`}
                onClick={() => {
                  setRolloutStateRolloutId(readString(rollout, 'updateRolloutId'));
                  setRolloutPackageId(readString(rollout, 'updatePackageId'));
                  setPackageStatePackageId(readString(rollout, 'updatePackageId'));
                }}
              >
                <strong>{updateComponentLabel(readString(rollout, 'component'))} {readString(rollout, 'version', t('op.settings.updates.versionFallback'))}</strong>
                <b>{updateRolloutStateLabel(readString(rollout, 'state'))}</b>
                <span>{t('op.settings.rollouts.rolloutTargetDetail', { targetKind: updateTargetKindLabel(readString(rollout, 'targetKind')), batchPercent: readNumber(rollout, 'batchPercent', 0), deviceCount: readArray(rollout, 'deviceStatuses').length })}</span>
              </button>
            ))}
          </div>
          {selectedRollout && (
            <div className="settings-device-detail-grid">
              <span><strong>{t('op.settings.rollouts.detail.rollout')}</strong><b>{updateComponentLabel(readString(selectedRollout, 'component'))} {readString(selectedRollout, 'version', t('op.settings.updates.versionFallback'))}</b></span>
              <span><strong>{t('op.settings.rollouts.detail.state')}</strong><b>{updateRolloutStateLabel(readString(selectedRollout, 'state'))}</b></span>
              <span><strong>{t('op.settings.rollouts.detail.target')}</strong><b>{updateTargetKindLabel(readString(selectedRollout, 'targetKind'))} · {readNumber(selectedRollout, 'batchPercent', 0)}%</b></span>
              <span><strong>{t('op.settings.rollouts.detail.channel')}</strong><b>{updateChannelLabel(readString(selectedRollout, 'channel'))}</b></span>
              <span><strong>{t('op.settings.rollouts.detail.package')}</strong><b>{updateComponentLabel(readString(selectedRollout, 'component'))} {readString(selectedRollout, 'version', t('op.settings.updates.versionFallback'))}</b></span>
              <span><strong>{t('op.settings.rollouts.detail.starts')}</strong><b>{formatTime(readString(selectedRollout, 'startsAtUtc'))}</b></span>
              <span><strong>{t('op.settings.rollouts.detail.completed')}</strong><b>{formatTime(readString(selectedRollout, 'completedAtUtc'))}</b></span>
              <span><strong>{t('op.settings.rollouts.detail.devices')}</strong><b>{selectedRolloutDeviceStatuses.length}</b></span>
            </div>
          )}
          {selectedRolloutDeviceStatuses.length > 0 && (
            <div className="settings-command-history">
              {selectedRolloutDeviceStatuses.map((status) => (
                <span key={`${readString(status, 'deviceId')}-${readString(status, 'updatedAtUtc')}`}>
                  <strong>{getDeviceInventoryName(readString(status, 'deviceId'))}</strong>
                  <b>{updateDeviceStatusLabel(readString(status, 'status', 'unknown'))}</b>
                  <em>{updateDeviceMessageLabel(readString(status, 'message')) || t('op.settings.rollouts.detail.versionArrow', { from: readString(status, 'installedVersion'), to: readString(status, 'targetVersion') })}</em>
                </span>
              ))}
            </div>
          )}
          <div className="settings-form-grid settings-update-form">
            <label className="settings-form-wide">{t('op.settings.rollouts.packageForRollout')}
              <select value={rolloutPackageId} disabled={!canManageUpdateRollouts || updatePackageOptions.length === 0} onChange={(event) => setRolloutPackageId(event.currentTarget.value)}>
                {updatePackageOptions.length === 0 && <option value="">{t('op.settings.rollouts.noPackages')}</option>}
                {rolloutPackageId && !updatePackageOptions.some((option) => option.id === rolloutPackageId) && (
                  <option value={rolloutPackageId}>{t('op.settings.rollouts.selectedPackage')}</option>
                )}
                {updatePackageOptions.map((option) => (
                  <option key={option.id} value={option.id}>{option.label}</option>
                ))}
              </select>
            </label>
            <label>{t('op.settings.rollouts.channel')}
              <select value={rolloutChannel} disabled={!canManageUpdateRollouts} onChange={(event) => setRolloutChannel(event.currentTarget.value)}>
                <option value="internal">{updateChannelLabel('internal')}</option>
                <option value="beta">{updateChannelLabel('beta')}</option>
                <option value="stable">{updateChannelLabel('stable')}</option>
              </select>
            </label>
            <label>{t('op.settings.rollouts.target')}
              <select value={rolloutTargetKind} disabled={!canManageUpdateRollouts} onChange={(event) => setRolloutTargetKind(event.currentTarget.value)}>
                <option value="branch">{updateTargetKindLabel('branch')}</option>
                <option value="device">{updateTargetKindLabel('device')}</option>
              </select>
            </label>
            <label>{t('op.settings.rollouts.batchPercent')}<input inputMode="numeric" value={rolloutBatchPercent} disabled={!canManageUpdateRollouts} onChange={(event) => setRolloutBatchPercent(event.currentTarget.value)} /></label>
            <label className="settings-form-wide">{t('op.settings.rollouts.targetDevices')}
              <select multiple value={Array.from(rolloutTargetDeviceIdSet)} disabled={!canManageUpdateRollouts || rolloutTargetKind !== 'device' || deviceOptions.length === 0} onChange={(event) => setRolloutTargetDeviceIds(Array.from(event.currentTarget.selectedOptions).map((option) => option.value).join(','))}>
                {deviceOptions.length === 0 && <option value="">{t('op.settings.rollouts.noDevices')}</option>}
                {deviceOptions.map((device) => (
                  <option key={device.id} value={device.id}>{device.label}</option>
                ))}
              </select>
            </label>
            <label className="settings-form-wide">{t('op.settings.rollouts.startsAt')}<input value={rolloutStartsAtUtc} disabled={!canManageUpdateRollouts} onChange={(event) => setRolloutStartsAtUtc(event.currentTarget.value)} /></label>
            <label className="settings-form-wide">{t('op.settings.rollouts.rolloutReason')}<input value={rolloutReason} disabled={!canManageUpdateRollouts} onChange={(event) => setRolloutReason(event.currentTarget.value)} /></label>
          </div>

          <div className="settings-section-title">
            <span>{t('op.settings.packageState.title')}</span>
            <button
              type="button"
              disabled={!canManageUpdatePackages}
              onClick={() => {
                setFeedback(emptyFeedback);
                setCriticalAction('package-state-change');
              }}
            >
              {changePackageStateActionKey}
            </button>
            <button
              type="button"
              disabled={!canManageUpdateRollouts}
              onClick={() => {
                setFeedback(emptyFeedback);
                setCriticalAction('rollout-state-change');
              }}
            >
              {changeRolloutStateActionKey}
            </button>
          </div>
          <div className="settings-form-grid settings-update-form">
            <label className="settings-form-wide">{t('op.settings.packageState.packageLabel')}
              <select value={packageStatePackageId} disabled={!canManageUpdatePackages || updatePackageOptions.length === 0} onChange={(event) => setPackageStatePackageId(event.currentTarget.value)}>
                {updatePackageOptions.length === 0 && <option value="">{t('op.settings.packageState.noPackages')}</option>}
                {packageStatePackageId && !updatePackageOptions.some((option) => option.id === packageStatePackageId) && (
                  <option value={packageStatePackageId}>{t('op.settings.packageState.selectedPackage')}</option>
                )}
                {updatePackageOptions.map((option) => (
                  <option key={option.id} value={option.id}>{option.label}</option>
                ))}
              </select>
            </label>
            <label>{t('op.settings.packageState.stateLabel')}
              <select value={packageState} disabled={!canManageUpdatePackages} onChange={(event) => setPackageState(event.currentTarget.value)}>
                <option value="registered">{updatePackageStateLabel('registered')}</option>
                <option value="validated">{updatePackageStateLabel('validated')}</option>
                <option value="rejected">{updatePackageStateLabel('rejected')}</option>
                <option value="retired">{updatePackageStateLabel('retired')}</option>
              </select>
            </label>
            <label>{t('op.settings.packageState.reasonLabel')}<input value={packageStateReason} disabled={!canManageUpdatePackages} onChange={(event) => setPackageStateReason(event.currentTarget.value)} /></label>
            <label className="settings-form-wide">{t('op.settings.packageState.rolloutLabel')}
              <select value={rolloutStateRolloutId} disabled={!canManageUpdateRollouts || rolloutOptions.length === 0} onChange={(event) => setRolloutStateRolloutId(event.currentTarget.value)}>
                {rolloutOptions.length === 0 && <option value="">{t('op.settings.rollouts.noRollouts')}</option>}
                {rolloutStateRolloutId && !rolloutOptions.some((option) => option.id === rolloutStateRolloutId) && (
                  <option value={rolloutStateRolloutId}>{t('op.settings.rollouts.selectedRollout')}</option>
                )}
                {rolloutOptions.map((option) => (
                  <option key={option.id} value={option.id}>{option.label}</option>
                ))}
              </select>
            </label>
            <label>{t('op.settings.packageState.rolloutStateLabel')}
              <select value={rolloutState} disabled={!canManageUpdateRollouts} onChange={(event) => setRolloutState(event.currentTarget.value)}>
                <option value="active">{updateRolloutStateLabel('active')}</option>
                <option value="paused">{updateRolloutStateLabel('paused')}</option>
                <option value="completed">{updateRolloutStateLabel('completed')}</option>
                <option value="rollback_requested">{updateRolloutStateLabel('rollback_requested')}</option>
                <option value="rolled_back">{updateRolloutStateLabel('rolled_back')}</option>
                <option value="cancelled">{updateRolloutStateLabel('cancelled')}</option>
              </select>
            </label>
            <label>{t('op.settings.packageState.rolloutReasonLabel')}<input value={rolloutStateReason} disabled={!canManageUpdateRollouts} onChange={(event) => setRolloutStateReason(event.currentTarget.value)} /></label>
          </div>
          {criticalAction === 'package-state-change' && (
            <CriticalActionConfirmation
              title={t('op.settings.packageState.confirmPackage.title')}
              detail={`${updatePackageOptions.find((option) => option.id === packageStatePackageId)?.label ?? t('op.settings.packageState.confirmPackage.packageFallback')} · ${updatePackageStateLabel(packageState)}`}
              impact={t('op.settings.packageState.confirmPackage.impact', { reason: packageStateReason.trim() || t('op.settings.packageState.confirmPackage.impactNoReason') })}
              confirmLabel={t('op.settings.packageState.confirmPackage.confirm')}
              disabled={feedback.state === 'pending'}
              onCancel={() => setCriticalAction(null)}
              onConfirm={() => void runSettingsAction(changePackageStateActionKey)}
            />
          )}
          {criticalAction === 'rollout-state-change' && (
            <CriticalActionConfirmation
              title={t('op.settings.packageState.confirmRollout.title')}
              detail={`${rolloutOptions.find((option) => option.id === rolloutStateRolloutId)?.label ?? t('op.settings.packageState.confirmRollout.rolloutFallback')} · ${updateRolloutStateLabel(rolloutState)}`}
              impact={t('op.settings.packageState.confirmRollout.impact', { reason: rolloutStateReason.trim() || t('op.settings.packageState.confirmRollout.impactNoReason') })}
              confirmLabel={t('op.settings.packageState.confirmRollout.confirm')}
              disabled={feedback.state === 'pending'}
              onCancel={() => setCriticalAction(null)}
              onConfirm={() => void runSettingsAction(changeRolloutStateActionKey)}
            />
          )}
        </>
      );
    }

    return (
      <>
        <div className="settings-form-grid">
          <label>{t('op.settings.profile.clubName')}<input value={clubName} onChange={(event) => { setClubName(event.currentTarget.value); setSettingsDirty(true); }} /></label>
          <label>{t('op.settings.profile.city')}<input value={city} onChange={(event) => { setCity(event.currentTarget.value); setSettingsDirty(true); }} /></label>
          <label>{t('op.settings.profile.currency')}<input value={currencyCode} readOnly /></label>
          <label>{t('op.settings.profile.branch')}<input value={backend ? t('op.settings.profile.branchCurrent') : t('op.settings.profile.branchLocal')} readOnly /></label>
        </div>
        <div className="settings-save-row">
          <span>{settingsDirty ? t('op.settings.profile.dirty') : t('op.settings.profile.clean')}</span>
          <button type="button" onClick={saveSettings}>{t('common.save')}</button>
        </div>
      </>
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
          <span className={`map-load-state ${loadStatus === 'backend' ? 'ready' : loadStatus}`}>{workspaceLoadStatusLabel(loadStatus, t('op.settings.profile.loadFeedbackLabel'))}</span>
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
          <FeedbackNotice feedback={feedback} />
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
              {actions.map(([label, detail, Icon]) => (
                <button key={label} type="button" className="settings-action-card" onClick={() => runSettingsAction(label)}>
                  <Icon size={17} />
                  <strong>{label}</strong>
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

