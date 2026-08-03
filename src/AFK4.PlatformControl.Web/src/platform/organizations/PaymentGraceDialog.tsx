import { useState } from 'react';
import { Dialog, DialogContent, DialogTitle, DialogDescription, DialogFooter } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { SubscriptionsApi } from '@/api/platformClients/subscriptions';
import type { OrganizationSubscription } from '@/api/types';

type Client = Pick<SubscriptionsApi, 'updateSubscription'>;

interface Props {
  client: Client;
  organizationId: string;
  currentGraceUntilUtc: string | null;
  onClose: () => void;
  onUpdated: (next: OrganizationSubscription) => void;
}

export function PaymentGraceDialog({ client, organizationId, currentGraceUntilUtc, onClose, onUpdated }: Props) {
  const { t, formatDate } = useI18n();
  const { toast } = useToast();
  const [until, setUntil] = useState(currentGraceUntilUtc !== null ? currentGraceUntilUtc.slice(0, 10) : '');
  const [pending, setPending] = useState(false);

  async function save() {
    if (until === '') return;
    setPending(true);
    try {
      const next = await client.updateSubscription(organizationId, {
        planCode: null,
        billingInterval: null,
        status: null,
        cancelAtPeriodEnd: null,
        amountMinorUnits: null,
        currentPeriodEndUtc: null,
        paymentGraceUntilUtc: new Date(`${until}T23:59:59Z`).toISOString(),
        clearPaymentGrace: null
      });
      onUpdated(next);
      toast({ title: t('platform.organization.paymentGraceDialog.updated'), variant: 'success' });
    } catch {
      toast({ title: t('platform.organization.action.error'), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  async function clear() {
    setPending(true);
    try {
      const next = await client.updateSubscription(organizationId, {
        planCode: null,
        billingInterval: null,
        status: null,
        cancelAtPeriodEnd: null,
        amountMinorUnits: null,
        currentPeriodEndUtc: null,
        paymentGraceUntilUtc: null,
        clearPaymentGrace: true
      });
      onUpdated(next);
      toast({ title: t('platform.organization.paymentGraceDialog.cleared'), variant: 'success' });
    } catch {
      toast({ title: t('platform.organization.action.error'), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  return (
    <Dialog open onOpenChange={open => { if (!open) onClose(); }}>
      <DialogContent>
        <DialogTitle>{t('platform.organization.paymentGraceDialog.title')}</DialogTitle>
        <DialogDescription>{t('platform.organization.paymentGraceDialog.description')}</DialogDescription>
        <div className="flex flex-col gap-3">
          <div className="flex items-center justify-between text-sm">
            <span className="text-muted-foreground">{t('platform.organization.paymentGraceDialog.current')}</span>
            <span className="font-medium">{currentGraceUntilUtc !== null ? formatDate(currentGraceUntilUtc) : t('platform.organization.paymentGraceDialog.none')}</span>
          </div>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('platform.organization.paymentGraceDialog.until')}</span>
            <Input type="date" aria-label={t('platform.organization.paymentGraceDialog.until')} value={until} onChange={e => setUntil(e.target.value)} />
          </label>
          {currentGraceUntilUtc !== null ? (
            <div>
              <Button variant="outline" size="sm" disabled={pending} onClick={() => void clear()}>
                {t('platform.organization.paymentGraceDialog.clear')}
              </Button>
            </div>
          ) : null}
        </div>
        <DialogFooter>
          <Button variant="outline" disabled={pending} onClick={onClose}>{t('platform.organization.paymentGraceDialog.cancel')}</Button>
          <Button disabled={pending || until === ''} onClick={() => void save()}>{t('platform.organization.paymentGraceDialog.save')}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
