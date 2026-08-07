import type { HealthOverview, Incident, JobHealth } from '@/api/types';

export type JobStatus = 'ok' | 'failing' | 'never';

// Статус выводится из данных задания, а не приходит строкой с сервера:
// сервер отдаёт факты, экран решает, как их назвать.
export function jobStatus(job: JobHealth): JobStatus {
  if (job.lastSuccessAtUtc === null && job.lastRunAtUtc === null) return 'never';
  if (job.consecutiveFailures > 0) return 'failing';
  return 'ok';
}

export function sortIncidents(incidents: readonly Incident[]): Incident[] {
  return [...incidents].sort((left, right) => {
    if (left.severity !== right.severity) return left.severity === 'critical' ? -1 : 1;
    return left.openedAtUtc.localeCompare(right.openedAtUtc);
  });
}

export function hasCritical(overview: HealthOverview): boolean {
  return overview.openIncidents.some(incident => incident.severity === 'critical');
}
