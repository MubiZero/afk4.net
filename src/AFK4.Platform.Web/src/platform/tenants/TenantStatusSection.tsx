import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select';
import { ConfirmDialog } from '@/components/shared/ConfirmDialog';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { PlatformApiClient } from '@/api/platformApi';
import type { TenantDetail } from '@/api/types';
import { STATUS_OPTIONS, STATUS_LABEL } from './tenantsModel';

type Updater = Pick<PlatformApiClient, 'updateStatus'>;

interface Props {
  client: Updater;
  tenant: TenantDetail;
  onUpdated: (next: TenantDetail) => void;
}

export function TenantStatusSection({ client, tenant, onUpdated }: Props) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [status, setStatus] = useState(tenant.status);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [pending, setPending] = useState(false);

  const requiresReason = status !== 'active';

  async function submit(reason: string) {
    setPending(true);
    try {
      const next = await client.updateStatus(tenant.organizationId, status, reason);
      onUpdated(next);
      setConfirmOpen(false);
      toast({ title: t('platform.tenant.statusForm.updated'), variant: 'success' });
    } catch {
      toast({ title: t('platform.tenant.action.error'), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  return (
    <Card>
      <CardHeader><CardTitle>{t('platform.tenant.section.status')}</CardTitle></CardHeader>
      <CardContent className="flex flex-col gap-3">
        <label className="block text-sm">
          <span className="mb-1 block text-muted-foreground">{t('platform.tenant.statusForm.newStatus')}</span>
          <Select value={status} onValueChange={setStatus}>
            <SelectTrigger aria-label={t('platform.tenant.statusForm.newStatus')}><SelectValue /></SelectTrigger>
            <SelectContent>
              {STATUS_OPTIONS.map(s => <SelectItem key={s} value={s}>{t(STATUS_LABEL[s])}</SelectItem>)}
            </SelectContent>
          </Select>
        </label>
        <div>
          <Button onClick={() => setConfirmOpen(true)} disabled={status === tenant.status}>
            {t('platform.tenant.statusForm.apply')}
          </Button>
        </div>
      </CardContent>
      <ConfirmDialog
        open={confirmOpen}
        title={t('platform.tenant.statusForm.confirmTitle')}
        confirmLabel={t('platform.tenant.statusForm.confirm')}
        cancelLabel={t('platform.tenant.statusForm.cancel')}
        reasonLabel={requiresReason ? t('platform.tenant.statusForm.reason') : undefined}
        destructive={requiresReason}
        pending={pending}
        onConfirm={reason => void submit(reason)}
        onOpenChange={open => { if (!open) setConfirmOpen(false); }}
      />
    </Card>
  );
}
