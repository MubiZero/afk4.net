// src/club/settings/CreateOperatorDialog.tsx
import { useState } from 'react';
import { Dialog, DialogContent, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { StaffApi } from '@/api/clients/staff';
import { ASSIGNABLE_ROLES, roleLabelKey } from './roles';

type Actions = Pick<StaffApi, 'createStaffInvite'>;

const EMAIL_PATTERN = /^[^@\s]+@[^@\s]+\.[^@\s]+$/;

export function CreateOperatorDialog({ open, branchId, organizationId, client, onOpenChange, onDone }: {
  open: boolean;
  branchId: string;
  organizationId: string;
  client: Actions;
  onOpenChange: (open: boolean) => void;
  onDone: () => void;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [userName, setUserName] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [email, setEmail] = useState('');
  const [roles, setRoles] = useState<string[]>([]);
  const [pending, setPending] = useState(false);
  const [inviteCode, setInviteCode] = useState<string | null>(null);

  const valid = userName.trim() !== '' && displayName.trim() !== '' && EMAIL_PATTERN.test(email.trim()) && roles.length > 0;

  function close() {
    setInviteCode(null);
    onOpenChange(false);
  }

  async function submit() {
    setPending(true);
    try {
      const invite = await client.createStaffInvite(branchId, {
        organizationId,
        userName: userName.trim(),
        displayName: displayName.trim(),
        email: email.trim(),
        roleNames: roles
      });
      toast({ title: t('operators.create.inviteSent'), variant: 'success' });
      setInviteCode(invite.code);
      onDone();
    } catch {
      toast({ title: t('toast.failed'), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogTitle>{t('operators.create.title')}</DialogTitle>
        {inviteCode === null ? (
          <>
            <div className="flex flex-col gap-3">
              <label className="block text-sm">
                <span className="mb-1 block text-muted-foreground">{t('operators.field.userName')}</span>
                <Input aria-label={t('operators.field.userName')} value={userName} onChange={e => setUserName(e.target.value)} />
              </label>
              <label className="block text-sm">
                <span className="mb-1 block text-muted-foreground">{t('operators.field.displayName')}</span>
                <Input aria-label={t('operators.field.displayName')} value={displayName} onChange={e => setDisplayName(e.target.value)} />
              </label>
              <label className="block text-sm">
                <span className="mb-1 block text-muted-foreground">{t('operators.field.email')}</span>
                <Input type="email" aria-label={t('operators.field.email')} value={email} onChange={e => setEmail(e.target.value)} />
              </label>
              <fieldset className="flex flex-col gap-2">
                <legend className="mb-1 text-sm font-medium">{t('operators.section.roles')}</legend>
                {ASSIGNABLE_ROLES.map(role => (
                  <label key={role} className="flex items-center gap-2 text-sm">
                    <Checkbox checked={roles.includes(role)} aria-label={t(roleLabelKey(role))}
                      onCheckedChange={c => setRoles(prev => (c === true ? [...new Set([...prev, role])] : prev.filter(r => r !== role)))} />
                    {t(roleLabelKey(role))}
                  </label>
                ))}
              </fieldset>
            </div>
            <DialogFooter>
              <Button variant="outline" disabled={pending} onClick={close}>{t('common.cancel')}</Button>
              <Button disabled={pending || !valid} onClick={() => void submit()}>{t('operators.create.submit')}</Button>
            </DialogFooter>
          </>
        ) : (
          <>
            <div className="flex flex-col gap-2">
              <span className="text-sm text-muted-foreground">{t('operators.create.inviteSent')}</span>
              <span className="text-sm text-muted-foreground">{t('operators.create.codeLabel')}</span>
              <code className="select-all break-all rounded bg-muted px-2 py-1 text-sm" aria-label={t('operators.create.codeLabel')}>{inviteCode}</code>
            </div>
            <DialogFooter>
              <Button onClick={close}>{t('operators.create.done')}</Button>
            </DialogFooter>
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}
