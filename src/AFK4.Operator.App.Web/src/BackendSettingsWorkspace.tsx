import { useEffect, useState } from 'react';
import { CircleDollarSign, MonitorCheck, UserRoundPlus, Wifi, Wrench } from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
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
  const [selectedSection, setSelectedSection] = useState('Профиль клуба');
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
  const [deviceCommandReason, setDeviceCommandReason] = useState('Проверка оператором');
  const [lastDeviceCommand, setLastDeviceCommand] = useState<Record<string, unknown> | null>(null);
  const [layoutZoneName, setLayoutZoneName] = useState('Основной зал');
  const [layoutZoneSortOrder, setLayoutZoneSortOrder] = useState('10');
  const [layoutSeatZoneId, setLayoutSeatZoneId] = useState('');
  const [layoutSeatName, setLayoutSeatName] = useState('PC-01');
  const [layoutSeatSortOrder, setLayoutSeatSortOrder] = useState('10');
  const [selectedLayoutZoneId, setSelectedLayoutZoneId] = useState('');
  const [selectedLayoutSeatId, setSelectedLayoutSeatId] = useState('');
  const [inviteUserName, setInviteUserName] = useState('operator');
  const [inviteDisplayName, setInviteDisplayName] = useState('Новый оператор');
  const [inviteEmail, setInviteEmail] = useState('');
  const [inviteCode, setInviteCode] = useState<string | null>(null);
  const [resetPassword, setResetPassword] = useState('');
  const [inviteRoleName, setInviteRoleName] = useState('cashier_operator');
  const [selectedStaffUserId, setSelectedStaffUserId] = useState('');
  const [staffProfileUserName, setStaffProfileUserName] = useState('');
  const [staffProfileDisplayName, setStaffProfileDisplayName] = useState('');
  const [staffRoleName, setStaffRoleName] = useState('cashier_operator');
  const [productCategoryName, setProductCategoryName] = useState('Категория 1');
  const [productName, setProductName] = useState('Товар 1');
  const [productSku, setProductSku] = useState('SKU-001');
  const [productPrice, setProductPrice] = useState('12.00');
  const [productTrackStock, setProductTrackStock] = useState(true);
  const [productAllowNegativeStock, setProductAllowNegativeStock] = useState(false);
  const [selectedProductId, setSelectedProductId] = useState('');
  const [stockProductId, setStockProductId] = useState('');
  const [stockMovementType, setStockMovementType] = useState('purchase');
  const [stockQuantityDelta, setStockQuantityDelta] = useState('10');
  const [stockUnitCost, setStockUnitCost] = useState('0.00');
  const [stockReason, setStockReason] = useState('Первичное поступление');
  const [tariffName, setTariffName] = useState('Дневной тариф');
  const [tariffPricePerHour, setTariffPricePerHour] = useState('90.00');
  const [tariffMinimumMinutes, setTariffMinimumMinutes] = useState('15');
  const [tariffRoundingMinutes, setTariffRoundingMinutes] = useState('5');
  const [tariffEffectiveFromUtc, setTariffEffectiveFromUtc] = useState(() => new Date().toISOString());
  const [selectedTariffVersionId, setSelectedTariffVersionId] = useState('');
  const [packageName, setPackageName] = useState('Ночной пакет 5ч');
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
  const [updateReleaseNotes, setUpdateReleaseNotes] = useState('Пакет обновления приложения оператора.');
  const [rolloutPackageId, setRolloutPackageId] = useState('');
  const [rolloutChannel, setRolloutChannel] = useState('internal');
  const [rolloutTargetKind, setRolloutTargetKind] = useState('branch');
  const [rolloutTargetDeviceIds, setRolloutTargetDeviceIds] = useState('');
  const [rolloutBatchPercent, setRolloutBatchPercent] = useState('100');
  const [rolloutStartsAtUtc, setRolloutStartsAtUtc] = useState(() => new Date(Date.now() + 60 * 60 * 1000).toISOString());
  const [rolloutReason, setRolloutReason] = useState('Публикация обновления оператором.');
  const [packageStatePackageId, setPackageStatePackageId] = useState('');
  const [packageState, setPackageState] = useState('validated');
  const [packageStateReason, setPackageStateReason] = useState('Подпись проверена.');
  const [rolloutStateRolloutId, setRolloutStateRolloutId] = useState('');
  const [rolloutState, setRolloutState] = useState('paused');
  const [rolloutStateReason, setRolloutStateReason] = useState('Изменение состояния оператором.');

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
      setFeedback({ label: 'Настройки', state: 'failed', detail: projectOperatorError(error).detail });
    }
  };

  useEffect(() => {
    void loadSettings();
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, currencyCode]);

  useEffect(() => {
    setCriticalAction(null);
  }, [deviceAssignmentDeviceId]);

  const sections = [
    ['Профиль клуба', 'название, город, валюта'],
    ['Залы и ПК', 'зоны, рабочие места, статусы'],
    ['Тарифы', 'пакеты, постоплата, VIP'],
    ['Персонал', 'операторы, роли, доступы'],
    ['Товары и склад', 'товары, остатки, чеки'],
    ['Интеграции', 'платежи, обновления, экспорт']
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
    label: `${readString(zone, 'name', 'Зал')} · ${readString(seat, 'name', 'ПК')}`
  }))).filter((seat) => isGuid(seat.seatId));
  const trackedCatalog = catalog.filter((product) => readBoolean(product, 'trackStock'));
  const deviceRecentCommands = deviceCommandHistory.length > 0
    ? deviceCommandHistory
    : readArray<Record<string, unknown>>(deviceDetail, 'recentCommands');
  const getDeviceInventoryName = (deviceId: string) =>
    readString(deviceInventory.find((device) => readString(device, 'deviceId') === deviceId), 'machineName', 'Устройство');
  const selectedRollout = rollouts.find((rollout) => readString(rollout, 'updateRolloutId') === rolloutStateRolloutId) ?? rollouts[0] ?? null;
  const selectedRolloutDeviceStatuses = readArray<Record<string, unknown>>(selectedRollout, 'deviceStatuses');
  const deviceOptions = deviceInventory
    .map((device) => ({
      id: readString(device, 'deviceId'),
      label: `${readString(device, 'machineName', 'Устройство')} · ${readString(device, 'zoneName', 'без зала')} · ${readString(device, 'seatName', 'без места')}`
    }))
    .filter((device) => isGuid(device.id));
  const selectedDeviceLabel = getDeviceInventoryName(deviceAssignmentDeviceId);
  const updatePackageOptions = Array.from(new Map([
    ...rollouts.map((rollout) => ({
      id: readString(rollout, 'updatePackageId'),
      label: `${updateComponentLabel(readString(rollout, 'component'))} ${readString(rollout, 'version', 'версия')} · ${updateChannelLabel(readString(rollout, 'channel'))}`
    })),
    ...registeredUpdatePackages.map((updatePackage) => ({
      id: readString(updatePackage, 'updatePackageId'),
      label: `${updateComponentLabel(readString(updatePackage, 'component'))} ${readString(updatePackage, 'version', 'версия')} · ${updateChannelLabel(readString(updatePackage, 'channel'))} · ${updatePackageStateLabel(readString(updatePackage, 'state', 'registered'))}`
    }))
  ].filter((option) => isGuid(option.id)).map((option) => [option.id, option])).values());
  const rolloutOptions = rollouts
    .map((rollout) => ({
      id: readString(rollout, 'updateRolloutId'),
      label: `${updateComponentLabel(readString(rollout, 'component'))} ${readString(rollout, 'version', 'версия')} · ${updateRolloutStateLabel(readString(rollout, 'state'))}`
    }))
    .filter((rollout) => isGuid(rollout.id));
  const rolloutTargetDeviceIdSet = new Set(rolloutTargetDeviceIds.split(/[\s,;]+/).map((value) => value.trim()).filter(Boolean));
  const rotatedCredentialId = readString(rotatedCredential, 'credentialId');
  const rotatedCredentialLabel = rotatedCredentialId ? `готов к отзыву для ${selectedDeviceLabel}` : 'сначала смените ключ';
  const selectLayoutZone = (zone: Record<string, unknown>) => {
    const zoneId = readString(zone, 'zoneId');
    setSelectedLayoutZoneId(zoneId);
    setLayoutSeatZoneId(zoneId);
    setLayoutZoneName(readString(zone, 'name', layoutZoneName));
    setLayoutZoneSortOrder(String(readNumber(zone, 'sortOrder', Number(layoutZoneSortOrder))));
    triggerFeedback(setFeedback, readString(zone, 'name', 'Зал'), 'confirmed');
  };
  const selectLayoutSeat = (zone: Record<string, unknown>, seat: Record<string, unknown>) => {
    setSelectedLayoutSeatId(readString(seat, 'seatId'));
    setLayoutSeatZoneId(readString(zone, 'zoneId'));
    setLayoutSeatName(readString(seat, 'name', layoutSeatName));
    setLayoutSeatSortOrder(String(readNumber(seat, 'sortOrder', Number(layoutSeatSortOrder))));
    triggerFeedback(setFeedback, readString(seat, 'name', 'ПК'), 'confirmed');
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
    triggerFeedback(setFeedback, readString(product, 'name', 'Товар'), 'confirmed');
  };
  const selectTariffOption = (option: TariffOptionDto) => {
    setSelectedTariffVersionId(readString(option, 'tariffVersionId'));
    setTariffName(readString(option, 'name', tariffName));
    setTariffPricePerHour(formatMoneyInputMinorUnits(readNumber(option, 'pricePerMinuteMinorUnits', 0) * 60));
    setTariffMinimumMinutes(String(readNumber(option, 'minimumBillableMinutes', Number(tariffMinimumMinutes))));
    setTariffRoundingMinutes(String(readNumber(option, 'roundingIncrementMinutes', Number(tariffRoundingMinutes))));
    setTariffEffectiveFromUtc(readString(option, 'effectiveFromUtc', new Date().toISOString()));
    triggerFeedback(setFeedback, readString(option, 'name', 'Тариф'), 'confirmed');
  };
  const selectPackageOption = (option: PackageOptionDto) => {
    setSelectedPackageDefinitionId(readString(option, 'packageDefinitionId'));
    setPackageName(readString(option, 'name', packageName));
    setPackagePrice(formatMoneyInputMinorUnits(readNumber(option, 'priceMinorUnits', 0)));
    setPackageMinutes(String(Math.round(readNumber(option, 'includedSeconds', 0) / 60)));
    setPackageBonusMinutes(String(Math.round(readNumber(option, 'bonusSeconds', 0) / 60)));
    setPackageExpiresDays(String(readNumber(option, 'expiresAfterDays', 30)));
    triggerFeedback(setFeedback, readString(option, 'name', 'Пакет'), 'confirmed');
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
    triggerFeedback(setFeedback, readString(device, 'machineName', 'Устройство'), 'confirmed');
  };
  const readiness = [
    ['Профиль клуба', `${clubName} · ${city}`],
    ['Залы и ПК', `${zones.reduce((sum, zone) => sum + readArray(zone, 'seats').length, 0)} рабочих мест`],
    ['Персонал', `${staffUsers.length} сотрудников`],
    ['Касса', `${currencyCode} · отчёты платформы`],
    ['Устройства', `${readNumber(deviceSummary, 'onlineDevices', 0)} из ${readNumber(deviceSummary, 'totalDevices', 0)} онлайн`]
  ];
  const actions: Array<[string, string, LucideIcon]> = [
    ['Добавить ПК', 'новое рабочее место', MonitorCheck],
    ['Создать тариф', 'цена и правила списания', CircleDollarSign],
    ['Пригласить сотрудника', canManageBranchStaff ? 'создать учётную запись' : 'нет прав доступа', UserRoundPlus],
    ['Обновить профиль сотрудника', canManageBranchStaff ? 'редактировать логин и имя' : 'нет прав доступа', Wrench],
    ['Проверить устройства', 'обновить диагностику', Wifi]
  ];

  const runSettingsAction = async (label: string) => {
    setCriticalAction(null);
    setFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      if (label === 'Проверить устройства') {
        setDiagnostics(await apiClients.diagnostics.getDiagnostics(nextBackend.branchId));
      } else if (label === 'Обновить историю команд') {
        if (!hasPermission(nextBackend.session, permissionNames.viewDeviceCommandStatus)) {
          throw new Error('Нет прав на просмотр истории команд устройств.');
        }

        const commands = await apiClients.devices.listBranchDeviceCommands(nextBackend.branchId, { limit: 50 });
        setBranchDeviceCommandHistory(Array.isArray(commands) ? commands : []);
      } else if (label === 'Создать код подключения') {
        if (!hasPermission(nextBackend.session, permissionNames.createDeviceEnrollmentCode)) {
          throw new Error('Нет прав на создание кода подключения устройства.');
        }

        const expiresInMinutes = Number(enrollmentExpiresMinutes);
        if (!Number.isInteger(expiresInMinutes) || expiresInMinutes < 1 || expiresInMinutes > 1440) {
          throw new Error('Срок действия кода должен быть от 1 минуты до 24 часов.');
        }

        const expiresInSeconds = expiresInMinutes * 60;
        const code = await apiClients.devices.createEnrollmentCode(nextBackend.branchId, nextBackend.session.organizationId, expiresInSeconds);
        setEnrollmentCode(code);
      } else if (label === 'Назначить устройство') {
        if (!hasPermission(nextBackend.session, permissionNames.assignDeviceSeat)) {
          throw new Error('Нет прав на назначение устройства рабочему месту.');
        }

        const deviceId = deviceAssignmentDeviceId.trim();
        const seatId = deviceAssignmentSeatId.trim();
        if (!isGuid(deviceId) || !isGuid(seatId)) {
          throw new Error('Выберите устройство и рабочее место.');
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
      } else if (label === 'Открыть карточку устройства') {
        if (!hasPermission(nextBackend.session, permissionNames.viewDeviceDetail)) {
          throw new Error('Нет прав на просмотр карточки устройства.');
        }

        const deviceId = deviceAssignmentDeviceId.trim();
        if (!isGuid(deviceId)) {
          throw new Error('Выберите устройство.');
        }

        const [detail, commands] = await Promise.all([
          apiClients.devices.getDeviceDetail(deviceId),
          hasPermission(nextBackend.session, permissionNames.viewDeviceCommandStatus)
            ? apiClients.devices.listDeviceCommands(deviceId, { limit: 25 }).catch(() => [])
            : Promise.resolve([])
        ]);
        setDeviceDetail(detail);
        setDeviceCommandHistory(Array.isArray(commands) ? commands : []);
      } else if (label === 'Отправить команду') {
        if (!hasPermission(nextBackend.session, permissionNames.dispatchDeviceCommand)) {
          throw new Error('Нет прав на отправку команд устройствам.');
        }

        const deviceId = deviceAssignmentDeviceId.trim();
        const type = deviceCommandType.trim();
        const reason = deviceCommandReason.trim();
        if (!isGuid(deviceId) || !type || !reason) {
          throw new Error('Выберите устройство, команду и причину.');
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
      } else if (label === 'Выдать новый ключ') {
        if (!hasPermission(nextBackend.session, permissionNames.rotateDeviceCredential)) {
          throw new Error('Нет прав на смену ключа устройства.');
        }

        const deviceId = deviceAssignmentDeviceId.trim();
        if (!isGuid(deviceId)) {
          throw new Error('Выберите устройство.');
        }

        const rotated = await apiClients.devices.rotateDeviceCredential(deviceId);
        setRotatedCredential(rotated);
        setCredentialIdToRevoke(readString(rotated, 'credentialId'));
        if (hasPermission(nextBackend.session, permissionNames.viewDeviceDetail)) {
          setDeviceInventory(await apiClients.devices.listDevices(nextBackend.branchId).catch(() => deviceInventory));
        }
      } else if (label === 'Отозвать ключ') {
        if (!hasPermission(nextBackend.session, permissionNames.revokeDeviceCredential)) {
          throw new Error('Нет прав на отзыв ключа устройства.');
        }

        const deviceId = deviceAssignmentDeviceId.trim();
        const credentialId = credentialIdToRevoke.trim();
        if (!isGuid(deviceId) || !isGuid(credentialId)) {
          throw new Error('Выберите устройство и ключ для отзыва.');
        }

        await apiClients.devices.revokeDeviceCredential(deviceId, credentialId);
        setRotatedCredential(null);
        if (hasPermission(nextBackend.session, permissionNames.viewDeviceDetail)) {
          setDeviceInventory(await apiClients.devices.listDevices(nextBackend.branchId).catch(() => deviceInventory));
        }
      } else if (label === 'Добавить зал' || label === 'Обновить зал') {
        if (!hasPermission(nextBackend.session, permissionNames.manageLayout)) {
          throw new Error('Нет прав на управление залами и рабочими местами.');
        }

        const name = layoutZoneName.trim();
        const sortOrder = Number(layoutZoneSortOrder);
        if (!name || !Number.isInteger(sortOrder)) {
          throw new Error('Заполните название зала и целый порядок сортировки.');
        }
        if (label === 'Обновить зал' && !isGuid(selectedLayoutZoneId)) {
          throw new Error('Выберите зал для обновления.');
        }

        const zone = label === 'Обновить зал'
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
        if (label === 'Добавить зал') {
          setLayoutZoneName(`Zone ${zones.length + 2}`);
          setLayoutZoneSortOrder(String(sortOrder + 10));
        }
        await loadSettings(nextBackend);
      } else if (label === 'Удалить зал') {
        if (!hasPermission(nextBackend.session, permissionNames.manageLayout)) {
          throw new Error('Нет прав на управление залами и рабочими местами.');
        }

        const zoneId = selectedLayoutZoneId.trim();
        if (!isGuid(zoneId)) {
          throw new Error('Выберите зал для удаления.');
        }

        await apiClients.settings.deleteZone(nextBackend.branchId, zoneId, nextBackend.session.organizationId);
        setSelectedLayoutZoneId('');
        setLayoutSeatZoneId('');
        await loadSettings(nextBackend);
      } else if (label === 'Добавить ПК' || label === 'Обновить ПК') {
        if (!hasPermission(nextBackend.session, permissionNames.manageLayout)) {
          throw new Error('Нет прав на управление залами и рабочими местами.');
        }

        const zoneId = layoutSeatZoneId.trim();
        if (!zoneId) {
          throw new Error('Сначала создайте зал для нового ПК.');
        }

        const name = layoutSeatName.trim();
        const sortOrder = Number(layoutSeatSortOrder);
        if (!name || !Number.isInteger(sortOrder)) {
          throw new Error('Заполните название ПК и целый порядок сортировки.');
        }
        if (label === 'Обновить ПК' && !isGuid(selectedLayoutSeatId)) {
          throw new Error('Выберите ПК для обновления.');
        }

        const seat = label === 'Обновить ПК'
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
        if (label === 'Добавить ПК') {
          setLayoutSeatName(`PC-${zones.reduce((sum, zone) => sum + readArray(zone, 'seats').length, 0) + 2}`);
          setLayoutSeatSortOrder(String(sortOrder + 10));
        }
        await loadSettings(nextBackend);
      } else if (label === 'Удалить ПК') {
        if (!hasPermission(nextBackend.session, permissionNames.manageLayout)) {
          throw new Error('Нет прав на управление залами и рабочими местами.');
        }

        const seatId = selectedLayoutSeatId.trim();
        if (!isGuid(seatId)) {
          throw new Error('Выберите ПК для удаления.');
        }

        await apiClients.settings.deleteSeat(nextBackend.branchId, seatId, nextBackend.session.organizationId);
        setSelectedLayoutSeatId('');
        setDeviceAssignmentSeatId('');
        await loadSettings(nextBackend);
      } else if (label === 'Создать тариф') {
        if (!hasPermission(nextBackend.session, permissionNames.manageTariffs)) {
          throw new Error('Нет прав на управление тарифами.');
        }

        const name = tariffName.trim();
        const pricePerHourMinorUnits = parseMoneyInputMinorUnits(tariffPricePerHour);
        const minimumBillableMinutes = Number(tariffMinimumMinutes);
        const roundingIncrementMinutes = Number(tariffRoundingMinutes);
        if (!name || pricePerHourMinorUnits === null
          || !Number.isInteger(minimumBillableMinutes) || minimumBillableMinutes <= 0
          || !Number.isInteger(roundingIncrementMinutes) || roundingIncrementMinutes <= 0) {
          throw new Error('Заполните название тарифа, цену за час, минимум и округление.');
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
        setTariffName(`Тариф ${tariffs.length + 2}`);
        setTariffEffectiveFromUtc(new Date().toISOString());
        await loadSettings(nextBackend);
      } else if (label === 'Обновить тариф' || label === 'Снять тариф') {
        if (!hasPermission(nextBackend.session, permissionNames.manageTariffs)) {
          throw new Error('Нет прав на управление тарифами.');
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
          throw new Error('Выберите тариф и заполните название, цену за час, минимум и округление.');
        }

        const isActive = label !== 'Снять тариф';
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
      } else if (label === 'Создать пакет') {
        if (!hasPermission(nextBackend.session, permissionNames.managePackages)) {
          throw new Error('Нет прав на управление пакетами.');
        }

        const name = packageName.trim();
        const priceMinorUnits = parseMoneyInputMinorUnits(packagePrice);
        const includedMinutes = Number(packageMinutes);
        const bonusMinutes = Number(packageBonusMinutes);
        const expiresDays = Number(packageExpiresDays);
        if (!name || priceMinorUnits === null || !Number.isInteger(includedMinutes) || includedMinutes <= 0
          || !Number.isInteger(bonusMinutes) || bonusMinutes < 0
          || !Number.isInteger(expiresDays) || expiresDays <= 0) {
          throw new Error('Заполните название, цену, минуты, бонус и срок действия пакета.');
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
      } else if (label === 'Обновить пакет' || label === 'Снять пакет') {
        if (!hasPermission(nextBackend.session, permissionNames.managePackages)) {
          throw new Error('Нет прав на управление пакетами.');
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
          throw new Error('Выберите пакет и заполните название, цену, минуты, бонус и срок действия.');
        }

        await apiClients.settings.updatePackageDefinition(nextBackend.branchId, packageDefinitionId, {
          organizationId: nextBackend.session.organizationId,
          name,
          price: { currencyCode, minorUnits: priceMinorUnits },
          includedSeconds: includedMinutes * 60,
          bonusSeconds: bonusMinutes * 60,
          expiresAfterDays: expiresDays,
          isActive: label !== 'Снять пакет'
        });
        if (label === 'Снять пакет') {
          setSelectedPackageDefinitionId('');
        }
        await loadSettings(nextBackend);
      } else if (label === 'Пригласить сотрудника') {
        if (!hasPermission(nextBackend.session, permissionNames.manageBranchStaff)) {
          throw new Error('Нет прав на управление сотрудниками.');
        }

        const userName = inviteUserName.trim();
        const displayName = inviteDisplayName.trim();
        const email = inviteEmail.trim();
        if (!userName || !displayName || !email) {
          throw new Error('Заполните имя пользователя, имя сотрудника и email.');
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
        setInviteDisplayName('Новый оператор');
        setInviteEmail('');
      } else if (label === 'Обновить профиль сотрудника') {
        if (!hasPermission(nextBackend.session, permissionNames.manageBranchStaff)) {
          throw new Error('Нет прав на управление сотрудниками.');
        }

        const staffUserId = selectedStaffUserId.trim();
        const userName = staffProfileUserName.trim();
        const displayName = staffProfileDisplayName.trim();
        if (!isGuid(staffUserId) || !userName || !displayName) {
          throw new Error('Выберите сотрудника и заполните логин и имя профиля.');
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
      } else if (label === 'Обновить роль') {
        if (!hasPermission(nextBackend.session, permissionNames.manageRoles)) {
          throw new Error('Нет прав на изменение ролей сотрудников.');
        }

        const staffUserId = selectedStaffUserId.trim();
        const roleName = staffRoleName.trim();
        if (!isGuid(staffUserId) || !staffRoleOptions.includes(roleName as (typeof staffRoleOptions)[number])) {
          throw new Error('Выберите сотрудника и роль.');
        }

        const staffUser = await apiClients.settings.updateStaffUserRoles(nextBackend.branchId, staffUserId, {
          organizationId: nextBackend.session.organizationId,
          roleNames: [roleName]
        });
        setStaffUsers((items) => items.map((item) => readString(item, 'staffUserId') === staffUserId ? staffUser : item));
        setSelectedStaffUserId(readString(staffUser, 'staffUserId'));
        setStaffRoleName(readArray<string>(staffUser, 'roleNames')[0] ?? roleName);
      } else if (label === 'Отключить сотрудника' || label === 'Включить сотрудника') {
        if (!hasPermission(nextBackend.session, permissionNames.manageBranchStaff)) {
          throw new Error('Нет прав на управление сотрудниками.');
        }

        const staffUserId = selectedStaffUserId.trim();
        if (!isGuid(staffUserId)) {
          throw new Error('Выберите сотрудника.');
        }

        const staffUser = await apiClients.settings.updateStaffUserState(nextBackend.branchId, staffUserId, {
          organizationId: nextBackend.session.organizationId,
          isActive: label === 'Включить сотрудника'
        });
        setStaffUsers((items) => items.map((item) => readString(item, 'staffUserId') === staffUserId ? staffUser : item));
        setSelectedStaffUserId(readString(staffUser, 'staffUserId'));
        setStaffRoleName(readArray<string>(staffUser, 'roleNames')[0] ?? staffRoleName);
      } else if (label === 'Сбросить пароль') {
        if (!hasPermission(nextBackend.session, permissionNames.manageBranchStaff)) {
          throw new Error('Нет прав на управление сотрудниками.');
        }

        const staffUserId = selectedStaffUserId.trim();
        const nextPassword = resetPassword.trim();
        if (!isGuid(staffUserId) || nextPassword.length < 8) {
          throw new Error('Выберите сотрудника и задайте пароль не короче 8 символов.');
        }

        const staffUser = await apiClients.settings.resetStaffUserPassword(nextBackend.branchId, staffUserId, {
          organizationId: nextBackend.session.organizationId,
          newPassword: nextPassword
        });
        setStaffUsers((items) => items.map((item) => readString(item, 'staffUserId') === staffUserId ? staffUser : item));
        setSelectedStaffUserId(readString(staffUser, 'staffUserId'));
        setResetPassword('');
      } else if (label === 'Создать товар') {
        if (!hasPermission(nextBackend.session, permissionNames.managePosCatalog)) {
          throw new Error('Нет прав на управление каталогом товаров.');
        }

        const categoryName = productCategoryName.trim();
        const nextProductName = productName.trim();
        const sku = productSku.trim();
        const priceMinorUnits = parseMoneyInputMinorUnits(productPrice);
        if (!categoryName || !nextProductName || !sku || priceMinorUnits === null) {
          throw new Error('Заполните категорию, товар, артикул и цену больше нуля.');
        }

        const category = await apiClients.settings.createProductCategory(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          name: categoryName,
          idempotencyKey: createIdempotencyKey('pos-category-create')
        });
        const categoryId = readString(category, 'categoryId');
        if (!categoryId) {
          throw new Error('Категория создана, но платформа не подтвердила её. Повторите операцию или обратитесь в поддержку.');
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
        setProductCategoryName(`Категория ${nextIndex}`);
        setProductName(`Товар ${nextIndex}`);
        setProductSku(`SKU-${String(nextIndex).padStart(3, '0')}`);
        setProductPrice('12.00');
        setProductTrackStock(true);
        setProductAllowNegativeStock(false);
        setSelectedProductId(readString(product, 'productId'));
      } else if (label === 'Обновить товар' || label === 'Снять с продажи') {
        if (!hasPermission(nextBackend.session, permissionNames.managePosCatalog)) {
          throw new Error('Нет прав на управление каталогом товаров.');
        }

        const selectedProduct = catalog.find((product) => readString(product, 'productId') === selectedProductId);
        const nextProductName = productName.trim();
        const sku = productSku.trim();
        const priceMinorUnits = parseMoneyInputMinorUnits(productPrice);
        if (!selectedProduct || !nextProductName || !sku || priceMinorUnits === null) {
          throw new Error('Выберите товар и заполните товар, артикул и цену больше нуля.');
        }

        await apiClients.settings.updateProduct(nextBackend.branchId, readString(selectedProduct, 'productId'), {
          organizationId: nextBackend.session.organizationId,
          categoryId: readString(selectedProduct, 'categoryId'),
          name: nextProductName,
          sku,
          price: { currencyCode, minorUnits: priceMinorUnits },
          trackStock: productTrackStock,
          allowNegativeStock: productAllowNegativeStock,
          isActive: label !== 'Снять с продажи'
        });
        if (label === 'Снять с продажи') {
          setSelectedProductId('');
        }
        await loadSettings(nextBackend);
      } else if (label === 'Записать движение') {
        if (!hasPermission(nextBackend.session, permissionNames.manageInventoryStock)) {
          throw new Error('Нет прав на управление остатками.');
        }

        const selectedProduct = trackedCatalog.find((product) => readString(product, 'productId') === stockProductId);
        const quantityDelta = Number(stockQuantityDelta);
        const unitCostMinorUnits = parseNonNegativeMoneyInputMinorUnits(stockUnitCost);
        const reason = stockReason.trim();
        if (!selectedProduct || !Number.isInteger(quantityDelta) || quantityDelta === 0 || unitCostMinorUnits === null || !reason) {
          throw new Error('Выберите товар с учётом остатков, количество не равное нулю, себестоимость и причину.');
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
      } else if (label === 'Добавить пакет обновления') {
        if (!hasPermission(nextBackend.session, permissionNames.manageUpdatePackages)) {
          throw new Error('Нет прав на управление пакетами обновлений.');
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
          throw new Error('Заполните приложение, версию, канал, ссылку на установщик, проверочную сумму, подпись, способ проверки подписи и размер файла.');
        }

        try {
          new URL(artifactUri);
        } catch {
          throw new Error('Ссылка на установщик должна быть полной.');
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
          throw new Error('Пакет обновления создан, но платформа не подтвердила его. Повторите операцию или обратитесь в поддержку.');
        }

        setRegisteredUpdatePackages((items) => [createdPackage, ...items.filter((item) => readString(item, 'updatePackageId') !== updatePackageId)]);
        setRolloutPackageId(updatePackageId);
        setPackageStatePackageId(updatePackageId);
      } else if (label === 'Создать публикацию обновления') {
        if (!hasPermission(nextBackend.session, permissionNames.manageUpdateRollouts)) {
          throw new Error('Нет прав на управление публикациями обновлений.');
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
          throw new Error('Выберите пакет, канал, цель, долю, старт и причину публикации.');
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
      } else if (label === 'Изменить состояние пакета') {
        if (!hasPermission(nextBackend.session, permissionNames.manageUpdatePackages)) {
          throw new Error('Нет прав на управление пакетами обновлений.');
        }

        const updatePackageId = packageStatePackageId.trim();
        const state = packageState.trim();
        const reason = packageStateReason.trim();
        if (!isGuid(updatePackageId) || !state || !reason) {
          throw new Error('Выберите пакет, состояние и причину.');
        }

        const updatePackage = await apiClients.updates.changePackageState(nextBackend.branchId, updatePackageId, {
          organizationId: nextBackend.session.organizationId,
          state,
          reason
        });
        setRegisteredUpdatePackages((items) => [updatePackage, ...items.filter((item) => readString(item, 'updatePackageId') !== updatePackageId)]);
      } else if (label === 'Изменить состояние публикации') {
        if (!hasPermission(nextBackend.session, permissionNames.manageUpdateRollouts)) {
          throw new Error('Нет прав на управление публикациями обновлений.');
        }

        const updateRolloutId = rolloutStateRolloutId.trim();
        const state = rolloutState.trim();
        const reason = rolloutStateReason.trim();
        if (!isGuid(updateRolloutId) || !state || !reason) {
          throw new Error('Выберите публикацию, состояние и причину.');
        }

        const rollout = await apiClients.updates.changeRolloutState(nextBackend.branchId, updateRolloutId, {
          organizationId: nextBackend.session.organizationId,
          state,
          reason
        });
        setRollouts((items) => items.map((item) => readString(item, 'updateRolloutId') === updateRolloutId ? rollout : item));
      } else {
        throw new Error('Действие настроек пока не подключено к платформе.');
      }

      setFeedback({ label, state: 'confirmed' });
    } catch (error) {
      setFeedback({ label, state: 'failed', detail: projectOperatorError(error).detail });
    }
  };

  const saveSettings = async () => {
    if (!clubName.trim() || !city.trim()) {
      triggerFeedback(setFeedback, 'Проверить обязательные поля', 'failed', 'Заполните обязательные поля.');
      return;
    }

    setFeedback({ label: 'Профиль клуба', state: 'pending' });
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
      setFeedback({ label: 'Профиль клуба', state: 'confirmed' });
    } catch (error) {
      setFeedback({ label: 'Профиль клуба', state: 'failed', detail: projectOperatorError(error).detail });
    }
  };

  const renderSettingsContent = () => {
    if (selectedSection === 'Залы и ПК') {
      return (
        <>
          <div className="settings-section-title">
            <span>Залы и рабочие места</span>
            <div className="settings-section-actions">
              <button type="button" disabled={!canManageLayout} onClick={() => runSettingsAction('Добавить зал')}>Создать зал</button>
              <button type="button" disabled={!canManageLayout || !selectedLayoutZoneId} onClick={() => runSettingsAction('Обновить зал')}>Обновить зал</button>
              <button
                type="button"
                disabled={!canManageLayout || !selectedLayoutZoneId}
                onClick={() => {
                  setFeedback(emptyFeedback);
                  setCriticalAction('layout-zone-delete');
                }}
              >
                Удалить зал
              </button>
            </div>
          </div>
          <div className="settings-form-grid settings-layout-form">
            <label>Название зала<input value={layoutZoneName} disabled={!canManageLayout} onChange={(event) => setLayoutZoneName(event.currentTarget.value)} /></label>
            <label>Порядок зала<input inputMode="numeric" value={layoutZoneSortOrder} disabled={!canManageLayout} onChange={(event) => setLayoutZoneSortOrder(event.currentTarget.value)} /></label>
            <label>Зона ПК
              <select value={layoutSeatZoneId} disabled={!canManageLayout || zones.length === 0} onChange={(event) => setLayoutSeatZoneId(event.currentTarget.value)}>
                {zones.length === 0 && <option value="">нет залов</option>}
                {zones.map((zone) => (
                  <option key={readString(zone, 'zoneId')} value={readString(zone, 'zoneId')}>{readString(zone, 'name', 'Зал')}</option>
                ))}
              </select>
            </label>
            <label>Название ПК<input value={layoutSeatName} disabled={!canManageLayout} onChange={(event) => setLayoutSeatName(event.currentTarget.value)} /></label>
            <label>Порядок ПК<input inputMode="numeric" value={layoutSeatSortOrder} disabled={!canManageLayout} onChange={(event) => setLayoutSeatSortOrder(event.currentTarget.value)} /></label>
            <button type="button" disabled={!canManageLayout || !layoutSeatZoneId} onClick={() => runSettingsAction('Добавить ПК')}>Создать ПК</button>
            <button type="button" disabled={!canManageLayout || !selectedLayoutSeatId || !layoutSeatZoneId} onClick={() => runSettingsAction('Обновить ПК')}>Обновить ПК</button>
            <button
              type="button"
              disabled={!canManageLayout || !selectedLayoutSeatId}
              onClick={() => {
                setFeedback(emptyFeedback);
                setCriticalAction('layout-seat-delete');
              }}
            >
              Удалить ПК
            </button>
          </div>
          {criticalAction === 'layout-zone-delete' && (
            <CriticalActionConfirmation
              title="Подтвердите удаление зала"
              detail={`${readString(selectedLayoutZone, 'name', layoutZoneName || 'Зал')} · ${readArray(selectedLayoutZone, 'seats').length} ПК`}
              impact="Зал будет удален из схемы клуба. Удаление доступно только для пустых залов."
              confirmLabel="Подтвердить удаление зала"
              disabled={feedback.state === 'pending'}
              onCancel={() => setCriticalAction(null)}
              onConfirm={() => void runSettingsAction('Удалить зал')}
            />
          )}
          {criticalAction === 'layout-seat-delete' && (
            <CriticalActionConfirmation
              title="Подтвердите удаление ПК"
              detail={readString(selectedLayoutSeat, 'name', layoutSeatName || 'ПК')}
              impact="Рабочее место будет удалено из схемы клуба. Проверьте активные сессии и привязку устройства до удаления."
              confirmLabel="Подтвердить удаление ПК"
              disabled={feedback.state === 'pending'}
              onCancel={() => setCriticalAction(null)}
              onConfirm={() => void runSettingsAction('Удалить ПК')}
            />
          )}
          <div className="settings-room-grid">
            {zones.map((zone) => (
              <button key={readString(zone, 'zoneId')} type="button" className={`settings-room-card ${readString(zone, 'zoneId') === selectedLayoutZoneId ? 'active' : ''}`} onClick={() => selectLayoutZone(zone)}>
                <strong>{readString(zone, 'name', 'Зал')}</strong>
                <b>{readArray(zone, 'seats').length} ПК</b>
                <span>порядок {readNumber(zone, 'sortOrder', 0)}</span>
              </button>
            ))}
          </div>
          <div className="settings-tariff-list">
            {zones.flatMap((zone) => readArray<Record<string, unknown>>(zone, 'seats').map((seat) => (
              <button key={readString(seat, 'seatId')} type="button" className={`settings-tariff-row ${readString(seat, 'seatId') === selectedLayoutSeatId ? 'active' : ''}`} onClick={() => selectLayoutSeat(zone, seat)}>
                <strong>{readString(seat, 'name', 'ПК')}</strong>
                <b>{readString(zone, 'name', 'Зал')}</b>
                <span>порядок {readNumber(seat, 'sortOrder', 0)}</span>
              </button>
            )))}
          </div>
          <div className="settings-section-title">
            <span>Подключение устройств</span>
            <button type="button" disabled={!canCreateDeviceEnrollmentCode} onClick={() => runSettingsAction('Создать код подключения')}>Создать код подключения</button>
            <button type="button" disabled={!canAssignDeviceSeat || layoutSeatOptions.length === 0} onClick={() => runSettingsAction('Назначить устройство')}>Назначить устройство</button>
            <button type="button" disabled={!canViewDeviceCommandStatus} onClick={() => runSettingsAction('Обновить историю команд')}>Обновить историю команд</button>
          </div>
          <div className="settings-form-grid settings-device-form">
            <label>Срок действия кода, минут<input inputMode="numeric" value={enrollmentExpiresMinutes} disabled={!canCreateDeviceEnrollmentCode} onChange={(event) => setEnrollmentExpiresMinutes(event.currentTarget.value)} /></label>
            <label>Код подключения<input value={readString(enrollmentCode, 'code', '—')} readOnly /></label>
            <label>Устройство
              <select value={deviceAssignmentDeviceId} disabled={deviceOptions.length === 0 || (!canAssignDeviceSeat && !canViewDeviceDetail)} onChange={(event) => setDeviceAssignmentDeviceId(event.currentTarget.value)}>
                {deviceOptions.length === 0 && <option value="">нет подключенных устройств</option>}
                {deviceAssignmentDeviceId && !deviceOptions.some((device) => device.id === deviceAssignmentDeviceId) && (
                  <option value={deviceAssignmentDeviceId}>выбранное устройство</option>
                )}
                {deviceOptions.map((device) => (
                  <option key={device.id} value={device.id}>{device.label}</option>
                ))}
              </select>
            </label>
            <label>Рабочее место
              <select value={deviceAssignmentSeatId} disabled={!canAssignDeviceSeat || layoutSeatOptions.length === 0} onChange={(event) => setDeviceAssignmentSeatId(event.currentTarget.value)}>
                {layoutSeatOptions.length === 0 && <option value="">нет рабочих мест</option>}
                {layoutSeatOptions.map((seat) => (
                  <option key={seat.seatId} value={seat.seatId}>{seat.label}</option>
                ))}
              </select>
            </label>
            <label>Карточка устройства<input value={deviceDetail ? `${readString(deviceDetail, 'machineName', 'Устройство')} · ${readString(deviceDetail, 'seatName', 'без места')}` : 'не открыта'} readOnly /></label>
            <button type="button" disabled={!canViewDeviceDetail || !isGuid(deviceAssignmentDeviceId)} onClick={() => runSettingsAction('Открыть карточку устройства')}>Открыть карточку устройства</button>
            <label>Команда
              <select value={deviceCommandType} disabled={!canDispatchDeviceCommand} onChange={(event) => setDeviceCommandType(event.currentTarget.value)}>
                <option value="lock">блокировка</option>
                <option value="unlock">разблокировка</option>
              </select>
            </label>
            <label>Причина команды<input value={deviceCommandReason} disabled={!canDispatchDeviceCommand} onChange={(event) => setDeviceCommandReason(event.currentTarget.value)} /></label>
            <label>Последняя команда<input value={lastDeviceCommand ? `${commandTypeLabel(readString(lastDeviceCommand, 'type', 'command'))} · отправлена` : 'не отправлена'} readOnly /></label>
            <button type="button" disabled={!canDispatchDeviceCommand || !isGuid(deviceAssignmentDeviceId)} onClick={() => runSettingsAction('Отправить команду')}>Отправить команду</button>
            <label>Ключ для отзыва<input value={rotatedCredentialLabel} readOnly /></label>
            <label>Новый ключ устройства<input value={rotatedCredentialId ? 'создан' : '—'} readOnly /></label>
            <label className="settings-form-wide">Код нового подключения<input value={readString(rotatedCredential, 'credentialSecret', '—')} readOnly /></label>
            <button type="button" disabled={!canRotateDeviceCredential || !isGuid(deviceAssignmentDeviceId)} onClick={() => runSettingsAction('Выдать новый ключ')}>Выдать новый ключ</button>
            <button
              type="button"
              disabled={!canRevokeDeviceCredential || !isGuid(deviceAssignmentDeviceId) || !isGuid(credentialIdToRevoke) || feedback.state === 'pending'}
              onClick={() => {
                setFeedback(emptyFeedback);
                setCriticalAction('credential-revoke');
              }}
            >
              Отозвать ключ
            </button>
          </div>
          {criticalAction === 'credential-revoke' && (
            <CriticalActionConfirmation
              title="Подтвердите отзыв ключа"
              detail={`${selectedDeviceLabel} · новый ключ устройства`}
              impact="После отзыва этот ключ больше нельзя использовать для подключения выбранного ПК."
              confirmLabel="Отозвать ключ"
              disabled={feedback.state === 'pending'}
              onCancel={() => setCriticalAction(null)}
              onConfirm={() => void runSettingsAction('Отозвать ключ')}
            />
          )}
          <div className="settings-device-inventory" aria-label="Устройства филиала">
            {deviceInventory.map((device) => {
              const pendingCommands = readNumber(device, 'pendingCommandCount', 0);
              const failedCommands = readNumber(device, 'failedCommandCount', 0);
              return (
                <button
                  key={readString(device, 'deviceId')}
                  type="button"
                  aria-label={readString(device, 'machineName', 'Устройство')}
                  className={`settings-device-row ${readString(device, 'deviceId') === deviceAssignmentDeviceId ? 'active' : ''}${failedCommands > 0 ? ' attention' : ''}`}
                  disabled={!canViewDeviceDetail}
                  onClick={() => selectDeviceInventoryItem(device)}
                >
                  <strong>{readString(device, 'machineName', 'Устройство')}</strong>
                  <b>{readBoolean(device, 'isOnline') ? 'онлайн' : 'офлайн'} · {readBoolean(device, 'isLocked') ? 'заблокирован' : 'разблокирован'}</b>
                  <span>{readString(device, 'zoneName', 'без зала')} · {readString(device, 'seatName', 'без места')}</span>
                  <em>Агент {readString(device, 'agentVersion', '—')} · приложений {readNumber(device, 'installedAppCount', 0)} · в работе {pendingCommands} · ошибок {failedCommands} · {formatTime(readString(device, 'lastHeartbeatAtUtc'))}</em>
                </button>
              );
            })}
            {deviceInventory.length === 0 && (
              <span className="settings-device-empty">Нет подключенных устройств в филиале</span>
            )}
          </div>
          {deviceDetail && (
            <div className="settings-device-detail-grid">
              <span><strong>Устройство</strong><b>{readString(deviceDetail, 'machineName', 'Устройство')}</b></span>
              <span><strong>Статус</strong><b>{readBoolean(deviceDetail, 'isOnline') ? 'онлайн' : 'офлайн'} · {readBoolean(deviceDetail, 'isLocked') ? 'заблокирован' : 'разблокирован'}</b></span>
              <span><strong>Место</strong><b>{readString(deviceDetail, 'zoneName', 'без зала')} · {readString(deviceDetail, 'seatName', 'без места')}</b></span>
              <span><strong>Пульс</strong><b>{formatTime(readString(deviceDetail, 'lastHeartbeatAtUtc'))}</b></span>
              <span><strong>Агент</strong><b>{readString(deviceDetail, 'agentVersion', '—')}</b></span>
              <span><strong>Оболочка</strong><b>{readString(deviceDetail, 'shellVersion', '—')}</b></span>
              <span><strong>Ключи</strong><b>{readNumber(deviceDetail, 'activeCredentialCount', 0)}</b></span>
              <span><strong>Приложения</strong><b>{readNumber(deviceDetail, 'installedAppCount', 0)}</b></span>
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
                <span>История команд филиала</span>
                <strong>{branchDeviceCommandHistory.length} последних</strong>
              </div>
              <div className="settings-command-history" aria-label="История команд филиала">
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

    if (selectedSection === 'Тарифы') {
      return (
        <>
          <div className="settings-section-title">
            <span>Тарифы</span>
            <div className="settings-section-actions">
              <button type="button" disabled={!canManageTariffs} onClick={() => runSettingsAction('Создать тариф')}>Создать тариф</button>
              <button type="button" disabled={!canManageTariffs || !selectedTariffVersionId} onClick={() => runSettingsAction('Обновить тариф')}>Обновить тариф</button>
              <button type="button" disabled={!canManageTariffs || !selectedTariffVersionId} onClick={() => runSettingsAction('Снять тариф')}>Снять тариф</button>
            </div>
          </div>
          <div className="settings-form-grid settings-tariff-form">
            <label>Название тарифа<input value={tariffName} disabled={!canManageTariffs} onChange={(event) => setTariffName(event.currentTarget.value)} /></label>
            <label>Цена за час<input inputMode="decimal" value={tariffPricePerHour} disabled={!canManageTariffs} onChange={(event) => setTariffPricePerHour(event.currentTarget.value)} /></label>
            <label>Минимум, мин<input inputMode="numeric" value={tariffMinimumMinutes} disabled={!canManageTariffs} onChange={(event) => setTariffMinimumMinutes(event.currentTarget.value)} /></label>
            <label>Шаг округления, мин<input inputMode="numeric" value={tariffRoundingMinutes} disabled={!canManageTariffs} onChange={(event) => setTariffRoundingMinutes(event.currentTarget.value)} /></label>
          </div>
          <div className="settings-tariff-list">
            {tariffs.map((tariff) => (
              <button key={readString(tariff, 'tariffVersionId')} type="button" className={`settings-tariff-row ${readString(tariff, 'tariffVersionId') === selectedTariffVersionId ? 'active' : ''}`} onClick={() => selectTariffOption(tariff)}>
                <strong>{readString(tariff, 'name', 'Тариф')}</strong>
                <b>{formatMinorUnits(readNumber(tariff, 'pricePerMinuteMinorUnits', 0) * 60, readString(tariff, 'currencyCode', currencyCode))} / час</b>
                <span>минимум {readNumber(tariff, 'minimumBillableMinutes', 0)} мин · шаг {readNumber(tariff, 'roundingIncrementMinutes', 0)} мин · {readBoolean(tariff, 'isActive', true) ? 'активен' : 'снят'}</span>
              </button>
            ))}
          </div>
          <div className="settings-section-title">
            <span>Пакеты</span>
            <div className="settings-section-actions">
              <button type="button" disabled={!canManagePackages} onClick={() => runSettingsAction('Создать пакет')}>Создать пакет</button>
              <button type="button" disabled={!canManagePackages || !selectedPackageDefinitionId} onClick={() => runSettingsAction('Обновить пакет')}>Обновить пакет</button>
              <button type="button" disabled={!canManagePackages || !selectedPackageDefinitionId} onClick={() => runSettingsAction('Снять пакет')}>Снять пакет</button>
            </div>
          </div>
          <div className="settings-tariff-list">
            {packageOptions.map((option) => (
              <button key={readString(option, 'packageDefinitionId')} type="button" className={`settings-tariff-row ${readString(option, 'packageDefinitionId') === selectedPackageDefinitionId ? 'active' : ''}`} onClick={() => selectPackageOption(option)}>
                <strong>{readString(option, 'name', 'Пакет')}</strong>
                <b>{formatMinorUnits(readNumber(option, 'priceMinorUnits', 0), readString(option, 'currencyCode', currencyCode))}</b>
                <span>{Math.round(readNumber(option, 'includedSeconds', 0) / 60)} мин · +{Math.round(readNumber(option, 'bonusSeconds', 0) / 60)} бонус · действует {readNumber(option, 'expiresAfterDays', 0)} дн.</span>
              </button>
            ))}
          </div>
          <div className="settings-form-grid settings-package-form">
            <label>Название пакета<input value={packageName} disabled={!canManagePackages} onChange={(event) => setPackageName(event.currentTarget.value)} /></label>
            <label>Цена<input inputMode="decimal" value={packagePrice} disabled={!canManagePackages} onChange={(event) => setPackagePrice(event.currentTarget.value)} /></label>
            <label>Включено, мин<input inputMode="numeric" value={packageMinutes} disabled={!canManagePackages} onChange={(event) => setPackageMinutes(event.currentTarget.value)} /></label>
            <label>Бонус, мин<input inputMode="numeric" value={packageBonusMinutes} disabled={!canManagePackages} onChange={(event) => setPackageBonusMinutes(event.currentTarget.value)} /></label>
            <label>Срок, дней<input inputMode="numeric" value={packageExpiresDays} disabled={!canManagePackages} onChange={(event) => setPackageExpiresDays(event.currentTarget.value)} /></label>
          </div>
        </>
      );
    }

    if (selectedSection === 'Персонал') {
      return (
        <>
          <div className="settings-section-title">
            <span>Сотрудники</span>
            <div className="settings-section-actions">
              <button type="button" disabled={!canManageBranchStaff} onClick={() => runSettingsAction('Пригласить сотрудника')}>Пригласить сотрудника</button>
              <button type="button" disabled={!canManageBranchStaff || !selectedStaffUserId} onClick={() => runSettingsAction(selectedStaffIsActive ? 'Отключить сотрудника' : 'Включить сотрудника')}>
                {selectedStaffIsActive ? 'Отключить сотрудника' : 'Включить сотрудника'}
              </button>
              <button type="button" disabled={!canManageBranchStaff || !selectedStaffUserId} onClick={() => runSettingsAction('Сбросить пароль')}>Сбросить пароль</button>
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
                    triggerFeedback(setFeedback, operatorDisplayNameLabel(readString(user, 'displayName', 'Сотрудник')), 'confirmed');
                  }}
                >
                  <strong>{operatorDisplayNameLabel(readString(user, 'displayName', 'Сотрудник'))}</strong>
                  <span>{readString(user, 'userName', 'Пользователь')} · {readArray<string>(user, 'roleNames').map(staffRoleLabel).join(', ') || 'роль не задана'} · {readBoolean(user, 'isActive', true) ? 'активен' : 'отключен'}</span>
                </button>
              ))}
            </div>
            <div className="settings-form-grid settings-staff-form">
              <label>Логин для входа<input value={inviteUserName} disabled={!canManageBranchStaff} onChange={(event) => setInviteUserName(event.currentTarget.value)} /></label>
              <label>Имя в смене<input value={inviteDisplayName} disabled={!canManageBranchStaff} onChange={(event) => setInviteDisplayName(event.currentTarget.value)} /></label>
              <label>Email для приглашения<input type="email" value={inviteEmail} disabled={!canManageBranchStaff} onChange={(event) => setInviteEmail(event.currentTarget.value)} /></label>
              {inviteCode && <label>Код приглашения<input readOnly value={inviteCode} onFocus={(event) => event.currentTarget.select()} /></label>}
              <label>Роль доступа
                <select value={inviteRoleName} disabled={!canManageBranchStaff} onChange={(event) => setInviteRoleName(event.currentTarget.value)}>
                  {staffRoleOptions.map((roleName) => <option key={roleName} value={roleName}>{staffRoleLabel(roleName)}</option>)}
                </select>
              </label>
              <label>Логин профиля<input value={staffProfileUserName} disabled={!canManageBranchStaff || !selectedStaffUserId} onChange={(event) => setStaffProfileUserName(event.currentTarget.value)} /></label>
              <label>Имя профиля<input value={staffProfileDisplayName} disabled={!canManageBranchStaff || !selectedStaffUserId} onChange={(event) => setStaffProfileDisplayName(event.currentTarget.value)} /></label>
              <button type="button" disabled={!canManageBranchStaff || !selectedStaffUserId} onClick={() => runSettingsAction('Обновить профиль сотрудника')}>Обновить профиль</button>
              <label>Новая роль
                <select value={staffRoleName} disabled={!canManageRoles || !selectedStaffUserId} onChange={(event) => setStaffRoleName(event.currentTarget.value)}>
                  {staffRoleOptions.map((roleName) => <option key={roleName} value={roleName}>{staffRoleLabel(roleName)}</option>)}
                </select>
              </label>
              <label>Новый пароль для входа<input type="password" value={resetPassword} disabled={!canManageBranchStaff || !selectedStaffUserId} onChange={(event) => setResetPassword(event.currentTarget.value)} /></label>
              <button type="button" disabled={!canManageRoles || !selectedStaffUserId} onClick={() => runSettingsAction('Обновить роль')}>Обновить роль</button>
            </div>
          </div>
        </>
      );
    }

    if (selectedSection === 'Товары и склад') {
      return (
        <>
          <div className="settings-section-title">
            <span>Каталог товаров</span>
            <div className="settings-section-actions">
              <button type="button" disabled={!canManagePosCatalog} onClick={() => runSettingsAction('Создать товар')}>Создать товар</button>
              <button type="button" disabled={!canManagePosCatalog || !selectedProductId} onClick={() => runSettingsAction('Обновить товар')}>Обновить товар</button>
              <button type="button" disabled={!canManagePosCatalog || !selectedProductId} onClick={() => runSettingsAction('Снять с продажи')}>Снять с продажи</button>
            </div>
          </div>
          <div className="settings-config-grid">
            {catalog.slice(0, 8).map((product) => (
              <button key={readString(product, 'productId')} type="button" className={readString(product, 'productId') === selectedProductId ? 'active' : undefined} onClick={() => selectCatalogProduct(product)}>
                <strong>{readString(product, 'name', 'Товар')}</strong>
                <span>{formatMoney(readMoney(product, 'price'), currencyCode)} · остаток {readNumber(product, 'stockOnHand', 0)}</span>
              </button>
            ))}
          </div>
          <div className="settings-form-grid settings-pos-form">
            <label>Категория<input value={productCategoryName} disabled={!canManagePosCatalog} onChange={(event) => setProductCategoryName(event.currentTarget.value)} /></label>
            <label>Товар<input value={productName} disabled={!canManagePosCatalog} onChange={(event) => setProductName(event.currentTarget.value)} /></label>
            <label>Артикул<input value={productSku} disabled={!canManagePosCatalog} onChange={(event) => setProductSku(event.currentTarget.value)} /></label>
            <label>Цена<input inputMode="decimal" value={productPrice} disabled={!canManagePosCatalog} onChange={(event) => setProductPrice(event.currentTarget.value)} /></label>
            <label>Учёт остатков
              <select value={productTrackStock ? 'yes' : 'no'} disabled={!canManagePosCatalog} onChange={(event) => setProductTrackStock(event.currentTarget.value === 'yes')}>
                <option value="yes">да</option>
                <option value="no">нет</option>
              </select>
            </label>
            <label>Минусовой остаток
              <select value={productAllowNegativeStock ? 'yes' : 'no'} disabled={!canManagePosCatalog} onChange={(event) => setProductAllowNegativeStock(event.currentTarget.value === 'yes')}>
                <option value="no">нет</option>
                <option value="yes">да</option>
              </select>
            </label>
          </div>
          <div className="settings-section-title">
            <span>Остатки</span>
            <button type="button" disabled={!canManageInventoryStock || trackedCatalog.length === 0} onClick={() => runSettingsAction('Записать движение')}>Записать движение</button>
          </div>
          <div className="settings-form-grid settings-stock-form">
            <label>Товар склада
              <select value={stockProductId} disabled={!canManageInventoryStock || trackedCatalog.length === 0} onChange={(event) => setStockProductId(event.currentTarget.value)}>
                {trackedCatalog.length === 0 && <option value="">нет товаров с остатками</option>}
                {trackedCatalog.map((product) => (
                  <option key={readString(product, 'productId')} value={readString(product, 'productId')}>{readString(product, 'name', 'Товар')} · остаток {readNumber(product, 'stockOnHand', 0)}</option>
                ))}
              </select>
            </label>
            <label>Тип
              <select value={stockMovementType} disabled={!canManageInventoryStock} onChange={(event) => setStockMovementType(event.currentTarget.value)}>
                <option value="purchase">Приход</option>
                <option value="adjustment">Коррекция</option>
              </select>
            </label>
            <label>Кол-во<input inputMode="numeric" value={stockQuantityDelta} disabled={!canManageInventoryStock} onChange={(event) => setStockQuantityDelta(event.currentTarget.value)} /></label>
            <label>Себестоимость<input inputMode="decimal" value={stockUnitCost} disabled={!canManageInventoryStock} onChange={(event) => setStockUnitCost(event.currentTarget.value)} /></label>
            <label>Причина<input value={stockReason} disabled={!canManageInventoryStock} onChange={(event) => setStockReason(event.currentTarget.value)} /></label>
          </div>
          <div className="settings-section-title">
            <span>История склада</span>
            <strong>{stockMovements.length} последних</strong>
          </div>
          <div className="settings-config-grid settings-stock-history">
            {stockMovements.length === 0 && (
              <button type="button" disabled>
                <strong>Нет движений</strong>
                <span>движений по складу пока нет</span>
              </button>
            )}
            {stockMovements.map((movement) => {
              const productId = readString(movement, 'productId');
              const productName = readString(
                catalog.find((product) => readString(product, 'productId') === productId),
                'name',
                'Товар');
              const quantityDelta = readNumber(movement, 'quantityDelta', 0);
              const reason = readString(movement, 'reason', 'причина не указана');
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

    if (selectedSection === 'Интеграции') {
      return (
        <>
          <div className="settings-config-grid">
            {[
              ['Платежи', 'ручное подтверждение'],
              ['Обновления', `публикаций: ${rollouts.length}`],
              ['Ошибки обновлений', `ПК с ошибками: ${readNumber(updateSummary, 'failedDevices', 0)}`],
              ['Связь', backend ? 'подключена' : 'локальные данные']
            ].map(([name, detail]) => (
              <button key={name} type="button" onClick={() => triggerFeedback(setFeedback, name, 'confirmed')}>
                <strong>{name}</strong>
                <span>{detail}</span>
              </button>
            ))}
          </div>

          <div className="settings-section-title">
            <span>Пакеты для обновления</span>
            <button type="button" disabled={!canManageUpdatePackages} onClick={() => runSettingsAction('Добавить пакет обновления')}>Добавить пакет</button>
          </div>
          <div className="settings-form-grid settings-update-form">
            <label>Приложение
              <select value={updateComponent} disabled={!canManageUpdatePackages} onChange={(event) => setUpdateComponent(event.currentTarget.value)}>
                <option value="operator-app">{updateComponentLabel('operator-app')}</option>
                <option value="agent-service">{updateComponentLabel('agent-service')}</option>
                <option value="player-shell">{updateComponentLabel('player-shell')}</option>
              </select>
            </label>
            <label>Версия<input value={updateVersion} disabled={!canManageUpdatePackages} onChange={(event) => setUpdateVersion(event.currentTarget.value)} /></label>
            <label>Канал
              <select value={updateChannel} disabled={!canManageUpdatePackages} onChange={(event) => setUpdateChannel(event.currentTarget.value)}>
                <option value="internal">{updateChannelLabel('internal')}</option>
                <option value="beta">{updateChannelLabel('beta')}</option>
                <option value="stable">{updateChannelLabel('stable')}</option>
              </select>
            </label>
            <label className="settings-form-wide">Файл установщика<input value={updateArtifactUri} disabled={!canManageUpdatePackages} onChange={(event) => setUpdateArtifactUri(event.currentTarget.value)} /></label>
            <label className="settings-form-wide">Проверочная сумма<input value={updateSha256} disabled={!canManageUpdatePackages} onChange={(event) => setUpdateSha256(event.currentTarget.value)} /></label>
            <label>Подпись пакета<input value={updateSignature} disabled={!canManageUpdatePackages} onChange={(event) => setUpdateSignature(event.currentTarget.value)} /></label>
            <label>Способ проверки подписи<input value={updateSignatureAlgorithm} disabled={!canManageUpdatePackages} onChange={(event) => setUpdateSignatureAlgorithm(event.currentTarget.value)} /></label>
            <label>Размер файла, КБ<input inputMode="numeric" value={updateSizeKilobytes} disabled={!canManageUpdatePackages} onChange={(event) => setUpdateSizeKilobytes(event.currentTarget.value)} /></label>
            <label className="settings-form-wide">Описание релиза<input value={updateReleaseNotes} disabled={!canManageUpdatePackages} onChange={(event) => setUpdateReleaseNotes(event.currentTarget.value)} /></label>
          </div>

          <div className="settings-section-title">
            <span>Публикации обновлений</span>
            <button type="button" disabled={!canManageUpdateRollouts} onClick={() => runSettingsAction('Создать публикацию обновления')}>Создать публикацию</button>
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
                <strong>{updateComponentLabel(readString(rollout, 'component'))} {readString(rollout, 'version', 'версия')}</strong>
                <b>{updateRolloutStateLabel(readString(rollout, 'state'))}</b>
                <span>{updateTargetKindLabel(readString(rollout, 'targetKind'))} · {readNumber(rollout, 'batchPercent', 0)}% · ПК: {readArray(rollout, 'deviceStatuses').length}</span>
              </button>
            ))}
          </div>
          {selectedRollout && (
            <div className="settings-device-detail-grid">
              <span><strong>Публикация</strong><b>{updateComponentLabel(readString(selectedRollout, 'component'))} {readString(selectedRollout, 'version', 'версия')}</b></span>
              <span><strong>Состояние</strong><b>{updateRolloutStateLabel(readString(selectedRollout, 'state'))}</b></span>
              <span><strong>Цель</strong><b>{updateTargetKindLabel(readString(selectedRollout, 'targetKind'))} · {readNumber(selectedRollout, 'batchPercent', 0)}%</b></span>
              <span><strong>Канал</strong><b>{updateChannelLabel(readString(selectedRollout, 'channel'))}</b></span>
              <span><strong>Пакет</strong><b>{updateComponentLabel(readString(selectedRollout, 'component'))} {readString(selectedRollout, 'version', 'версия')}</b></span>
              <span><strong>Старт</strong><b>{formatTime(readString(selectedRollout, 'startsAtUtc'))}</b></span>
              <span><strong>Завершено</strong><b>{formatTime(readString(selectedRollout, 'completedAtUtc'))}</b></span>
              <span><strong>ПК</strong><b>{selectedRolloutDeviceStatuses.length}</b></span>
            </div>
          )}
          {selectedRolloutDeviceStatuses.length > 0 && (
            <div className="settings-command-history">
              {selectedRolloutDeviceStatuses.map((status) => (
                <span key={`${readString(status, 'deviceId')}-${readString(status, 'updatedAtUtc')}`}>
                  <strong>{getDeviceInventoryName(readString(status, 'deviceId'))}</strong>
                  <b>{updateDeviceStatusLabel(readString(status, 'status', 'unknown'))}</b>
                  <em>{updateDeviceMessageLabel(readString(status, 'message')) || `${readString(status, 'installedVersion')} → ${readString(status, 'targetVersion')}`}</em>
                </span>
              ))}
            </div>
          )}
          <div className="settings-form-grid settings-update-form">
            <label className="settings-form-wide">Пакет для публикации
              <select value={rolloutPackageId} disabled={!canManageUpdateRollouts || updatePackageOptions.length === 0} onChange={(event) => setRolloutPackageId(event.currentTarget.value)}>
                {updatePackageOptions.length === 0 && <option value="">сначала зарегистрируйте пакет</option>}
                {rolloutPackageId && !updatePackageOptions.some((option) => option.id === rolloutPackageId) && (
                  <option value={rolloutPackageId}>выбранный пакет</option>
                )}
                {updatePackageOptions.map((option) => (
                  <option key={option.id} value={option.id}>{option.label}</option>
                ))}
              </select>
            </label>
            <label>Канал
              <select value={rolloutChannel} disabled={!canManageUpdateRollouts} onChange={(event) => setRolloutChannel(event.currentTarget.value)}>
                <option value="internal">{updateChannelLabel('internal')}</option>
                <option value="beta">{updateChannelLabel('beta')}</option>
                <option value="stable">{updateChannelLabel('stable')}</option>
              </select>
            </label>
            <label>Цель
              <select value={rolloutTargetKind} disabled={!canManageUpdateRollouts} onChange={(event) => setRolloutTargetKind(event.currentTarget.value)}>
                <option value="branch">{updateTargetKindLabel('branch')}</option>
                <option value="device">{updateTargetKindLabel('device')}</option>
              </select>
            </label>
            <label>Доля %<input inputMode="numeric" value={rolloutBatchPercent} disabled={!canManageUpdateRollouts} onChange={(event) => setRolloutBatchPercent(event.currentTarget.value)} /></label>
            <label className="settings-form-wide">Целевые ПК
              <select multiple value={Array.from(rolloutTargetDeviceIdSet)} disabled={!canManageUpdateRollouts || rolloutTargetKind !== 'device' || deviceOptions.length === 0} onChange={(event) => setRolloutTargetDeviceIds(Array.from(event.currentTarget.selectedOptions).map((option) => option.value).join(','))}>
                {deviceOptions.length === 0 && <option value="">нет подключенных устройств</option>}
                {deviceOptions.map((device) => (
                  <option key={device.id} value={device.id}>{device.label}</option>
                ))}
              </select>
            </label>
            <label className="settings-form-wide">Начало публикации<input value={rolloutStartsAtUtc} disabled={!canManageUpdateRollouts} onChange={(event) => setRolloutStartsAtUtc(event.currentTarget.value)} /></label>
            <label className="settings-form-wide">Причина публикации<input value={rolloutReason} disabled={!canManageUpdateRollouts} onChange={(event) => setRolloutReason(event.currentTarget.value)} /></label>
          </div>

          <div className="settings-section-title">
            <span>Состояния обновлений</span>
            <button
              type="button"
              disabled={!canManageUpdatePackages}
              onClick={() => {
                setFeedback(emptyFeedback);
                setCriticalAction('package-state-change');
              }}
            >
              Изменить состояние пакета
            </button>
            <button
              type="button"
              disabled={!canManageUpdateRollouts}
              onClick={() => {
                setFeedback(emptyFeedback);
                setCriticalAction('rollout-state-change');
              }}
            >
              Изменить состояние публикации
            </button>
          </div>
          <div className="settings-form-grid settings-update-form">
            <label className="settings-form-wide">Пакет обновления
              <select value={packageStatePackageId} disabled={!canManageUpdatePackages || updatePackageOptions.length === 0} onChange={(event) => setPackageStatePackageId(event.currentTarget.value)}>
                {updatePackageOptions.length === 0 && <option value="">нет пакетов</option>}
                {packageStatePackageId && !updatePackageOptions.some((option) => option.id === packageStatePackageId) && (
                  <option value={packageStatePackageId}>выбранный пакет</option>
                )}
                {updatePackageOptions.map((option) => (
                  <option key={option.id} value={option.id}>{option.label}</option>
                ))}
              </select>
            </label>
            <label>Состояние пакета
              <select value={packageState} disabled={!canManageUpdatePackages} onChange={(event) => setPackageState(event.currentTarget.value)}>
                <option value="registered">{updatePackageStateLabel('registered')}</option>
                <option value="validated">{updatePackageStateLabel('validated')}</option>
                <option value="rejected">{updatePackageStateLabel('rejected')}</option>
                <option value="retired">{updatePackageStateLabel('retired')}</option>
              </select>
            </label>
            <label>Причина пакета<input value={packageStateReason} disabled={!canManageUpdatePackages} onChange={(event) => setPackageStateReason(event.currentTarget.value)} /></label>
            <label className="settings-form-wide">Публикация
              <select value={rolloutStateRolloutId} disabled={!canManageUpdateRollouts || rolloutOptions.length === 0} onChange={(event) => setRolloutStateRolloutId(event.currentTarget.value)}>
                {rolloutOptions.length === 0 && <option value="">нет публикаций</option>}
                {rolloutStateRolloutId && !rolloutOptions.some((option) => option.id === rolloutStateRolloutId) && (
                  <option value={rolloutStateRolloutId}>выбранная публикация</option>
                )}
                {rolloutOptions.map((option) => (
                  <option key={option.id} value={option.id}>{option.label}</option>
                ))}
              </select>
            </label>
            <label>Состояние публикации
              <select value={rolloutState} disabled={!canManageUpdateRollouts} onChange={(event) => setRolloutState(event.currentTarget.value)}>
                <option value="active">{updateRolloutStateLabel('active')}</option>
                <option value="paused">{updateRolloutStateLabel('paused')}</option>
                <option value="completed">{updateRolloutStateLabel('completed')}</option>
                <option value="rollback_requested">{updateRolloutStateLabel('rollback_requested')}</option>
                <option value="rolled_back">{updateRolloutStateLabel('rolled_back')}</option>
                <option value="cancelled">{updateRolloutStateLabel('cancelled')}</option>
              </select>
            </label>
            <label>Причина публикации<input value={rolloutStateReason} disabled={!canManageUpdateRollouts} onChange={(event) => setRolloutStateReason(event.currentTarget.value)} /></label>
          </div>
          {criticalAction === 'package-state-change' && (
            <CriticalActionConfirmation
              title="Подтвердите состояние пакета"
              detail={`${updatePackageOptions.find((option) => option.id === packageStatePackageId)?.label ?? 'Пакет'} · ${updatePackageStateLabel(packageState)}`}
              impact={`Причина будет записана в журнал: ${packageStateReason.trim() || 'не указана'}`}
              confirmLabel="Подтвердить состояние пакета"
              disabled={feedback.state === 'pending'}
              onCancel={() => setCriticalAction(null)}
              onConfirm={() => void runSettingsAction('Изменить состояние пакета')}
            />
          )}
          {criticalAction === 'rollout-state-change' && (
            <CriticalActionConfirmation
              title="Подтвердите состояние публикации"
              detail={`${rolloutOptions.find((option) => option.id === rolloutStateRolloutId)?.label ?? 'Публикация'} · ${updateRolloutStateLabel(rolloutState)}`}
              impact={`Изменение повлияет на выдачу обновлений устройствам. Причина: ${rolloutStateReason.trim() || 'не указана'}`}
              confirmLabel="Подтвердить состояние публикации"
              disabled={feedback.state === 'pending'}
              onCancel={() => setCriticalAction(null)}
              onConfirm={() => void runSettingsAction('Изменить состояние публикации')}
            />
          )}
        </>
      );
    }

    return (
      <>
        <div className="settings-form-grid">
          <label>Название клуба<input value={clubName} onChange={(event) => { setClubName(event.currentTarget.value); setSettingsDirty(true); }} /></label>
          <label>Город<input value={city} onChange={(event) => { setCity(event.currentTarget.value); setSettingsDirty(true); }} /></label>
          <label>Валюта<input value={currencyCode} readOnly /></label>
          <label>Филиал<input value={backend ? 'текущий филиал' : 'локальный режим'} readOnly /></label>
        </div>
        <div className="settings-save-row">
          <span>{settingsDirty ? 'есть несохранённые изменения' : 'изменений нет'}</span>
          <button type="button" onClick={saveSettings}>Сохранить</button>
        </div>
      </>
    );
  };

  return (
    <main className="workspace-screen settings-screen">
      <section className="screen-head settings-head">
        <div>
          <span>Настройки</span>
          <h1>Настройки · клуб и правила работы</h1>
        </div>
        <div className="screen-actions">
          <span className={`map-load-state ${loadStatus === 'backend' ? 'ready' : loadStatus}`}>{workspaceLoadStatusLabel(loadStatus, 'Настройки загружены')}</span>
        </div>
      </section>

      <section className="settings-layout">
        <aside className="settings-nav-panel">
          <span>Разделы</span>
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
              <span>Готовность клуба</span>
              <strong>срез настроек платформы</strong>
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
              <span>Быстрые настройки</span>
              <strong>частые действия администратора</strong>
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

