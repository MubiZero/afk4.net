import { useCallback, useEffect, useState } from 'react';
import { PlatformApiClient, PlatformApiError } from '../api/platformApi';
import type { TenantSummary } from '../api/types';
import { EmptyState, ErrorBanner, Loading, StatusBadge, formatDate } from './ui';

export interface TenantListProps {
  client: PlatformApiClient;
  onOpenTenant: (organizationId: string) => void;
  onCreateTenant: () => void;
}

export function TenantList({ client, onOpenTenant, onCreateTenant }: TenantListProps) {
  const [tenants, setTenants] = useState<TenantSummary[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setLoading] = useState(true);

  const reload = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await client.listTenants();
      setTenants(data);
    } catch (cause) {
      setError(toErrorMessage(cause));
    } finally {
      setLoading(false);
    }
  }, [client]);

  useEffect(() => {
    void reload();
  }, [reload]);

  return (
    <div className="page">
      <div className="page-header">
        <h1>Tenants</h1>
        <div className="actions">
          <button type="button" onClick={() => void reload()} disabled={isLoading}>
            Refresh
          </button>
          <button type="button" className="primary" onClick={onCreateTenant}>
            New tenant
          </button>
        </div>
      </div>
      <ErrorBanner message={error} onDismiss={() => setError(null)} />
      {isLoading && tenants === null && <Loading label="Loading tenants…" />}
      {tenants !== null && tenants.length === 0 && (
        <EmptyState>No tenants yet. Create the first one with “New tenant”.</EmptyState>
      )}
      {tenants !== null && tenants.length > 0 && (
        <table className="table">
          <thead>
            <tr>
              <th>Name</th>
              <th>Slug</th>
              <th>Status</th>
              <th>Plan</th>
              <th>Subscription</th>
              <th>Branches</th>
              <th>Updated</th>
            </tr>
          </thead>
          <tbody>
            {tenants.map(tenant => (
              <tr key={tenant.organizationId} className="row-clickable" onClick={() => onOpenTenant(tenant.organizationId)}>
                <td>{tenant.name}</td>
                <td><code>{tenant.slug}</code></td>
                <td><StatusBadge status={tenant.status} /></td>
                <td>{tenant.planCode}</td>
                <td>{tenant.subscriptionStatus}</td>
                <td>{tenant.branchCount}</td>
                <td>{formatDate(tenant.updatedAtUtc)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}

function toErrorMessage(cause: unknown): string {
  if (cause instanceof PlatformApiError) {
    return cause.message;
  }
  if (cause instanceof Error) {
    return cause.message;
  }
  return 'Failed to load tenants.';
}
