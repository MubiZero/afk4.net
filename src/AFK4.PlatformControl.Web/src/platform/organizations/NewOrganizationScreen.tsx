import { useState, type FormEvent } from 'react';
import { slugify } from '@/lib/slugify';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { OrganizationsApi } from '@/api/platformClients/organizations';
import { OrganizationPlanCode, SubscriptionStatus, type CreateOrganizationResponse, type OrganizationLimits } from '@/api/types';

type Client = Pick<OrganizationsApi, 'createOrganization'>;

export interface NewOrganizationScreenProps {
  client: Client;
  onCreated: (response: CreateOrganizationResponse) => void;
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
  planCode: OrganizationPlanCode.Starter,
  subscriptionStatus: SubscriptionStatus.Trial,
  ownerUserName: '',
  ownerDisplayName: '',
  maxBranches: '',
  maxDevicesPerBranch: '',
  maxConcurrentSessions: '',
  maxStaffUsersPerBranch: ''
};

export function NewOrganizationScreen({ client, onCreated, onCancel }: NewOrganizationScreenProps) {
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
      const response = await client.createOrganization({
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
        organizationOwnerInviteLifetime: null
      });
      toast({ title: t('platform.newOrganization.created'), variant: 'success' });
      onCreated(response);
    } catch (cause) {
      const message = cause instanceof Error ? cause.message : t('platform.newOrganization.error');
      setError(message);
      toast({ title: t('platform.newOrganization.error'), variant: 'error' });
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
        <CardHeader><CardTitle>{t('platform.newOrganization.section.organization')}</CardTitle></CardHeader>
        <CardContent className="flex flex-col gap-3">
          <LabeledInput label={t('platform.newOrganization.field.orgSlug')} hint={t('platform.newOrganization.field.orgSlugHint')}
            value={form.organizationSlug} onChange={v => { setSlugTouched(true); update('organizationSlug', v); }} required />
          <LabeledInput label={t('platform.newOrganization.field.orgName')}
            value={form.organizationName} onChange={v => setForm(current => ({
              ...current,
              organizationName: v,
              organizationSlug: slugTouched ? current.organizationSlug : slugify(v)
            }))} required />
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>{t('platform.newOrganization.section.branch')}</CardTitle></CardHeader>
        <CardContent className="flex flex-col gap-3">
          <LabeledInput label={t('platform.newOrganization.field.branchSlug')} value={form.branchSlug} onChange={v => update('branchSlug', v)} required />
          <LabeledInput label={t('platform.newOrganization.field.branchName')} value={form.branchName} onChange={v => update('branchName', v)} required />
          <LabeledInput label={t('platform.newOrganization.field.branchCity')} value={form.branchCity} onChange={v => update('branchCity', v)} required />
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>{t('platform.newOrganization.section.plan')}</CardTitle></CardHeader>
        <CardContent className="flex flex-col gap-3">
          <label className="block">
            <span className="mb-1 block text-sm text-muted-foreground">{t('platform.newOrganization.field.planCode')}</span>
            <Select value={form.planCode} onValueChange={v => update('planCode', v)}>
              <SelectTrigger aria-label={t('platform.newOrganization.field.planCode')}><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value={OrganizationPlanCode.Starter}>{t('platform.plan.starter')}</SelectItem>
                <SelectItem value={OrganizationPlanCode.Growth}>{t('platform.plan.growth')}</SelectItem>
                <SelectItem value={OrganizationPlanCode.Scale}>{t('platform.plan.scale')}</SelectItem>
              </SelectContent>
            </Select>
          </label>
          <label className="block">
            <span className="mb-1 block text-sm text-muted-foreground">{t('platform.newOrganization.field.subscriptionStatus')}</span>
            <Select value={form.subscriptionStatus} onValueChange={v => update('subscriptionStatus', v)}>
              <SelectTrigger aria-label={t('platform.newOrganization.field.subscriptionStatus')}><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value={SubscriptionStatus.Trial}>{t('platform.newOrganization.sub.trial')}</SelectItem>
                <SelectItem value={SubscriptionStatus.Active}>{t('platform.newOrganization.sub.active')}</SelectItem>
                <SelectItem value={SubscriptionStatus.PastDue}>{t('platform.newOrganization.sub.pastDue')}</SelectItem>
                <SelectItem value={SubscriptionStatus.Cancelled}>{t('platform.newOrganization.sub.cancelled')}</SelectItem>
              </SelectContent>
            </Select>
          </label>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>{t('platform.newOrganization.section.limits')}</CardTitle></CardHeader>
        <CardContent className="flex flex-col gap-3">
          <LabeledInput label={t('platform.newOrganization.field.maxBranches')} type="number" value={form.maxBranches} onChange={v => update('maxBranches', v)} />
          <LabeledInput label={t('platform.newOrganization.field.maxDevices')} type="number" value={form.maxDevicesPerBranch} onChange={v => update('maxDevicesPerBranch', v)} />
          <LabeledInput label={t('platform.newOrganization.field.maxSessions')} type="number" value={form.maxConcurrentSessions} onChange={v => update('maxConcurrentSessions', v)} />
          <LabeledInput label={t('platform.newOrganization.field.maxStaff')} type="number" value={form.maxStaffUsersPerBranch} onChange={v => update('maxStaffUsersPerBranch', v)} />
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>{t('platform.newOrganization.section.owner')}</CardTitle></CardHeader>
        <CardContent className="flex flex-col gap-3">
          <LabeledInput label={t('platform.newOrganization.field.ownerUserName')} value={form.ownerUserName} onChange={v => update('ownerUserName', v)} />
          <LabeledInput label={t('platform.newOrganization.field.ownerDisplayName')} value={form.ownerDisplayName} onChange={v => update('ownerDisplayName', v)} />
        </CardContent>
      </Card>

      <div className="flex gap-2">
        <Button type="submit" disabled={submitting}>
          {submitting ? t('platform.newOrganization.submitting') : t('platform.newOrganization.submit')}
        </Button>
        <Button type="button" variant="outline" onClick={onCancel} disabled={submitting}>
          {t('platform.newOrganization.cancel')}
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

function buildLimits(form: FormState): OrganizationLimits | null {
  const parsed: OrganizationLimits = {
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
