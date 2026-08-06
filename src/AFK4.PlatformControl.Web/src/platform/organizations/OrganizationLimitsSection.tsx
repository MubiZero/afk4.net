import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { OrganizationsApi } from '@/api/platformClients/organizations';
import type { OrganizationDetail, OrganizationLimits } from '@/api/types';

type Updater = Pick<OrganizationsApi, 'updateLimits'>;

interface Props {
  client: Updater;
  organization: OrganizationDetail;
  onUpdated: (next: OrganizationDetail) => void;
}

function toField(value: number | null): string {
  return value === null ? '' : String(value);
}
function toLimit(value: string): number | null {
  const trimmed = value.trim();
  if (trimmed === '') return null;
  const n = Number.parseInt(trimmed, 10);
  return Number.isNaN(n) || n < 0 ? null : n;
}

export function OrganizationLimitsSection({ client, organization, onUpdated }: Props) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [maxBranches, setMaxBranches] = useState(toField(organization.limits.maxBranches));
  const [maxDevices, setMaxDevices] = useState(toField(organization.limits.maxDevicesPerBranch));
  const [maxSessions, setMaxSessions] = useState(toField(organization.limits.maxConcurrentSessions));
  const [maxStaff, setMaxStaff] = useState(toField(organization.limits.maxStaffUsersPerBranch));
  const [pending, setPending] = useState(false);

  async function submit() {
    setPending(true);
    const limits: OrganizationLimits = {
      maxBranches: toLimit(maxBranches),
      maxDevicesPerBranch: toLimit(maxDevices),
      maxConcurrentSessions: toLimit(maxSessions),
      maxStaffUsersPerBranch: toLimit(maxStaff)
    };
    try {
      const next = await client.updateLimits(organization.organizationId, limits);
      onUpdated(next);
      toast({ title: t('platform.organization.limitsForm.updated'), variant: 'success' });
    } catch {
      toast({ title: t('platform.organization.action.error'), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  const field = (label: string, value: string, set: (v: string) => void) => (
    <label className="ui-field">
      <span>{label}</span>
      <Input type="number" inputMode="numeric" min="0" step="1" aria-label={label} value={value} onChange={e => set(e.target.value)} />
    </label>
  );

  return (
    <Card>
      <CardHeader><CardTitle>{t('platform.organization.section.limits')}</CardTitle></CardHeader>
      <CardContent>
        {field(t('platform.organization.limitsForm.maxBranches'), maxBranches, setMaxBranches)}
        {field(t('platform.organization.limitsForm.maxDevices'), maxDevices, setMaxDevices)}
        {field(t('platform.organization.limitsForm.maxSessions'), maxSessions, setMaxSessions)}
        {field(t('platform.organization.limitsForm.maxStaff'), maxStaff, setMaxStaff)}
        <div>
          <Button onClick={() => void submit()} disabled={pending}>{t('platform.organization.limitsForm.apply')}</Button>
        </div>
      </CardContent>
    </Card>
  );
}
