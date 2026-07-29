import { useI18n } from '@afk4/i18n';
import type { OrganizationAdminUpdatePreferenceDto, UpdateRolloutStatusDto } from '../operatorApiClients';
import { formatTime, readNumber, updateComponentLabel, updateDeviceStatusLabel, updateRolloutStateLabel } from '../operatorHelpers';
import { OrganizationAdminUpdateCard } from './OrganizationAdminUpdateCard';

export interface SettingsIntegrationsSectionProps {
  rollouts: UpdateRolloutStatusDto[];
  updateSummary: Record<string, unknown> | null;
  preference: OrganizationAdminUpdatePreferenceDto | null;
  canViewUpdates: boolean;
  canManagePreference: boolean;
  onSavePreference(start: string, end: string): void | Promise<void>;
  onRestart(rolloutId: string, packageId: string): void | Promise<void>;
}

// Package publication and rollout control belong to Platform Control. Organization Admin
// deliberately exposes only the status that a club can act on locally.
export function SettingsIntegrationsSection({ rollouts, updateSummary, preference, canViewUpdates, canManagePreference, onSavePreference, onRestart }: SettingsIntegrationsSectionProps) {
  const { t } = useI18n();
  const latest = rollouts[0];
  const deviceStatuses = latest?.deviceStatuses ?? [];

  if (!canViewUpdates) {
    return null;
  }

  return (
    <>
      <div className="settings-section-title">
        <span>{t('op.settings.updates.statusTitle')}</span>
        <small>{t('op.settings.updates.platformManagedHint')}</small>
      </div>
      <div className="settings-readiness-grid">
        <article><span>{t('op.settings.updates.activeRollouts')}</span><strong>{readNumber(updateSummary, 'activeRollouts', rollouts.filter(item => item.state === 'active').length)}</strong></article>
        <article><span>{t('op.settings.updates.installingDevices')}</span><strong>{readNumber(updateSummary, 'installingDevices', 0)}</strong></article>
        <article><span>{t('op.settings.updates.failedDevices')}</span><strong>{readNumber(updateSummary, 'failedDevices', 0)}</strong></article>
        <article><span>{t('op.settings.updates.rollbackDevices')}</span><strong>{readNumber(updateSummary, 'rollbackDevices', 0)}</strong></article>
      </div>

      <OrganizationAdminUpdateCard
        rollouts={rollouts}
        preference={preference}
        canManagePreference={canManagePreference}
        onSavePreference={onSavePreference}
        onRestart={onRestart}
      />

      {latest === undefined ? (
        <div className="settings-empty-state">{t('op.settings.rollouts.noRollouts')}</div>
      ) : (
        <div className="settings-update-status">
          <div className="settings-section-title"><span>{t('op.settings.updates.currentRollout')}</span></div>
          <dl className="settings-summary-list">
            <div><dt>{t('op.settings.updates.component')}</dt><dd>{updateComponentLabel(latest.component, t)}</dd></div>
            <div><dt>{t('op.settings.updates.version')}</dt><dd>{latest.version}</dd></div>
            <div><dt>{t('op.settings.updates.state')}</dt><dd>{updateRolloutStateLabel(latest.state, t)}</dd></div>
            <div><dt>{t('op.settings.updates.started')}</dt><dd>{formatTime(latest.startsAtUtc)}</dd></div>
          </dl>
          {deviceStatuses.length > 0 && (
            <div className="settings-table-wrap"><table><thead><tr><th>{t('op.settings.updates.device')}</th><th>{t('op.settings.updates.installedVersion')}</th><th>{t('op.settings.updates.targetVersion')}</th><th>{t('op.settings.updates.state')}</th><th>{t('op.settings.updates.updated')}</th></tr></thead><tbody>
              {deviceStatuses.map((status, index) => <tr key={`${status.deviceId}-${index}`}><td>{status.deviceId}</td><td>{status.installedVersion}</td><td>{status.targetVersion}</td><td>{updateDeviceStatusLabel(status.status, t)}</td><td>{formatTime(status.updatedAtUtc)}</td></tr>)}
            </tbody></table></div>
          )}
        </div>
      )}
    </>
  );
}
