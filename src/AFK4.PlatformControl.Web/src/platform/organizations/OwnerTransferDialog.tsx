import { useState } from 'react';
import { Dialog, DialogContent, DialogTitle, DialogDescription, DialogFooter } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { OrganizationsApi } from '@/api/platformClients/organizations';

type Client = Pick<OrganizationsApi, 'transferOwner'>;

interface Props {
  client: Client;
  organizationId: string;
  onClose: () => void;
  onTransferred: () => void;
}

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/u;

export function OwnerTransferDialog({ client, organizationId, onClose, onTransferred }: Props) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [newOwnerEmail, setNewOwnerEmail] = useState('');
  const [reason, setReason] = useState('');
  const [pending, setPending] = useState(false);

  const emailValid = EMAIL_PATTERN.test(newOwnerEmail.trim());
  const canSubmit = emailValid && reason.trim().length > 0;

  async function submit() {
    if (!canSubmit) return;
    setPending(true);
    try {
      await client.transferOwner(organizationId, { newOwnerEmail: newOwnerEmail.trim(), reason: reason.trim() });
      onTransferred();
      toast({ title: t('platform.organization.ownerTransferDialog.updated'), variant: 'success' });
    } catch {
      toast({ title: t('platform.organization.action.error'), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  return (
    <Dialog open onOpenChange={open => { if (!open) onClose(); }}>
      <DialogContent>
        <DialogTitle>{t('platform.organization.ownerTransferDialog.title')}</DialogTitle>
        <DialogDescription>{t('platform.organization.ownerTransferDialog.description')}</DialogDescription>
        <div className="flex flex-col gap-3">
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('platform.organization.ownerTransferDialog.newOwnerEmail')}</span>
            <Input type="email" aria-label={t('platform.organization.ownerTransferDialog.newOwnerEmail')} value={newOwnerEmail} onChange={e => setNewOwnerEmail(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('platform.organization.ownerTransferDialog.reason')}</span>
            <Input aria-label={t('platform.organization.ownerTransferDialog.reason')} value={reason} onChange={e => setReason(e.target.value)} />
          </label>
        </div>
        <DialogFooter>
          <Button variant="outline" disabled={pending} onClick={onClose}>{t('platform.organization.ownerTransferDialog.cancel')}</Button>
          <Button variant="destructive" disabled={pending || !canSubmit} onClick={() => void submit()}>{t('platform.organization.ownerTransferDialog.save')}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
