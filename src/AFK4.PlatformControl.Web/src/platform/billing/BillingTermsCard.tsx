import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Field } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { ErrorState } from '@/components/ui/states';
import { Loading, SkeletonCard, SkeletonControl } from '@/components/ui/skeletons';
import { useToast } from '@/components/ui/toast';
import { describeApiError } from '@/api/describeApiError';
import { useI18n } from '@/i18n/I18nProvider';
import type { PlansApi } from '@/api/platformClients/plans';
import type { BillingTermsDto } from '@/api/types';
import { useLoadable } from '../useLoadable';

type Client = Pick<PlansApi, 'getTerms' | 'updateTerms'>;

/**
 * Условия оплаты для клубов (спека тарифов клуба, §3–§5). Ноль дней выключает предложение: клуб
 * не увидит кнопку пробного периода или обещанного платежа.
 */
export function BillingTermsCard({ client, canManage }: { client: Client; canManage: boolean }) {
  const { t } = useI18n();
  const state = useLoadable(() => client.getTerms());

  if (state.status === 'loading') {
    return (
      <Loading>
        <SkeletonCard description>
          <div className="mgmt-form-grid">
            <SkeletonControl /><SkeletonControl /><SkeletonControl />
          </div>
          {canManage ? <SkeletonControl width="10rem" /> : null}
        </SkeletonCard>
      </Loading>
    );
  }
  if (state.status === 'error') {
    return <ErrorState message={state.message} retryLabel={state.canRetry ? t('state.retry') : undefined} onRetry={state.canRetry ? state.retry : undefined} />;
  }

  return <TermsForm client={client} terms={state.data} onSaved={state.apply} canManage={canManage} />;
}

type Draft = { trialDays: string; promisedPaymentDays: string; fallbackAfterOverdueDays: string };
const toDraft = (terms: BillingTermsDto): Draft => ({
  trialDays: String(terms.trialDays),
  promisedPaymentDays: String(terms.promisedPaymentDays),
  fallbackAfterOverdueDays: String(terms.fallbackAfterOverdueDays)
});

// Отдельный компонент — не приём поверх useLoadable: draft должен взять значения ровно один раз,
// когда условия загрузились, и больше не подменяться собой при перерисовках карточки. Компонент
// монтируется впервые вместе с готовыми данными, поэтому ленивый useState — тот самый один раз,
// без эффекта, который мог бы догнать правки в поле собственным перезапуском.
function TermsForm({ client, terms, onSaved, canManage }: {
  client: Client;
  terms: BillingTermsDto;
  onSaved: (next: BillingTermsDto) => void;
  canManage: boolean;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [draft, setDraft] = useState<Draft>(() => toDraft(terms));
  const [pending, setPending] = useState(false);
  const [invalid, setInvalid] = useState<Set<keyof Draft>>(new Set());

  // «-5», «2,5» и пустое поле раньше молча становились нулём — а ноль здесь что-то выключает или
  // убирает. Не число дней — не сохраняем и говорим у поля.
  const days = (value: string): number | null => {
    if (value.trim() === '') return null;
    const parsed = Number(value);
    return Number.isInteger(parsed) && parsed >= 0 ? parsed : null;
  };
  async function save() {
    const parsed = {
      trialDays: days(draft.trialDays),
      promisedPaymentDays: days(draft.promisedPaymentDays),
      fallbackAfterOverdueDays: days(draft.fallbackAfterOverdueDays)
    };
    const wrong = new Set((Object.keys(parsed) as (keyof Draft)[]).filter(key => parsed[key] === null));
    setInvalid(wrong);
    if (wrong.size > 0) return;
    setPending(true);
    try {
      const saved = await client.updateTerms({
        trialDays: parsed.trialDays!,
        promisedPaymentDays: parsed.promisedPaymentDays!,
        fallbackAfterOverdueDays: parsed.fallbackAfterOverdueDays!
      });
      onSaved(saved);
      toast({ title: t('platform.billing.terms.saved'), variant: 'success' });
    } catch (cause) {
      toast({ title: describeApiError(cause, t), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  const field = (id: keyof Draft, label: string, hint?: string) => (
    <Field
      label={label}
      htmlFor={`terms-${id}`}
      hint={hint}
      error={invalid.has(id) ? t('platform.billing.terms.invalid') : undefined}
    >
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
          {field('fallbackAfterOverdueDays', t('platform.billing.terms.fallbackDays'), t('platform.billing.terms.fallbackHint'))}
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
