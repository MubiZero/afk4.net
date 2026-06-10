import { useState, type FormEvent } from 'react';
import { slugify } from '@/lib/slugify';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { TenantsApi } from '@/api/platformClients/tenants';
import { TenantPlanCode, SubscriptionStatus, type CreateTenantResponse, type TenantLimits } from '@/api/types';

type Client = Pick<TenantsApi, 'createTenant'>;

export interface NewTenantScreenProps {
  client: Client;
  onCreated: (response: CreateTenantResponse) => void;
  onCancel: () => void;
}

interface FormState {
  organizationSlug: string;
  organizationName: string;
  branchSlug: string;
  branchName: string;
  branchCity: string;
  planCode: string;
  subscriptionStatus: string;
  ownerUserName: string;
  ownerDisplayName: string;
  maxBranches: string;
  maxDevicesPerBranch: string;
  maxConcurrentSessions: string;
  maxStaffUsersPerBranch: string;
}

const defaultState: FormState = {
  organizationSlug: '',
  organizationName: '',
  branchSlug: 'main',
  branchName: 'Main Branch',
  branchCity: '',
  planCode: TenantPlanCode.Starter,
  subscriptionStatus: SubscriptionStatus.Trial,
  ownerUserName: '',
  ownerDisplayName: '',
  maxBranches: '',
  maxDevicesPerBranch: '',
  maxConcurrentSessions: '',
  maxStaffUsersPerBranch: ''
};

