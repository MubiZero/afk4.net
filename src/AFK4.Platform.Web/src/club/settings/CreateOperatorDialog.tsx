// src/club/settings/CreateOperatorDialog.tsx
import { useState } from 'react';
import { Dialog, DialogContent, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import { ASSIGNABLE_ROLES, roleLabelKey } from './roles';

type Actions = Pick<ClubApiClient, 'createStaff'>;

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
  const [password, setPassword] = useState('');
  const [roles, setRoles] = useState<string[]>([]);
  const [pending, setPending] = useState(false);

  const valid = userName.trim() !== '' && displayName.trim() !== '' && password.trim().length >= 8 && roles.length > 0;

  async function submit() {
    setPending(true);
    try {
      await client.createStaff(branchId, {
        organizationId,
        userName: userName.trim(),
        displayName: displayName.trim(),
        password: password.trim(),
        roleNames: roles
      });
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
        <DialogTitle>{t('operators.create.title')}</DialogTitle>
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
            <span className="mb-1 block text-muted-foreground">{t('operators.field.password')}</span>
            <Input type="password" aria-label={t('operators.field.password')} value={password} onChange={e => setPassword(e.target.value)} />
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
          <Button variant="outline" disabled={pending} onClick={() => onOpenChange(false)}>{t('common.cancel')}</Button>
          <Button disabled={pending || !valid} onClick={() => void submit()}>{t('operators.create.submit')}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
