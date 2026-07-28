import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { projectOperatorError } from '../apiErrors';
import type { DeviceInventoryItemDto, UpdatePackageDto, UpdateRolloutStatusDto } from '../operatorApiClients';
import type { Feedback, OperatorBackendContext } from '../operatorTypes';
import { hasPermission, permissionNames } from '../operatorPermissions';
import {
  createAuthenticatedOperatorClients,
  createIdempotencyKey,
  formatTime,
  isGuid,
  readArray,
  readNumber,
  readString,
  requireBackend,
  updateChannelLabel,
  updateComponentLabel,
  updateDeviceMessageLabel,
  updateDeviceStatusLabel,
  updatePackageStateLabel,
  updateRolloutStateLabel,
  updateTargetKindLabel
} from '../operatorHelpers';
import { CriticalActionConfirmation } from '../operatorPrimitives';

// Раздел «Интеграции»: управление пакетами обновлений, роллаутами, состояниями пакетов/роллаутов.
// Родитель отдаёт серверные rollouts/registeredUpdatePackages/deviceInventory и их сеттеры + onFeedback.
export function SettingsIntegrationsSection({
  rollouts,
  registeredUpdatePackages,
  deviceInventory,
  updateSummary,
  backend,
  canManageUpdatePackages,
  canManageUpdateRollouts,
  onRolloutsChange,
  onRegisteredUpdatePackagesChange,
  onFeedback
}: {
  rollouts: UpdateRolloutStatusDto[];
  registeredUpdatePackages: UpdatePackageDto[];
  deviceInventory: DeviceInventoryItemDto[];
  updateSummary: Record<string, unknown> | null;
  backend: OperatorBackendContext | null;
  canManageUpdatePackages: boolean;
  canManageUpdateRollouts: boolean;
  onRolloutsChange: (rollouts: UpdateRolloutStatusDto[]) => void;
  onRegisteredUpdatePackagesChange: (packages: UpdatePackageDto[]) => void;
  onFeedback: (feedback: Feedback) => void;
}) {
  const { t } = useI18n();

  const addUpdatePackageActionKey = t('op.settings.action.addUpdatePackage');
  const createRolloutActionKey = t('op.settings.action.createRollout');
  const changePackageStateActionKey = t('op.settings.action.changePackageState');
  const changeRolloutStateActionKey = t('op.settings.action.changeRolloutState');

  const [updateComponent, setUpdateComponent] = useState('organization-admin');
  const [updateVersion, setUpdateVersion] = useState('0.1.0');
  const [updateChannel, setUpdateChannel] = useState('internal');
  const [updateArtifactUri, setUpdateArtifactUri] = useState('https://updates.afk4.staging.mubi.dev/organization-admin/0.1.0/organization-admin.msi');
  const [updateSha256, setUpdateSha256] = useState('0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef');
  const [updateSignature, setUpdateSignature] = useState('signed-update-package');
  const [updateSignatureAlgorithm, setUpdateSignatureAlgorithm] = useState('ECDSA-P256-SHA256-IEEE-P1363');
  const [updateSizeKilobytes, setUpdateSizeKilobytes] = useState('1024');
  const [updateReleaseNotes, setUpdateReleaseNotes] = useState(() => t('op.settings.prefill.updateReleaseNotes'));
  const [rolloutPackageId, setRolloutPackageId] = useState('');
  const [rolloutChannel, setRolloutChannel] = useState('internal');
  const [rolloutTargetKind, setRolloutTargetKind] = useState('branch');
  const [rolloutTargetDeviceIds, setRolloutTargetDeviceIds] = useState('');
  const [rolloutBatchPercent, setRolloutBatchPercent] = useState('100');
  const [rolloutStartsAtUtc, setRolloutStartsAtUtc] = useState(() => new Date(Date.now() + 60 * 60 * 1000).toISOString());
  const [rolloutReason, setRolloutReason] = useState(() => t('op.settings.prefill.rolloutReason'));
  const [packageStatePackageId, setPackageStatePackageId] = useState('');
  const [packageState, setPackageState] = useState('validated');
  const [packageStateReason, setPackageStateReason] = useState(() => t('op.settings.prefill.packageStateReason'));
  const [rolloutStateRolloutId, setRolloutStateRolloutId] = useState('');
  const [rolloutState, setRolloutState] = useState('paused');
  const [rolloutStateReason, setRolloutStateReason] = useState(() => t('op.settings.prefill.rolloutStateReason'));
  const [criticalAction, setCriticalAction] = useState<
    'package-state-change' |
    'rollout-state-change' |
    null
  >(null);

  // Seeding selection state from server data (mirrors the parent's loadSettings seed logic)
  useEffect(() => {
    setRolloutPackageId((current) => rollouts.some((rollout) => readString(rollout, 'updatePackageId') === current)
      ? current
      : readString(rollouts[0], 'updatePackageId'));
    setPackageStatePackageId((current) => rollouts.some((rollout) => readString(rollout, 'updatePackageId') === current)
      ? current
      : readString(rollouts[0], 'updatePackageId'));
    setRolloutStateRolloutId((current) => rollouts.some((rollout) => readString(rollout, 'updateRolloutId') === current)
      ? current
      : readString(rollouts[0], 'updateRolloutId'));
  }, [rollouts]);

  const getDeviceInventoryName = (deviceId: string) =>
    readString(deviceInventory.find((device) => readString(device, 'deviceId') === deviceId), 'machineName', t('op.settings.devices.deviceFallback'));
  const deviceOptions = deviceInventory
    .map((device) => ({
      id: readString(device, 'deviceId'),
      label: `${readString(device, 'machineName', t('op.settings.devices.deviceFallback'))} · ${readString(device, 'zoneName', t('op.settings.devices.zoneFallback'))} · ${readString(device, 'seatName', t('op.settings.devices.seatFallback'))}`
    }))
    .filter((device) => isGuid(device.id));
  const selectedRollout = rollouts.find((rollout) => readString(rollout, 'updateRolloutId') === rolloutStateRolloutId) ?? rollouts[0] ?? null;
  const selectedRolloutDeviceStatuses = readArray<Record<string, unknown>>(selectedRollout, 'deviceStatuses');
  const updatePackageOptions = Array.from(new Map([
    ...rollouts.map((rollout) => ({
      id: readString(rollout, 'updatePackageId'),
      label: `${updateComponentLabel(readString(rollout, 'component'), t)} ${readString(rollout, 'version', t('op.settings.updates.versionFallback'))} · ${updateChannelLabel(readString(rollout, 'channel'), t)}`
    })),
    ...registeredUpdatePackages.map((updatePackage) => ({
      id: readString(updatePackage, 'updatePackageId'),
      label: `${updateComponentLabel(readString(updatePackage, 'component'), t)} ${readString(updatePackage, 'version', t('op.settings.updates.versionFallback'))} · ${updateChannelLabel(readString(updatePackage, 'channel'), t)} · ${updatePackageStateLabel(readString(updatePackage, 'state', 'registered'), t)}`
    }))
  ].filter((option) => isGuid(option.id)).map((option) => [option.id, option])).values());
  const rolloutOptions = rollouts
    .map((rollout) => ({
      id: readString(rollout, 'updateRolloutId'),
      label: `${updateComponentLabel(readString(rollout, 'component'), t)} ${readString(rollout, 'version', t('op.settings.updates.versionFallback'))} · ${updateRolloutStateLabel(readString(rollout, 'state'), t)}`
    }))
    .filter((rollout) => isGuid(rollout.id));
  const rolloutTargetDeviceIdSet = new Set(rolloutTargetDeviceIds.split(/[\s,;]+/).map((value) => value.trim()).filter(Boolean));

  const runAction = async (label: string) => {
    setCriticalAction(null);
    onFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      if (label === addUpdatePackageActionKey) {
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

        onRegisteredUpdatePackagesChange([createdPackage, ...registeredUpdatePackages.filter((item) => readString(item, 'updatePackageId') !== updatePackageId)]);
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
          onRolloutsChange([rollout, ...rollouts.filter((item) => readString(item, 'updateRolloutId') !== updateRolloutId)]);
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
        onRegisteredUpdatePackagesChange([updatePackage, ...registeredUpdatePackages.filter((item) => readString(item, 'updatePackageId') !== updatePackageId)]);
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
        onRolloutsChange(rollouts.map((item) => readString(item, 'updateRolloutId') === updateRolloutId ? rollout : item));
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
      <div className="settings-config-grid">
        {[
          [t('op.settings.integrations.payments'), t('op.settings.integrations.payments.detail')],
          [t('op.settings.integrations.updates'), t('op.settings.integrations.rolloutCount', { count: rollouts.length })],
          [t('op.settings.integrations.errors'), t('op.settings.integrations.errors.detail', { count: readNumber(updateSummary, 'failedDevices', 0) })],
          [t('op.settings.integrations.connection'), backend ? t('op.settings.integrations.connectionOk') : t('op.settings.integrations.connectionLocal')]
        ].map(([name, detail]) => (
          <button key={name} type="button" onClick={() => onFeedback({ label: name ?? '', state: 'confirmed' })}>
            <strong>{name}</strong>
            <span>{detail}</span>
          </button>
        ))}
      </div>

      <div className="settings-section-title">
        <span>{t('op.settings.updates.packagesTitle')}</span>
        <button type="button" disabled={!canManageUpdatePackages} onClick={() => runAction(addUpdatePackageActionKey)}>{t('op.settings.updates.addBtn')}</button>
      </div>
      <div className="settings-form-grid settings-update-form">
        <label>{t('op.settings.updates.component')}
          <select value={updateComponent} disabled={!canManageUpdatePackages} onChange={(event) => setUpdateComponent(event.currentTarget.value)}>
            <option value="organization-admin">{updateComponentLabel('organization-admin', t)}</option>
            <option value="agent-service">{updateComponentLabel('agent-service', t)}</option>
            <option value="player-shell">{updateComponentLabel('player-shell', t)}</option>
          </select>
        </label>
        <label>{t('op.settings.updates.version')}<input value={updateVersion} disabled={!canManageUpdatePackages} onChange={(event) => setUpdateVersion(event.currentTarget.value)} /></label>
        <label>{t('op.settings.updates.channel')}
          <select value={updateChannel} disabled={!canManageUpdatePackages} onChange={(event) => setUpdateChannel(event.currentTarget.value)}>
            <option value="internal">{updateChannelLabel('internal', t)}</option>
            <option value="beta">{updateChannelLabel('beta', t)}</option>
            <option value="stable">{updateChannelLabel('stable', t)}</option>
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
        <button type="button" disabled={!canManageUpdateRollouts} onClick={() => runAction(createRolloutActionKey)}>{t('op.settings.rollouts.createBtn')}</button>
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
            <strong>{updateComponentLabel(readString(rollout, 'component'), t)} {readString(rollout, 'version', t('op.settings.updates.versionFallback'))}</strong>
            <b>{updateRolloutStateLabel(readString(rollout, 'state'), t)}</b>
            <span>{t('op.settings.rollouts.rolloutTargetDetail', { targetKind: updateTargetKindLabel(readString(rollout, 'targetKind'), t), batchPercent: readNumber(rollout, 'batchPercent', 0), deviceCount: readArray(rollout, 'deviceStatuses').length })}</span>
          </button>
        ))}
      </div>
      {selectedRollout && (
        <div className="settings-device-detail-grid">
          <span><strong>{t('op.settings.rollouts.detail.rollout')}</strong><b>{updateComponentLabel(readString(selectedRollout, 'component'), t)} {readString(selectedRollout, 'version', t('op.settings.updates.versionFallback'))}</b></span>
          <span><strong>{t('op.settings.rollouts.detail.state')}</strong><b>{updateRolloutStateLabel(readString(selectedRollout, 'state'), t)}</b></span>
          <span><strong>{t('op.settings.rollouts.detail.target')}</strong><b>{updateTargetKindLabel(readString(selectedRollout, 'targetKind'), t)} · {readNumber(selectedRollout, 'batchPercent', 0)}%</b></span>
          <span><strong>{t('op.settings.rollouts.detail.channel')}</strong><b>{updateChannelLabel(readString(selectedRollout, 'channel'), t)}</b></span>
          <span><strong>{t('op.settings.rollouts.detail.package')}</strong><b>{updateComponentLabel(readString(selectedRollout, 'component'), t)} {readString(selectedRollout, 'version', t('op.settings.updates.versionFallback'))}</b></span>
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
              <b>{updateDeviceStatusLabel(readString(status, 'status', 'unknown'), t)}</b>
              <em>{updateDeviceMessageLabel(readString(status, 'message'), t) || t('op.settings.rollouts.detail.versionArrow', { from: readString(status, 'installedVersion'), to: readString(status, 'targetVersion') })}</em>
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
            <option value="internal">{updateChannelLabel('internal', t)}</option>
            <option value="beta">{updateChannelLabel('beta', t)}</option>
            <option value="stable">{updateChannelLabel('stable', t)}</option>
          </select>
        </label>
        <label>{t('op.settings.rollouts.target')}
          <select value={rolloutTargetKind} disabled={!canManageUpdateRollouts} onChange={(event) => setRolloutTargetKind(event.currentTarget.value)}>
            <option value="branch">{updateTargetKindLabel('branch', t)}</option>
            <option value="device">{updateTargetKindLabel('device', t)}</option>
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
            onFeedback({ label: changePackageStateActionKey, state: 'pending' });
            setCriticalAction('package-state-change');
          }}
        >
          {changePackageStateActionKey}
        </button>
        <button
          type="button"
          disabled={!canManageUpdateRollouts}
          onClick={() => {
            onFeedback({ label: changeRolloutStateActionKey, state: 'pending' });
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
            <option value="registered">{updatePackageStateLabel('registered', t)}</option>
            <option value="validated">{updatePackageStateLabel('validated', t)}</option>
            <option value="rejected">{updatePackageStateLabel('rejected', t)}</option>
            <option value="retired">{updatePackageStateLabel('retired', t)}</option>
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
            <option value="active">{updateRolloutStateLabel('active', t)}</option>
            <option value="paused">{updateRolloutStateLabel('paused', t)}</option>
            <option value="completed">{updateRolloutStateLabel('completed', t)}</option>
            <option value="rollback_requested">{updateRolloutStateLabel('rollback_requested', t)}</option>
            <option value="rolled_back">{updateRolloutStateLabel('rolled_back', t)}</option>
            <option value="cancelled">{updateRolloutStateLabel('cancelled', t)}</option>
          </select>
        </label>
        <label>{t('op.settings.packageState.rolloutReasonLabel')}<input value={rolloutStateReason} disabled={!canManageUpdateRollouts} onChange={(event) => setRolloutStateReason(event.currentTarget.value)} /></label>
      </div>
      {criticalAction === 'package-state-change' && (
        <CriticalActionConfirmation
          title={t('op.settings.packageState.confirmPackage.title')}
          detail={`${updatePackageOptions.find((option) => option.id === packageStatePackageId)?.label ?? t('op.settings.packageState.confirmPackage.packageFallback')} · ${updatePackageStateLabel(packageState, t)}`}
          impact={t('op.settings.packageState.confirmPackage.impact', { reason: packageStateReason.trim() || t('op.settings.packageState.confirmPackage.impactNoReason') })}
          confirmLabel={t('op.settings.packageState.confirmPackage.confirm')}
          disabled={false}
          onCancel={() => setCriticalAction(null)}
          onConfirm={() => void runAction(changePackageStateActionKey)}
        />
      )}
      {criticalAction === 'rollout-state-change' && (
        <CriticalActionConfirmation
          title={t('op.settings.packageState.confirmRollout.title')}
          detail={`${rolloutOptions.find((option) => option.id === rolloutStateRolloutId)?.label ?? t('op.settings.packageState.confirmRollout.rolloutFallback')} · ${updateRolloutStateLabel(rolloutState, t)}`}
          impact={t('op.settings.packageState.confirmRollout.impact', { reason: rolloutStateReason.trim() || t('op.settings.packageState.confirmRollout.impactNoReason') })}
          confirmLabel={t('op.settings.packageState.confirmRollout.confirm')}
          disabled={false}
          onCancel={() => setCriticalAction(null)}
          onConfirm={() => void runAction(changeRolloutStateActionKey)}
        />
      )}
    </>
  );
}