export function NewTenantScreen({ client, onCreated, onCancel }: NewTenantScreenProps) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [form, setForm] = useState<FormState>(defaultState);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [slugTouched, setSlugTouched] = useState(false);

  function update(field: keyof FormState, value: string) {
    setForm(current => ({ ...current, [field]: value }));
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      const response = await client.createTenant({
        organizationSlug: form.organizationSlug.trim(),
        organizationName: form.organizationName.trim(),
        branchSlug: form.branchSlug.trim(),
        branchName: form.branchName.trim(),
        branchCity: form.branchCity.trim(),
        planCode: form.planCode,
        subscriptionStatus: form.subscriptionStatus,
        limits: buildLimits(form),
        ownerUserName: form.ownerUserName.trim() === '' ? null : form.ownerUserName.trim(),
        ownerDisplayName: form.ownerDisplayName.trim() === '' ? null : form.ownerDisplayName.trim(),
        ownerInviteLifetime: null
      });
      toast({ title: t('platform.newTenant.created'), variant: 'success' });
      onCreated(response);
    } catch (cause) {
      const message = cause instanceof Error ? cause.message : t('platform.newTenant.error');
      setError(message);
      toast({ title: t('platform.newTenant.error'), variant: 'error' });
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form className="flex max-w-3xl flex-col gap-4" onSubmit={handleSubmit}>
      {error !== null && (
        <Card><CardContent className="py-3 text-sm text-destructive">{error}</CardContent></Card>
      )}

      <Card>
        <CardHeader><CardTitle>{t('platform.newTenant.section.organization')}</CardTitle></CardHeader>
        <CardContent className="flex flex-col gap-3">
          <LabeledInput label={t('platform.newTenant.field.orgSlug')} hint={t('platform.newTenant.field.orgSlugHint')}
            value={form.organizationSlug} onChange={v => { setSlugTouched(true); update('organizationSlug', v); }} required />
          <LabeledInput label={t('platform.newTenant.field.orgName')}
            value={form.organizationName} onChange={v => setForm(current => ({
              ...current,
              organizationName: v,
              organizationSlug: slugTouched ? current.organizationSlug : slugify(v)
            }))} required />
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>{t('platform.newTenant.section.branch')}</CardTitle></CardHeader>
        <CardContent className="flex flex-col gap-3">
          <LabeledInput label={t('platform.newTenant.field.branchSlug')} value={form.branchSlug} onChange={v => update('branchSlug', v)} required />
          <LabeledInput label={t('platform.newTenant.field.branchName')} value={form.branchName} onChange={v => update('branchName', v)} required />
          <LabeledInput label={t('platform.newTenant.field.branchCity')} value={form.branchCity} onChange={v => update('branchCity', v)} required />
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>{t('platform.newTenant.section.plan')}</CardTitle></CardHeader>
        <CardContent className="flex flex-col gap-3">
          <label className="block">
            <span className="mb-1 block text-sm text-muted-foreground">{t('platform.newTenant.field.planCode')}</span>
            <Select value={form.planCode} onValueChange={v => update('planCode', v)}>
              <SelectTrigger aria-label={t('platform.newTenant.field.planCode')}><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value={TenantPlanCode.Starter}>{t('platform.plan.starter')}</SelectItem>
                <SelectItem value={TenantPlanCode.Growth}>{t('platform.plan.growth')}</SelectItem>
                <SelectItem value={TenantPlanCode.Scale}>{t('platform.plan.scale')}</SelectItem>
              </SelectContent>
            </Select>
          </label>
          <label className="block">
            <span className="mb-1 block text-sm text-muted-foreground">{t('platform.newTenant.field.subscriptionStatus')}</span>
            <Select value={form.subscriptionStatus} onValueChange={v => update('subscriptionStatus', v)}>
              <SelectTrigger aria-label={t('platform.newTenant.field.subscriptionStatus')}><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value={SubscriptionStatus.Trial}>{t('platform.newTenant.sub.trial')}</SelectItem>
                <SelectItem value={SubscriptionStatus.Active}>{t('platform.newTenant.sub.active')}</SelectItem>
                <SelectItem value={SubscriptionStatus.PastDue}>{t('platform.newTenant.sub.pastDue')}</SelectItem>
                <SelectItem value={SubscriptionStatus.Cancelled}>{t('platform.newTenant.sub.cancelled')}</SelectItem>
              </SelectContent>
            </Select>
          </label>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>{t('platform.newTenant.section.limits')}</CardTitle></CardHeader>
        <CardContent className="flex flex-col gap-3">
          <LabeledInput label={t('platform.newTenant.field.maxBranches')} type="number" value={form.maxBranches} onChange={v => update('maxBranches', v)} />
          <LabeledInput label={t('platform.newTenant.field.maxDevices')} type="number" value={form.maxDevicesPerBranch} onChange={v => update('maxDevicesPerBranch', v)} />
          <LabeledInput label={t('platform.newTenant.field.maxSessions')} type="number" value={form.maxConcurrentSessions} onChange={v => update('maxConcurrentSessions', v)} />
          <LabeledInput label={t('platform.newTenant.field.maxStaff')} type="number" value={form.maxStaffUsersPerBranch} onChange={v => update('maxStaffUsersPerBranch', v)} />
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>{t('platform.newTenant.section.owner')}</CardTitle></CardHeader>
        <CardContent className="flex flex-col gap-3">
          <LabeledInput label={t('platform.newTenant.field.ownerUserName')} value={form.ownerUserName} onChange={v => update('ownerUserName', v)} />
          <LabeledInput label={t('platform.newTenant.field.ownerDisplayName')} value={form.ownerDisplayName} onChange={v => update('ownerDisplayName', v)} />
        </CardContent>
      </Card>

      <div className="flex gap-2">
        <Button type="submit" disabled={submitting}>
          {submitting ? t('platform.newTenant.submitting') : t('platform.newTenant.submit')}
        </Button>
        <Button type="button" variant="outline" onClick={onCancel} disabled={submitting}>
          {t('platform.newTenant.cancel')}
        </Button>
      </div>
    </form>
  );
}

function LabeledInput({ label, hint, value, onChange, type, required }: {
  label: string;
  hint?: string;
  value: string;
  onChange: (value: string) => void;
  type?: string;
  required?: boolean;
}) {
  return (
    <label className="block">
      <span className="mb-1 block text-sm text-muted-foreground">{label}</span>
      <Input aria-label={label} type={type} value={value} required={required} onChange={e => onChange(e.target.value)} />
      {hint !== undefined && <span className="mt-1 block text-xs text-muted-foreground">{hint}</span>}
    </label>
  );
}

function buildLimits(form: FormState): TenantLimits | null {
  const parsed: TenantLimits = {
    maxBranches: parseOptional(form.maxBranches),
    maxDevicesPerBranch: parseOptional(form.maxDevicesPerBranch),
    maxConcurrentSessions: parseOptional(form.maxConcurrentSessions),
    maxStaffUsersPerBranch: parseOptional(form.maxStaffUsersPerBranch)
  };
  if (Object.values(parsed).every(value => value === null)) {
    return null;
  }
  return parsed;
}

function parseOptional(value: string): number | null {
  if (value.trim() === '') {
    return null;
  }
  const parsed = Number.parseInt(value, 10);
  return Number.isNaN(parsed) ? null : parsed;
}
