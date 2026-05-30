// src/club/settings/OperatorDrawer.tsx
import { useState } from 'react';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { ConfirmDialog } from '@/components/shared/ConfirmDialog';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import { ASSIGNABLE_ROLES, roleLabelKey } from './roles';
import type { OperatorRow } from './settingsModel';

type Actions = Pick<ClubApiClient, 'updateStaffProfile' | 'updateStaffRoles' | 'updateStaffState' | 'resetStaffPassword'>;

export function OperatorDrawer({ operator, branchId, currentStaffUserId, client, onDone }: {
  operator: OperatorRow;
  branchId: string;
  currentStaffUserId: string;
  client: Actions;
  onDone: () => void;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [userName, setUserName] = useState(operator.userName);
  const [displayName, setDisplayName] = useState(operator.displayName);
  const [roles, setRoles] = useState<string[]>(operator.roleNames);
  const [pending, setPending] = useState(false);
  const [confirm, setConfirm] = useState<null | 'deactivate' | 'password'>(null);
  const isSelf = operator.staffUserId === currentStaffUserId;
  const org = operator.organizationId;

  async function run(action: () => Promise<unknown>) {
    setPending(true);
    try {
      await action();
      toast({ title: t('toast.saved'), variant: 'success' });
      onDone();
    } catch {
      toast({ title: t('toast.failed'), variant: 'error' });
    } finally {
      setPending(false);
      setConfirm(null);
    }
  }

  function toggleRole(role: string, checked: boolean) {
    setRoles(prev => (checked ? [...new Set([...prev, role])] : prev.filter(r => r !== role)));
  }

  return (
    <div className="flex flex-col gap-5">
      <label className="block text-sm">
        <span className="mb-1 block text-muted-foreground">{t('operators.field.userName')}</span>
        <Input aria-label={t('operators.field.userName')} value={userName} onChange={e => setUserName(e.target.value)} />
      </label>
      <label className="block text-sm">
        <span className="mb-1 block text-muted-foreground">{t('operators.field.displayName')}</span>
        <Input aria-label={t('operators.field.displayName')} value={displayName} onChange={e => setDisplayName(e.target.value)} />
      </label>
      <Button disabled={pending || userName.trim() === '' || displayName.trim() === ''}
        onClick={() => void run(() => client.updateStaffProfile(branchId, operator.staffUserId, { organizationId: org, userName: userName.trim(), displayName: displayName.trim() }))}>
        {t('operators.save.profile')}
      </Button>

      <fieldset className="flex flex-col gap-2 border-t border-border pt-4">
        <legend className="mb-1 text-sm font-medium">{t('operators.section.roles')}</legend>
        {ASSIGNABLE_ROLES.map(role => (
          <label key={role} className="flex items-center gap-2 text-sm">
            <Checkbox checked={roles.includes(role)} aria-label={t(roleLabelKey(role))}
              onCheckedChange={c => toggleRole(role, c === true)} />
            {t(roleLabelKey(role))}
          </label>
        ))}
        <Button className="mt-1" disabled={pending || roles.length === 0}
          onClick={() => void run(() => client.updateStaffRoles(branchId, operator.staffUserId, { organizationId: org, roleNames: roles }))}>
          {t('operators.save.roles')}
        </Button>
      </fieldset>

      <div className="flex flex-col gap-3 border-t border-border pt-4">
        {operator.isActive ? (
          <Button variant="destructive" disabled={pending || isSelf} onClick={() => setConfirm('deactivate')}>
            {t('operators.action.deactivate')}
          </Button>
        ) : (
          <Button variant="outline" disabled={pending}
            onClick={() => void run(() => client.updateStaffState(branchId, operator.staffUserId, { organizationId: org, isActive: true }))}>
            {t('operators.action.activate')}
          </Button>
        )}
        <Button variant="outline" disabled={pending} onClick={() => setConfirm('password')}>
          {t('operators.action.resetPassword')}
        </Button>
      </div>

      <ConfirmDialog
        open={confirm === 'deactivate'} title={t('operators.deactivate.confirm')}
        confirmLabel={t('operators.action.deactivate')} cancelLabel={t('common.cancel')}
        destructive pending={pending}
        onConfirm={() => void run(() => client.updateStaffState(branchId, operator.staffUserId, { organizationId: org, isActive: false }))}
        onOpenChange={open => { if (!open) setConfirm(null); }}
      />
      <ConfirmDialog
        open={confirm === 'password'} title={t('operators.resetPassword.confirm')}
        confirmLabel={t('operators.action.resetPassword')} cancelLabel={t('common.cancel')}
        reasonLabel={t('operators.field.newPassword')} pending={pending}
        onConfirm={value => {
          if (value.trim().length < 8) { toast({ title: t('operators.password.tooShort'), variant: 'error' }); return; }
          void run(() => client.resetStaffPassword(branchId, operator.staffUserId, { organizationId: org, newPassword: value.trim() }));
        }}
        onOpenChange={open => { if (!open) setConfirm(null); }}
      />
    </div>
  );
}
