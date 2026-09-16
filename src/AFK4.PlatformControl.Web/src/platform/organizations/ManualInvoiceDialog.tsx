import { useState } from 'react';
import { Dialog } from '@/components/ui/dialog';
import { Field } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Select } from '@/components/ui/select';
import { useToast } from '@/components/ui/toast';
import { describeApiError } from '@/api/describeApiError';
import { useI18n } from '@/i18n/I18nProvider';
import { majorToMinor } from '@/lib/money';
import type { InvoicesApi } from '@/api/platformClients/invoices';
import type { Invoice } from '@/api/types';
import { INVOICE_KIND_LABEL } from '@/platform/billing/billingModel';

type Client = Pick<InvoicesApi, 'createInvoice'>;

// Автоматика выставляет только счета подписки. Руками заводят то, чего в подписке нет: разовую
// работу — счётом, уступку клубу — кредит-нотой.
const KIND_OPTIONS = ['one_off', 'credit'] as const;

export function ManualInvoiceDialog({ client, organizationId, currencyCode, onClose, onCreated }: {
  client: Client;
  organizationId: string;
  /** Валюта берётся из уже выставленных счетов клуба; у клуба без счетов её ещё нет — тогда
   *  подсказку с суммой не показываем вовсе, вместо того чтобы назвать валюту наугад. */
  currencyCode: string | null;
  onClose: () => void;
  onCreated: (invoice: Invoice) => void;
}) {
  const { t, formatCurrency } = useI18n();
  const { toast } = useToast();
  const [kind, setKind] = useState<string>('one_off');
  const [amount, setAmount] = useState('');
  const [description, setDescription] = useState('');
  const [dueAt, setDueAt] = useState('');
  const [pending, setPending] = useState(false);

  const amountValue = Number.parseFloat(amount.replace(',', '.'));
  const amountValid = Number.isFinite(amountValue) && amountValue > 0;
  const descriptionValid = description.trim().length > 0;
  const isCredit = kind === 'credit';
  const formattedAmount = amountValid && currencyCode !== null ? formatCurrency(amountValue, currencyCode) : null;
  const amountHint = isCredit
    ? [formattedAmount, t('platform.organization.invoiceDialog.amountCreditHint')].filter(part => part !== null).join(' · ')
    : formattedAmount ?? undefined;

  async function submit() {
    if (!amountValid || !descriptionValid) return;
    setPending(true);
    try {
      // Сумму всегда вводят положительной, а знак ставит вид счёта: минус в поле — верный способ
      // однажды стереть настоящий долг опечаткой.
      const minorUnits = majorToMinor(amountValue) * (isCredit ? -1 : 1);
      const invoice = await client.createInvoice(organizationId, {
        kind,
        amountMinorUnits: minorUnits,
        description: description.trim(),
        dueAtUtc: dueAt.length > 0 ? new Date(`${dueAt}T00:00:00Z`).toISOString() : null
      });
      onCreated(invoice);
      toast({ title: t('platform.organization.invoiceDialog.created'), variant: 'success' });
    } catch (cause) {
      toast({ title: describeApiError(cause, t), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  return (
    <Dialog
      open
      title={t('platform.organization.invoiceDialog.title')}
      onClose={onClose}
      footer={
        <>
          <Button variant="outline" disabled={pending} onClick={onClose}>{t('platform.billing.action.cancel')}</Button>
          <Button disabled={pending || !amountValid || !descriptionValid} onClick={() => void submit()}>
            {t('platform.organization.invoiceDialog.submit')}
          </Button>
        </>
      }
    >
      <div className="mgmt-form">
        <Field label={t('platform.organization.invoiceDialog.kind')} htmlFor="invoice-kind">
          <Select id="invoice-kind" value={kind} onChange={event => setKind(event.target.value)}>
            {KIND_OPTIONS.map(option => <option key={option} value={option}>{t(INVOICE_KIND_LABEL[option])}</option>)}
          </Select>
        </Field>

        <Field
          label={t('platform.organization.invoiceDialog.amount')}
          htmlFor="invoice-amount"
          hint={amountHint}
        >
          <Input id="invoice-amount" inputMode="decimal" value={amount} onChange={event => setAmount(event.target.value)} />
        </Field>

        <Field label={t('platform.organization.invoiceDialog.description')} htmlFor="invoice-description">
          <Input
            id="invoice-description"
            value={description}
            placeholder={t('platform.organization.invoiceDialog.descriptionPlaceholder')}
            onChange={event => setDescription(event.target.value)}
          />
        </Field>

        {isCredit ? null : (
          <Field
            label={t('platform.organization.invoiceDialog.dueAt')}
            htmlFor="invoice-due"
            hint={t('platform.organization.invoiceDialog.dueAtHint')}
          >
            <Input id="invoice-due" type="date" value={dueAt} onChange={event => setDueAt(event.target.value)} />
          </Field>
        )}
      </div>
    </Dialog>
  );
}
