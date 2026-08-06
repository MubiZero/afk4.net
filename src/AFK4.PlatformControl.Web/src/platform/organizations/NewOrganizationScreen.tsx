import { useState, type FormEvent } from 'react';
import { slugify } from '@/lib/slugify';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { useToast } from '@/components/ui/toast';
import { describeApiError } from '@/api/describeApiError';
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
      // 409 здесь — это всегда занятый ключ, и это ровно та конкретика, которая помогает
      // исправить форму. Остальные коды не несут для пользователя ничего сверх «не вышло».
      const message = describeApiError(cause, t, { 409: 'platform.newOrganization.error.slugTaken' });
      setError(message);
      toast({ title: t('platform.newOrganization.error'), variant: 'error' });
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form className="mgmt-form" onSubmit={handleSubmit}>
      {error !== null && (
        <Card><CardContent className="pc-error-text">{error}</CardContent></Card>
      )}

      <Card>
        <CardHeader><CardTitle>{t('platform.newOrganization.section.organization')}</CardTitle></CardHeader>
        <CardContent>
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
        <CardContent>
          <LabeledInput label={t('platform.newOrganization.field.branchSlug')} value={form.branchSlug} onChange={v => update('branchSlug', v)} required />
          <LabeledInput label={t('platform.newOrganization.field.branchName')} value={form.branchName} onChange={v => update('branchName', v)} required />
          <LabeledInput label={t('platform.newOrganization.field.branchCity')} value={form.branchCity} onChange={v => update('branchCity', v)} required />
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>{t('platform.newOrganization.section.plan')}</CardTitle></CardHeader>
        <CardContent>
          <label className="ui-field">
            <span>{t('platform.newOrganization.field.planCode')}</span>
            <Select value={form.planCode} onChange={event => update('planCode', event.target.value)}>
                <option value={OrganizationPlanCode.Starter}>{t('platform.plan.starter')}</option>
                <option value={OrganizationPlanCode.Growth}>{t('platform.plan.growth')}</option>
                <option value={OrganizationPlanCode.Scale}>{t('platform.plan.scale')}</option>
            </Select>
          </label>
          <label className="ui-field">
            <span>{t('platform.newOrganization.field.subscriptionStatus')}</span>
            <Select value={form.subscriptionStatus} onChange={event => update('subscriptionStatus', event.target.value)}>
                <option value={SubscriptionStatus.Trial}>{t('platform.newOrganization.sub.trial')}</option>
                <option value={SubscriptionStatus.Active}>{t('platform.newOrganization.sub.active')}</option>
                <option value={SubscriptionStatus.PastDue}>{t('platform.newOrganization.sub.pastDue')}</option>
                <option value={SubscriptionStatus.Cancelled}>{t('platform.newOrganization.sub.cancelled')}</option>
            </Select>
          </label>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>{t('platform.newOrganization.section.limits')}</CardTitle></CardHeader>
        <CardContent>
          <LabeledInput label={t('platform.newOrganization.field.maxBranches')} type="number" value={form.maxBranches} onChange={v => update('maxBranches', v)} />
          <LabeledInput label={t('platform.newOrganization.field.maxDevices')} type="number" value={form.maxDevicesPerBranch} onChange={v => update('maxDevicesPerBranch', v)} />
          <LabeledInput label={t('platform.newOrganization.field.maxSessions')} type="number" value={form.maxConcurrentSessions} onChange={v => update('maxConcurrentSessions', v)} />
          <LabeledInput label={t('platform.newOrganization.field.maxStaff')} type="number" value={form.maxStaffUsersPerBranch} onChange={v => update('maxStaffUsersPerBranch', v)} />
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>{t('platform.newOrganization.section.owner')}</CardTitle></CardHeader>
        <CardContent>
          <LabeledInput label={t('platform.newOrganization.field.ownerUserName')} value={form.ownerUserName} onChange={v => update('ownerUserName', v)} />
          <LabeledInput label={t('platform.newOrganization.field.ownerDisplayName')} value={form.ownerDisplayName} onChange={v => update('ownerDisplayName', v)} />
        </CardContent>
      </Card>

      <div className="pc-cell-actions">
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
    <label className="ui-field">
      <span>{label}</span>
      <Input aria-label={label} type={type} value={value} required={required} onChange={e => onChange(e.target.value)} />
      {hint !== undefined && <span className="mgmt-drawer-hint">{hint}</span>}
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
