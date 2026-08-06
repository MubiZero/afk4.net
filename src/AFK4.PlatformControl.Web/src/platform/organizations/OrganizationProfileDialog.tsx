import { useState } from 'react';
import { Dialog } from '@/components/ui/dialog';
import { Field } from '@/components/ui/field';
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
    <Dialog
      open
      title={t('platform.organization.profileDialog.title')}
      onClose={onClose}
      footer={
        <>
          <Button variant="outline" disabled={pending} onClick={onClose}>{t('platform.organization.profileDialog.cancel')}</Button>
          <Button disabled={pending || name.trim() === ''} onClick={() => void submit()}>{t('platform.organization.profileDialog.save')}</Button>
        </>
      }
    >
      <div className="mgmt-form">
        <Field label={t('platform.organization.profileDialog.name')} htmlFor="profile-name">
          <Input id="profile-name" value={name} onChange={event => setName(event.target.value)} />
        </Field>
        <Field label={t('platform.organization.profileDialog.contactEmail')} htmlFor="profile-email">
          <Input id="profile-email" type="email" value={contactEmail} onChange={event => setContactEmail(event.target.value)} />
        </Field>
        <Field label={t('platform.organization.profileDialog.contactPhone')} htmlFor="profile-phone">
          <Input id="profile-phone" type="tel" value={contactPhone} onChange={event => setContactPhone(event.target.value)} />
        </Field>
        <Field label={t('platform.organization.profileDialog.legalDetails')} htmlFor="profile-legal">
          <Textarea id="profile-legal" rows={3} value={legalDetails} onChange={event => setLegalDetails(event.target.value)} />
        </Field>
      </div>
    </Dialog>
  );
}
