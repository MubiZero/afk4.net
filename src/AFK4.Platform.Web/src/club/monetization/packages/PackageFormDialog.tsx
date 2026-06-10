import { useState } from 'react';
import { Dialog, DialogContent, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Switch } from '@/components/ui/switch';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { PackagesApi } from '@/api/clients/packages';
import {
  buildCreatePackageRequest, buildUpdatePackageRequest, type PackageFormValues, type PackageRow
} from './packagesModel';

type Actions = Pick<PackagesApi, 'createPackageDefinition' | 'updatePackageDefinition'>;

export function PackageFormDialog({ open, mode, branchId, organizationId, client, initial, onOpenChange, onDone }: {
  open: boolean;
  mode: 'create' | 'edit';
  branchId: string;
  organizationId: string;
  client: Actions;
  initial?: PackageRow;
  onOpenChange: (open: boolean) => void;
  onDone: () => void;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [name, setName] = useState(initial?.name ?? '');
  const [currency, setCurrency] = useState(initial?.currencyCode ?? 'RUB');
  const [price, setPrice] = useState(String(initial?.price ?? '0'));
  const [includedMinutes, setIncludedMinutes] = useState(String(initial?.includedMinutes ?? '0'));
  const [bonusMinutes, setBonusMinutes] = useState(String(initial?.bonusMinutes ?? '0'));
  const [expiresAfterDays, setExpiresAfterDays] = useState(String(initial?.expiresAfterDays ?? '0'));
  const [active, setActive] = useState(true);
  const [pending, setPending] = useState(false);

  const valid = name.trim() !== '' && currency.trim() !== ''
    && Number(price) >= 0 && Number(includedMinutes) >= 0 && Number(bonusMinutes) >= 0 && Number(expiresAfterDays) >= 0;

  function formValues(): PackageFormValues {
    return {
      name,
      currencyCode: currency.trim(),
      price: Number(price),
      includedMinutes: Number(includedMinutes),
      bonusMinutes: Number(bonusMinutes),
      expiresAfterDays: Number(expiresAfterDays)
    };
  }

  async function submit() {
    setPending(true);
    try {
      if (mode === 'create') {
        await client.createPackageDefinition(branchId, buildCreatePackageRequest(organizationId, formValues(), crypto.randomUUID()));
      } else if (initial !== undefined) {
        await client.updatePackageDefinition(branchId, initial.packageDefinitionId, buildUpdatePackageRequest(organizationId, formValues(), active));
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
        <DialogTitle>{mode === 'create' ? t('loyalty.create.title') : t('loyalty.edit.title')}</DialogTitle>
        <div className="flex flex-col gap-3">
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('loyalty.field.name')}</span>
            <Input aria-label={t('loyalty.field.name')} value={name} onChange={e => setName(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('loyalty.field.price')}</span>
            <Input aria-label={t('loyalty.field.price')} value={price} onChange={e => setPrice(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('loyalty.field.currency')}</span>
            <Input aria-label={t('loyalty.field.currency')} value={currency} onChange={e => setCurrency(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('loyalty.field.included')}</span>
            <Input aria-label={t('loyalty.field.included')} value={includedMinutes} onChange={e => setIncludedMinutes(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('loyalty.field.bonus')}</span>
            <Input aria-label={t('loyalty.field.bonus')} value={bonusMinutes} onChange={e => setBonusMinutes(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('loyalty.field.expires')}</span>
            <Input aria-label={t('loyalty.field.expires')} value={expiresAfterDays} onChange={e => setExpiresAfterDays(e.target.value)} />
          </label>
          {mode === 'edit' && (
            <label className="flex items-center gap-2 text-sm">
              <Switch checked={active} aria-label={t('loyalty.field.active')} onCheckedChange={setActive} />
              {t('loyalty.field.active')}
            </label>
          )}
        </div>
        <DialogFooter>
          <Button variant="outline" disabled={pending} onClick={() => onOpenChange(false)}>{t('common.cancel')}</Button>
          <Button disabled={pending || !valid} onClick={() => void submit()}>
            {mode === 'create' ? t('loyalty.create.submit') : t('loyalty.edit.submit')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
