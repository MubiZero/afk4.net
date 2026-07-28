import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { projectOperatorError } from '../apiErrors';
import type { DeviceCommandStatusDto, DeviceInventoryItemDto, ZoneDto } from '../operatorApiClients';
import type { Feedback, OperatorBackendContext } from '../operatorTypes';
import { hasPermission, permissionNames } from '../operatorPermissions';
import {
  commandStatusLabel,
  commandStatusMessageLabel,
  commandTypeLabel,
  createAuthenticatedOperatorClients,
  formatTime,
  isGuid,
  readArray,
  readBoolean,
  readNumber,
  readString,
  requireBackend,
  triggerFeedback
} from '../operatorHelpers';
import { CriticalActionConfirmation } from '../operatorPrimitives';

// Раздел «Планировка и устройства»: управление зонами/местами, устройствами, командами, ключами.
// Родитель отдаёт серверные zones/deviceInventory/branchDeviceCommandHistory и их сеттеры + onFeedback + onReload.
export function SettingsLayoutSection({
  zones,
  deviceInventory,
  branchDeviceCommandHistory,
  backend,
  canManageLayout,
  canCreateDeviceEnrollmentCode,
  canAssignDeviceSeat,
  canViewDeviceDetail,
  canViewDeviceCommandStatus,
  canDispatchDeviceCommand,
  canRotateDeviceCredential,
  canRevokeDeviceCredential,
  onBranchDeviceCommandHistoryChange,
  onDeviceInventoryChange,
  onReload,
  onFeedback
}: {
  zones: ZoneDto[];
  deviceInventory: DeviceInventoryItemDto[];
  branchDeviceCommandHistory: DeviceCommandStatusDto[];
  backend: OperatorBackendContext | null;
  canManageLayout: boolean;
  canCreateDeviceEnrollmentCode: boolean;
  canAssignDeviceSeat: boolean;
  canViewDeviceDetail: boolean;
  canViewDeviceCommandStatus: boolean;
  canDispatchDeviceCommand: boolean;
  canRotateDeviceCredential: boolean;
  canRevokeDeviceCredential: boolean;
  onBranchDeviceCommandHistoryChange: (history: DeviceCommandStatusDto[]) => void;
  onDeviceInventoryChange: (inventory: DeviceInventoryItemDto[]) => void;
  onReload: (nextBackend: OperatorBackendContext) => Promise<void>;
  onFeedback: (feedback: Feedback) => void;
}) {
  const { t } = useI18n();

  // Локальные копии quick-action констант (дублирование намеренное — родитель держит свои для панели)
  const addSeatActionKey = t('op.settings.action.addSeat');
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
  const updateSeatActionKey = t('op.settings.action.updateSeat');
  const deleteSeatActionKey = t('op.settings.action.deleteSeat');

  const [layoutZoneName, setLayoutZoneName] = useState(() => t('op.settings.prefill.zoneName'));
  const [layoutZoneSortOrder, setLayoutZoneSortOrder] = useState('10');
  const [layoutSeatZoneId, setLayoutSeatZoneId] = useState('');
  const [layoutSeatName, setLayoutSeatName] = useState('PC-01');
  const [layoutSeatSortOrder, setLayoutSeatSortOrder] = useState('10');
  const [selectedLayoutZoneId, setSelectedLayoutZoneId] = useState('');
  const [selectedLayoutSeatId, setSelectedLayoutSeatId] = useState('');
  const [deviceAssignmentDeviceId, setDeviceAssignmentDeviceId] = useState('');
  const [deviceAssignmentSeatId, setDeviceAssignmentSeatId] = useState('');
  const [enrollmentExpiresMinutes, setEnrollmentExpiresMinutes] = useState('15');
  const [enrollmentCode, setEnrollmentCode] = useState<Record<string, unknown> | null>(null);
  const [deviceDetail, setDeviceDetail] = useState<Record<string, unknown> | null>(null);
  const [deviceCommandHistory, setDeviceCommandHistory] = useState<DeviceCommandStatusDto[]>([]);
  const [credentialIdToRevoke, setCredentialIdToRevoke] = useState('');
  const [rotatedCredential, setRotatedCredential] = useState<Record<string, unknown> | null>(null);
  const [criticalAction, setCriticalAction] = useState<
    'credential-revoke' |
    'layout-zone-delete' |
    'layout-seat-delete' |
    null
  >(null);
  const [deviceCommandType, setDeviceCommandType] = useState('lock');
  const [deviceCommandReason, setDeviceCommandReason] = useState(() => t('op.settings.prefill.deviceCommandReason'));
  const [lastDeviceCommand, setLastDeviceCommand] = useState<Record<string, unknown> | null>(null);

  // Засев выбора из загруженных данных
  useEffect(() => {
    const zoneRows = zones;
    const firstZoneId = readString(zoneRows[0], 'zoneId');
    setLayoutSeatZoneId((current) => zoneRows.some((zone) => readString(zone, 'zoneId') === current) ? current : firstZoneId);
    setSelectedLayoutZoneId((current) => zoneRows.some((zone) => readString(zone, 'zoneId') === current) ? current : '');
    const firstSeatId = zoneRows.flatMap((zone) => readArray<Record<string, unknown>>(zone, 'seats')).map((seat) => readString(seat, 'seatId')).find(Boolean) ?? '';
    setSelectedLayoutSeatId((current) => zoneRows.some((zone) => readArray<Record<string, unknown>>(zone, 'seats').some((seat) => readString(seat, 'seatId') === current)) ? current : '');
    setDeviceAssignmentSeatId((current) => isGuid(current) ? current : firstSeatId);
  }, [zones]);

  useEffect(() => {
    const nextDeviceInventory = deviceInventory;
    setDeviceAssignmentDeviceId((current) => nextDeviceInventory.some((device) => readString(device, 'deviceId') === current)
      ? current
      : readString(nextDeviceInventory[0], 'deviceId'));
  }, [deviceInventory]);

  // Сбрасывает critical action при смене устройства (было в родителе)
  useEffect(() => {
    setCriticalAction(null);
  }, [deviceAssignmentDeviceId]);

  const selectedLayoutZone = zones.find((zone) => readString(zone, 'zoneId') === selectedLayoutZoneId) ?? null;
  const selectedLayoutSeat = zones
    .flatMap((zone) => readArray<Record<string, unknown>>(zone, 'seats'))
    .find((seat) => readString(seat, 'seatId') === selectedLayoutSeatId) ?? null;
  const layoutSeatOptions = zones.flatMap((zone) => readArray<Record<string, unknown>>(zone, 'seats').map((seat) => ({
    seatId: readString(seat, 'seatId'),
    label: `${readString(zone, 'name', t('op.settings.layout.zoneFallback'))} · ${readString(seat, 'name', t('op.settings.layout.seatFallback'))}`
  }))).filter((seat) => isGuid(seat.seatId));
  const deviceRecentCommands = deviceCommandHistory.length > 0
    ? deviceCommandHistory
    : readArray<Record<string, unknown>>(deviceDetail, 'recentCommands');
  const getDeviceInventoryName = (deviceId: string) =>
    readString(deviceInventory.find((device) => readString(device, 'deviceId') === deviceId), 'machineName', t('op.settings.devices.deviceFallback'));
  const deviceOptions = deviceInventory
    .map((device) => ({
      id: readString(device, 'deviceId'),
      label: `${readString(device, 'machineName', t('op.settings.devices.deviceFallback'))} · ${readString(device, 'zoneName', t('op.settings.devices.zoneFallback'))} · ${readString(device, 'seatName', t('op.settings.devices.seatFallback'))}`
    }))
    .filter((device) => isGuid(device.id));
  const selectedDeviceLabel = getDeviceInventoryName(deviceAssignmentDeviceId);
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
    triggerFeedback(onFeedback, readString(zone, 'name', t('op.settings.layout.zoneFallback')), 'confirmed');
  };
  const selectLayoutSeat = (zone: Record<string, unknown>, seat: Record<string, unknown>) => {
    setSelectedLayoutSeatId(readString(seat, 'seatId'));
    setLayoutSeatZoneId(readString(zone, 'zoneId'));
    setLayoutSeatName(readString(seat, 'name', layoutSeatName));
    setLayoutSeatSortOrder(String(readNumber(seat, 'sortOrder', Number(layoutSeatSortOrder))));
    triggerFeedback(onFeedback, readString(seat, 'name', t('op.settings.layout.seatFallback')), 'confirmed');
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
    triggerFeedback(onFeedback, readString(device, 'machineName', t('op.settings.devices.deviceFallback')), 'confirmed');
  };

  const runAction = async (label: string) => {
    setCriticalAction(null);
    onFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      if (label === refreshCommandHistoryActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.viewDeviceCommandStatus)) {
          throw new Error(t('op.settings.devices.error.noPermCommandHistory'));
        }

        const commands = await apiClients.devices.listBranchDeviceCommands(nextBackend.branchId, { limit: 50 });
        onBranchDeviceCommandHistoryChange(Array.isArray(commands) ? commands : []);
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
        await onReload(nextBackend);
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
          onDeviceInventoryChange(await apiClients.devices.listDevices(nextBackend.branchId).catch(() => deviceInventory));
        }
        if (hasPermission(nextBackend.session, permissionNames.viewDeviceCommandStatus)) {
          const [selectedCommands, branchCommands] = await Promise.all([
            apiClients.devices.listDeviceCommands(deviceId, { limit: 25 }).catch(() => deviceCommandHistory),
            apiClients.devices.listBranchDeviceCommands(nextBackend.branchId, { limit: 50 }).catch(() => branchDeviceCommandHistory)
          ]);
          setDeviceCommandHistory(Array.isArray(selectedCommands) ? selectedCommands : []);
          onBranchDeviceCommandHistoryChange(Array.isArray(branchCommands) ? branchCommands : []);
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
          onDeviceInventoryChange(await apiClients.devices.listDevices(nextBackend.branchId).catch(() => deviceInventory));
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
          onDeviceInventoryChange(await apiClients.devices.listDevices(nextBackend.branchId).catch(() => deviceInventory));
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
        await onReload(nextBackend);
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
        await onReload(nextBackend);
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
        await onReload(nextBackend);
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
        await onReload(nextBackend);
      } else {
        throw new Error(t('op.settings.generic.error.notConnected'));
      }

      onFeedback({ label, state: 'confirmed' });
    } catch (error) {
      onFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    }
  };

  return (
    <>
      <div className="settings-section-title">
        <span>{t('op.settings.layout.title')}</span>
        <div className="settings-section-actions">
          <button type="button" disabled={!canManageLayout} onClick={() => runAction(createZoneActionKey)}>{t('op.settings.layout.createZoneBtn')}</button>
          <button type="button" disabled={!canManageLayout || !selectedLayoutZoneId} onClick={() => runAction(updateZoneActionKey)}>{updateZoneActionKey}</button>
          <button
            type="button"
            disabled={!canManageLayout || !selectedLayoutZoneId}
            onClick={() => {
              onFeedback({ label: deleteZoneActionKey, state: 'pending' });
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
        <button type="button" disabled={!canManageLayout || !layoutSeatZoneId} onClick={() => runAction(addSeatActionKey)}>{t('op.settings.layout.addSeatBtn')}</button>
        <button type="button" disabled={!canManageLayout || !selectedLayoutSeatId || !layoutSeatZoneId} onClick={() => runAction(updateSeatActionKey)}>{updateSeatActionKey}</button>
        <button
          type="button"
          disabled={!canManageLayout || !selectedLayoutSeatId}
          onClick={() => {
            onFeedback({ label: deleteSeatActionKey, state: 'pending' });
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
          disabled={false}
          onCancel={() => setCriticalAction(null)}
          onConfirm={() => void runAction(deleteZoneActionKey)}
        />
      )}
      {criticalAction === 'layout-seat-delete' && (
        <CriticalActionConfirmation
          title={t('op.settings.layout.confirmDeleteSeat.title')}
          detail={readString(selectedLayoutSeat, 'name', layoutSeatName || t('op.settings.layout.seatFallback'))}
          impact={t('op.settings.layout.confirmDeleteSeat.impact')}
          confirmLabel={t('op.settings.layout.confirmDeleteSeat.confirm')}
          disabled={false}
          onCancel={() => setCriticalAction(null)}
          onConfirm={() => void runAction(deleteSeatActionKey)}
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
        <button type="button" disabled={!canCreateDeviceEnrollmentCode} onClick={() => runAction(createEnrollmentCodeActionKey)}>{createEnrollmentCodeActionKey}</button>
        <button type="button" disabled={!canAssignDeviceSeat || layoutSeatOptions.length === 0} onClick={() => runAction(assignDeviceActionKey)}>{assignDeviceActionKey}</button>
        <button type="button" disabled={!canViewDeviceCommandStatus} onClick={() => runAction(refreshCommandHistoryActionKey)}>{refreshCommandHistoryActionKey}</button>
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
        <button type="button" disabled={!canViewDeviceDetail || !isGuid(deviceAssignmentDeviceId)} onClick={() => runAction(openDeviceCardActionKey)}>{openDeviceCardActionKey}</button>
        <label>{t('op.settings.devices.commandType')}
          <select value={deviceCommandType} disabled={!canDispatchDeviceCommand} onChange={(event) => setDeviceCommandType(event.currentTarget.value)}>
            <option value="lock">{t('op.settings.devices.commandLock')}</option>
            <option value="unlock">{t('op.settings.devices.commandUnlock')}</option>
          </select>
        </label>
        <label>{t('op.settings.devices.commandReason')}<input value={deviceCommandReason} disabled={!canDispatchDeviceCommand} onChange={(event) => setDeviceCommandReason(event.currentTarget.value)} /></label>
        <label>{t('op.settings.devices.lastCommand')}<input value={lastDeviceCommand ? `${commandTypeLabel(readString(lastDeviceCommand, 'type', 'command'), t)} · ${t('op.settings.devices.lastCommandSent')}` : t('op.settings.devices.noCommand')} readOnly /></label>
        <button type="button" disabled={!canDispatchDeviceCommand || !isGuid(deviceAssignmentDeviceId)} onClick={() => runAction(sendCommandActionKey)}>{sendCommandActionKey}</button>
        <label>{t('op.settings.devices.credentialToRevoke')}<input value={rotatedCredentialLabel} readOnly /></label>
        <label>{t('op.settings.devices.newCredential')}<input value={rotatedCredentialId ? t('op.settings.devices.newCredentialCreated') : t('op.settings.devices.newCredentialEmpty')} readOnly /></label>
        <label className="settings-form-wide">{t('op.settings.devices.enrollmentSecret')}<input value={readString(rotatedCredential, 'credentialSecret', '—')} readOnly /></label>
        <button type="button" disabled={!canRotateDeviceCredential || !isGuid(deviceAssignmentDeviceId)} onClick={() => runAction(rotateKeyActionKey)}>{rotateKeyActionKey}</button>
        <button
          type="button"
          disabled={!canRevokeDeviceCredential || !isGuid(deviceAssignmentDeviceId) || !isGuid(credentialIdToRevoke)}
          onClick={() => {
            onFeedback({ label: revokeKeyActionKey, state: 'pending' });
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
          disabled={false}
          onCancel={() => setCriticalAction(null)}
          onConfirm={() => void runAction(revokeKeyActionKey)}
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
              <strong>{commandTypeLabel(readString(command, 'type', 'command'), t)}</strong>
              <b>{commandStatusLabel(readString(command, 'status', 'unknown'), t)}</b>
              <em>{commandStatusMessageLabel(readString(command, 'message'), t) || formatTime(readString(command, 'updatedAtUtc'))}</em>
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
                  <b>{commandTypeLabel(readString(command, 'type', 'command'), t)} · {commandStatusLabel(readString(command, 'status', 'unknown'), t)}</b>
                  <em>{commandStatusMessageLabel(readString(command, 'message'), t) || formatTime(readString(command, 'updatedAtUtc'))}</em>
                </span>
              );
            })}
          </div>
        </>
      )}
    </>
  );
}
