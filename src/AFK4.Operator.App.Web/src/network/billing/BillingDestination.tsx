import { useMemo } from 'react';
import type { JSX, ReactNode } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../../management/ManagementScreen';
import { Money } from '../../operatorPrimitives';
import { createAuthenticatedOperatorClients } from '../../operatorHelpers';
import type { OperatorBackendContext } from '../../operatorTypes';
import { useBilling, type BillingClient } from './useBilling';
import { subscriptionStatusLabelKey, subscriptionStatusTone, invoiceStatusLabelKey, invoiceStatusTone } from './billingModel';

const INVOICES_GRID = '0.6fr 1fr 1fr 1fr 0.8fr';

// Read-only «Сеть → Подписка» screen — org subscription plan/status/period + invoice history.
// No plan-management actions here by design (upgrade/cancel/payment-method live on the platform
// side); this is a status mirror for the org's own operators (owner-exclusive, see billingModel
// gate in networkNav.ts).
export function BillingDestination({ backend }: { backend: OperatorBackendContext | null }): JSX.Element {
  const { t, formatDate } = useI18n();

  const client = useMemo<BillingClient | null>(() => {
    if (backend === null) return null;
    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    return {
      getSubscription: (id) => clients.orgBilling.getSubscription(id),
      listInvoices: (id) => clients.orgBilling.listInvoices(id)
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [backend?.config.platformBaseUrl, backend?.session.accessToken]);

  const state = useBilling(
    client ?? { getSubscription: async () => { throw new Error('no backend'); }, listInvoices: async () => [] },
    backend?.session.organizationId ?? ''
  );

  const screenState = backend === null ? 'loading' : state.status === 'loading' ? 'loading' : state.status === 'error' ? 'error' : 'ready';

  return (
    <ManagementScreen
      title={t('op.network.dest.billing')}
      subtitle={t('op.network.dest.billing.subtitle')}
      contentWidth="full"
      state={screenState}
      onRetry={state.status === 'error' ? state.retry : undefined}
    >
      {state.status === 'ready' && (
        <>
          <section className="management-panel network-billing-sub">
            <h3>{t('op.network.billing.subscription')}</h3>
            <dl className="network-billing-grid">
              <Field label={t('op.network.billing.plan')} value={state.subscription.planCode} />
              <Field
                label={t('op.network.billing.status')}
                value={
                  <span className={`ui-chip ui-chip--status ${subscriptionStatusTone(state.subscription.status)}`}>
                    {subscriptionStatusLabelKey(state.subscription.status)
                      ? t(subscriptionStatusLabelKey(state.subscription.status)!)
                      : state.subscription.status}
                  </span>
                }
              />
              <Field
                label={t('op.network.billing.amount')}
                value={<Money minorUnits={state.subscription.amountMinorUnits} currencyCode={state.subscription.currencyCode} />}
              />
              <Field
                label={t('op.network.billing.period')}
                value={`${formatDate(state.subscription.currentPeriodStartUtc)} — ${formatDate(state.subscription.currentPeriodEndUtc)}`}
              />
              <Field
                label={t('op.network.billing.nextInvoice')}
                value={state.subscription.nextInvoiceUtc ? formatDate(state.subscription.nextInvoiceUtc) : '—'}
              />
            </dl>
          </section>

          <section className="management-panel network-billing-invoices">
            <h3>{t('op.network.billing.invoices')}</h3>
            {state.invoices.length === 0 ? (
              <p className="network-billing-empty">{t('op.network.billing.invoices.empty')}</p>
            ) : (
              <div className="table-panel">
                <div className="ctable-head" style={{ gridTemplateColumns: INVOICES_GRID }} aria-hidden="true">
                  <span>{t('op.network.billing.col.number')}</span>
                  <span>{t('op.network.billing.col.issued')}</span>
                  <span>{t('op.network.billing.col.due')}</span>
                  <span>{t('op.network.billing.col.amount')}</span>
                  <span>{t('op.network.billing.col.status')}</span>
                </div>
                <div className="ctable-body">
                  {state.invoices.map((inv) => (
                    <div key={inv.invoiceId} className="ctable-row" style={{ gridTemplateColumns: INVOICES_GRID }}>
                      <span>{inv.number}</span>
                      <span>{formatDate(inv.issuedAtUtc)}</span>
                      <span>{formatDate(inv.dueAtUtc)}</span>
                      <span><Money minorUnits={inv.amountMinorUnits} currencyCode={inv.currencyCode} /></span>
                      <span className={`ui-chip ui-chip--status ${invoiceStatusTone(inv.status)}`}>
                        {invoiceStatusLabelKey(inv.status) ? t(invoiceStatusLabelKey(inv.status)!) : inv.status}
                      </span>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </section>
        </>
      )}
    </ManagementScreen>
  );
}

function Field({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div className="network-stat">
      <dt>{label}</dt>
      <dd>{value}</dd>
    </div>
  );
}
