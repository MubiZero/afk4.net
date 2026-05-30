// src/club/settings/BranchProfileForm.tsx
import { useState } from 'react';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Switch } from '@/components/ui/switch';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import type { BranchProfileView } from './settingsModel';

type Actions = Pick<ClubApiClient, 'updateBranchProfile' | 'updateBranchSettings'>;

export function BranchProfileForm({ profile, requireManualDeviceApproval, branchId, client, onDone }: {
  profile: BranchProfileView;
  requireManualDeviceApproval: boolean;
  branchId: string;
  client: Actions;
  onDone: () => void;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [name, setName] = useState(profile.name);
  const [city, setCity] = useState(profile.city);
  const [approval, setApproval] = useState(requireManualDeviceApproval);
  const [pending, setPending] = useState(false);

  async function saveProfile() {
    setPending(true);
    try {
      await client.updateBranchProfile(branchId, { organizationId: profile.organizationId, name: name.trim(), city: city.trim() });
      toast({ title: t('toast.saved'), variant: 'success' });
      onDone();
    } catch {
      toast({ title: t('toast.failed'), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  function toggleApproval(next: boolean) {
    setApproval(next);
    setPending(true);
    void (async () => {
      try {
        await client.updateBranchSettings(branchId, { organizationId: profile.organizationId, requireManualDeviceApproval: next });
        toast({ title: t('toast.saved'), variant: 'success' });
        onDone();
      } catch {
        setApproval(!next);
        toast({ title: t('toast.failed'), variant: 'error' });
      } finally {
        setPending(false);
      }
    })();
  }

  return (
    <div className="flex max-w-md flex-col gap-5">
      <label className="block text-sm">
        <span className="mb-1 block text-muted-foreground">{t('settings.branch.name')}</span>
        <Input aria-label={t('settings.branch.name')} value={name} onChange={e => setName(e.target.value)} />
      </label>
      <label className="block text-sm">
        <span className="mb-1 block text-muted-foreground">{t('settings.branch.city')}</span>
        <Input aria-label={t('settings.branch.city')} value={city} onChange={e => setCity(e.target.value)} />
      </label>
      <Button disabled={pending || name.trim() === '' || city.trim() === ''} onClick={() => void saveProfile()}>
        {t('common.save')}
      </Button>

      <div className="flex items-center justify-between border-t border-border pt-4">
        <div>
          <div className="text-sm font-medium">{t('settings.branch.approval')}</div>
          <div className="text-xs text-muted-foreground">{t('settings.branch.approval.hint')}</div>
        </div>
        <Switch
          aria-label={t('settings.branch.approval')}
          checked={approval}
          disabled={pending}
          onCheckedChange={toggleApproval}
        />
      </div>
    </div>
  );
}
