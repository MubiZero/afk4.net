import { useState, type FormEvent } from 'react';
import { PlatformApiClient, PlatformApiError } from '../api/platformApi';
import type { TenantDetail, TenantLimits } from '../api/types';
import { ErrorBanner, Field } from './ui';

export interface LimitsControlProps {
  client: PlatformApiClient;
  tenant: TenantDetail;
  onUpdated: (next: TenantDetail) => void;
}

interface LimitsForm {
  maxBranches: string;
  maxDevicesPerBranch: string;
  maxConcurrentSessions: string;
  maxStaffUsersPerBranch: string;
}

export function LimitsControl({ client, tenant, onUpdated }: LimitsControlProps) {
  const [form, setForm] = useState<LimitsForm>(toForm(tenant.limits));
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setSubmitting] = useState(false);

  function update(field: keyof LimitsForm, value: string) {
    setForm(current => ({ ...current, [field]: value }));
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      const limits = toLimits(form);
      const next = await client.updateLimits(tenant.organizationId, limits);
      onUpdated(next);
    } catch (cause) {
      setError(cause instanceof PlatformApiError ? cause.message : (cause as Error).message);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <section className="section">
      <header className="section-header">
        <h2>Limits</h2>
      </header>
      <form className="form form-grid" onSubmit={handleSubmit}>
        <ErrorBanner message={error} onDismiss={() => setError(null)} />
        <Field label="Max branches" htmlFor="ed-branches">
          <input id="ed-branches" type="number" min={0} value={form.maxBranches} onChange={e => update('maxBranches', e.target.value)} />
        </Field>
        <Field label="Max devices per branch" htmlFor="ed-devices">
          <input id="ed-devices" type="number" min={0} value={form.maxDevicesPerBranch} onChange={e => update('maxDevicesPerBranch', e.target.value)} />
        </Field>
        <Field label="Max concurrent sessions" htmlFor="ed-sessions">
          <input id="ed-sessions" type="number" min={0} value={form.maxConcurrentSessions} onChange={e => update('maxConcurrentSessions', e.target.value)} />
        </Field>
        <Field label="Max staff users per branch" htmlFor="ed-staff">
          <input id="ed-staff" type="number" min={0} value={form.maxStaffUsersPerBranch} onChange={e => update('maxStaffUsersPerBranch', e.target.value)} />
        </Field>
        <div className="actions">
          <button type="submit" className="primary" disabled={isSubmitting}>
            {isSubmitting ? 'Saving…' : 'Apply limits'}
          </button>
        </div>
      </form>
    </section>
  );
}

function toForm(limits: TenantLimits): LimitsForm {
  return {
    maxBranches: limits.maxBranches?.toString() ?? '',
    maxDevicesPerBranch: limits.maxDevicesPerBranch?.toString() ?? '',
    maxConcurrentSessions: limits.maxConcurrentSessions?.toString() ?? '',
    maxStaffUsersPerBranch: limits.maxStaffUsersPerBranch?.toString() ?? ''
  };
}

function toLimits(form: LimitsForm): TenantLimits {
  return {
    maxBranches: parseOptional(form.maxBranches),
    maxDevicesPerBranch: parseOptional(form.maxDevicesPerBranch),
    maxConcurrentSessions: parseOptional(form.maxConcurrentSessions),
    maxStaffUsersPerBranch: parseOptional(form.maxStaffUsersPerBranch)
  };
}

function parseOptional(value: string): number | null {
  if (value.trim() === '') {
    return null;
  }
  const parsed = Number.parseInt(value, 10);
  return Number.isNaN(parsed) ? null : parsed;
}
