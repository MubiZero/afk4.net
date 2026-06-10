import { useState } from 'react';
import { Dialog, DialogContent, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { PlayersApi } from '@/api/clients/players';
import { buildAmountReasonRequest } from './moneyOpsModel';

type Actions = Pick<PlayersApi, 'topUpWallet' | 'payDebt'>;

export function AmountReasonDialog({ open, kind, client, playerAccountId, organizationId, currencyCode, onOpenChange, onDone }: {
  open: boolean;
  kind: 'topUp' | 'payDebt';
  client: Actions;
  playerAccountId: string;
  organizationId: string;
  currencyCode: string;
  onOpenChange: (open: boolean) => void;
  onDone: () => void;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [amount, setAmount] = useState('');
  const [reason, setReason] = useState('');
  const [pending, setPending] = useState(false);

  const valid = Number(amount) > 0 && reason.trim() !== '';

  async function submit() {
    setPending(true);
    try {
      const request = buildAmountReasonRequest(organizationId, currencyCode, Number(amount), reason, crypto.randomUUID());
      if (kind === 'topUp') {
        await client.topUpWallet(playerAccountId, request);
      } else {
        await client.payDebt(playerAccountId, request);
      }
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
        <DialogTitle>{kind === 'topUp' ? t('money.topUp.title') : t('money.payDebt.title')}</DialogTitle>
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
