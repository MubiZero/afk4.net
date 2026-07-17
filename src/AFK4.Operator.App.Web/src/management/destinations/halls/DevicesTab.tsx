import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { KeyRound, Lock, MonitorSmartphone, RefreshCw, Unlock, Wifi, WifiOff } from 'lucide-react';
import { MgmtTable } from '../../kit/MgmtTable';
import { MgmtDrawer } from '../../kit/MgmtDrawer';
import { PanelModal } from '../../../PanelModal';
import { CriticalActionConfirmation, EmptyState, Skeleton } from '../../../operatorPrimitives';
import { hasPermission, permissionNames } from '../../../operatorPermissions';
import { projectOperatorError } from '../../../apiErrors';
import {
  commandStatusLabel,
  commandStatusMessageLabel,
  commandTypeLabel,
  createAuthenticatedOperatorClients,
  formatTime,
  isGuid,
  readBoolean,
  readNumber,
  readString,
  requireBackend
} from '../../../operatorHelpers';
import type { DeviceCommandStatusDto, DeviceInventoryItemDto } from '../../../operatorApiClients';
import type { Feedback, OperatorBackendContext } from '../../../operatorTypes';

type Device = Record<string, unknown>;
type SeatOption = { seatId: string; label: string };

interface DevicesTabProps {
  deviceInventory: DeviceInventoryItemDto[];
  branchDeviceCommandHistory: DeviceCommandStatusDto[];
  layoutSeatOptions: SeatOption[];
  backend: OperatorBackendContext | null;
  canCreateDeviceEnrollmentCode: boolean;
  canAssignDeviceSeat: boolean;
  canViewDeviceDetail: boolean;
  canViewDeviceCommandStatus: boolean;
  canDispatchDeviceCommand: boolean;
  canRotateDeviceCredential: boolean;
  canRevokeDeviceCredential: boolean;
  onDeviceInventoryChange: (inventory: DeviceInventoryItemDto[]) => void;
  onBranchDeviceCommandHistoryChange: (history: DeviceCommandStatusDto[]) => void;
  onReload: (nextBackend: OperatorBackendContext) => Promise<void>;
  onFeedback: (feedback: Feedback) => void;
}

