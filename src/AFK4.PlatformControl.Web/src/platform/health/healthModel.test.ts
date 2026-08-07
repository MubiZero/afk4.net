import { expect, it } from 'bun:test';
import { hasCritical, jobStatus, sortIncidents } from './healthModel';
import type { HealthOverview, Incident, JobHealth } from '@/api/types';

function job(overrides: Partial<JobHealth> = {}): JobHealth {
  return {
    jobName: 'invoice_generation',
    lastRunAtUtc: '2026-08-01T00:00:00Z',
    lastSuccessAtUtc: '2026-08-01T00:00:00Z',
    lastOutcome: 'success',
    lastItemsProcessed: 3,
    lastError: null,
    consecutiveFailures: 0,
    ...overrides
  };
}

function incident(overrides: Partial<Incident> = {}): Incident {
  return {
    incidentId: 'i1',
    kind: 'job_overdue',
    dedupKey: 'job_overdue:invoice_generation',
    severity: 'warning',
    detailsJson: null,
    openedAtUtc: '2026-08-01T00:00:00Z',
    lastSeenAtUtc: '2026-08-01T00:00:00Z',
    ...overrides
  };
}

function overview(incidents: Incident[]): HealthOverview {
  return { generatedAtUtc: '2026-08-07T00:00:00Z', jobs: [], queues: [], openIncidents: incidents };
}

it('a job that never ran is "never"', () => {
  expect(jobStatus(job({ lastRunAtUtc: null, lastSuccessAtUtc: null }))).toBe('never');
});

it('a job with consecutive failures is "failing", even if it once succeeded', () => {
  expect(jobStatus(job({ lastSuccessAtUtc: '2026-07-01T00:00:00Z', consecutiveFailures: 2 }))).toBe('failing');
});

it('a job with a fresh success and no failures is "ok"', () => {
  expect(jobStatus(job({ consecutiveFailures: 0 }))).toBe('ok');
});

it('sorts critical incidents ahead of warning ones', () => {
  const sorted = sortIncidents([
    incident({ incidentId: 'w1', severity: 'warning' }),
    incident({ incidentId: 'c1', severity: 'critical' })
  ]);
  expect(sorted.map(i => i.incidentId)).toEqual(['c1', 'w1']);
});

it('within the same severity, orders the older incident first', () => {
  const sorted = sortIncidents([
    incident({ incidentId: 'newer', severity: 'critical', openedAtUtc: '2026-08-05T00:00:00Z' }),
    incident({ incidentId: 'older', severity: 'critical', openedAtUtc: '2026-08-01T00:00:00Z' })
  ]);
  expect(sorted.map(i => i.incidentId)).toEqual(['older', 'newer']);
});

it('hasCritical is true only when a critical incident is present', () => {
  expect(hasCritical(overview([incident({ severity: 'warning' })]))).toBe(false);
  expect(hasCritical(overview([incident({ severity: 'warning' }), incident({ severity: 'critical' })]))).toBe(true);
  expect(hasCritical(overview([]))).toBe(false);
});
