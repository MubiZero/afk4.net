import { useState } from 'react';
import { Dialog, DialogContent, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Switch } from '@/components/ui/switch';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { CatalogApi } from '@/api/clients/catalog';
import {
  buildCreateProductRequest, buildUpdateProductRequest, type CategoryOption, type ProductFormValues, type ProductRow
} from './catalogModel';

type Actions = Pick<CatalogApi, 'createProduct' | 'updateProduct'>;

export function ProductFormDialog({ open, mode, branchId, organizationId, client, categories, initial, onOpenChange, onDone }: {
  open: boolean;
  mode: 'create' | 'edit';
  branchId: string;
  organizationId: string;
  client: Actions;
  categories: CategoryOption[];
  initial?: ProductRow;
  onOpenChange: (open: boolean) => void;
  onDone: () => void;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [categoryId, setCategoryId] = useState(initial?.categoryId ?? categories[0]?.categoryId ?? '');
  const [name, setName] = useState(initial?.name ?? '');
  const [sku, setSku] = useState(initial?.sku ?? '');
  const [price, setPrice] = useState(String(initial?.price ?? '0'));
  const [currency, setCurrency] = useState(initial?.currencyCode ?? 'RUB');
  const [trackStock, setTrackStock] = useState(initial?.trackStock ?? false);
  const [allowNegativeStock, setAllowNegativeStock] = useState(initial?.allowNegativeStock ?? false);
  const [active, setActive] = useState(initial?.isActive ?? true);
  const [pending, setPending] = useState(false);

  const valid = categoryId !== '' && name.trim() !== '' && currency.trim() !== '' && Number(price) >= 0;

  function formValues(): ProductFormValues {
    return { categoryId, name, sku, price: Number(price), currencyCode: currency.trim(), trackStock, allowNegativeStock };
  }

  async function submit() {
    setPending(true);
    try {
      if (mode === 'create') {
        await client.createProduct(branchId, buildCreateProductRequest(organizationId, formValues(), crypto.randomUUID()));
      } else if (initial !== undefined) {
        await client.updateProduct(branchId, initial.productId, buildUpdateProductRequest(organizationId, formValues(), active));
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
        <DialogTitle>{mode === 'create' ? t('products.create.title') : t('products.edit.title')}</DialogTitle>
        <div className="flex flex-col gap-3">
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('products.field.category')}</span>
            <Select value={categoryId} onValueChange={setCategoryId}>
              <SelectTrigger aria-label={t('products.field.category')}><SelectValue placeholder={t('products.field.category')} /></SelectTrigger>
              <SelectContent>
                {categories.map(c => <SelectItem key={c.categoryId} value={c.categoryId}>{c.name}</SelectItem>)}
              </SelectContent>
            </Select>
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('products.field.name')}</span>
            <Input aria-label={t('products.field.name')} value={name} onChange={e => setName(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('products.field.sku')}</span>
            <Input aria-label={t('products.field.sku')} value={sku} onChange={e => setSku(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('products.field.price')}</span>
            <Input aria-label={t('products.field.price')} value={price} onChange={e => setPrice(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('products.field.currency')}</span>
            <Input aria-label={t('products.field.currency')} value={currency} onChange={e => setCurrency(e.target.value)} />
          </label>
          <label className="flex items-center gap-2 text-sm">
            <Checkbox checked={trackStock} aria-label={t('products.field.trackStock')} onCheckedChange={c => setTrackStock(c === true)} />
            {t('products.field.trackStock')}
          </label>
          <label className="flex items-center gap-2 text-sm">
            <Checkbox checked={allowNegativeStock} aria-label={t('products.field.allowNegativeStock')} onCheckedChange={c => setAllowNegativeStock(c === true)} />
            {t('products.field.allowNegativeStock')}
          </label>
          {mode === 'edit' && (
            <label className="flex items-center gap-2 text-sm">
              <Switch checked={active} aria-label={t('products.field.active')} onCheckedChange={setActive} />
              {t('products.field.active')}
            </label>
          )}
        </div>
        <DialogFooter>
          <Button variant="outline" disabled={pending} onClick={() => onOpenChange(false)}>{t('common.cancel')}</Button>
          <Button disabled={pending || !valid} onClick={() => void submit()}>
            {mode === 'create' ? t('products.create.submit') : t('products.edit.submit')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
