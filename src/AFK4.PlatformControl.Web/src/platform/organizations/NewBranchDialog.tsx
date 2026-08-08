import { useState } from 'react';
import { Dialog } from '@/components/ui/dialog';
import { Field } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import { PlatformApiError } from '@/api/platformTransport';
import type { OrganizationsApi } from '@/api/platformClients/organizations';
import type { OrganizationBranch, PlanLimitExceeded } from '@/api/types';

type Client = Pick<OrganizationsApi, 'createBranch'>;

interface Props {
  client: Client;
  organizationId: string;
  onClose: () => void;
  onCreated: (branch: OrganizationBranch) => void;
}

const DEFAULT_TIME_ZONE = 'Asia/Dushanbe';

// A plan-limit rejection and an occupied-slug rejection both answer 409, but they call for
// different messages: one is fixed by upgrading the plan, the other by picking another address.
// `errorCode` on PlatformApiError only ever mirrors the human `error` string (see
// platformTransport.ts), so telling the two apart means reading the raw body for `planLimit`.
function readPlanLimit(error: unknown): PlanLimitExceeded | null {
  if (!(error instanceof PlatformApiError)) return null;
  try {
    const parsed = JSON.parse(error.body) as { planLimit?: PlanLimitExceeded };
    return parsed.planLimit ?? null;
  } catch {
    return null;
  }
}

export function NewBranchDialog({ client, organizationId, onClose, onCreated }: Props) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [name, setName] = useState('');
  const [city, setCity] = useState('');
  const [slug, setSlug] = useState('');
  const [timeZone, setTimeZone] = useState(DEFAULT_TIME_ZONE);
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  const canSubmit = name.trim() !== '' && city.trim() !== '' && slug.trim() !== '';

  async function submit() {
    if (!canSubmit) return;
    setPending(true);
    setError(null);
    try {
      const branch = await client.createBranch(organizationId, {
        slug: slug.trim(),
        name: name.trim(),
        city: city.trim(),
        preferredTimeZone: timeZone.trim() === '' ? null : timeZone.trim()
      });
      onCreated(branch);
    } catch (cause) {
      const planLimit = readPlanLimit(cause);
      if (planLimit !== null) {
        setError(t('platform.organization.branches.planLimit', {
          planCode: planLimit.planCode,
          current: planLimit.current,
          limit: planLimit.limit
        }));
      } else if (cause instanceof PlatformApiError && cause.status === 409) {
        setError(t('platform.organization.branches.slugTaken'));
      } else {
        setError(t('platform.organization.action.error'));
      }
      toast({ title: t('platform.organization.action.error'), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  return (
    <Dialog
      open
      title={t('platform.organization.branches.dialogTitle')}
      onClose={onClose}
      footer={
        <>
          <Button variant="outline" disabled={pending} onClick={onClose}>{t('platform.organization.profileDialog.cancel')}</Button>
          <Button disabled={pending || !canSubmit} onClick={() => void submit()}>{t('platform.organization.branches.create')}</Button>
        </>
      }
    >
      <div className="mgmt-form">
        {error !== null ? <p className="pc-error-text">{error}</p> : null}
        <Field label={t('platform.organization.branches.name')} htmlFor="branch-name">
          <Input id="branch-name" value={name} onChange={event => setName(event.target.value)} />
        </Field>
        <Field label={t('platform.organization.branches.city')} htmlFor="branch-city">
          <Input id="branch-city" value={city} onChange={event => setCity(event.target.value)} />
        </Field>
        <Field label={t('platform.organization.branches.slug')} htmlFor="branch-slug">
          <Input id="branch-slug" value={slug} onChange={event => setSlug(event.target.value)} />
        </Field>
        <Field label={t('platform.organization.branches.timeZone')} htmlFor="branch-timezone">
          <Input id="branch-timezone" value={timeZone} onChange={event => setTimeZone(event.target.value)} />
        </Field>
      </div>
    </Dialog>
  );
}
