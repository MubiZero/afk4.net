import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { LoadingCards, ErrorState } from '@/components/ui/states';
import { ConfirmDialog } from '@/components/shared/ConfirmDialog';
import { useToast } from '@/components/ui/toast';
import { useBlockedReason } from '@/components/ui/blockedReason';
import { useI18n } from '@/i18n/I18nProvider';
import type { OffboardingApi } from '@/api/platformClients/offboarding';
import { purgeBlockReasonKey } from './offboardingModel';
import { describeApiError } from '@/api/describeApiError';
import { useLoadable } from '../useLoadable';

type Client = Pick<OffboardingApi, 'getOffboarding' | 'purge' | 'downloadExport'>;

export function OffboardingTab({
  client,
  organizationId,
  onPurged
}: {
  client: Client;
  organizationId: string;
  onPurged?: () => void;
}) {
  const { t, formatDate } = useI18n();
  const { toast } = useToast();
  const state = useLoadable(() => client.getOffboarding(organizationId), [organizationId]);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [pending, setPending] = useState(false);
  const offboarding = state.status === 'ready' ? state.data : null;
  // Стёртому клубу рычаг не показывается вовсе, так что и объяснять его отказ незачем.
  const blockReason = offboarding === null || offboarding.status === 'purged' ? null : purgeBlockReasonKey(offboarding);
  const blocked = useBlockedReason(blockReason === null ? null : t(blockReason));

  async function download() {
    if (pending) return;
    setPending(true);
    try {
      const blob = await client.downloadExport(organizationId);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `${offboarding?.slug ?? organizationId}-export.zip`;
      link.click();
      URL.revokeObjectURL(url);
      toast({ title: t('platform.offboarding.exported'), variant: 'success' });
    } catch (cause) {
      toast({ title: describeApiError(cause, t), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  async function purge(typedSlug: string) {
    if (offboarding === null || pending) return;
    setPending(true);
    try {
      await client.purge(organizationId, typedSlug.trim());
      toast({ title: t('platform.offboarding.purged'), variant: 'success' });
      setConfirmOpen(false);
      state.retry();
      onPurged?.();
    } catch (cause) {
      toast({ title: describeApiError(cause, t), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  if (state.status === 'error') {
    return <ErrorState title={t('platform.offboarding.error.load')} message={state.message} retryLabel={state.canRetry ? t('state.retry') : undefined} onRetry={state.canRetry ? state.retry : undefined} />;
  }
  if (offboarding === null) return <LoadingCards count={1} />;

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('platform.offboarding.title')}</CardTitle>
        {offboarding.status === 'purged'
          ? <Badge variant="outline">{t('platform.offboarding.status.purged')}</Badge>
          : null}
      </CardHeader>
      <CardContent>
        <p className="mgmt-drawer-hint">{t('platform.offboarding.description')}</p>

        <dl className="pc-kv-list">
          <div className="pc-kv">
            <dt>{t('platform.offboarding.field.slug')}</dt>
            <dd><code>{offboarding.slug}</code></dd>
          </div>
          {offboarding.purgedAtUtc !== null ? (
            <div className="pc-kv">
              <dt>{t('platform.offboarding.field.purgedAt')}</dt>
              <dd>{formatDate(offboarding.purgedAtUtc)}</dd>
            </div>
          ) : (
            <div className="pc-kv">
              <dt>{t('platform.offboarding.field.eligibleAt')}</dt>
              <dd>
                {offboarding.purgeEligibleAtUtc === null
                  ? t('platform.offboarding.field.eligibleAt.none')
                  : formatDate(offboarding.purgeEligibleAtUtc)}
              </dd>
            </div>
          )}
        </dl>

        {offboarding.status === 'purged' ? (
          // У стёртого клуба предлагать нечего: данных за ним нет, осталась только архивная строка.
          <p className="mgmt-drawer-hint">{t('platform.offboarding.purgedHint')}</p>
        ) : (
          <div className="pc-cell-actions">
            <Button variant="outline" disabled={pending} onClick={() => void download()}>
              {t('platform.offboarding.export')}
            </Button>
            <Button
              variant="destructive"
              disabled={pending || blockReason !== null}
              aria-describedby={blocked.describedBy}
              onClick={() => setConfirmOpen(true)}
            >
              {t('platform.offboarding.purge')}
            </Button>
          </div>
        )}

        {/* Причина недоступности — текстом, а не подсказкой курсора: рычаг, который заведомо
            ответит отказом, обязан объяснить себя без наведения мыши. */}
        {blocked.hint}

        <ConfirmDialog
          open={confirmOpen}
          title={t('platform.offboarding.purgeConfirm.title')}
          description={t('platform.offboarding.purgeConfirm.body', { slug: offboarding.slug })}
          confirmLabel={t('platform.offboarding.purge')}
          cancelLabel={t('common.cancel')}
          reasonLabel={t('platform.offboarding.purgeConfirm.slugLabel')}
          destructive
          pending={pending}
          onConfirm={typedSlug => void purge(typedSlug)}
          onOpenChange={open => { if (!open) setConfirmOpen(false); }}
        />
      </CardContent>
    </Card>
  );
}
