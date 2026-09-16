import { useEffect, useState } from 'react';
import { Dialog } from '@/components/ui/dialog';
import { Field } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Switch } from '@/components/ui/switch';
import { Select } from '@/components/ui/select';
import { useToast } from '@/components/ui/toast';
import { describeApiError } from '@/api/describeApiError';
import { useI18n } from '@/i18n/I18nProvider';
import { minorToMajor } from '@/lib/money';
import type { PlansApi } from '@/api/platformClients/plans';
import type { SubscriptionsApi } from '@/api/platformClients/subscriptions';
import type { OrganizationSubscription, SubscriptionPlan } from '@/api/types';
import { SUBSCRIPTION_STATUS_LABEL } from '@/platform/billing/billingModel';
import {
  BILLING_INTERVAL_OPTIONS,
  SUBSCRIPTION_STATUS_OPTIONS,
  subscriptionFormToRequest,
  subscriptionToForm,
  validateSubscriptionForm,
  type DiscountKind
} from './subscriptionForm';

type Client = Pick<SubscriptionsApi, 'updateSubscription'>;
type Plans = Pick<PlansApi, 'listPlans'>;

const DISCOUNT_KINDS: readonly DiscountKind[] = ['none', 'percent', 'amount'];

interface Props {
  client: Client;
  /// Каталог тарифов. Без него список планов пуст, и клуб остаётся на своём — именно так панель
  /// и работала: `planCode` всегда уходил пустым, и перевести клуб с «Старта» было негде.
  plansClient: Plans;
  organizationId: string;
  subscription: OrganizationSubscription;
  onClose: () => void;
  onUpdated: (next: OrganizationSubscription) => void;
}

