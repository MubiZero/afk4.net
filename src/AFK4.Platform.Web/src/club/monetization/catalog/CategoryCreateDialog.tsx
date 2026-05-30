import { useState } from 'react';
import { Dialog, DialogContent, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import { buildCreateCategoryRequest, type CategoryOption } from './catalogModel';

type Actions = Pick<ClubApiClient, 'createProductCategory'>;

export function CategoryCreateDialog({ open, branchId, organizationId, client, onCreated, onOpenChange }: {
  open: boolean;
  branchId: string;
  organizationId: string;
  client: Actions;
  onCreated: (category: CategoryOption) => void;
  onOpenChange: (open: boolean) => void;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [name, setName] = useState('');
  const [pending, setPending] = useState(false);

  async function submit() {
    setPending(true);
    try {
      const created = await client.createProductCategory(branchId, buildCreateCategoryRequest(organizationId, name, crypto.randomUUID()));
      onCreated({ categoryId: created.categoryId, name: created.name });
      toast({ title: t('toast.saved'), variant: 'success' });
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
        <DialogTitle>{t('products.createCategory.title')}</DialogTitle>
        <label className="block text-sm">
          <span className="mb-1 block text-muted-foreground">{t('products.field.categoryName')}</span>
          <Input aria-label={t('products.field.categoryName')} value={name} onChange={e => setName(e.target.value)} />
        </label>
        <DialogFooter>
          <Button variant="outline" disabled={pending} onClick={() => onOpenChange(false)}>{t('common.cancel')}</Button>
          <Button disabled={pending || name.trim() === ''} onClick={() => void submit()}>{t('products.createCategory.submit')}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
