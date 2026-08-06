import { useState } from 'react';
import { Dialog } from '@/components/ui/dialog';
import { Field } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Switch } from '@/components/ui/switch';
import { Select } from '@/components/ui/select';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import { minorToMajor, majorToMinor } from '@/lib/money';
import type { SubscriptionsApi } from '@/api/platformClients/subscriptions';
import type { OrganizationSubscription } from '@/api/types';
import { SUBSCRIPTION_STATUS_LABEL } from '@/platform/billing/billingModel';

type Client = Pick<SubscriptionsApi, 'updateSubscription'>;

const STATUS_OPTIONS = ['trial', 'active', 'past_due', 'cancelled'] as const;
const INTERVAL_OPTIONS = ['monthly', 'yearly'] as const;

interface Props {
  client: Client;
  organizationId: string;
  subscription: OrganizationSubscription;
  onClose: () => void;
  onUpdated: (next: OrganizationSubscription) => void;
}

export function SubscriptionDialog({ client, organizationId, subscription, onClose, onUpdated }: Props) {
  const { t, formatCurrency } = useI18n();
  const { toast } = useToast();
  const [interval, setInterval] = useState(subscription.billingInterval);
  const [status, setStatus] = useState(subscription.status);
  const [cancelAtPeriodEnd, setCancelAtPeriodEnd] = useState(subscription.cancelAtPeriodEnd);
  const [amount, setAmount] = useState(String(minorToMajor(subscription.amountMinorUnits)));
  const [currentPeriodEnd, setCurrentPeriodEnd] = useState(subscription.currentPeriodEndUtc.slice(0, 10));
  const [pending, setPending] = useState(false);

  const amountValue = Number.parseFloat(amount.replace(',', '.'));
  const amountValid = Number.isFinite(amountValue) && amountValue >= 0;

  async function submit() {
    if (!amountValid) return;
    setPending(true);
    try {
      const next = await client.updateSubscription(organizationId, {
        planCode: null,
        billingInterval: interval !== subscription.billingInterval ? interval : null,
        status: status !== subscription.status ? status : null,
        cancelAtPeriodEnd: cancelAtPeriodEnd !== subscription.cancelAtPeriodEnd ? cancelAtPeriodEnd : null,
        amountMinorUnits: majorToMinor(amountValue) !== subscription.amountMinorUnits ? majorToMinor(amountValue) : null,
        currentPeriodEndUtc: currentPeriodEnd !== subscription.currentPeriodEndUtc.slice(0, 10) ? new Date(`${currentPeriodEnd}T00:00:00Z`).toISOString() : null,
        paymentGraceUntilUtc: null,
        clearPaymentGrace: null
      });
      onUpdated(next);
      toast({ title: t('platform.organization.subscriptionDialog.updated'), variant: 'success' });
    } catch {
      toast({ title: t('platform.organization.action.error'), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  return (
    <Dialog
      open
      title={t('platform.organization.subscriptionDialog.title')}
      onClose={onClose}
      footer={
        <>
          <Button variant="outline" disabled={pending} onClick={onClose}>{t('platform.organization.subscriptionDialog.cancel')}</Button>
          <Button disabled={pending || !amountValid} onClick={() => void submit()}>{t('platform.organization.subscriptionDialog.save')}</Button>
        </>
      }
    >
      <div className="mgmt-form">
        <Field label={t('platform.organization.subscriptionForm.interval')} htmlFor="subscription-interval">
          <Select id="subscription-interval" value={interval} onChange={event => setInterval(event.target.value)}>
            {INTERVAL_OPTIONS.map(option => (
              <option key={option} value={option}>
                {t(option === 'monthly' ? 'platform.billing.interval.monthly' : 'platform.billing.interval.yearly')}
              </option>
            ))}
          </Select>
        </Field>

        <Field label={t('platform.organization.subscriptionForm.status')} htmlFor="subscription-status">
          <Select id="subscription-status" value={status} onChange={event => setStatus(event.target.value)}>
            {STATUS_OPTIONS.map(option => <option key={option} value={option}>{t(SUBSCRIPTION_STATUS_LABEL[option])}</option>)}
          </Select>
        </Field>

        <label className="pc-check-row">
          <Switch checked={cancelAtPeriodEnd} onCheckedChange={setCancelAtPeriodEnd} />
          {t('platform.organization.subscriptionForm.cancelAtPeriodEnd')}
        </label>

        <Field
          label={t('platform.organization.subscriptionDialog.amount')}
          htmlFor="subscription-amount"
          hint={amountValid ? formatCurrency(amountValue, subscription.currencyCode) : undefined}
        >
          <Input id="subscription-amount" inputMode="decimal" value={amount} onChange={event => setAmount(event.target.value)} />
        </Field>

        <Field label={t('platform.organization.subscriptionDialog.currentPeriodEnd')} htmlFor="subscription-period-end">
          <Input id="subscription-period-end" type="date" value={currentPeriodEnd} onChange={event => setCurrentPeriodEnd(event.target.value)} />
        </Field>
      </div>
    </Dialog>
  );
}
