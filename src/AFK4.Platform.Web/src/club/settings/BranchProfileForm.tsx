// src/club/settings/BranchProfileForm.tsx
import { useState } from 'react';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Switch } from '@/components/ui/switch';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { BranchApi } from '@/api/clients/branches';
import type { BranchProfileView } from './settingsModel';

type Actions = Pick<BranchApi, 'updateBranchProfile' | 'updateBranchSettings'>;

const BRANCH_LOCALES = ['ru', 'en', 'tg'] as const;
const LOCALE_ENDONYM: Record<string, string> = { ru: 'Русский', en: 'English', tg: 'Тоҷикӣ' };

export function BranchProfileForm({ profile, requireManualDeviceApproval, preferredLocale, branchId, client, onDone }: {
  profile: BranchProfileView;
  requireManualDeviceApproval: boolean;
  preferredLocale: string;
  branchId: string;
  client: Actions;
  onDone: () => void;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [name, setName] = useState(profile.name);
  const [city, setCity] = useState(profile.city);
  const [approval, setApproval] = useState(requireManualDeviceApproval);
  const [locale, setLocale] = useState(preferredLocale);
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

  function saveSettings(nextApproval: boolean, nextLocale: string, rollback: () => void) {
    setPending(true);
    void (async () => {
      try {
        await client.updateBranchSettings(branchId, {
          organizationId: profile.organizationId,
          requireManualDeviceApproval: nextApproval,
          preferredLocale: nextLocale,
        });
        toast({ title: t('toast.saved'), variant: 'success' });
        onDone();
      } catch {
        rollback();
        toast({ title: t('toast.failed'), variant: 'error' });
      } finally {
        setPending(false);
      }
    })();
  }

  function toggleApproval(next: boolean) {
    setApproval(next);
    saveSettings(next, locale, () => setApproval(!next));
  }

  function changeLocale(next: string) {
    const previous = locale;
    setLocale(next);
    saveSettings(approval, next, () => setLocale(previous));
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

      <div className="flex items-center justify-between border-t border-border pt-4">
        <div>
          <div className="text-sm font-medium">{t('settings.branch.locale')}</div>
          <div className="text-xs text-muted-foreground">{t('settings.branch.locale.hint')}</div>
        </div>
        <Select value={locale} onValueChange={changeLocale} disabled={pending}>
          <SelectTrigger aria-label={t('settings.branch.locale')} className="w-40"><SelectValue /></SelectTrigger>
          <SelectContent>
            {BRANCH_LOCALES.map(l => <SelectItem key={l} value={l}>{LOCALE_ENDONYM[l]}</SelectItem>)}
          </SelectContent>
        </Select>
      </div>
    </div>
  );
}
