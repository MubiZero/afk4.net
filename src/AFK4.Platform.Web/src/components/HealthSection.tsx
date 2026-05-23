import { useCallback, useEffect, useState } from 'react';
import { PlatformApiClient, PlatformApiError } from '../api/platformApi';
import type { TenantHealth } from '../api/types';
import { EmptyState, ErrorBanner, Loading, StatusBadge, formatDate } from './ui';

export interface HealthSectionProps {
  client: PlatformApiClient;
  organizationId: string;
}

export function HealthSection({ client, organizationId }: HealthSectionProps) {
  const [health, setHealth] = useState<TenantHealth | null>(null);
  const [isLoading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const reload = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await client.getHealth(organizationId);
      setHealth(data);
    } catch (cause) {
      if (cause instanceof PlatformApiError) {
        setError(cause.message);
      } else if (cause instanceof Error) {
        setError(cause.message);
      } else {
        setError('Failed to load tenant health.');
      }
    } finally {
      setLoading(false);
    }
  }, [client, organizationId]);

  useEffect(() => {
    void reload();
  }, [reload]);

  return (
    <section className="section">
      <header className="section-header">
        <h2>Health</h2>
        <button type="button" onClick={() => void reload()} disabled={isLoading}>Refresh</button>
      </header>
      <ErrorBanner message={error} onDismiss={() => setError(null)} />
      {isLoading && health === null && <Loading label="Loading health…" />}
      {health !== null && (
        <>
          <dl className="kv">
            <dt>Status</dt><dd><StatusBadge status={health.status} /></dd>
            <dt>Branches</dt><dd>{health.branchCount}</dd>
            <dt>Devices</dt><dd>{health.deviceCount}</dd>
            <dt>Active staff users</dt><dd>{health.activeStaffUserCount}</dd>
            <dt>Latest staff sign-in</dt><dd>{formatDate(health.latestStaffSignInAtUtc)}</dd>
            <dt>Latest migration</dt><dd>{health.latestMigration ?? '—'}</dd>
            <dt>Recent denied audits (7d)</dt><dd>{health.recentErrorCount}</dd>
          </dl>
          <h3>Recent denied audits</h3>
          {health.recentErrors.length === 0 ? (
            <EmptyState>No denied audit records in the last 7 days.</EmptyState>
          ) : (
            <table className="table">
              <thead>
                <tr>
                  <th>Time</th>
                  <th>Source</th>
                  <th>Action</th>
                  <th>Outcome</th>
                  <th>Preview</th>
                </tr>
              </thead>
              <tbody>
                {health.recentErrors.map((entry, index) => (
                  <tr key={`${entry.createdAtUtc}-${index}`}>
                    <td>{formatDate(entry.createdAtUtc)}</td>
                    <td>{entry.source}</td>
                    <td>{entry.action}</td>
                    <td>{entry.outcome}</td>
                    <td className="truncate"><code>{entry.message ?? ''}</code></td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </>
      )}
    </section>
  );
}
