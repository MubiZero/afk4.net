import { useI18n, type MessageKey } from '@afk4/i18n';
import { DeviceHealthIssueNames, DiskHealthNames, type DeviceHealthDto } from '@afk4/contracts';

const ISSUE_KEYS: Record<string, MessageKey> = {
  [DeviceHealthIssueNames.DiskFailing]: 'op.health.issue.diskFailing',
  [DeviceHealthIssueNames.DiskWarning]: 'op.health.issue.diskWarning',
  [DeviceHealthIssueNames.DiskHot]: 'op.health.issue.diskHot',
  [DeviceHealthIssueNames.DiskWorn]: 'op.health.issue.diskWorn',
  [DeviceHealthIssueNames.GpuOverheating]: 'op.health.issue.gpuOverheating',
  [DeviceHealthIssueNames.GpuHot]: 'op.health.issue.gpuHot',
  [DeviceHealthIssueNames.BoardHot]: 'op.health.issue.boardHot',
  [DeviceHealthIssueNames.LowDiskSpace]: 'op.health.issue.lowDiskSpace'
};

const DISK_HEALTH_KEYS: Record<string, MessageKey> = {
  [DiskHealthNames.Healthy]: 'op.health.disk.healthy',
  [DiskHealthNames.Warning]: 'op.health.disk.warning',
  [DiskHealthNames.Unhealthy]: 'op.health.disk.unhealthy'
};

/** Что сказать стойке о проблеме ПК: словами и с подсказкой, что делать. Неизвестный код — общая фраза. */
export function healthIssueKey(issue: string): MessageKey {
  return ISSUE_KEYS[issue] ?? 'op.health.issue.other';
}

/**
 * Здоровье ПК (docs/operations/pc-health.md): последние показатели, раз в пять минут. Чего Windows без
 * драйвера не отдаёт — температуры процессора и корпусных вентиляторов, — не рисуем, а говорим об этом.
 */
export function DeviceHealthReadings({ health, reportedAtUtc, issue }: {
  health: DeviceHealthDto;
  reportedAtUtc: string | null;
  issue: string | null;
}) {
  const { t, formatDate } = useI18n();
  const percent = (value: number | null | undefined) => (value === null || value === undefined ? null : `${value}%`);
  const celsius = (value: number | null | undefined) => (value === null || value === undefined ? null : `${value} °C`);
  const join = (parts: (string | null)[]) => parts.filter(Boolean).join(' · ') || null;

  const rows: [string, string | null][] = [
    [t('op.health.cpuLoad'), percent(health.cpuLoadPercent)],
    [t('op.health.memory'), percent(health.memoryUsedPercent)],
    [t('op.health.board'), celsius(health.boardTemperatureC)],
    ...health.gpus.map((gpu): [string, string | null] => [gpu.name, join([
      celsius(gpu.temperatureC),
      gpu.fanPercent === null || gpu.fanPercent === undefined ? null : t('op.health.fan', { percent: gpu.fanPercent }),
      gpu.loadPercent === null || gpu.loadPercent === undefined ? null : t('op.health.load', { percent: gpu.loadPercent })
    ])]),
    ...health.disks.map((disk): [string, string | null] => [disk.model, join([
      disk.health ? t(DISK_HEALTH_KEYS[disk.health] ?? 'op.health.disk.unknown') : null,
      celsius(disk.temperatureC),
      disk.wearPercent === null || disk.wearPercent === undefined ? null : t('op.health.wear', { percent: disk.wearPercent })
    ])]),
    ...health.volumes.map((volume): [string, string | null] => [volume.name, t('op.health.space', { free: volume.freeGb, size: volume.sizeGb })])
  ];

  return (
    <section className="device-health" aria-label={t('op.health.title')}>
      <strong>{t('op.health.title')}</strong>
      {issue && <p className="ui-blocked-reason" role="status">{t(healthIssueKey(issue))}</p>}
      <div className="settings-device-detail-grid">
        {rows.filter(([, value]) => value !== null).map(([label, value]) => (
          <span key={label}><strong>{label}</strong><b>{value}</b></span>
        ))}
      </div>
      <p className="mgmt-drawer-hint">
        {reportedAtUtc && t('op.health.reportedAt', { time: formatDate(reportedAtUtc) })}
        {' '}
        {t('op.health.notReadable')}
      </p>
    </section>
  );
}
