import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { PlatformApiClient } from '@/api/platformApi';
import type { TenantDetail, TenantLimits } from '@/api/types';

type Updater = Pick<PlatformApiClient, 'updateLimits'>;

interface Props {
  client: Updater;
  tenant: TenantDetail;
  onUpdated: (next: TenantDetail) => void;
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

export function TenantLimitsSection({ client, tenant, onUpdated }: Props) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [maxBranches, setMaxBranches] = useState(toField(tenant.limits.maxBranches));
  const [maxDevices, setMaxDevices] = useState(toField(tenant.limits.maxDevicesPerBranch));
  const [maxSessions, setMaxSessions] = useState(toField(tenant.limits.maxConcurrentSessions));
  const [maxStaff, setMaxStaff] = useState(toField(tenant.limits.maxStaffUsersPerBranch));
  const [pending, setPending] = useState(false);

  async function submit() {
    setPending(true);
    const limits: TenantLimits = {
      maxBranches: toLimit(maxBranches),
      maxDevicesPerBranch: toLimit(maxDevices),
      maxConcurrentSessions: toLimit(maxSessions),
      maxStaffUsersPerBranch: toLimit(maxStaff)
    };
    try {
      const next = await client.updateLimits(tenant.organizationId, limits);
      onUpdated(next);
      toast({ title: t('platform.tenant.limitsForm.updated'), variant: 'success' });
    } catch {
      toast({ title: t('platform.tenant.action.error'), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  const field = (label: string, value: string, set: (v: string) => void) => (
    <label className="block text-sm">
      <span className="mb-1 block text-muted-foreground">{label}</span>
      <Input type="number" inputMode="numeric" min="0" step="1" aria-label={label} value={value} onChange={e => set(e.target.value)} />
    </label>
  );

  return (
    <Card>
      <CardHeader><CardTitle>{t('platform.tenant.section.limits')}</CardTitle></CardHeader>
      <CardContent className="flex flex-col gap-3">
        {field(t('platform.tenant.limitsForm.maxBranches'), maxBranches, setMaxBranches)}
        {field(t('platform.tenant.limitsForm.maxDevices'), maxDevices, setMaxDevices)}
        {field(t('platform.tenant.limitsForm.maxSessions'), maxSessions, setMaxSessions)}
        {field(t('platform.tenant.limitsForm.maxStaff'), maxStaff, setMaxStaff)}
        <div>
          <Button onClick={() => void submit()} disabled={pending}>{t('platform.tenant.limitsForm.apply')}</Button>
        </div>
      </CardContent>
    </Card>
  );
}
