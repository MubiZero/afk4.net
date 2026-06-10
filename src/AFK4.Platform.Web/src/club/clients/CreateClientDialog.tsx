import { useState } from 'react';
import { Dialog, DialogContent, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { PlayersApi } from '@/api/clients/players';
import { buildCreatePlayerRequest } from './clientsModel';

type Actions = Pick<PlayersApi, 'createPlayer'>;

export function CreateClientDialog({ open, branchId, organizationId, client, onOpenChange, onDone }: {
  open: boolean;
  branchId: string;
  organizationId: string;
  client: Actions;
  onOpenChange: (open: boolean) => void;
  onDone: () => void;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [displayName, setDisplayName] = useState('');
  const [phone, setPhone] = useState('');
  const [pending, setPending] = useState(false);

  const valid = displayName.trim() !== '';

  async function submit() {
    setPending(true);
    try {
      await client.createPlayer(branchId, buildCreatePlayerRequest(organizationId, displayName, phone, crypto.randomUUID()));
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
        <DialogTitle>{t('clients.create.title')}</DialogTitle>
        <div className="flex flex-col gap-3">
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('clients.field.displayName')}</span>
            <Input aria-label={t('clients.field.displayName')} value={displayName} onChange={e => setDisplayName(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('clients.field.phone')}</span>
            <Input aria-label={t('clients.field.phone')} value={phone} onChange={e => setPhone(e.target.value)} />
          </label>
        </div>
        <DialogFooter>
          <Button variant="outline" disabled={pending} onClick={() => onOpenChange(false)}>{t('common.cancel')}</Button>
          <Button disabled={pending || !valid} onClick={() => void submit()}>{t('clients.create.submit')}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
