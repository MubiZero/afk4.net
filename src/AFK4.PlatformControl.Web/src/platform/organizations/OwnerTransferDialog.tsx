import { useState } from 'react';
import { Dialog } from '@/components/ui/dialog';
import { Field } from '@/components/ui/field';
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
    <Dialog
      open
      tone="danger"
      title={t('platform.organization.ownerTransferDialog.title')}
      description={t('platform.organization.ownerTransferDialog.description')}
      onClose={onClose}
      footer={
        <>
          <Button variant="outline" disabled={pending} onClick={onClose}>{t('platform.organization.ownerTransferDialog.cancel')}</Button>
          <Button variant="destructive" disabled={pending || !canSubmit} onClick={() => void submit()}>{t('platform.organization.ownerTransferDialog.save')}</Button>
        </>
      }
    >
      <div className="mgmt-form">
        <Field label={t('platform.organization.ownerTransferDialog.newOwnerEmail')} htmlFor="owner-email">
          <Input id="owner-email" type="email" value={newOwnerEmail} onChange={event => setNewOwnerEmail(event.target.value)} />
        </Field>
        <Field label={t('platform.organization.ownerTransferDialog.reason')} htmlFor="owner-reason">
          <Input id="owner-reason" value={reason} onChange={event => setReason(event.target.value)} />
        </Field>
      </div>
    </Dialog>
  );
}
