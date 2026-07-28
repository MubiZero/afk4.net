import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { KeyRound, Lock, MonitorSmartphone, Unlock, Wifi, WifiOff } from 'lucide-react';
import { MgmtTable } from '../../kit/MgmtTable';
import { MgmtDrawer } from '../../kit/MgmtDrawer';
import { CriticalActionConfirmation, Skeleton } from '../../../operatorPrimitives';
import { hasPermission, permissionNames } from '../../../operatorPermissions';
import { projectOperatorError } from '../../../apiErrors';
import {
  createAuthenticatedOperatorClients,
  formatTime,
  isGuid,
  readBoolean,
  readNumber,
  readString,
  requireBackend
} from '../../../operatorHelpers';
import type { DeviceInventoryItemDto } from '../../../operatorApiClients';
import type { Feedback, OperatorBackendContext } from '../../../operatorTypes';

type Device = Record<string, unknown>;
type SeatOption = { seatId: string; label: string };

interface DevicesTabProps {
  deviceInventory: DeviceInventoryItemDto[];
  layoutSeatOptions: SeatOption[];
  backend: OperatorBackendContext | null;
  canAssignDeviceSeat: boolean;
  canViewDeviceDetail: boolean;
  canRotateDeviceCredential: boolean;
  canRevokeDeviceCredential: boolean;
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
export function DevicesTab({
  deviceInventory,
  layoutSeatOptions,
  backend,
  canAssignDeviceSeat,
  canViewDeviceDetail,
  canRotateDeviceCredential,
  canRevokeDeviceCredential,
  onDeviceInventoryChange,
  onReload,
  onFeedback
}: DevicesTabProps) {
  const { t } = useI18n();
  const [selectedDeviceId, setSelectedDeviceId] = useState<string | null>(null);
  const [deviceDetail, setDeviceDetail] = useState<Record<string, unknown> | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [assignSeatId, setAssignSeatId] = useState('');
  const [rotatedCredential, setRotatedCredential] = useState<Record<string, unknown> | null>(null);
  const [credentialIdToRevoke, setCredentialIdToRevoke] = useState('');
  const [criticalAction, setCriticalAction] = useState<{ kind: 'revoke' | 'remove'; deviceId: string } | null>(null);
  const [displayName, setDisplayName] = useState('');
  const [removeReason, setRemoveReason] = useState('');
  const [busy, setBusy] = useState(false);

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
      const detail = await apiClients.devices.getDeviceDetail(deviceId);
      setDeviceDetail(detail);
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
    setRotatedCredential(null);
    setCredentialIdToRevoke('');
    setDisplayName(readString(selectedDevice, 'machineName'));
    setRemoveReason('');
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
          toolbar={{ title: t('op.management.halls.devicesTable.title') }}
          empty={{
            icon: <MonitorSmartphone size={22} aria-hidden="true" />,
            title: t('op.management.halls.devicesEmpty.title'),
            description: t('op.management.halls.devicesEmpty.description')
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
                    <button type="button" className="ui-btn ui-btn--primary" disabled={busy || layoutSeatOptions.length === 0} onClick={() => void assignSeat()}>
                      {t('op.settings.action.assignDevice')}
                    </button>
                  </div>
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
                      <button type="button" className="ui-btn" disabled={busy} onClick={() => void rotateCredential()}>
                        <KeyRound size={14} aria-hidden="true" />{t('op.settings.action.rotateKey')}
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
    </>
  );
}
