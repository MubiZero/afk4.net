import { useI18n } from '@afk4/i18n';
import type { UpdateRolloutStatusDto } from '../operatorApiClients';
import { formatTime, readArray, readNumber, readString, updateComponentLabel, updateDeviceStatusLabel, updateRolloutStateLabel } from '../operatorHelpers';

export interface SettingsIntegrationsSectionProps {
  rollouts: UpdateRolloutStatusDto[];
  updateSummary: Record<string, unknown> | null;
}

// Package publication and rollout control belong to Platform Control. Organization Admin
// deliberately exposes only the status that a club can act on locally.
export function SettingsIntegrationsSection({ rollouts, updateSummary }: SettingsIntegrationsSectionProps) {
  const { t } = useI18n();
  const latest = rollouts[0];
  const deviceStatuses = latest === undefined ? [] : readArray(latest, 'deviceStatuses');

  return (
    <>
      <div className="settings-section-title">
        <span>{t('op.settings.updates.statusTitle')}</span>
        <small>{t('op.settings.updates.platformManagedHint')}</small>
      </div>
      <div className="settings-readiness-grid">
        <article><span>{t('op.settings.updates.activeRollouts')}</span><strong>{readNumber(updateSummary, 'activeRollouts', rollouts.filter(item => readString(item, 'state') === 'active').length)}</strong></article>
        <article><span>{t('op.settings.updates.installingDevices')}</span><strong>{readNumber(updateSummary, 'installingDevices', 0)}</strong></article>
        <article><span>{t('op.settings.updates.failedDevices')}</span><strong>{readNumber(updateSummary, 'failedDevices', 0)}</strong></article>
        <article><span>{t('op.settings.updates.rollbackDevices')}</span><strong>{readNumber(updateSummary, 'rollbackDevices', 0)}</strong></article>
      </div>

      {latest === undefined ? (
        <div className="settings-empty-state">{t('op.settings.rollouts.noRollouts')}</div>
      ) : (
        <div className="settings-update-status">
          <div className="settings-section-title"><span>{t('op.settings.updates.currentRollout')}</span></div>
          <dl className="settings-summary-list">
            <div><dt>{t('op.settings.updates.component')}</dt><dd>{updateComponentLabel(readString(latest, 'component'), t)}</dd></div>
            <div><dt>{t('op.settings.updates.version')}</dt><dd>{readString(latest, 'version')}</dd></div>
            <div><dt>{t('op.settings.updates.state')}</dt><dd>{updateRolloutStateLabel(readString(latest, 'state'), t)}</dd></div>
            <div><dt>{t('op.settings.updates.started')}</dt><dd>{formatTime(readString(latest, 'startsAtUtc'))}</dd></div>
          </dl>
          {deviceStatuses.length > 0 && (
            <div className="settings-table-wrap"><table><thead><tr><th>{t('op.settings.updates.device')}</th><th>{t('op.settings.updates.installedVersion')}</th><th>{t('op.settings.updates.targetVersion')}</th><th>{t('op.settings.updates.state')}</th><th>{t('op.settings.updates.updated')}</th></tr></thead><tbody>
              {deviceStatuses.map((status, index) => <tr key={`${readString(status, 'deviceId')}-${index}`}><td>{readString(status, 'deviceId')}</td><td>{readString(status, 'installedVersion')}</td><td>{readString(status, 'targetVersion')}</td><td>{updateDeviceStatusLabel(readString(status, 'status'), t)}</td><td>{formatTime(readString(status, 'updatedAtUtc'))}</td></tr>)}
            </tbody></table></div>
          )}
        </div>
      )}
    </>
  );
}