export function SubscriptionDialog({ client, plansClient, organizationId, subscription, onClose, onUpdated }: Props) {
  const { t, formatCurrency } = useI18n();
  const { toast } = useToast();
  const [form, setForm] = useState(() => subscriptionToForm(subscription));
  const [plans, setPlans] = useState<SubscriptionPlan[]>([]);
  const [pending, setPending] = useState(false);

  useEffect(() => {
    let cancelled = false;
    plansClient
      .listPlans(false)
      .then(result => { if (!cancelled) setPlans(result); })
      // Каталог не доехал — остальные поля подписки правятся как прежде, а список планов
      // покажет только текущий: молча подставлять чужой план нельзя.
      .catch(() => { if (!cancelled) setPlans([]); });
    return () => { cancelled = true; };
  }, [plansClient]);

  const problem = validateSubscriptionForm(form);
  const planOptions = plans.some(plan => plan.planCode === form.planCode)
    ? plans
    : [...plans, { planCode: subscription.planCode, name: subscription.planCode } as SubscriptionPlan];

  async function submit() {
    if (problem !== null) return;
    setPending(true);
    try {
      const next = await client.updateSubscription(organizationId, subscriptionFormToRequest(form, subscription));
      onUpdated(next);
      toast({ title: t('platform.organization.subscriptionDialog.updated'), variant: 'success' });
    } catch (cause) {
      toast({ title: describeApiError(cause, t), variant: 'error' });
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
          <Button disabled={pending || problem !== null} onClick={() => void submit()}>{t('platform.organization.subscriptionDialog.save')}</Button>
        </>
      }
    >
      <div className="mgmt-form">
        <Field label={t('platform.organization.subscriptionForm.plan')} htmlFor="subscription-plan">
          <Select
            id="subscription-plan"
            value={form.planCode}
            onChange={event => setForm(current => ({ ...current, planCode: event.target.value }))}
          >
            {planOptions.map(plan => (
              <option key={plan.planCode} value={plan.planCode}>{plan.name}</option>
            ))}
          </Select>
        </Field>

        <Field label={t('platform.organization.subscriptionForm.interval')} htmlFor="subscription-interval">
          <Select
            id="subscription-interval"
            value={form.billingInterval}
            onChange={event => setForm(current => ({ ...current, billingInterval: event.target.value }))}
          >
            {BILLING_INTERVAL_OPTIONS.map(option => (
              <option key={option} value={option}>
                {t(option === 'monthly' ? 'platform.billing.interval.monthly' : 'platform.billing.interval.yearly')}
              </option>
            ))}
          </Select>
        </Field>

        <Field label={t('platform.organization.subscriptionForm.status')} htmlFor="subscription-status">
          <Select
            id="subscription-status"
            value={form.status}
            onChange={event => setForm(current => ({ ...current, status: event.target.value }))}
          >
            {SUBSCRIPTION_STATUS_OPTIONS.map(option => (
              <option key={option} value={option}>{t(SUBSCRIPTION_STATUS_LABEL[option])}</option>
            ))}
          </Select>
        </Field>

        <label className="pc-check-row">
          <Switch
            checked={form.cancelAtPeriodEnd}
            onCheckedChange={value => setForm(current => ({ ...current, cancelAtPeriodEnd: value }))}
          />
          {t('platform.organization.subscriptionForm.cancelAtPeriodEnd')}
        </label>

        <Field
          label={t('platform.organization.subscriptionDialog.amount')}
          htmlFor="subscription-amount"
          hint={problem === null ? formatCurrency(Number.parseFloat(form.amount.replace(',', '.')), subscription.currencyCode) : undefined}
        >
          <Input
            id="subscription-amount"
            inputMode="decimal"
            value={form.amount}
            onChange={event => setForm(current => ({ ...current, amount: event.target.value }))}
          />
        </Field>

        <Field label={t('platform.organization.subscriptionDialog.currentPeriodEnd')} htmlFor="subscription-period-end">
          <Input
            id="subscription-period-end"
            type="date"
            value={form.currentPeriodEnd}
            onChange={event => setForm(current => ({ ...current, currentPeriodEnd: event.target.value }))}
          />
        </Field>

        {/* Скидку сервер принимал всегда, а панель её не показывала и не давала задать: клуб с
            персональной ценой выглядел как клуб по прайсу. */}
        <Field label={t('platform.organization.subscriptionForm.discount')} htmlFor="subscription-discount-kind">
          <Select
            id="subscription-discount-kind"
            value={form.discountKind}
            onChange={event => setForm(current => ({ ...current, discountKind: event.target.value as DiscountKind }))}
          >
            {DISCOUNT_KINDS.map(kind => (
              <option key={kind} value={kind}>
                {t(kind === 'none'
                  ? 'platform.organization.subscriptionForm.discount.none'
                  : kind === 'percent'
                    ? 'platform.organization.subscriptionForm.discount.percent'
                    : 'platform.organization.subscriptionForm.discount.amount')}
              </option>
            ))}
          </Select>
        </Field>

        {form.discountKind !== 'none' ? (
          <>
            <Field
              label={t(form.discountKind === 'percent'
                ? 'platform.organization.subscriptionForm.discountPercentValue'
                : 'platform.organization.subscriptionForm.discountAmountValue')}
              htmlFor="subscription-discount-value"
            >
              <Input
                id="subscription-discount-value"
                inputMode="decimal"
                value={form.discountValue}
                onChange={event => setForm(current => ({ ...current, discountValue: event.target.value }))}
              />
            </Field>

            <Field label={t('platform.organization.subscriptionForm.discountUntil')} htmlFor="subscription-discount-until">
              <Input
                id="subscription-discount-until"
                type="date"
                value={form.discountUntil}
                onChange={event => setForm(current => ({ ...current, discountUntil: event.target.value }))}
              />
            </Field>

            <Field label={t('platform.organization.subscriptionForm.discountReason')} htmlFor="subscription-discount-reason">
              <Input
                id="subscription-discount-reason"
                value={form.discountReason}
                onChange={event => setForm(current => ({ ...current, discountReason: event.target.value }))}
              />
            </Field>
          </>
        ) : null}

        {subscription.discountPercent !== null || subscription.discountAmountMinorUnits !== null ? (
          <p className="mgmt-drawer-hint">
            {t('platform.organization.subscriptionForm.discountCurrent', {
              value: subscription.discountPercent !== null
                ? `${subscription.discountPercent}%`
                : formatCurrency(minorToMajor(subscription.discountAmountMinorUnits ?? 0), subscription.currencyCode)
            })}
          </p>
        ) : null}

        {problem !== null ? <p className="ui-alert" role="alert">{t(problem)}</p> : null}
      </div>
    </Dialog>
  );
}
