import { useState, type FormEvent } from 'react';
import { PlatformApiClient, PlatformApiError } from '../api/platformApi';
import { SubscriptionStatus, TenantPlanCode, type TenantDetail } from '../api/types';
import { ErrorBanner, Field } from './ui';

export interface PlanControlProps {
  client: PlatformApiClient;
  tenant: TenantDetail;
  onUpdated: (next: TenantDetail) => void;
}

export function PlanControl({ client, tenant, onUpdated }: PlanControlProps) {
  const [planCode, setPlanCode] = useState(tenant.planCode);
  const [subscriptionStatus, setSubscriptionStatus] = useState(tenant.subscriptionStatus);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      const next = await client.updatePlan(tenant.organizationId, planCode, subscriptionStatus);
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
        <h2>Plan &amp; subscription</h2>
      </header>
      <form className="form form-inline" onSubmit={handleSubmit}>
        <ErrorBanner message={error} onDismiss={() => setError(null)} />
        <Field label="Plan" htmlFor="plan-code-edit">
          <select id="plan-code-edit" value={planCode} onChange={e => setPlanCode(e.target.value)}>
            <option value={TenantPlanCode.Starter}>starter</option>
            <option value={TenantPlanCode.Growth}>growth</option>
            <option value={TenantPlanCode.Scale}>scale</option>
          </select>
        </Field>
        <Field label="Subscription" htmlFor="subscription-edit">
          <select id="subscription-edit" value={subscriptionStatus} onChange={e => setSubscriptionStatus(e.target.value)}>
            <option value={SubscriptionStatus.Trial}>trial</option>
            <option value={SubscriptionStatus.Active}>active</option>
            <option value={SubscriptionStatus.PastDue}>past_due</option>
            <option value={SubscriptionStatus.Cancelled}>cancelled</option>
          </select>
        </Field>
        <button type="submit" className="primary" disabled={isSubmitting}>
          {isSubmitting ? 'Saving…' : 'Apply plan'}
        </button>
      </form>
    </section>
  );
}
