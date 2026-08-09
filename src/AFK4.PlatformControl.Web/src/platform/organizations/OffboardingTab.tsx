import { useEffect, useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { LoadingCards, ErrorState } from '@/components/ui/states';
import { ConfirmDialog } from '@/components/shared/ConfirmDialog';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { OffboardingApi } from '@/api/platformClients/offboarding';
import type { OrganizationOffboarding } from '@/api/types';
import { describeOffboardingError, purgeBlockReasonKey } from './offboardingModel';

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
  const [state, setState] = useState<OrganizationOffboarding | null>(null);
  const [failed, setFailed] = useState(false);
  const [tick, setTick] = useState(0);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [pending, setPending] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setFailed(false);
    client.getOffboarding(organizationId)
      .then(loaded => { if (!cancelled) setState(loaded); })
      .catch(() => { if (!cancelled) setFailed(true); });
    return () => { cancelled = true; };
  }, [client, organizationId, tick]);

  async function download() {
    if (pending) return;
    setPending(true);
    try {
      const blob = await client.downloadExport(organizationId);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `${state?.slug ?? organizationId}-export.zip`;
      link.click();
      URL.revokeObjectURL(url);
      toast({ title: t('platform.offboarding.exported'), variant: 'success' });
    } catch (cause) {
      toast({ title: describeOffboardingError(cause, t), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  async function purge(typedSlug: string) {
    if (state === null || pending) return;
    setPending(true);
    try {
      await client.purge(organizationId, typedSlug.trim());
      toast({ title: t('platform.offboarding.purged'), variant: 'success' });
      setConfirmOpen(false);
      setTick(value => value + 1);
      onPurged?.();
    } catch (cause) {
      toast({ title: describeOffboardingError(cause, t), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  if (failed) {
    return <ErrorState message={t('platform.offboarding.error.load')} retryLabel={t('state.retry')} onRetry={() => setTick(value => value + 1)} />;
  }
  if (state === null) return <LoadingCards count={1} />;

  const blockReason = purgeBlockReasonKey(state);

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('platform.offboarding.title')}</CardTitle>
        {state.status === 'purged'
          ? <Badge variant="outline">{t('platform.offboarding.status.purged')}</Badge>
          : null}
      </CardHeader>
      <CardContent>
        <p className="mgmt-drawer-hint">{t('platform.offboarding.description')}</p>

        <dl className="pc-kv-list">
          <div className="pc-kv">
            <dt>{t('platform.offboarding.field.slug')}</dt>
            <dd><code>{state.slug}</code></dd>
          </div>
          {state.purgedAtUtc !== null ? (
            <div className="pc-kv">
              <dt>{t('platform.offboarding.field.purgedAt')}</dt>
              <dd>{formatDate(state.purgedAtUtc)}</dd>
            </div>
          ) : (
            <div className="pc-kv">
              <dt>{t('platform.offboarding.field.eligibleAt')}</dt>
              <dd>
                {state.purgeEligibleAtUtc === null
                  ? t('platform.offboarding.field.eligibleAt.none')
                  : formatDate(state.purgeEligibleAtUtc)}
              </dd>
            </div>
          )}
        </dl>

        {state.status === 'purged' ? (
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
              title={blockReason !== null ? t(blockReason) : undefined}
              onClick={() => setConfirmOpen(true)}
            >
              {t('platform.offboarding.purge')}
            </Button>
          </div>
        )}

        {/* Причина недоступности показывается текстом, а не только подсказкой курсора: рычаг,
            который заведомо ответит отказом, обязан объяснить себя без наведения мыши. */}
        {blockReason !== null && state.status !== 'purged'
          ? <p className="mgmt-drawer-hint">{t(blockReason)}</p>
          : null}

        <ConfirmDialog
          open={confirmOpen}
          title={t('platform.offboarding.purgeConfirm.title')}
          description={t('platform.offboarding.purgeConfirm.body', { slug: state.slug })}
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
