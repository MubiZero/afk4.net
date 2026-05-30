import { useState } from 'react';
import { Dialog, DialogContent, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import { buildPurchasePackageRequest, type PackageChoice } from './playerPackagesModel';

type Actions = Pick<ClubApiClient, 'purchasePackage'>;

export function PurchasePackageDialog({ open, client, playerAccountId, organizationId, choices, onOpenChange, onDone }: {
  open: boolean;
  client: Actions;
  playerAccountId: string;
  organizationId: string;
  choices: PackageChoice[];
  onOpenChange: (open: boolean) => void;
  onDone: () => void;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [selected, setSelected] = useState(choices[0]?.packageDefinitionId ?? '');
  const [pending, setPending] = useState(false);

  const valid = selected !== '';

  async function submit() {
    setPending(true);
    try {
      await client.purchasePackage(playerAccountId, buildPurchasePackageRequest(organizationId, selected, crypto.randomUUID()));
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
        <DialogTitle>{t('clientPackages.purchase.title')}</DialogTitle>
        {choices.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t('clientPackages.noChoices')}</p>
        ) : (
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('clientPackages.field.package')}</span>
            <Select value={selected} onValueChange={setSelected}>
              <SelectTrigger aria-label={t('clientPackages.field.package')}>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {choices.map(c => (
                  <SelectItem key={c.packageDefinitionId} value={c.packageDefinitionId}>{c.name}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </label>
        )}
        <DialogFooter>
          <Button variant="outline" disabled={pending} onClick={() => onOpenChange(false)}>{t('common.cancel')}</Button>
          <Button disabled={pending || !valid} onClick={() => void submit()}>{t('clientPackages.purchase.submit')}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
