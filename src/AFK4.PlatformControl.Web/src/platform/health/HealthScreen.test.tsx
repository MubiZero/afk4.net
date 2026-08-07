import { describe, expect, it, mock } from 'bun:test';
import { render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { HealthScreen } from './HealthScreen';
import type { HealthOverview, Incident, JobHealth, QueueHealth } from '@/api/types';

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
    severity: 'critical',
    detailsJson: null,
    openedAtUtc: '2026-08-01T00:00:00Z',
    lastSeenAtUtc: '2026-08-01T00:00:00Z',
    ...overrides
  };
}

function queue(overrides: Partial<QueueHealth> = {}): QueueHealth {
  return { queueName: 'notifications', pendingCount: 0, failedCount: 0, stuckCount: 0, ...overrides };
}

function overview(overrides: Partial<HealthOverview> = {}): HealthOverview {
  return { generatedAtUtc: '2026-08-07T00:00:00Z', jobs: [job()], queues: [queue()], openIncidents: [], ...overrides };
}

function fakeClient(result: HealthOverview | (() => Promise<HealthOverview>)) {
  return { getOverview: typeof result === 'function' ? mock(result) : mock().mockResolvedValue(result) };
}

describe('HealthScreen', () => {
  it('shows a critical incident with its translated title and severity label', async () => {
    render(
      <I18nProvider>
        <HealthScreen client={fakeClient(overview({ openIncidents: [incident({ kind: 'job_overdue', severity: 'critical' })] }))} />
      </I18nProvider>
    );
    await waitFor(() => expect(screen.getByText('Задание не отрабатывает')).toBeInTheDocument());
    expect(screen.getAllByText('Критично').length).toBeGreaterThan(0);
  });

  it('shows the empty-incidents copy and no incident titles when there are none', async () => {
    render(
      <I18nProvider>
        <HealthScreen client={fakeClient(overview({ openIncidents: [] }))} />
      </I18nProvider>
    );
    await waitFor(() => expect(screen.getByText('Открытых проблем нет')).toBeInTheDocument());
    expect(screen.queryByText('Задание не отрабатывает')).not.toBeInTheDocument();
  });

  it('shows an error state with retry, never the "no incidents" copy, when the load fails', async () => {
    render(
      <I18nProvider>
        <HealthScreen client={fakeClient(() => Promise.reject(new Error('network')))} />
      </I18nProvider>
    );
    await waitFor(() => expect(screen.getByRole('alert')).toBeInTheDocument());
    expect(screen.getByText('Повторить')).toBeInTheDocument();
    expect(screen.queryByText('Открытых проблем нет')).not.toBeInTheDocument();
  });
});
