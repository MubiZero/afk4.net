import { useState, type FormEvent } from 'react';
import { PlatformApiClient, PlatformApiError } from '../api/platformApi';
import { TenantStatus, type TenantDetail } from '../api/types';
import { ErrorBanner, Field, StatusBadge, formatDate } from './ui';

export interface StatusControlProps {
  client: PlatformApiClient;
  tenant: TenantDetail;
  onUpdated: (next: TenantDetail) => void;
}

export function StatusControl({ client, tenant, onUpdated }: StatusControlProps) {
  const [status, setStatus] = useState(tenant.status);
  const [reason, setReason] = useState(tenant.statusReason ?? '');
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      const next = await client.updateStatus(tenant.organizationId, status, reason);
      onUpdated(next);
    } catch (cause) {
      setError(cause instanceof PlatformApiError ? cause.message : (cause as Error).message);
    } finally {
      setSubmitting(false);
    }
  }

  const requiresReason = status !== TenantStatus.Active;

  return (
    <section className="section">
      <header className="section-header">
        <h2>Status</h2>
        <div><StatusBadge status={tenant.status} /> · changed {formatDate(tenant.statusChangedAtUtc)}</div>
      </header>
      <form className="form" onSubmit={handleSubmit}>
        <ErrorBanner message={error} onDismiss={() => setError(null)} />
        <Field label="New status" htmlFor="status-select">
          <select id="status-select" value={status} onChange={e => setStatus(e.target.value)}>
            <option value={TenantStatus.Active}>active</option>
            <option value={TenantStatus.Suspended}>suspended</option>
            <option value={TenantStatus.DeletionPending}>deletion_pending</option>
          </select>
        </Field>
        <Field
          label={requiresReason ? 'Reason (required)' : 'Reason (optional)'}
          htmlFor="status-reason"
          hint={requiresReason ? 'Will be shown in audit and the support UI.' : undefined}
        >
          <textarea
            id="status-reason"
            value={reason}
            onChange={e => setReason(e.target.value)}
            rows={3}
            required={requiresReason}
          />
        </Field>
        <div className="actions">
          <button type="submit" className="primary" disabled={isSubmitting}>
            {isSubmitting ? 'Saving…' : 'Apply status'}
          </button>
        </div>
      </form>
    </section>
  );
}
