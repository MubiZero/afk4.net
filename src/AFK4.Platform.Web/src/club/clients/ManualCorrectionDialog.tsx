import { useState } from 'react';
import { Dialog, DialogContent, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import { buildManualCorrectionRequest } from './moneyOpsModel';

type Actions = Pick<ClubApiClient, 'createManualCorrection'>;

const MONEY_ACCOUNTS = ['wallet', 'debt'];

export function ManualCorrectionDialog({ open, client, playerAccountId, organizationId, currencyCode, onOpenChange, onDone }: {
  open: boolean;
  client: Actions;
  playerAccountId: string;
  organizationId: string;
  currencyCode: string;
  onOpenChange: (open: boolean) => void;
  onDone: () => void;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [accountType, setAccountType] = useState('wallet');
  const [amount, setAmount] = useState('');
  const [minutes, setMinutes] = useState('');
  const [reason, setReason] = useState('');
  const [pending, setPending] = useState(false);

  const isMoney = MONEY_ACCOUNTS.includes(accountType);
  const valueValid = isMoney ? Number(amount) !== 0 : Number(minutes) !== 0;
  const valid = valueValid && reason.trim() !== '';

  async function submit() {
    setPending(true);
    try {
      const request = buildManualCorrectionRequest(
        organizationId, accountType, currencyCode,
        isMoney ? Number(amount) : 0,
        isMoney ? 0 : Number(minutes),
        reason, crypto.randomUUID()
      );
      await client.createManualCorrection(playerAccountId, request);
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
        <DialogTitle>{t('money.correction.title')}</DialogTitle>
        <div className="flex flex-col gap-3">
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('money.field.account')}</span>
            <Select value={accountType} onValueChange={setAccountType}>
              <SelectTrigger aria-label={t('money.field.account')}>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="wallet">{t('ledger.account.wallet')}</SelectItem>
                <SelectItem value="debt">{t('ledger.account.debt')}</SelectItem>
                <SelectItem value="package_time">{t('ledger.account.package_time')}</SelectItem>
                <SelectItem value="bonus_time">{t('ledger.account.bonus_time')}</SelectItem>
              </SelectContent>
            </Select>
          </label>
          {isMoney ? (
            <label className="block text-sm">
              <span className="mb-1 block text-muted-foreground">{t('money.field.amount')}</span>
              <Input aria-label={t('money.field.amount')} value={amount} onChange={e => setAmount(e.target.value)} />
            </label>
          ) : (
            <label className="block text-sm">
              <span className="mb-1 block text-muted-foreground">{t('money.field.minutes')}</span>
              <Input aria-label={t('money.field.minutes')} value={minutes} onChange={e => setMinutes(e.target.value)} />
            </label>
          )}
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
