import { useEffect, useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Field } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { useToast } from '@/components/ui/toast';
import { describeApiError } from '@/api/describeApiError';
import { useI18n } from '@/i18n/I18nProvider';
import type { PlansApi } from '@/api/platformClients/plans';
import type { BillingTermsDto } from '@/api/types';

type Client = Pick<PlansApi, 'getTerms' | 'updateTerms'>;

/**
 * Условия оплаты для клубов (спека тарифов клуба, §3–§5). Ноль дней выключает предложение: клуб
 * не увидит кнопку пробного периода или обещанного платежа.
 */
export function BillingTermsCard({ client, canManage }: { client: Client; canManage: boolean }) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [terms, setTerms] = useState<BillingTermsDto | null>(null);
  const [draft, setDraft] = useState({ trialDays: '', promisedPaymentDays: '', fallbackAfterOverdueDays: '' });
  const [pending, setPending] = useState(false);

  useEffect(() => {
    let active = true;
    client.getTerms().then(loaded => {
      if (!active) return;
      setTerms(loaded);
      setDraft({
        trialDays: String(loaded.trialDays),
        promisedPaymentDays: String(loaded.promisedPaymentDays),
        fallbackAfterOverdueDays: String(loaded.fallbackAfterOverdueDays)
      });
    }).catch(cause => { if (active) toast({ title: describeApiError(cause, t), variant: 'error' }); });
    return () => { active = false; };
  }, [client, t, toast]);

  if (terms === null) return null;

  const days = (value: string) => Math.max(0, Math.trunc(Number(value) || 0));
  async function save() {
    setPending(true);
    try {
      const saved = await client.updateTerms({
        trialDays: days(draft.trialDays),
        promisedPaymentDays: days(draft.promisedPaymentDays),
        fallbackAfterOverdueDays: days(draft.fallbackAfterOverdueDays)
      });
      setTerms(saved);
      toast({ title: t('platform.billing.terms.saved'), variant: 'success' });
    } catch (cause) {
      toast({ title: describeApiError(cause, t), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  const field = (id: keyof typeof draft, label: string) => (
    <Field label={label} htmlFor={`terms-${id}`}>
      <Input
        id={`terms-${id}`}
        type="number"
        min="0"
        value={draft[id]}
        disabled={!canManage || pending}
        onChange={event => setDraft({ ...draft, [id]: event.target.value })}
      />
    </Field>
  );

  return (
    <Card>
      <CardHeader><CardTitle>{t('platform.billing.terms.title')}</CardTitle></CardHeader>
      <CardContent>
        <p className="mgmt-drawer-hint">{t('platform.billing.terms.lead')}</p>
        <div className="mgmt-form-grid">
          {field('trialDays', t('platform.billing.terms.trialDays'))}
          {field('promisedPaymentDays', t('platform.billing.terms.promisedPaymentDays'))}
          {field('fallbackAfterOverdueDays', t('platform.billing.terms.fallbackDays'))}
        </div>
        {canManage ? (
          <div>
            <Button disabled={pending} onClick={() => void save()}>{t('platform.billing.terms.save')}</Button>
          </div>
        ) : null}
      </CardContent>
    </Card>
  );
}
