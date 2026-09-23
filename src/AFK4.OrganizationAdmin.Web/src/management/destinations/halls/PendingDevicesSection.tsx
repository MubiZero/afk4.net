import { useCallback, useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { ShieldCheck } from 'lucide-react';
import { CriticalActionConfirmation, EmptyState, PartialLoadFailure, Skeleton } from '../../../operatorPrimitives';
import { hasPermission, permissionNames } from '../../../operatorPermissions';
import { projectOperatorError } from '../../../apiErrors';
import {
  createAuthenticatedOperatorClients,
  readString,
  requireBackend
} from '../../../operatorHelpers';
import type { BranchSettingsDto } from '../../../api/clients/settings';
import type { DeviceInventoryItemDto } from '../../../operatorApiClients';
import type { Feedback, OperatorBackendContext } from '../../../operatorTypes';

/**
 * Очередь новых ПК, ждущих решения человека, и переключатель, который её вообще включает.
 *
 * На сервере обе половины работали с самого начала и не имели ни одного клиента. Получалось хуже,
 * чем «функции нет»: включить проверку было нечем, а если бы её включили мимо интерфейса — очередь
 * стало бы некому разбирать, и каждый новый ПК висел бы в «ожидает» вечно, занимая место в лимите
 * тарифа и не пуская за себя игроков.
 */
type PendingState =
  | { status: 'loading' }
  | { status: 'ready'; devices: DeviceInventoryItemDto[] }
  | { status: 'failed'; error: unknown };

export function PendingDevicesSection({
  backend,
  canViewDeviceDetail,
  canApproveDevice,
  canManageBranchSettings,
  onApproved,
  onFeedback
}: {
  backend: OperatorBackendContext | null;
  canViewDeviceDetail: boolean;
  canApproveDevice: boolean;
  canManageBranchSettings: boolean;
  onApproved: (nextBackend: OperatorBackendContext) => Promise<void>;
  onFeedback: (feedback: Feedback) => void;
}) {
  const { t } = useI18n();
  const [pending, setPending] = useState<PendingState>({ status: 'loading' });
  const [settings, setSettings] = useState<BranchSettingsDto | null>(null);
  const [busy, setBusy] = useState(false);
  const [rejectTarget, setRejectTarget] = useState<DeviceInventoryItemDto | null>(null);
  const [rejectReason, setRejectReason] = useState('');

  const branchId = backend?.branchId ?? null;

  const load = useCallback(async () => {
    if (backend === null || !canViewDeviceDetail) return;
    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    try {
      setPending({ status: 'ready', devices: await clients.devices.listPendingDevices(backend.branchId) });
    } catch (error) {
      setPending({ status: 'failed', error });
    }
    if (canManageBranchSettings) {
      // Настройки грузятся отдельно от очереди: без них переключатель нечем показать, но очередь
      // от этого не перестаёт существовать.
      setSettings(await clients.settings.getBranchSettings(backend.branchId).catch(() => null));
    }
  }, [backend, canViewDeviceDetail, canManageBranchSettings]);

  useEffect(() => { void load(); }, [load, branchId]);

  const toggleApproval = async (nextValue: boolean) => {
    const label = t('op.management.halls.pending.settingLabel');
    setBusy(true);
    onFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.manageBranchSettings)) {
        throw new Error(t('op.management.halls.pending.error.noPermSettings'));
      }
      if (settings === null) throw new Error(t('op.management.halls.pending.error.settingsUnknown'));

      const clients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      // Запись заменяется целиком, поэтому язык филиала едет обратно тем же, каким приехал.
      setSettings(await clients.settings.updateBranchSettings(nextBackend.branchId, {
        organizationId: nextBackend.session.organizationId,
        requireManualDeviceApproval: nextValue,
        preferredLocale: settings.preferredLocale
      }));
      await load();
      onFeedback({ label, state: 'confirmed' });
    } catch (error) {
      onFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setBusy(false);
    }
  };

  const approve = async (device: DeviceInventoryItemDto) => {
    const label = t('op.management.halls.pending.approve');
    setBusy(true);
    onFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.assignDeviceSeat)) {
        throw new Error(t('op.management.halls.pending.error.noPermDecide'));
      }
      const clients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      await clients.devices.approveDevice(readString(device, 'deviceId'), {
        organizationId: nextBackend.session.organizationId
      });
      await load();
      await onApproved(nextBackend);
      onFeedback({ label, state: 'confirmed' });
    } catch (error) {
      onFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setBusy(false);
    }
  };

  const confirmReject = async () => {
    const device = rejectTarget;
    if (device === null) return;
    const label = t('op.management.halls.pending.reject');
    setRejectTarget(null);
    setBusy(true);
    onFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.assignDeviceSeat)) {
        throw new Error(t('op.management.halls.pending.error.noPermDecide'));
      }
      const clients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const reason = rejectReason.trim();
      await clients.devices.rejectDevice(readString(device, 'deviceId'), {
        organizationId: nextBackend.session.organizationId,
        reason: reason.length > 0 ? reason : null
      });
      setRejectReason('');
      await load();
      onFeedback({ label, state: 'confirmed' });
    } catch (error) {
      onFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setBusy(false);
    }
  };

  if (!canViewDeviceDetail) return null;

  const queue = pending.status === 'ready' ? pending.devices : [];
  const loadFailure = pending.status === 'failed' ? projectOperatorError(pending.error, t) : null;

  return (
    <section className="mgmt-drawer-section">
      <div className="mgmt-section-title">
        <span><ShieldCheck size={14} aria-hidden="true" /> {t('op.management.halls.pending.title')}</span>
      </div>

      {canManageBranchSettings && settings !== null && (
        <div className="mgmt-form">
          <label className="mgmt-check mgmt-form-wide">
            <input
              type="checkbox"
              checked={settings.requireManualDeviceApproval}
              disabled={busy}
              onChange={(event) => void toggleApproval(event.currentTarget.checked)}
            />
            {t('op.management.halls.pending.settingLabel')}
          </label>
          <p className="mgmt-drawer-hint">{t('op.management.halls.pending.settingHint')}</p>
        </div>
      )}

      {pending.status === 'loading' && <Skeleton variant="text" lines={2} />}
      {loadFailure !== null && (
        <PartialLoadFailure
          text={t('op.management.halls.pending.failed', { reason: loadFailure.detail })}
          failure={loadFailure}
          onRetry={() => void load()}
        />
      )}
      {pending.status === 'ready' && queue.length === 0 && (
        <EmptyState
          inline
          className="mgmt-drawer-hint"
          title={t('op.management.halls.pending.empty')}
          next={{ kind: 'calm', hint: t('op.management.halls.pending.emptyHint') }}
        />
      )}
      {queue.length > 0 && (
        <ul>
          {queue.map((device) => (
            <li key={readString(device, 'deviceId')} className="mgmt-zone-row">
              <span>{readString(device, 'machineName', t('op.settings.devices.deviceFallback'))}</span>
              {canApproveDevice && (
                <span className="mgmt-status-pair">
                  <button type="button" className="ui-btn ui-btn--primary ui-btn--sm" disabled={busy} onClick={() => void approve(device)}>
                    {t('op.management.halls.pending.approve')}
                  </button>
                  <button type="button" className="ui-btn ui-btn--sm" disabled={busy} onClick={() => setRejectTarget(device)}>
                    {t('op.management.halls.pending.reject')}
                  </button>
                </span>
              )}
            </li>
          ))}
        </ul>
      )}

      {rejectTarget !== null && (
        <CriticalActionConfirmation
          title={t('op.management.halls.pending.rejectConfirmTitle')}
          detail={readString(rejectTarget, 'machineName', t('op.settings.devices.deviceFallback'))}
          impact={t('op.management.halls.pending.rejectImpact')}
          confirmLabel={t('op.management.halls.pending.rejectConfirm')}
          onCancel={() => { setRejectTarget(null); setRejectReason(''); }}
          onConfirm={() => void confirmReject()}
        >
          <label>
            {t('op.management.halls.pending.rejectReason')}
            <input
              type="text"
              value={rejectReason}
              onChange={(event) => setRejectReason(event.target.value)}
            />
          </label>
        </CriticalActionConfirmation>
      )}
    </section>
  );
}
