import { useState } from 'react';
import { Dialog } from '@/components/ui/dialog';
import { ErrorBanner, Field } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { Button } from '@/components/ui/button';
import { useI18n } from '@/i18n/I18nProvider';
import type { AdminsApi } from '@/api/platformClients/admins';
import type { CreateInvitationResponse } from '@/api/types';
import { ROLE_PLATFORM_ADMIN, ROLE_PLATFORM_SUPPORT, describeAdminActionError } from './adminsModel';

type Client = Pick<AdminsApi, 'invite'>;

const DEFAULT_LIFETIME_HOURS = 72;

// Two steps in one dialog: the invite form, then — once the server has issued a code — a
// read-only confirmation. The code is returned exactly once (Task 6's API contract); there is no
// endpoint to fetch it again, so this second step exists specifically to make that unmissable
// before the admin can close the dialog.
export function AdminInviteDialog({ open, client, onOpenChange, onCreated }: {
  open: boolean;
  client: Client;
  onOpenChange: (open: boolean) => void;
  onCreated: () => void;
}) {
  const { t } = useI18n();
  const [role, setRole] = useState<string>(ROLE_PLATFORM_SUPPORT);
  const [lifetimeHours, setLifetimeHours] = useState(DEFAULT_LIFETIME_HOURS);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [created, setCreated] = useState<CreateInvitationResponse | null>(null);
  const [copied, setCopied] = useState(false);

  function reset() {
    setRole(ROLE_PLATFORM_SUPPORT);
    setLifetimeHours(DEFAULT_LIFETIME_HOURS);
    setError(null);
    setCreated(null);
    setCopied(false);
  }

  function closeForm() {
    reset();
    onOpenChange(false);
  }

  function closeAfterCreated() {
    reset();
    onOpenChange(false);
    onCreated();
  }

  async function submit() {
    setPending(true);
    setError(null);
    try {
      const response = await client.invite(role, lifetimeHours);
      setCreated(response);
    } catch (cause) {
      setError(describeAdminActionError(cause, t));
    } finally {
      setPending(false);
    }
  }

  function copyCode() {
    if (created === null) return;
    void navigator.clipboard?.writeText(created.code);
    setCopied(true);
    setTimeout(() => setCopied(false), 1500);
  }

  if (created !== null) {
    return (
      <Dialog
        open={open}
        title={t('platform.settings.invite.created.title')}
        onClose={closeAfterCreated}
        footer={<Button onClick={closeAfterCreated}>{t('platform.settings.invite.done')}</Button>}
      >
        <div className="mgmt-form">
          <p role="alert">{t('platform.settings.invite.created.warning')}</p>
          <Field label={t('platform.settings.invite.codeLabel')} htmlFor="invite-code">
            <Input id="invite-code" readOnly value={created.code} onFocus={event => event.currentTarget.select()} />
          </Field>
          <Button variant="outline" onClick={copyCode}>
            {copied ? t('platform.settings.invite.copied') : t('platform.settings.invite.copy')}
          </Button>
        </div>
      </Dialog>
    );
  }

  return (
    <Dialog
      open={open}
      title={t('platform.settings.invite.title')}
      description={t('platform.settings.invite.description')}
      onClose={closeForm}
      footer={
        <>
          <Button variant="outline" disabled={pending} onClick={closeForm}>{t('common.cancel')}</Button>
          <Button disabled={pending} onClick={() => void submit()}>{pending ? t('common.saving') : t('platform.settings.invite.submit')}</Button>
        </>
      }
    >
      <div className="mgmt-form">
        <ErrorBanner message={error} dismissLabel={t('common.close')} onDismiss={() => setError(null)} />
        <Field label={t('platform.settings.invite.roleLabel')} htmlFor="invite-role">
          <Select id="invite-role" value={role} onChange={event => setRole(event.target.value)}>
            <option value={ROLE_PLATFORM_SUPPORT}>{t('platform.settings.role.support')}</option>
            <option value={ROLE_PLATFORM_ADMIN}>{t('platform.settings.role.admin')}</option>
          </Select>
        </Field>
        <Field label={t('platform.settings.invite.lifetimeLabel')} htmlFor="invite-lifetime">
          <Input
            id="invite-lifetime"
            type="number"
            min="1"
            value={String(lifetimeHours)}
            onChange={event => setLifetimeHours(Math.max(1, Math.trunc(Number(event.target.value)) || 1))}
          />
        </Field>
      </div>
    </Dialog>
  );
}
