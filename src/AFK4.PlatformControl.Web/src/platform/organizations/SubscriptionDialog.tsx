import { useState } from 'react';
import { Dialog, DialogContent, DialogTitle, DialogDescription, DialogFooter } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Switch } from '@/components/ui/switch';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select';
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
    <Dialog open onOpenChange={open => { if (!open) onClose(); }}>
      <DialogContent>
        <DialogTitle>{t('platform.organization.subscriptionDialog.title')}</DialogTitle>
        <DialogDescription className="sr-only">{t('platform.organization.subscriptionDialog.title')}</DialogDescription>
        <div className="flex flex-col gap-3">
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('platform.organization.subscriptionForm.interval')}</span>
            <Select value={interval} onValueChange={setInterval}>
              <SelectTrigger aria-label={t('platform.organization.subscriptionForm.interval')}><SelectValue /></SelectTrigger>
              <SelectContent>
                {INTERVAL_OPTIONS.map(i => <SelectItem key={i} value={i}>{t(i === 'monthly' ? 'platform.billing.interval.monthly' : 'platform.billing.interval.yearly')}</SelectItem>)}
              </SelectContent>
            </Select>
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('platform.organization.subscriptionForm.status')}</span>
            <Select value={status} onValueChange={setStatus}>
              <SelectTrigger aria-label={t('platform.organization.subscriptionForm.status')}><SelectValue /></SelectTrigger>
              <SelectContent>
                {STATUS_OPTIONS.map(s => <SelectItem key={s} value={s}>{t(SUBSCRIPTION_STATUS_LABEL[s])}</SelectItem>)}
              </SelectContent>
            </Select>
          </label>
          <label className="flex items-center justify-between text-sm">
            <span className="text-muted-foreground">{t('platform.organization.subscriptionForm.cancelAtPeriodEnd')}</span>
            <Switch checked={cancelAtPeriodEnd} onCheckedChange={setCancelAtPeriodEnd} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('platform.organization.subscriptionDialog.amount')}</span>
            <Input inputMode="decimal" aria-label={t('platform.organization.subscriptionDialog.amount')} value={amount} onChange={e => setAmount(e.target.value)} />
            <span className="mt-1 block text-xs text-muted-foreground">{amountValid ? formatCurrency(amountValue, subscription.currencyCode) : ''}</span>
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('platform.organization.subscriptionDialog.currentPeriodEnd')}</span>
            <Input type="date" aria-label={t('platform.organization.subscriptionDialog.currentPeriodEnd')} value={currentPeriodEnd} onChange={e => setCurrentPeriodEnd(e.target.value)} />
          </label>
        </div>
        <DialogFooter>
          <Button variant="outline" disabled={pending} onClick={onClose}>{t('platform.organization.subscriptionDialog.cancel')}</Button>
          <Button disabled={pending || !amountValid} onClick={() => void submit()}>{t('platform.organization.subscriptionDialog.save')}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
