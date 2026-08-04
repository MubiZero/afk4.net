import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Select } from '@/components/ui/select';
import { ConfirmDialog } from '@/components/shared/ConfirmDialog';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { OrganizationsApi } from '@/api/platformClients/organizations';
import type { OrganizationDetail } from '@/api/types';
import { STATUS_OPTIONS, STATUS_LABEL } from './organizationsModel';

type Updater = Pick<OrganizationsApi, 'updateStatus'>;

interface Props {
  client: Updater;
  organization: OrganizationDetail;
  onUpdated: (next: OrganizationDetail) => void;
}

export function OrganizationStatusSection({ client, organization, onUpdated }: Props) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [status, setStatus] = useState(organization.status);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [pending, setPending] = useState(false);

  const requiresReason = status !== 'active';

  async function submit(reason: string) {
    setPending(true);
    try {
      const next = await client.updateStatus(organization.organizationId, status, reason);
      onUpdated(next);
      setConfirmOpen(false);
      toast({ title: t('platform.organization.statusForm.updated'), variant: 'success' });
    } catch {
      toast({ title: t('platform.organization.action.error'), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  return (
    <Card>
      <CardHeader><CardTitle>{t('platform.organization.section.status')}</CardTitle></CardHeader>
      <CardContent>
        <label className="ui-field">
          <span>{t('platform.organization.statusForm.newStatus')}</span>
          <Select value={status} onChange={event => setStatus(event.target.value)}>
              {STATUS_OPTIONS.map(s => <option key={s} value={s}>{t(STATUS_LABEL[s])}</option>)}
          </Select>
        </label>
        <div>
          <Button onClick={() => setConfirmOpen(true)} disabled={status === organization.status}>
            {t('platform.organization.statusForm.apply')}
          </Button>
        </div>
      </CardContent>
      <ConfirmDialog
        open={confirmOpen}
        title={t('platform.organization.statusForm.confirmTitle')}
        confirmLabel={t('platform.organization.statusForm.confirm')}
        cancelLabel={t('platform.organization.statusForm.cancel')}
        reasonLabel={requiresReason ? t('platform.organization.statusForm.reason') : undefined}
        destructive={requiresReason}
        pending={pending}
        onConfirm={reason => void submit(reason)}
        onOpenChange={open => { if (!open) setConfirmOpen(false); }}
      />
    </Card>
  );
}
