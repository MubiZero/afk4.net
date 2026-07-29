import { useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import type { OrganizationAdminUpdatePreferenceDto, UpdateRolloutStatusDto } from '../api/clients/updates';
import { updateDeviceStatusLabel } from '../operatorHelpers';

export interface OrganizationAdminUpdateCardProps {
  rollouts: UpdateRolloutStatusDto[];
  preference: OrganizationAdminUpdatePreferenceDto | null;
  canManagePreference: boolean;
  onSavePreference(start: string, end: string): void | Promise<void>;
  onRestart(rolloutId: string, packageId: string): void | Promise<void>;
}

const actionableStatuses = new Set(['offered', 'downloaded', 'deferred', 'ready-to-install', 'awaiting-app-exit']);

export function OrganizationAdminUpdateCard({
  rollouts,
  preference,
  canManagePreference,
  onSavePreference,
  onRestart
}: OrganizationAdminUpdateCardProps) {
  const { t } = useI18n();
  const rollout = useMemo(() => rollouts.find(item => item.component === 'organization-admin'), [rollouts]);
  const status = rollout === undefined
    ? undefined
    : [...rollout.deviceStatuses].sort((left, right) => right.updatedAtUtc.localeCompare(left.updatedAtUtc))[0];
  const [start, setStart] = useState(toTimeInput(preference?.maintenanceWindowStart));
  const [end, setEnd] = useState(toTimeInput(preference?.maintenanceWindowEnd));
  const [dismissed, setDismissed] = useState(false);

  useEffect(() => {
    setStart(toTimeInput(preference?.maintenanceWindowStart));
    setEnd(toTimeInput(preference?.maintenanceWindowEnd));
  }, [preference?.maintenanceWindowStart, preference?.maintenanceWindowEnd]);
  useEffect(() => setDismissed(false), [rollout?.updateRolloutId]);

  if (!rollout) {
    return <div className="settings-empty-state" data-testid="admin-update-empty">{t('op.settings.updates.admin.empty')}</div>;
  }

  const canRestart = canManagePreference && status !== undefined && actionableStatuses.has(status.status) && !dismissed;
  const detail = safeUpdateMessage(status?.message, t('op.settings.updates.admin.detailFallback'));

  return (
    <section className="settings-update-status" aria-labelledby="admin-update-title">
      <div className="settings-section-title">
        <span id="admin-update-title">{t('op.settings.updates.admin.title')}</span>
        <small>{t('op.settings.updates.platformManagedHint')}</small>
      </div>
      <dl className="settings-summary-list">
        <div><dt>{t('op.settings.updates.installedVersion')}</dt><dd>{status?.installedVersion || '—'}</dd></div>
        <div><dt>{t('op.settings.updates.targetVersion')}</dt><dd>{status?.targetVersion || rollout.version}</dd></div>
        <div><dt>{t('op.settings.updates.state')}</dt><dd>{updateDeviceStatusLabel(status?.status ?? 'offered', t)}</dd></div>
        <div><dt>{t('op.settings.updates.admin.timeZone')}</dt><dd>{preference?.timeZone || '—'}</dd></div>
      </dl>
      {detail && <p role={status?.status === 'failed' ? 'alert' : undefined}>{detail}</p>}

      <fieldset className="settings-form-grid" disabled={!canManagePreference || preference === null}>
        <legend>{t('op.settings.updates.admin.window')}</legend>
        <label>{t('op.settings.updates.admin.windowStart')}<input data-testid="maintenance-start" type="time" value={start} onChange={event => setStart(event.target.value)} /></label>
        <label>{t('op.settings.updates.admin.windowEnd')}<input data-testid="maintenance-end" type="time" value={end} onChange={event => setEnd(event.target.value)} /></label>
        <button data-testid="maintenance-save" type="button" disabled={!canManagePreference || !start || !end || start === end} onClick={() => void onSavePreference(start, end)}>
          {t('op.settings.updates.admin.saveWindow')}
        </button>
      </fieldset>

      {canRestart && (
        <div className="settings-form-actions">
          <button data-testid="restart-and-update" type="button" onClick={() => void onRestart(rollout.updateRolloutId, rollout.updatePackageId)}>
            {t('op.settings.updates.admin.restart')}
          </button>
          <button data-testid="update-later" type="button" onClick={() => setDismissed(true)}>{t('op.settings.updates.admin.later')}</button>
        </div>
      )}
    </section>
  );
}

function toTimeInput(value?: string): string {
  return value?.slice(0, 5) ?? '';
}

function safeUpdateMessage(message: string | undefined, fallback: string): string {
  if (!message) return '';
  return /(https?:\/\/|sha-?256|signature|artifact|\.msi)/i.test(message) ? fallback : message;
}
