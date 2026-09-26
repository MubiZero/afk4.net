import { useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { KeyRound, Lock, MonitorSmartphone, Unlock, Wifi, WifiOff } from 'lucide-react';
import { MgmtTable } from '../../kit/MgmtTable';
import { SkeletonTable } from '../../../LoadingSkeleton';
import { MgmtDrawer } from '../../kit/MgmtDrawer';
import { PendingDevicesSection } from './PendingDevicesSection';
import { DeviceProtectionReport } from './DeviceProtectionReport';
import { DeviceHardwareSection } from './DeviceHardwareSection';
import { ConsoleSeatDialog } from './ConsoleSeatDialog';
import { commandOutcomeLabelKey } from './deviceCommandOutcomes';
import { CriticalActionConfirmation, EmptyState, Skeleton } from '../../../operatorPrimitives';
import { hasPermission, permissionNames } from '../../../operatorPermissions';
import { projectOperatorError } from '../../../apiErrors';
import {
  commandStatusLabel,
  commandStatusMessageLabel,
  commandTypeLabel,
  createAuthenticatedOperatorClients,
  formatDateTime,
  formatTime,
  isGuid,
  readBoolean,
  readNumber,
  readString,
  requireBackend
} from '../../../operatorHelpers';
import type {
  DeviceCommandStatusDto,
  DeviceDetailDto,
  DeviceInventoryItemDto,
  RotateDeviceCredentialResponse
} from '../../../operatorApiClients';
import type { Feedback, OperatorBackendContext } from '../../../operatorTypes';
import { useBlockedReason } from '../../../components/BlockedReason';

// Настоящий тип, а не `Record<string, unknown>`: таблица получает те же строки, что приходят с
// сервера, и поле, которого в ответе нет, теперь заметит компилятор.
type Device = DeviceInventoryItemDto;

// Статус — по ширине своих слов («онлайн · разблокирован» не лезла в прежние 160 px на русском),
// остальным — доли того, что осталось.
const DEVICES_GRID = 'minmax(0, 1.1fr) max-content minmax(0, 1fr) minmax(0, 1.7fr)';

// Форма вкладки, пока ПК грузятся. Полосу «ждут подтверждения» не рисуем: её может и не быть.
export function DevicesTabSkeleton() {
  return (
    <div className="mgmt-master-detail">
      <SkeletonTable gridTemplate={DEVICES_GRID} toolbar={{}} />
    </div>
  );
}
type SeatOption = { seatId: string; label: string };

interface DevicesTabProps {
  deviceInventory: DeviceInventoryItemDto[];
  layoutSeatOptions: SeatOption[];
  backend: OperatorBackendContext | null;
  canAssignDeviceSeat: boolean;
  canViewDeviceDetail: boolean;
  /// Журнал отправленных этому ПК команд. Отдельное право: статус команд сервер отдаёт по нему,
  /// а не по праву на карточку устройства.
  canViewDeviceCommands: boolean;
  canRotateDeviceCredential: boolean;
  canRevokeDeviceCredential: boolean;
  canManageBranchSettings: boolean;
  onDeviceInventoryChange: (inventory: DeviceInventoryItemDto[]) => void;
  onReload: (nextBackend: OperatorBackendContext) => Promise<void>;
  onFeedback: (feedback: Feedback) => void;
}

// Домен B раздела «Залы и ПК»: список устройств + drawer с карточкой/назначением/ключами.
// Подключение устройств живёт в Мастере настройки, а lock/unlock — на Карте (см.
// task-B2-halls-rework-brief.md) — здесь остались только B2 (назначение на место), B3 (карточка
// устройства через getDeviceDetail) и B6/B7 (выдать/отозвать ключ), каждая за своим двойным
// permission-гейтом (can*-проп на секцию/кнопку + серверный hasPermission(nextBackend.session, ...)
// на каждый вызов). Открытие карточки устройства (B3) происходит автоматически при клике по
// строке — так того требует список+drawer.
function commandDetail(command: DeviceCommandStatusDto, t: ReturnType<typeof useI18n>['t']) {
  const outcomeKey = commandOutcomeLabelKey(command.outcome);
  if (outcomeKey !== null) return <em>{t(outcomeKey)}</em>;

  return command.message ? <em>{commandStatusMessageLabel(command.message, t)}</em> : null;
}

export function DevicesTab({
  deviceInventory,
  layoutSeatOptions,
  backend,
  canAssignDeviceSeat,
  canViewDeviceDetail,
  canViewDeviceCommands,
  canRotateDeviceCredential,
  canRevokeDeviceCredential,
  canManageBranchSettings,
  onDeviceInventoryChange,
  onReload,
  onFeedback
}: DevicesTabProps) {
  const { t } = useI18n();
  // Привязать ПК некуда, пока в филиале нет мест, — и кнопка гасла без слова о том, где их завести.
  const assignBlocked = useBlockedReason(layoutSeatOptions.length === 0 ? t('op.settings.devices.noSeatsHint') : null);
  const [selectedDeviceId, setSelectedDeviceId] = useState<string | null>(null);
  const [deviceDetail, setDeviceDetail] = useState<DeviceDetailDto | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [commands, setCommands] = useState<DeviceCommandStatusDto[] | null>(null);
  const [commandsLoading, setCommandsLoading] = useState(false);
  const [assignSeatId, setAssignSeatId] = useState('');
  const [rotatedCredential, setRotatedCredential] = useState<RotateDeviceCredentialResponse | null>(null);
  const [credentialIdToRevoke, setCredentialIdToRevoke] = useState('');
  const [criticalAction, setCriticalAction] = useState<{ kind: 'revoke' | 'remove'; deviceId: string } | null>(null);
  const [displayName, setDisplayName] = useState('');
  const [removeReason, setRemoveReason] = useState('');
  const [busy, setBusy] = useState(false);
  const [consoleOpen, setConsoleOpen] = useState(false);
  // Консоль ставится только на место без устройства: две машины на месте — две правды о сессии.
  const takenSeatIds = new Set(deviceInventory
    .filter((device) => device.enrollmentState !== 'removed' && device.enrollmentState !== 'rejected')
    .map((device) => device.seatId)
    .filter((seatId): seatId is string => Boolean(seatId)));
  const freeSeats = layoutSeatOptions.filter((seat) => !takenSeatIds.has(seat.seatId));

  const createConsole = async (seatId: string, name: string) => {
    const label = t('op.settings.devices.console.create');
    setBusy(true);
    onFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      await apiClients.settings.createConsoleSeat(nextBackend.branchId, {
        organizationId: nextBackend.session.organizationId,
        seatId,
        displayName: name
      });
      setConsoleOpen(false);
      await onReload(nextBackend);
      onFeedback({ label, state: 'confirmed' });
    } catch (error) {
      onFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setBusy(false);
    }
  };

  const selectedDevice = deviceInventory.find((device) => readString(device, 'deviceId') === selectedDeviceId) ?? null;
  // Один набор клиентов на сессию: раздел «Железо» грузится заново только при смене ПК.
  const hardwareClients = useMemo(
    () => (backend ? createAuthenticatedOperatorClients(backend.config, backend.session) : null),
    [backend?.config, backend?.session]
  );

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
      const detail = await apiClients.devices.getDeviceDetail(deviceId);
      setDeviceDetail(detail);
      onFeedback({ label, state: 'confirmed' });
    } catch (error) {
      onFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setDetailLoading(false);
    }
  };

  // Что этому ПК отправляли раньше. Без журнала при разборе «команда не сработала» видно
  // только состояние последней команды, и понять, сколько раз её слали и чем каждая кончилась,
  // было неоткуда — хотя сервер этот список отдаёт.
  const loadCommandHistory = async (deviceId: string) => {
    setCommandsLoading(true);
    try {
      const nextBackend = requireBackend(backend, t);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      setCommands(await apiClients.devices.listDeviceCommands(deviceId, { limit: 20 }));
    } catch (error) {
      // Журнал — это подсказка при разборе, а не сама работа: его недоступность не должна
      // закрывать карточку устройства сообщением об ошибке поверх всего.
      onFeedback({ label: t('op.settings.devices.commands.title'), state: 'failed', detail: projectOperatorError(error, t).detail });
      setCommands([]);
    } finally {
      setCommandsLoading(false);
    }
  };

  // Клик по устройству открывает drawer и сразу тянет карточку (B3) — если есть право и связь
  // с сервером; офлайн/без права drawer всё равно открывается, просто без карточки.
  useEffect(() => {
    if (!selectedDeviceId) return;
    const currentSeatId = readString(selectedDevice, 'seatId');
    setAssignSeatId(isGuid(currentSeatId) ? currentSeatId : (layoutSeatOptions[0]?.seatId ?? ''));
    setDeviceDetail(null);
    setCommands(null);
    setRotatedCredential(null);
    setCredentialIdToRevoke('');
    setDisplayName(readString(selectedDevice, 'machineName'));
    setRemoveReason('');
    if (canViewDeviceDetail && backend !== null) {
      void loadDeviceCard(selectedDeviceId);
    }
    if (canViewDeviceCommands && backend !== null) {
      void loadCommandHistory(selectedDeviceId);
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
        setDeviceDetail(await apiClients.devices.getDeviceDetail(deviceId));
      }
      await onReload(nextBackend);
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

  /// Гигиена ключа: машина сменит его сама и останется на связи. Отдельная кнопка рядом с
  /// «Выдать новый ключ» — потому что тот отрезает ПК до тех пор, пока ключ не впишут руками,
  /// и годится для украденной машины, а не для планового обновления.
  const requestRotation = async () => {
    const label = t('op.settings.action.requestKeyRotation');
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
      await apiClients.devices.requestDeviceCredentialRotation(deviceId);
      onFeedback({ label, state: 'confirmed', detail: t('op.settings.devices.keyRotationRequested') });
    } catch (error) {
      onFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setBusy(false);
    }
  };

  const confirmRevoke = async () => {
    if (!criticalAction || criticalAction.kind !== 'revoke') return;
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

  const renameDevice = async () => {
    const label = t('op.settings.devices.rename');
    setBusy(true);
    onFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.assignDeviceSeat)) throw new Error(t('op.settings.devices.error.noPermAssign'));
      const deviceId = selectedDeviceId ?? '';
      const nextName = displayName.trim();
      if (!isGuid(deviceId) || !nextName) throw new Error(t('op.settings.devices.renameRequired'));
      await createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session).devices.renameDevice(deviceId, {
        organizationId: nextBackend.session.organizationId, displayName: nextName
      });
      await onReload(nextBackend);
      onFeedback({ label, state: 'confirmed' });
    } catch (error) {
      onFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally { setBusy(false); }
  };

  const confirmRemove = async () => {
    if (!criticalAction || criticalAction.kind !== 'remove') return;
    const deviceId = criticalAction.deviceId;
    setCriticalAction(null);
    const label = t('op.settings.devices.remove');
    onFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.revokeDeviceCredential)) throw new Error(t('op.settings.devices.error.noPermRevokeKey'));
      const reason = removeReason.trim();
      if (!isGuid(deviceId) || !reason) throw new Error(t('op.settings.devices.removeReasonRequired'));
      await createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session).devices.removeDevice(deviceId, {
        organizationId: nextBackend.session.organizationId, reason
      });
      setSelectedDeviceId(null);
      await onReload(nextBackend);
      onFeedback({ label, state: 'confirmed' });
    } catch (error) {
      onFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    }
  };

  const selectedDeviceName = readString(selectedDevice, 'machineName', t('op.settings.devices.deviceFallback'));
  const rotatedCredentialLabel = isGuid(credentialIdToRevoke)
    ? t('op.settings.devices.credentialRotated', { deviceName: selectedDeviceName })
    : t('op.settings.devices.credentialEmpty');

  return (
    <>
      <PendingDevicesSection
        backend={backend}
        canViewDeviceDetail={canViewDeviceDetail}
        canApproveDevice={canAssignDeviceSeat}
        canManageBranchSettings={canManageBranchSettings}
        onApproved={onReload}
        onFeedback={onFeedback}
      />
      <div className="mgmt-master-detail">
        <MgmtTable<Device>
          columns={[
            { key: 'device', header: t('op.settings.devices.device'), render: (device) => readString(device, 'machineName', t('op.settings.devices.deviceFallback')) },
            {
              key: 'status',
              header: t('op.settings.devices.detail.status'),
              render: (device) => {
                if (device.role === 'console') {
                  return <span className="mgmt-status-pair">{t('op.settings.devices.console')} · {t('op.settings.devices.console.noAgent')}</span>;
                }
                const online = readBoolean(device, 'isOnline');
                const locked = readBoolean(device, 'isLocked');
                return (
                  <span className="mgmt-status-pair">
                    {online ? <Wifi size={13} aria-hidden="true" /> : <WifiOff size={13} aria-hidden="true" />}
                    {online ? t('op.settings.devices.online') : t('op.settings.devices.offline')}
                    <span aria-hidden="true">·</span>
                    {locked ? <Lock size={13} aria-hidden="true" /> : <Unlock size={13} aria-hidden="true" />}
                    {locked ? t('op.settings.devices.locked') : t('op.settings.devices.unlocked')}
                    {device.hardwareChanged && <span className="ui-chip ui-chip--status ui-chip--xs is-warning">{t('op.hardware.changedChip')}</span>}
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
              render: (device) => device.role === 'console' ? '—' : t('op.settings.devices.deviceSummary', {
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
          gridTemplate={DEVICES_GRID}
          selectedKey={selectedDeviceId}
          onSelectRow={(device) => setSelectedDeviceId(readString(device, 'deviceId'))}
          toolbar={{
            title: t('op.management.halls.devicesTable.title'),
            primary: canAssignDeviceSeat ? { label: t('op.settings.devices.addConsole'), onClick: () => setConsoleOpen(true) } : undefined
          }}
          empty={{
            icon: <MonitorSmartphone size={22} aria-hidden="true" />,
            title: t('op.management.halls.devicesEmpty.title'),
            // Устройство здесь не создаётся: ПК встаёт в список сам, когда его подключили Мастером.
            next: { kind: 'elsewhere', hint: t('op.management.halls.devicesEmpty.description') }
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

            {deviceDetail && !detailLoading && (
              <div className="mgmt-drawer-section">
                <div className="mgmt-section-title"><span>{t('op.settings.devices.protectionReport')}</span></div>
                <DeviceProtectionReport report={deviceDetail.protectionReport} branchVersion={deviceDetail.branchProtectionVersion ?? 0} />
              </div>
            )}

            {canViewDeviceDetail && (
              <div className="mgmt-drawer-section">
                <div className="mgmt-section-title"><span>{t('op.hardware.title')}</span></div>
                <DeviceHardwareSection
                  clients={hardwareClients}
                  deviceId={readString(selectedDevice, 'deviceId')}
                  canAccept={hasPermission(backend?.session ?? null, permissionNames.acceptDeviceHardware)}
                  onAccepted={() => { if (backend) void onReload(backend); }}
                />
              </div>
            )}

            {canViewDeviceCommands && (
              <div className="mgmt-drawer-section">
                <div className="mgmt-section-title"><span>{t('op.settings.devices.commands.title')}</span></div>
                {commandsLoading ? (
                  <Skeleton variant="text" lines={3} />
                ) : commands !== null && commands.length > 0 ? (
                  <ul className="settings-device-commands">
                    {commands.map((command) => (
                      <li key={command.commandId}>
                        <span>{commandTypeLabel(command.type, t)}</span>
                        <b>{commandStatusLabel(command.status, t)}</b>
                        <small>{formatDateTime(command.updatedAtUtc)}</small>
                        {commandDetail(command, t)}
                      </li>
                    ))}
                  </ul>
                ) : (
                  <EmptyState
                    inline
                    className="mgmt-drawer-hint"
                    title={t('op.settings.devices.commands.empty')}
                    next={{ kind: 'calm', hint: t('op.settings.devices.commands.emptyHint') }}
                  />
                )}
              </div>
            )}

            {(canAssignDeviceSeat || canRevokeDeviceCredential) && <div className="mgmt-drawer-section">
              <div className="mgmt-section-title"><span>{t('op.settings.devices.lifecycle')}</span></div>
              <div className="mgmt-form">
                {canAssignDeviceSeat && <label>{t('op.settings.devices.displayName')}
                  <input value={displayName} disabled={busy} onChange={(event) => setDisplayName(event.currentTarget.value)} />
                </label>}
                {canAssignDeviceSeat && <button type="button" className="ui-btn" disabled={busy || !displayName.trim()} onClick={() => void renameDevice()}>{t('op.settings.devices.rename')}</button>}
                {canRevokeDeviceCredential && <label>{t('op.settings.devices.removeReason')}
                  <input value={removeReason} disabled={busy} onChange={(event) => setRemoveReason(event.currentTarget.value)} />
                </label>}
                {canRevokeDeviceCredential && <button type="button" className="ui-btn ui-btn--danger" disabled={busy || !removeReason.trim()} onClick={() => setCriticalAction({ kind: 'remove', deviceId: selectedDeviceId ?? '' })}>{t('op.settings.devices.remove')}</button>}
              </div>
            </div>}

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
                    <button type="button" className="ui-btn ui-btn--primary" disabled={busy || layoutSeatOptions.length === 0} aria-describedby={assignBlocked.describedBy} onClick={() => void assignSeat()}>
                      {t('op.settings.action.assignDevice')}
                    </button>
                  </div>
                  {assignBlocked.hint}
                </div>
              </div>
            )}

            {(canRotateDeviceCredential || canRevokeDeviceCredential) && (
              <div className="mgmt-drawer-section">
                <div className="mgmt-section-title"><span>{t('op.management.halls.deviceDrawer.securitySection')}</span></div>
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
                      <button type="button" className="ui-btn" disabled={busy} onClick={() => void requestRotation()}>
                        <KeyRound size={14} aria-hidden="true" />{t('op.settings.action.requestKeyRotation')}
                      </button>
                    )}
                    {canRotateDeviceCredential && (
                      <button type="button" className="ui-btn" disabled={busy} onClick={() => void rotateCredential()}>
                        {t('op.settings.action.rotateKey')}
                      </button>
                    )}
                    {canRevokeDeviceCredential && (
                      <button
                        type="button"
                        className="ui-btn ui-btn--danger"
                        disabled={busy || !isGuid(credentialIdToRevoke)}
                        onClick={() => setCriticalAction({ kind: 'revoke', deviceId: selectedDeviceId ?? '' })}
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

      {criticalAction?.kind === 'revoke' && (
        <CriticalActionConfirmation
          title={t('op.settings.devices.confirmRevokeKey.title')}
          detail={t('op.settings.devices.confirmRevokeKey.detail', { deviceName: selectedDeviceName })}
          impact={t('op.settings.devices.confirmRevokeKey.impact')}
          confirmLabel={t('op.settings.devices.confirmRevokeKey.confirm')}
          onCancel={() => setCriticalAction(null)}
          onConfirm={() => void confirmRevoke()}
        />
      )}
      {criticalAction?.kind === 'remove' && (
        <CriticalActionConfirmation
          title={t('op.settings.devices.removeConfirmTitle')}
          detail={selectedDeviceName}
          impact={removeReason.trim()}
          confirmLabel={t('op.settings.devices.remove')}
          onCancel={() => setCriticalAction(null)}
          onConfirm={() => void confirmRemove()}
        />
      )}
      {consoleOpen && (
        <ConsoleSeatDialog
          freeSeats={freeSeats}
          busy={busy}
          onSubmit={(seatId, name) => void createConsole(seatId, name)}
          onClose={() => setConsoleOpen(false)}
        />
      )}
    </>
  );
}
