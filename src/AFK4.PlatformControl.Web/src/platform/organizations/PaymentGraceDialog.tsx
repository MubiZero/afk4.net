import { useState } from 'react';
import { Dialog } from '@/components/ui/dialog';
import { Field } from '@/components/ui/field';
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

const EMPTY_PATCH = {
  planCode: null,
  billingInterval: null,
  status: null,
  cancelAtPeriodEnd: null,
  amountMinorUnits: null,
  currentPeriodEndUtc: null
} as const;

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
        ...EMPTY_PATCH,
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
        ...EMPTY_PATCH,
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
    <Dialog
      open
      title={t('platform.organization.paymentGraceDialog.title')}
      description={t('platform.organization.paymentGraceDialog.description')}
      onClose={onClose}
      footer={
        <>
          <Button variant="outline" disabled={pending} onClick={onClose}>{t('platform.organization.paymentGraceDialog.cancel')}</Button>
          <Button disabled={pending || until === ''} onClick={() => void save()}>{t('platform.organization.paymentGraceDialog.save')}</Button>
        </>
      }
    >
      <div className="mgmt-form">
        <div className="pc-passport-row">
          <dt>{t('platform.organization.paymentGraceDialog.current')}</dt>
          <dd>{currentGraceUntilUtc !== null ? formatDate(currentGraceUntilUtc) : t('platform.organization.paymentGraceDialog.none')}</dd>
        </div>
        <Field label={t('platform.organization.paymentGraceDialog.until')} htmlFor="grace-until">
          <Input id="grace-until" type="date" value={until} onChange={event => setUntil(event.target.value)} />
        </Field>
        {currentGraceUntilUtc !== null ? (
          <div>
            <Button variant="outline" size="sm" disabled={pending} onClick={() => void clear()}>
              {t('platform.organization.paymentGraceDialog.clear')}
            </Button>
          </div>
        ) : null}
      </div>
    </Dialog>
  );
}
