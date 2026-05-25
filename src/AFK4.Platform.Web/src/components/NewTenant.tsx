import { useState, type FormEvent } from 'react';
import { PlatformApiClient, PlatformApiError } from '../api/platformApi';
import {
  SubscriptionStatus,
  TenantPlanCode,
  type CreateTenantResponse,
  type TenantLimits
} from '../api/types';
import { ErrorBanner, Field } from './ui';

export interface NewTenantProps {
  client: PlatformApiClient;
  onCreated: (response: CreateTenantResponse) => void;
  onCancel: () => void;
}

interface FormState {
  organizationSlug: string;
  organizationName: string;
  branchSlug: string;
  branchName: string;
  branchCity: string;
  planCode: string;
  subscriptionStatus: string;
  ownerUserName: string;
  ownerDisplayName: string;
  maxBranches: string;
  maxDevicesPerBranch: string;
  maxConcurrentSessions: string;
  maxStaffUsersPerBranch: string;
}

const defaultState: FormState = {
  organizationSlug: '',
  organizationName: '',
  branchSlug: 'main',
  branchName: 'Main Branch',
  branchCity: '',
  planCode: TenantPlanCode.Starter,
  subscriptionStatus: SubscriptionStatus.Trial,
  ownerUserName: '',
  ownerDisplayName: '',
  maxBranches: '',
  maxDevicesPerBranch: '',
  maxConcurrentSessions: '',
  maxStaffUsersPerBranch: ''
};

export function NewTenant({ client, onCreated, onCancel }: NewTenantProps) {
  const [form, setForm] = useState<FormState>(defaultState);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setSubmitting] = useState(false);

  function update(field: keyof FormState, value: string) {
    setForm(current => ({ ...current, [field]: value }));
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      const limits = buildLimits(form);
      const response = await client.createTenant({
        organizationSlug: form.organizationSlug.trim(),
        organizationName: form.organizationName.trim(),
        branchSlug: form.branchSlug.trim(),
        branchName: form.branchName.trim(),
        branchCity: form.branchCity.trim(),
        planCode: form.planCode,
        subscriptionStatus: form.subscriptionStatus,
        limits,
        ownerUserName: form.ownerUserName.trim() === '' ? null : form.ownerUserName.trim(),
        ownerDisplayName: form.ownerDisplayName.trim() === '' ? null : form.ownerDisplayName.trim(),
        ownerInviteLifetime: null
      });
      onCreated(response);
    } catch (cause) {
      if (cause instanceof PlatformApiError) {
        setError(cause.message);
      } else if (cause instanceof Error) {
        setError(cause.message);
      } else {
        setError('Failed to create tenant.');
      }
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="page page-narrow">
      <div className="page-header">
        <h1>New tenant</h1>
        <button type="button" onClick={onCancel} disabled={isSubmitting}>Cancel</button>
      </div>
      <form className="form" onSubmit={handleSubmit}>
        <ErrorBanner message={error} onDismiss={() => setError(null)} />
        <fieldset className="fieldset">
          <legend>Organization</legend>
          <Field label="Tenant key" htmlFor="org-slug" hint="URL-safe key: 3-64 chars, a-z, 0-9, hyphens between segments.">
            <input id="org-slug" value={form.organizationSlug} onChange={e => update('organizationSlug', e.target.value)} required />
          </Field>
          <Field label="Name" htmlFor="org-name">
            <input id="org-name" value={form.organizationName} onChange={e => update('organizationName', e.target.value)} required />
          </Field>
        </fieldset>
        <fieldset className="fieldset">
          <legend>First branch</legend>
          <Field label="Branch key" htmlFor="branch-slug">
            <input id="branch-slug" value={form.branchSlug} onChange={e => update('branchSlug', e.target.value)} required />
          </Field>
          <Field label="Branch name" htmlFor="branch-name">
            <input id="branch-name" value={form.branchName} onChange={e => update('branchName', e.target.value)} required />
          </Field>
          <Field label="City" htmlFor="branch-city">
            <input id="branch-city" value={form.branchCity} onChange={e => update('branchCity', e.target.value)} required />
          </Field>
        </fieldset>
        <fieldset className="fieldset">
          <legend>Plan</legend>
          <Field label="Plan code" htmlFor="plan-code">
            <select id="plan-code" value={form.planCode} onChange={e => update('planCode', e.target.value)}>
              <option value={TenantPlanCode.Starter}>starter</option>
              <option value={TenantPlanCode.Growth}>growth</option>
              <option value={TenantPlanCode.Scale}>scale</option>
            </select>
          </Field>
          <Field label="Subscription status" htmlFor="subscription-status">
            <select id="subscription-status" value={form.subscriptionStatus} onChange={e => update('subscriptionStatus', e.target.value)}>
              <option value={SubscriptionStatus.Trial}>trial</option>
              <option value={SubscriptionStatus.Active}>active</option>
              <option value={SubscriptionStatus.PastDue}>past_due</option>
              <option value={SubscriptionStatus.Cancelled}>cancelled</option>
            </select>
          </Field>
        </fieldset>
        <fieldset className="fieldset">
          <legend>Limits (optional)</legend>
          <Field label="Max branches" htmlFor="lim-branches">
            <input id="lim-branches" type="number" min={0} value={form.maxBranches} onChange={e => update('maxBranches', e.target.value)} />
          </Field>
          <Field label="Max devices per branch" htmlFor="lim-devices">
            <input id="lim-devices" type="number" min={0} value={form.maxDevicesPerBranch} onChange={e => update('maxDevicesPerBranch', e.target.value)} />
          </Field>
          <Field label="Max concurrent sessions" htmlFor="lim-sessions">
            <input id="lim-sessions" type="number" min={0} value={form.maxConcurrentSessions} onChange={e => update('maxConcurrentSessions', e.target.value)} />
          </Field>
          <Field label="Max staff users per branch" htmlFor="lim-staff">
            <input id="lim-staff" type="number" min={0} value={form.maxStaffUsersPerBranch} onChange={e => update('maxStaffUsersPerBranch', e.target.value)} />
          </Field>
        </fieldset>
        <fieldset className="fieldset">
          <legend>Setup code recipient (optional)</legend>
          <Field label="Owner user name (email)" htmlFor="owner-username">
            <input id="owner-username" value={form.ownerUserName} onChange={e => update('ownerUserName', e.target.value)} />
          </Field>
          <Field label="Owner display name" htmlFor="owner-display">
            <input id="owner-display" value={form.ownerDisplayName} onChange={e => update('ownerDisplayName', e.target.value)} />
          </Field>
        </fieldset>
        <div className="actions">
          <button type="submit" className="primary" disabled={isSubmitting}>
            {isSubmitting ? 'Creating…' : 'Create tenant'}
          </button>
        </div>
      </form>
    </div>
  );
}

function buildLimits(form: FormState): TenantLimits | null {
  const parsed: TenantLimits = {
    maxBranches: parseOptional(form.maxBranches),
    maxDevicesPerBranch: parseOptional(form.maxDevicesPerBranch),
    maxConcurrentSessions: parseOptional(form.maxConcurrentSessions),
    maxStaffUsersPerBranch: parseOptional(form.maxStaffUsersPerBranch)
  };
  if (Object.values(parsed).every(value => value === null)) {
    return null;
  }
  return parsed;
}

function parseOptional(value: string): number | null {
  if (value.trim() === '') {
    return null;
  }
  const parsed = Number.parseInt(value, 10);
  return Number.isNaN(parsed) ? null : parsed;
}
