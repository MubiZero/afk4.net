import { useState } from 'react';
import { Dialog, DialogContent, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';

type Actions = Pick<ClubApiClient, 'updateBranchProfile'>;

export function RenameBranchDialog({ open, branchId, organizationId, initialName, initialCity, client, onOpenChange, onDone }: {
  open: boolean;
  branchId: string;
  organizationId: string;
  initialName: string;
  initialCity: string;
  client: Actions;
  onOpenChange: (open: boolean) => void;
  onDone: () => void;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [name, setName] = useState(initialName);
  const [city, setCity] = useState(initialCity);
  const [pending, setPending] = useState(false);

  const valid = name.trim() !== '' && city.trim() !== '';

  async function submit() {
    setPending(true);
    try {
      await client.updateBranchProfile(branchId, { organizationId, name: name.trim(), city: city.trim() });
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
        <DialogTitle>{t('branches.rename.title')}</DialogTitle>
        <div className="flex flex-col gap-3">
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('settings.branch.name')}</span>
            <Input aria-label={t('settings.branch.name')} value={name} onChange={e => setName(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('settings.branch.city')}</span>
            <Input aria-label={t('settings.branch.city')} value={city} onChange={e => setCity(e.target.value)} />
          </label>
        </div>
        <DialogFooter>
          <Button variant="outline" disabled={pending} onClick={() => onOpenChange(false)}>{t('common.cancel')}</Button>
          <Button disabled={pending || !valid} onClick={() => void submit()}>{t('common.save')}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
