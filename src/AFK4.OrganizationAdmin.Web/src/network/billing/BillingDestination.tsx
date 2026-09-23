import { useMemo } from 'react';
import type { JSX, ReactNode } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../../management/ManagementScreen';
import { Money } from '../../operatorPrimitives';
import { createAuthenticatedOperatorClients } from '../../operatorHelpers';
import { projectOperatorError } from '../../apiErrors';
import type { OperatorBackendContext } from '../../operatorTypes';
import { SectionState } from '../SectionState';
import { useBilling, type BillingClient } from './useBilling';
import { subscriptionStatusLabelKey, subscriptionStatusTone, invoiceStatusLabelKey, invoiceStatusTone } from './billingModel';

const INVOICES_GRID = '0.6fr 1fr 1fr 1fr 0.8fr';

// Read-only «Сеть → Подписка» screen — org subscription plan/status/period + invoice history.
// No plan-management actions here by design (upgrade/cancel/payment-method live on the platform
// side); this is a status mirror for the org's own operators (owner-exclusive, see billingModel
// gate in networkNav.ts).
// `client` подставляется в тестах — по той же причине, что у «Обновлений»: `mock.module` в bun
// течёт за пределы файла, и подмена общего фабричного хелпера задела бы соседние наборы.
export function BillingDestination({
  backend,
  client: injectedClient
}: {
  backend: OperatorBackendContext | null;
  client?: BillingClient;
}): JSX.Element {
  const { t, formatDate } = useI18n();

  const client = useMemo<BillingClient | null>(() => {
    if (injectedClient !== undefined) return injectedClient;
    if (backend === null) return null;
    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    return {
      getSubscription: (id) => clients.orgBilling.getSubscription(id),
      listInvoices: (id) => clients.orgBilling.listInvoices(id)
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [injectedClient, backend?.config.platformBaseUrl, backend?.session.accessToken]);

  const { subscription, invoices } = useBilling(
    client ?? { getSubscription: async () => { throw new Error('no backend'); }, listInvoices: async () => [] },
    backend?.session.organizationId ?? ''
  );

  // Весь экран меняется на ожидание или ошибку, только пока показать нечего. Как только пришла
  // хотя бы одна секция, она остаётся на экране, а соседняя говорит за себя сама.
  const nothingShown = subscription.status !== 'ready' && invoices.status !== 'ready';
  const screenState = backend === null ? 'loading'
    : !nothingShown ? 'ready'
    : subscription.status === 'loading' || invoices.status === 'loading' ? 'loading'
    : 'error';
  const retryAll = () => {
    if (subscription.status === 'error') subscription.retry();
    if (invoices.status === 'error') invoices.retry();
  };

  return (
    <ManagementScreen
      title={t('op.network.dest.billing')}
      subtitle={t('op.network.dest.billing.subtitle')}
      contentWidth="full"
      state={screenState}
      failure={subscription.status === 'error' ? projectOperatorError(subscription.error, t) : undefined}
      onRetry={retryAll}
    >
      {screenState === 'ready' && (
        <>
          <section className="management-panel network-billing-sub">
            <h3>{t('op.network.billing.subscription')}</h3>
            <SectionState section={subscription} failedTitle={t('op.network.billing.subscription.loadFailed')} />
            {subscription.status === 'ready' && (
              <dl className="network-billing-grid">
                <Field label={t('op.network.billing.plan')} value={subscription.data.planCode} />
                <Field
                  label={t('op.network.billing.status')}
                  value={
                    <span className={`ui-chip ui-chip--status ${subscriptionStatusTone(subscription.data.status)}`}>
                      {subscriptionStatusLabelKey(subscription.data.status)
                        ? t(subscriptionStatusLabelKey(subscription.data.status)!)
                        : subscription.data.status}
                    </span>
                  }
                />
                <Field
                  label={t('op.network.billing.amount')}
                  value={<Money minorUnits={subscription.data.amountMinorUnits} currencyCode={subscription.data.currencyCode} />}
                />
                <Field
                  label={t('op.network.billing.period')}
                  value={`${formatDate(subscription.data.currentPeriodStartUtc)} — ${formatDate(subscription.data.currentPeriodEndUtc)}`}
                />
                <Field
                  label={t('op.network.billing.nextInvoice')}
                  value={subscription.data.nextInvoiceUtc ? formatDate(subscription.data.nextInvoiceUtc) : '—'}
                />
              </dl>
            )}
          </section>

          <section className="management-panel network-billing-invoices">
            <h3>{t('op.network.billing.invoices')}</h3>
            <SectionState section={invoices} failedTitle={t('op.network.billing.invoices.loadFailed')} />
            {invoices.status !== 'ready' ? null : invoices.data.length === 0 ? (
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
                  {invoices.data.map((inv) => (
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
