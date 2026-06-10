import { useState } from 'react';
import { Dialog, DialogContent, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { PlayersApi } from '@/api/clients/players';
import { buildRefundRequest } from './moneyOpsModel';

type Actions = Pick<PlayersApi, 'refundLedgerEntry'>;

export interface RefundTarget {
  ledgerEntryId: string;
  amountMajor: number;
  currencyCode: string;
}

export function RefundDialog({ open, client, playerAccountId, organizationId, entry, onOpenChange, onDone }: {
  open: boolean;
  client: Actions;
  playerAccountId: string;
  organizationId: string;
  entry: RefundTarget;
  onOpenChange: (open: boolean) => void;
  onDone: () => void;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [amount, setAmount] = useState(String(entry.amountMajor));
  const [reason, setReason] = useState('');
  const [pending, setPending] = useState(false);

  const valid = Number(amount) > 0 && reason.trim() !== '';

  async function submit() {
    setPending(true);
    try {
      const request = buildRefundRequest(organizationId, entry.ledgerEntryId, entry.currencyCode, Number(amount), reason, crypto.randomUUID());
      await client.refundLedgerEntry(playerAccountId, entry.ledgerEntryId, request);
      toast({ title: t('toast.saved'), variant: 'success' });
      onDone();
      onOpenChange(false);
    } catch {
      toast({ title: t('toast.failed'), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogTitle>{t('money.refund.title')}</DialogTitle>
        <div className="flex flex-col gap-3">
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('money.field.amount')}</span>
            <Input aria-label={t('money.field.amount')} value={amount} onChange={e => setAmount(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('money.field.reason')}</span>
            <Input aria-label={t('money.field.reason')} value={reason} onChange={e => setReason(e.target.value)} />
          </label>
        </div>
        <DialogFooter>
          <Button variant="outline" disabled={pending} onClick={() => onOpenChange(false)}>{t('common.cancel')}</Button>
          <Button disabled={pending || !valid} onClick={() => void submit()}>{t('money.submit')}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
