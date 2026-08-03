import { useState } from 'react';
import { Dialog, DialogContent, DialogTitle, DialogDescription, DialogFooter } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { OrganizationsApi } from '@/api/platformClients/organizations';
import type { OrganizationDetail } from '@/api/types';

type Client = Pick<OrganizationsApi, 'updateProfile'>;

interface Props {
  client: Client;
  organization: OrganizationDetail;
  onClose: () => void;
  onUpdated: (next: OrganizationDetail) => void;
}

function blankToNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed === '' ? null : trimmed;
}

export function OrganizationProfileDialog({ client, organization, onClose, onUpdated }: Props) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [name, setName] = useState(organization.name);
  const [contactEmail, setContactEmail] = useState(organization.contactEmail ?? '');
  const [contactPhone, setContactPhone] = useState(organization.contactPhone ?? '');
  const [legalDetails, setLegalDetails] = useState(organization.legalDetails ?? '');
  const [pending, setPending] = useState(false);

  async function submit() {
    if (name.trim() === '') return;
    setPending(true);
    try {
      const next = await client.updateProfile(organization.organizationId, {
        name: name.trim(),
        contactEmail: blankToNull(contactEmail),
        contactPhone: blankToNull(contactPhone),
        legalDetails: blankToNull(legalDetails)
      });
      onUpdated(next);
      toast({ title: t('platform.organization.profileDialog.updated'), variant: 'success' });
    } catch {
      toast({ title: t('platform.organization.action.error'), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  return (
    <Dialog open onOpenChange={open => { if (!open) onClose(); }}>
      <DialogContent>
        <DialogTitle>{t('platform.organization.profileDialog.title')}</DialogTitle>
        <DialogDescription className="sr-only">{t('platform.organization.profileDialog.title')}</DialogDescription>
        <div className="flex flex-col gap-3">
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('platform.organization.profileDialog.name')}</span>
            <Input aria-label={t('platform.organization.profileDialog.name')} value={name} onChange={e => setName(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('platform.organization.profileDialog.contactEmail')}</span>
            <Input type="email" aria-label={t('platform.organization.profileDialog.contactEmail')} value={contactEmail} onChange={e => setContactEmail(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('platform.organization.profileDialog.contactPhone')}</span>
            <Input type="tel" aria-label={t('platform.organization.profileDialog.contactPhone')} value={contactPhone} onChange={e => setContactPhone(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('platform.organization.profileDialog.legalDetails')}</span>
            <Textarea aria-label={t('platform.organization.profileDialog.legalDetails')} rows={3} value={legalDetails} onChange={e => setLegalDetails(e.target.value)} />
          </label>
        </div>
        <DialogFooter>
          <Button variant="outline" disabled={pending} onClick={onClose}>{t('platform.organization.profileDialog.cancel')}</Button>
          <Button disabled={pending || name.trim() === ''} onClick={() => void submit()}>{t('platform.organization.profileDialog.save')}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
