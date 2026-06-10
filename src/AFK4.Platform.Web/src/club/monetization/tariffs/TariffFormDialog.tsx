import { useState } from 'react';
import { Dialog, DialogContent, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Switch } from '@/components/ui/switch';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { TariffsApi } from '@/api/clients/tariffs';
import {
  buildCreateTariffRequest, buildCreateVersionRequest, buildUpdateTariffRequest, buildUpdateVersionRequest,
  type TariffFormValues, type TariffRow
} from './tariffsModel';

type Actions = Pick<TariffsApi, 'createTariff' | 'createTariffVersion' | 'updateTariff' | 'updateTariffVersion'>;

export function TariffFormDialog({ open, mode, branchId, organizationId, client, initial, onOpenChange, onDone }: {
  open: boolean;
  mode: 'create' | 'edit';
  branchId: string;
  organizationId: string;
  client: Actions;
  initial?: TariffRow;
  onOpenChange: (open: boolean) => void;
  onDone: () => void;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [name, setName] = useState(initial?.name ?? '');
  const [currency, setCurrency] = useState(initial?.currencyCode ?? 'RUB');
  const [pricePerMinute, setPricePerMinute] = useState(String(initial?.pricePerMinute ?? '1'));
  const [minMinutes, setMinMinutes] = useState(String(initial?.minimumBillableMinutes ?? '1'));
  const [rounding, setRounding] = useState(String(initial?.roundingIncrementMinutes ?? '1'));
  const [active, setActive] = useState(true);
  const [pending, setPending] = useState(false);

  const valid = name.trim() !== '' && currency.trim() !== '' && Number(pricePerMinute) >= 0 && Number(minMinutes) >= 0 && Number(rounding) >= 0;

  function formValues(): TariffFormValues {
    return {
      name,
      currencyCode: currency.trim(),
      pricePerMinute: Number(pricePerMinute),
      minimumBillableMinutes: Number(minMinutes),
      roundingIncrementMinutes: Number(rounding)
    };
  }

  async function submit() {
    setPending(true);
    const effectiveFromUtc = new Date().toISOString();
    try {
      if (mode === 'create') {
        const tariff = await client.createTariff(branchId, buildCreateTariffRequest(organizationId, name, crypto.randomUUID()));
        await client.createTariffVersion(
          branchId, tariff.tariffId,
          buildCreateVersionRequest(organizationId, tariff.tariffId, formValues(), effectiveFromUtc, crypto.randomUUID())
        );
      } else if (initial !== undefined) {
        await client.updateTariff(branchId, initial.tariffId, buildUpdateTariffRequest(organizationId, name, active));
        await client.updateTariffVersion(
          branchId, initial.tariffId, initial.tariffVersionId,
          buildUpdateVersionRequest(organizationId, formValues(), effectiveFromUtc, active)
        );
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
        <DialogTitle>{mode === 'create' ? t('tariffs.create.title') : t('tariffs.edit.title')}</DialogTitle>
        <div className="flex flex-col gap-3">
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('tariffs.field.name')}</span>
            <Input aria-label={t('tariffs.field.name')} value={name} onChange={e => setName(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('tariffs.field.pricePerMinute')}</span>
            <Input aria-label={t('tariffs.field.pricePerMinute')} value={pricePerMinute} onChange={e => setPricePerMinute(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('tariffs.field.currency')}</span>
            <Input aria-label={t('tariffs.field.currency')} value={currency} onChange={e => setCurrency(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('tariffs.field.minMinutes')}</span>
            <Input aria-label={t('tariffs.field.minMinutes')} value={minMinutes} onChange={e => setMinMinutes(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('tariffs.field.rounding')}</span>
            <Input aria-label={t('tariffs.field.rounding')} value={rounding} onChange={e => setRounding(e.target.value)} />
          </label>
          {mode === 'edit' && (
            <label className="flex items-center gap-2 text-sm">
              <Switch checked={active} aria-label={t('tariffs.field.active')} onCheckedChange={setActive} />
              {t('tariffs.field.active')}
            </label>
          )}
        </div>
        <DialogFooter>
          <Button variant="outline" disabled={pending} onClick={() => onOpenChange(false)}>{t('common.cancel')}</Button>
          <Button disabled={pending || !valid} onClick={() => void submit()}>
            {mode === 'create' ? t('tariffs.create.submit') : t('tariffs.edit.submit')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
