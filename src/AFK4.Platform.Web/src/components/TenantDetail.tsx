import { useCallback, useEffect, useState } from 'react';
import { PlatformApiClient, PlatformApiError } from '../api/platformApi';
import type { OwnerInvite, TenantDetail as TenantDetailDto } from '../api/types';
import { ErrorBanner, Loading, StatusBadge, formatDate } from './ui';
import { StatusControl } from './StatusControl';
import { PlanControl } from './PlanControl';
import { LimitsControl } from './LimitsControl';
import { OwnerInvitesSection } from './OwnerInvitesSection';
import { SupportNotesSection } from './SupportNotesSection';
import { HealthSection } from './HealthSection';

export interface TenantDetailViewProps {
  client: PlatformApiClient;
  organizationId: string;
  initialInvite?: OwnerInvite | null;
  onBack: () => void;
}

export function TenantDetailView({ client, organizationId, initialInvite, onBack }: TenantDetailViewProps) {
  const [tenant, setTenant] = useState<TenantDetailDto | null>(null);
  const [isLoading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const reload = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await client.getTenant(organizationId);
      setTenant(data);
    } catch (cause) {
      if (cause instanceof PlatformApiError) {
        setError(cause.message);
      } else if (cause instanceof Error) {
        setError(cause.message);
      } else {
        setError('Failed to load tenant detail.');
      }
    } finally {
      setLoading(false);
    }
  }, [client, organizationId]);

  useEffect(() => {
    void reload();
  }, [reload]);

  return (
    <div className="page">
      <div className="page-header">
        <div>
          <button type="button" onClick={onBack}>← Back</button>
          {tenant !== null && <h1>{tenant.name} <small><code>{tenant.slug}</code></small></h1>}
          {tenant === null && <h1>Tenant detail</h1>}
        </div>
        {tenant !== null && (
          <div>
            <StatusBadge status={tenant.status} />
            <span className="muted"> · {tenant.planCode} · {tenant.subscriptionStatus}</span>
          </div>
        )}
      </div>
      <ErrorBanner message={error} onDismiss={() => setError(null)} />
      {isLoading && tenant === null && <Loading label="Loading tenant…" />}
      {tenant !== null && (
        <>
          <section className="section">
            <header className="section-header">
              <h2>Overview</h2>
            </header>
            <dl className="kv">
              <dt>Slug</dt><dd><code>{tenant.slug}</code></dd>
              <dt>Created</dt><dd>{formatDate(tenant.createdAtUtc)}</dd>
              <dt>Updated</dt><dd>{formatDate(tenant.updatedAtUtc)}</dd>
              <dt>Branches</dt>
              <dd>
                <ul>
                  {tenant.branches.map(branch => (
                    <li key={branch.branchId}>
                      <code>{branch.slug}</code> — {branch.name} ({branch.city})
                    </li>
                  ))}
                </ul>
              </dd>
              {tenant.statusReason !== null && (
                <>
                  <dt>Status reason</dt>
                  <dd>{tenant.statusReason}</dd>
                </>
              )}
            </dl>
          </section>
          <StatusControl client={client} tenant={tenant} onUpdated={setTenant} />
          <PlanControl client={client} tenant={tenant} onUpdated={setTenant} />
          <LimitsControl client={client} tenant={tenant} onUpdated={setTenant} />
          <OwnerInvitesSection
            client={client}
            organizationId={tenant.organizationId}
            branches={tenant.branches}
            initialInvite={initialInvite ?? null}
          />
          <SupportNotesSection client={client} organizationId={tenant.organizationId} />
          <HealthSection client={client} organizationId={tenant.organizationId} />
        </>
      )}
    </div>
  );
}