// Домен B раздела «Залы и ПК»: список устройств + drawer с карточкой/назначением/командами/
// ключами. Все 7 операций (B1-B7, см. task-B-halls-brief.md) портированы 1:1 из
// settings/SettingsLayoutSection.tsx.runAction — тот же двойной permission-гейт (can*-проп на
// секцию/кнопку + серверный hasPermission(nextBackend.session, ...) на каждый вызов), та же
// валидация, тот же onReload/onDeviceInventoryChange/onBranchDeviceCommandHistoryChange по
// операции и тот же onFeedback pending→confirmed/failed. Открытие карточки устройства (B3)
// теперь происходит автоматически при клике по строке (а не отдельной кнопкой) — так того
// требует список+drawer, но сама операция и её feedback-цикл не изменились.
export function DevicesTab({
  deviceInventory,
  branchDeviceCommandHistory,
  layoutSeatOptions,
  backend,
  canCreateDeviceEnrollmentCode,
  canAssignDeviceSeat,
  canViewDeviceDetail,
  canViewDeviceCommandStatus,
  canDispatchDeviceCommand,
  canRotateDeviceCredential,
  canRevokeDeviceCredential,
  onDeviceInventoryChange,
  onBranchDeviceCommandHistoryChange,
  onReload,
  onFeedback
}: DevicesTabProps) {
  const { t } = useI18n();
  const [selectedDeviceId, setSelectedDeviceId] = useState<string | null>(null);
  const [deviceDetail, setDeviceDetail] = useState<Record<string, unknown> | null>(null);
  const [deviceCommands, setDeviceCommands] = useState<DeviceCommandStatusDto[]>([]);
  const [detailLoading, setDetailLoading] = useState(false);
  const [assignSeatId, setAssignSeatId] = useState('');
  const [commandType, setCommandType] = useState('lock');
  const [commandReason, setCommandReason] = useState(() => t('op.settings.prefill.deviceCommandReason'));
  const [lastCommand, setLastCommand] = useState<Record<string, unknown> | null>(null);
  const [rotatedCredential, setRotatedCredential] = useState<Record<string, unknown> | null>(null);
  const [credentialIdToRevoke, setCredentialIdToRevoke] = useState('');
  const [criticalAction, setCriticalAction] = useState<{ deviceId: string } | null>(null);
  const [enrollModalOpen, setEnrollModalOpen] = useState(false);
  const [enrollMinutes, setEnrollMinutes] = useState('15');
  const [enrollmentCode, setEnrollmentCode] = useState<Record<string, unknown> | null>(null);
  const [busy, setBusy] = useState(false);
  const [enrollBusy, setEnrollBusy] = useState(false);
  const [historyBusy, setHistoryBusy] = useState(false);

  const selectedDevice = deviceInventory.find((device) => readString(device, 'deviceId') === selectedDeviceId) ?? null;

  const loadDeviceCard = async (deviceId: string) => {
    const label = t('op.settings.action.openDeviceCard');
    setDetailLoading(true);
    onFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.viewDeviceDetail)) {
        throw new Error(t('op.settings.devices.error.noPermViewDetail'));
      }
      if (!isGuid(deviceId)) {
        throw new Error(t('op.settings.devices.error.selectDevice'));
      }

      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const [detail, commands] = await Promise.all([
        apiClients.devices.getDeviceDetail(deviceId),
        hasPermission(nextBackend.session, permissionNames.viewDeviceCommandStatus)
          ? apiClients.devices.listDeviceCommands(deviceId, { limit: 25 }).catch(() => [])
          : Promise.resolve([])
      ]);
      setDeviceDetail(detail);
      setDeviceCommands(Array.isArray(commands) ? commands : []);
      onFeedback({ label, state: 'confirmed' });
    } catch (error) {
      onFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setDetailLoading(false);
    }
  };

  // Клик по устройству открывает drawer и сразу тянет карточку (B3) — если есть право и связь
  // с сервером; офлайн/без права drawer всё равно открывается, просто без карточки.
  useEffect(() => {
    if (!selectedDeviceId) return;
    const currentSeatId = readString(selectedDevice, 'seatId');
    setAssignSeatId(isGuid(currentSeatId) ? currentSeatId : (layoutSeatOptions[0]?.seatId ?? ''));
    setDeviceDetail(null);
    setDeviceCommands([]);
    setRotatedCredential(null);
    setCredentialIdToRevoke('');
    setLastCommand(null);
    if (canViewDeviceDetail && backend !== null) {
      void loadDeviceCard(selectedDeviceId);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedDeviceId]);

  const assignSeat = async () => {
    const label = t('op.settings.action.assignDevice');
    setBusy(true);
    onFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.assignDeviceSeat)) {
        throw new Error(t('op.settings.devices.error.noPermAssign'));
      }

      const deviceId = (selectedDeviceId ?? '').trim();
      const seatId = assignSeatId.trim();
      if (!isGuid(deviceId) || !isGuid(seatId)) {
        throw new Error(t('op.settings.devices.error.selectDeviceAndSeat'));
      }

      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
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
        setDeviceCommands(Array.isArray(commands) ? commands : []);
      }
      await onReload(nextBackend);
      onFeedback({ label, state: 'confirmed' });
    } catch (error) {
      onFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setBusy(false);
    }
  };

  const sendCommand = async () => {
    const label = t('op.settings.action.sendCommand');
    setBusy(true);
    onFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.dispatchDeviceCommand)) {
        throw new Error(t('op.settings.devices.error.noPermSendCommand'));
      }

      const deviceId = (selectedDeviceId ?? '').trim();
      const type = commandType.trim();
      const reason = commandReason.trim();
      if (!isGuid(deviceId) || !type || !reason) {
        throw new Error(t('op.settings.devices.error.fillCommandFields'));
      }

      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const command = await apiClients.devices.dispatchDeviceCommand(deviceId, {
        type,
        payload: { reason, source: 'operator-settings' }
      });
      setLastCommand(command);
      if (hasPermission(nextBackend.session, permissionNames.viewDeviceDetail)) {
        onDeviceInventoryChange(await apiClients.devices.listDevices(nextBackend.branchId).catch(() => deviceInventory));
      }
      if (hasPermission(nextBackend.session, permissionNames.viewDeviceCommandStatus)) {
        const [selectedCommands, branchCommands] = await Promise.all([
          apiClients.devices.listDeviceCommands(deviceId, { limit: 25 }).catch(() => deviceCommands),
          apiClients.devices.listBranchDeviceCommands(nextBackend.branchId, { limit: 50 }).catch(() => branchDeviceCommandHistory)
        ]);
        setDeviceCommands(Array.isArray(selectedCommands) ? selectedCommands : []);
        onBranchDeviceCommandHistoryChange(Array.isArray(branchCommands) ? branchCommands : []);
      }
      onFeedback({ label, state: 'confirmed' });
    } catch (error) {
      onFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setBusy(false);
    }
  };

  const rotateCredential = async () => {
    const label = t('op.settings.action.rotateKey');
    setBusy(true);
    onFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.rotateDeviceCredential)) {
        throw new Error(t('op.settings.devices.error.noPermRotateKey'));
      }

      const deviceId = (selectedDeviceId ?? '').trim();
      if (!isGuid(deviceId)) {
        throw new Error(t('op.settings.devices.error.selectDevice'));
      }

      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const rotated = await apiClients.devices.rotateDeviceCredential(deviceId);
      setRotatedCredential(rotated);
      setCredentialIdToRevoke(readString(rotated, 'credentialId'));
      if (hasPermission(nextBackend.session, permissionNames.viewDeviceDetail)) {
        onDeviceInventoryChange(await apiClients.devices.listDevices(nextBackend.branchId).catch(() => deviceInventory));
      }
      onFeedback({ label, state: 'confirmed' });
    } catch (error) {
      onFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setBusy(false);
    }
  };

  const confirmRevoke = async () => {
    if (!criticalAction) return;
    const label = t('op.settings.action.revokeKey');
    const deviceId = criticalAction.deviceId;
    setCriticalAction(null);
    onFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.revokeDeviceCredential)) {
        throw new Error(t('op.settings.devices.error.noPermRevokeKey'));
      }

      const credentialId = credentialIdToRevoke.trim();
      if (!isGuid(deviceId) || !isGuid(credentialId)) {
        throw new Error(t('op.settings.devices.error.selectDeviceAndKey'));
      }

      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      await apiClients.devices.revokeDeviceCredential(deviceId, credentialId);
      setRotatedCredential(null);
      setCredentialIdToRevoke('');
      if (hasPermission(nextBackend.session, permissionNames.viewDeviceDetail)) {
        onDeviceInventoryChange(await apiClients.devices.listDevices(nextBackend.branchId).catch(() => deviceInventory));
      }
      onFeedback({ label, state: 'confirmed' });
    } catch (error) {
      onFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    }
  };

  const submitEnrollment = async () => {
    const label = t('op.settings.action.createEnrollmentCode');
    setEnrollBusy(true);
    onFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.createDeviceEnrollmentCode)) {
        throw new Error(t('op.settings.devices.error.noPermEnrollment'));
      }

      const expiresInMinutes = Number(enrollMinutes);
      if (!Number.isInteger(expiresInMinutes) || expiresInMinutes < 1 || expiresInMinutes > 1440) {
        throw new Error(t('op.settings.devices.error.enrollmentRange'));
      }

      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const code = await apiClients.devices.createEnrollmentCode(nextBackend.branchId, nextBackend.session.organizationId, expiresInMinutes * 60);
      setEnrollmentCode(code);
      onFeedback({ label, state: 'confirmed' });
    } catch (error) {
      onFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setEnrollBusy(false);
    }
  };

  const refreshBranchHistory = async () => {
    const label = t('op.settings.action.refreshCommandHistory');
    setHistoryBusy(true);
    onFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.viewDeviceCommandStatus)) {
        throw new Error(t('op.settings.devices.error.noPermCommandHistory'));
      }

      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const commands = await apiClients.devices.listBranchDeviceCommands(nextBackend.branchId, { limit: 50 });
      onBranchDeviceCommandHistoryChange(Array.isArray(commands) ? commands : []);
      onFeedback({ label, state: 'confirmed' });
    } catch (error) {
      onFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setHistoryBusy(false);
    }
  };

  const openEnrollModal = () => {
    setEnrollMinutes('15');
    setEnrollmentCode(null);
    setEnrollModalOpen(true);
  };

  const selectedDeviceName = readString(selectedDevice, 'machineName', t('op.settings.devices.deviceFallback'));
  const rotatedCredentialLabel = isGuid(credentialIdToRevoke)
    ? t('op.settings.devices.credentialRotated', { deviceName: selectedDeviceName })
    : t('op.settings.devices.credentialEmpty');

  return (
    <>
      <div className="mgmt-master-detail">
        <MgmtTable<Device>
          columns={[
            { key: 'device', header: t('op.settings.devices.device'), render: (device) => readString(device, 'machineName', t('op.settings.devices.deviceFallback')) },
            {
              key: 'status',
              header: t('op.settings.devices.detail.status'),
              render: (device) => {
                const online = readBoolean(device, 'isOnline');
                const locked = readBoolean(device, 'isLocked');
                return (
                  <span className="mgmt-status-pair">
                    {online ? <Wifi size={13} aria-hidden="true" /> : <WifiOff size={13} aria-hidden="true" />}
                    {online ? t('op.settings.devices.online') : t('op.settings.devices.offline')}
                    <span aria-hidden="true">·</span>
                    {locked ? <Lock size={13} aria-hidden="true" /> : <Unlock size={13} aria-hidden="true" />}
                    {locked ? t('op.settings.devices.locked') : t('op.settings.devices.unlocked')}
                  </span>
                );
              }
            },
            {
              key: 'location',
              header: t('op.management.halls.col.location'),
              render: (device) => `${readString(device, 'zoneName', t('op.settings.devices.zoneFallback'))} · ${readString(device, 'seatName', t('op.settings.devices.seatFallback'))}`
            },
            {
              key: 'health',
              header: t('op.management.halls.col.health'),
              align: 'end',
              render: (device) => t('op.settings.devices.deviceSummary', {
                agentVersion: readString(device, 'agentVersion', '—'),
                appCount: readNumber(device, 'installedAppCount', 0),
                pending: readNumber(device, 'pendingCommandCount', 0),
                failed: readNumber(device, 'failedCommandCount', 0),
                lastHeartbeat: formatTime(readString(device, 'lastHeartbeatAtUtc'))
              })
            }
          ]}
          rows={deviceInventory}
          rowKey={(device) => readString(device, 'deviceId')}
          gridTemplate="1.1fr 160px 1fr 1.7fr"
          selectedKey={selectedDeviceId}
          onSelectRow={(device) => setSelectedDeviceId(readString(device, 'deviceId'))}
          toolbar={{
            title: t('op.management.halls.devicesTable.title'),
            primary: canCreateDeviceEnrollmentCode ? { label: t('op.management.halls.connectDeviceCta'), onClick: openEnrollModal } : undefined
          }}
          empty={{
            icon: <MonitorSmartphone size={22} aria-hidden="true" />,
            title: t('op.management.halls.devicesEmpty.title'),
            description: t('op.settings.devices.emptyInventory'),
            action: canCreateDeviceEnrollmentCode ? { label: t('op.management.halls.connectDeviceCta'), onClick: openEnrollModal } : undefined
          }}
        />

        {selectedDevice && (
          <MgmtDrawer
            title={selectedDeviceName}
            subtitle={`${readString(selectedDevice, 'zoneName', t('op.settings.devices.zoneFallback'))} · ${readString(selectedDevice, 'seatName', t('op.settings.devices.seatFallback'))}`}
            onClose={() => setSelectedDeviceId(null)}
          >
            <div className="mgmt-drawer-section">
              <div className="mgmt-section-title"><span>{t('op.settings.devices.deviceCard')}</span></div>
              {detailLoading ? (
                <Skeleton variant="text" lines={4} />
              ) : deviceDetail ? (
                <div className="settings-device-detail-grid">
                  <span><strong>{t('op.settings.devices.detail.status')}</strong><b>{readBoolean(deviceDetail, 'isOnline') ? t('op.settings.devices.online') : t('op.settings.devices.offline')} · {readBoolean(deviceDetail, 'isLocked') ? t('op.settings.devices.locked') : t('op.settings.devices.unlocked')}</b></span>
                  <span><strong>{t('op.settings.devices.detail.heartbeat')}</strong><b>{formatTime(readString(deviceDetail, 'lastHeartbeatAtUtc'))}</b></span>
                  <span><strong>{t('op.settings.devices.detail.agent')}</strong><b>{readString(deviceDetail, 'agentVersion', '—')}</b></span>
                  <span><strong>{t('op.settings.devices.detail.shell')}</strong><b>{readString(deviceDetail, 'shellVersion', '—')}</b></span>
                  <span><strong>{t('op.settings.devices.detail.credentials')}</strong><b>{readNumber(deviceDetail, 'activeCredentialCount', 0)}</b></span>
                  <span><strong>{t('op.settings.devices.detail.apps')}</strong><b>{readNumber(deviceDetail, 'installedAppCount', 0)}</b></span>
                </div>
              ) : (
                <p className="mgmt-drawer-hint">{t('op.settings.devices.deviceCardNotOpen')}</p>
              )}
            </div>

            {canAssignDeviceSeat && (
              <div className="mgmt-drawer-section">
                <div className="mgmt-section-title"><span>{t('op.management.halls.deviceDrawer.assignSection')}</span></div>
                <div className="mgmt-form">
                  <label>{t('op.settings.devices.seat')}
                    <select value={assignSeatId} disabled={busy || layoutSeatOptions.length === 0} onChange={(event) => setAssignSeatId(event.currentTarget.value)}>
                      {layoutSeatOptions.length === 0 && <option value="">{t('op.settings.devices.noSeats')}</option>}
                      {layoutSeatOptions.map((seat) => <option key={seat.seatId} value={seat.seatId}>{seat.label}</option>)}
                    </select>
                  </label>
                  <div className="mgmt-form-actions">
                    <button type="button" className="ui-btn ui-btn--primary" disabled={busy || layoutSeatOptions.length === 0} onClick={() => void assignSeat()}>
                      {t('op.settings.action.assignDevice')}
                    </button>
                  </div>
                </div>
              </div>
            )}

            {(canDispatchDeviceCommand || canViewDeviceCommandStatus) && (
              <div className="mgmt-drawer-section">
                <div className="mgmt-section-title"><span>{t('op.management.halls.deviceDrawer.commandsSection')}</span></div>
                {canDispatchDeviceCommand && (
                  <div className="mgmt-form">
                    <label>{t('op.settings.devices.commandType')}
                      <select value={commandType} disabled={busy} onChange={(event) => setCommandType(event.currentTarget.value)}>
                        <option value="lock">{t('op.settings.devices.commandLock')}</option>
                        <option value="unlock">{t('op.settings.devices.commandUnlock')}</option>
                      </select>
                    </label>
                    <label>{t('op.settings.devices.commandReason')}
                      <input value={commandReason} disabled={busy} onChange={(event) => setCommandReason(event.currentTarget.value)} />
                    </label>
                    <div className="mgmt-form-actions">
                      <button type="button" className="ui-btn ui-btn--primary" disabled={busy} onClick={() => void sendCommand()}>
                        {t('op.settings.action.sendCommand')}
                      </button>
                    </div>
                  </div>
                )}
                {lastCommand && (
                  <p className="mgmt-drawer-hint">{t('op.settings.devices.lastCommand')}: {commandTypeLabel(readString(lastCommand, 'type', 'command'), t)} · {t('op.settings.devices.lastCommandSent')}</p>
                )}
                {canViewDeviceCommandStatus && (
                  deviceCommands.length > 0 ? (
                    <div className="settings-command-history">
                      <div className="mgmt-section-title"><span>{t('op.management.halls.deviceDrawer.recentCommands')}</span></div>
                      {deviceCommands.map((command) => (
                        <span key={readString(command, 'commandId')}>
                          <strong>{commandTypeLabel(readString(command, 'type', 'command'), t)}</strong>
                          <b>{commandStatusLabel(readString(command, 'status', 'unknown'), t)}</b>
                          <em>{commandStatusMessageLabel(readString(command, 'message'), t) || formatTime(readString(command, 'updatedAtUtc'))}</em>
                        </span>
                      ))}
                    </div>
                  ) : (
                    <p className="mgmt-drawer-hint">{t('op.management.halls.deviceDrawer.noRecentCommands')}</p>
                  )
                )}
              </div>
            )}

            {(canRotateDeviceCredential || canRevokeDeviceCredential) && (
              <div className="mgmt-drawer-section">
                <div className="mgmt-section-title"><span>{t('op.management.halls.deviceDrawer.keysSection')}</span></div>
                <div className="mgmt-form">
                  <label>{t('op.settings.devices.credentialToRevoke')}
                    <input readOnly value={rotatedCredentialLabel} />
                  </label>
                  {rotatedCredential && (
                    <label className="mgmt-form-wide">{t('op.management.halls.deviceDrawer.credentialSecret')}
                      <input readOnly value={readString(rotatedCredential, 'credentialSecret', '—')} />
                    </label>
                  )}
                  <div className="mgmt-form-actions">
                    {canRotateDeviceCredential && (
                      <button type="button" className="ui-btn" disabled={busy} onClick={() => void rotateCredential()}>
                        <KeyRound size={14} aria-hidden="true" />{t('op.settings.action.rotateKey')}
                      </button>
                    )}
                    {canRevokeDeviceCredential && (
                      <button
                        type="button"
                        className="ui-btn ui-btn--danger"
                        disabled={busy || !isGuid(credentialIdToRevoke)}
                        onClick={() => setCriticalAction({ deviceId: selectedDeviceId ?? '' })}
                      >
                        {t('op.settings.action.revokeKey')}
                      </button>
                    )}
                  </div>
                </div>
              </div>
            )}
          </MgmtDrawer>
        )}
      </div>

      <div className="mgmt-drawer-section" style={{ marginTop: 14 }}>
        <div className="mgmt-section-title">
          <span>{t('op.settings.devices.branchHistory')}</span>
          <button type="button" className="ui-btn ui-btn--ghost" disabled={!canViewDeviceCommandStatus || historyBusy} onClick={() => void refreshBranchHistory()}>
            <RefreshCw size={14} aria-hidden="true" />{t('op.settings.action.refreshCommandHistory')}
          </button>
        </div>
        {branchDeviceCommandHistory.length > 0 ? (
          <div className="settings-command-history" aria-label={t('op.settings.devices.branchHistory')}>
            {branchDeviceCommandHistory.map((command) => {
              const deviceId = readString(command, 'deviceId');
              const deviceName = readString(deviceInventory.find((device) => readString(device, 'deviceId') === deviceId), 'machineName', t('op.settings.devices.deviceFallback'));
              return (
                <span key={`${deviceId}-${readString(command, 'commandId')}`}>
                  <strong>{deviceName}</strong>
                  <b>{commandTypeLabel(readString(command, 'type', 'command'), t)} · {commandStatusLabel(readString(command, 'status', 'unknown'), t)}</b>
                  <em>{commandStatusMessageLabel(readString(command, 'message'), t) || formatTime(readString(command, 'updatedAtUtc'))}</em>
                </span>
              );
            })}
          </div>
        ) : (
          <EmptyState title={t('op.management.halls.deviceDrawer.noRecentCommands')} />
        )}
      </div>

      {enrollModalOpen && (
        <PanelModal title={t('op.management.halls.enrollModal.title')} onClose={() => setEnrollModalOpen(false)} closeDisabled={enrollBusy}>
          <form className="mgmt-form" onSubmit={(event) => { event.preventDefault(); void submitEnrollment(); }}>
            <div className="mgmt-form-grid">
              <label>{t('op.settings.devices.enrollmentMinutes')}
                <input inputMode="numeric" value={enrollMinutes} onChange={(event) => setEnrollMinutes(event.currentTarget.value)} autoFocus />
              </label>
              {enrollmentCode && (
                <label className="mgmt-form-wide">{t('op.settings.devices.enrollmentCode')}
                  <input readOnly value={readString(enrollmentCode, 'code', '—')} />
                </label>
              )}
            </div>
            <div className="mgmt-form-actions">
              <button type="button" className="ui-btn" onClick={() => setEnrollModalOpen(false)} disabled={enrollBusy}>{t('common.close')}</button>
              <button type="submit" className="ui-btn ui-btn--primary" disabled={enrollBusy}>{t('op.settings.action.createEnrollmentCode')}</button>
            </div>
          </form>
        </PanelModal>
      )}

      {criticalAction && (
        <CriticalActionConfirmation
          title={t('op.settings.devices.confirmRevokeKey.title')}
          detail={t('op.settings.devices.confirmRevokeKey.detail', { deviceName: selectedDeviceName })}
          impact={t('op.settings.devices.confirmRevokeKey.impact')}
          confirmLabel={t('op.settings.devices.confirmRevokeKey.confirm')}
          onCancel={() => setCriticalAction(null)}
          onConfirm={() => void confirmRevoke()}
        />
      )}
    </>
  );
}
