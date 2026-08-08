import { useEffect, useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/components/ui/toast';
import { EmptyState, ErrorState, LoadingCards } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import { describeApiError } from '@/api/describeApiError';
import type { FeaturesApi } from '@/api/platformClients/features';
import type { OrganizationFeatureState } from '@/api/types';

type Client = Pick<FeaturesApi, 'listFeatures' | 'setOverride' | 'clearOverride'>;

export function OrganizationFeaturesTab({ client, organizationId, planCode, canManage }: {
  client: Client;
  organizationId: string;
  planCode: string;
  // Право на мутацию (`platform.organizations.features.manage`) отдельно от права на просмотр:
  // поддержка видит вкладку и то, чем решена фича, но не рычаги — иначе кнопка «Применить» будет
  // выглядеть рабочей, а сервер молча ответит 403.
  canManage: boolean;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [revision, setRevision] = useState(0);
  const [features, setFeatures] = useState<OrganizationFeatureState[] | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setFeatures(null);
    setFailed(false);
    client.listFeatures(organizationId)
      .then(data => { if (!cancelled) setFeatures(data); })
      .catch(() => { if (!cancelled) setFailed(true); });
    return () => { cancelled = true; };
  }, [client, organizationId, revision]);

  if (failed) {
    return <ErrorState message={t('platform.organization.features.error')} retryLabel={t('state.retry')} onRetry={() => setRevision(value => value + 1)} />;
  }
  if (features === null) return <LoadingCards count={3} />;
  if (features.length === 0) return <EmptyState message={t('platform.organization.features.empty')} />;

  // Оба мутирующих метода отдают свежий полный список — экран просто подменяет состояние
  // ответом сервера, а не пересчитывает decisionLevel/planValue/defaultValue на клиенте.
  async function applyOverride(featureKey: string, request: { isEnabled: boolean; reason: string }) {
    try {
      const next = await client.setOverride(organizationId, featureKey, request);
      setFeatures(next);
      toast({ title: t('platform.organization.features.updated'), variant: 'success' });
    } catch (cause) {
      toast({ title: describeApiError(cause, t), variant: 'error' });
      throw cause;
    }
  }

  async function clearOverride(featureKey: string) {
    try {
      const next = await client.clearOverride(organizationId, featureKey);
      setFeatures(next);
      toast({ title: t('platform.organization.features.updated'), variant: 'success' });
    } catch (cause) {
      toast({ title: describeApiError(cause, t), variant: 'error' });
      throw cause;
    }
  }

  return (
    <div className="pc-analytics">
      {features.map(feature => (
        <FeatureRow
          key={feature.featureKey}
          feature={feature}
          planCode={planCode}
          canManage={canManage}
          onSetOverride={request => applyOverride(feature.featureKey, request)}
          onClearOverride={() => clearOverride(feature.featureKey)}
        />
      ))}
    </div>
  );
}

// Верхнеуровневая функция, а не компонент, вложенный в рендер OrganizationFeaturesTab: у строки
// собственный черновик (переключатель + причина), который не должен пересоздаваться на каждый
// рендер списка.
function FeatureRow({ feature, planCode, canManage, onSetOverride, onClearOverride }: {
  feature: OrganizationFeatureState;
  planCode: string;
  canManage: boolean;
  onSetOverride: (request: { isEnabled: boolean; reason: string }) => Promise<void>;
  onClearOverride: () => Promise<void>;
}) {
  const { t, formatDate } = useI18n();
  const [draftEnabled, setDraftEnabled] = useState(feature.isEnabled);
  const [reason, setReason] = useState('');
  const [pending, setPending] = useState(false);
  const trimmedReason = reason.trim();

  // Синхронизирует черновик с фактическим состоянием фичи: срабатывает и на первом рендере
  // строки, и после того, как список обновился ответом сервера на «Применить»/«Вернуть как у
  // тарифа» — так переключатель и поле причины не переживают собственное успешное действие.
  useEffect(() => {
    setDraftEnabled(feature.isEnabled);
    setReason('');
  }, [feature.isEnabled, feature.decisionLevel, feature.overrideReason]);

  const decisionLabel = feature.decisionLevel === 'override'
    ? t('platform.organization.features.byOverride')
    : feature.decisionLevel === 'plan'
      ? t('platform.organization.features.byPlan', { planCode })
      : t('platform.organization.features.byDefault');

  async function submit() {
    if (trimmedReason === '' || pending) return;
    setPending(true);
    try {
      await onSetOverride({ isEnabled: draftEnabled, reason: trimmedReason });
    } catch {
      // Тост об ошибке уже показан в OrganizationFeaturesTab — здесь только снимаем pending.
    } finally {
      setPending(false);
    }
  }

  async function clear() {
    if (pending) return;
    setPending(true);
    try {
      await onClearOverride();
    } catch {
      // см. submit()
    } finally {
      setPending(false);
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>{feature.name}</CardTitle>
        <Badge variant={feature.isEnabled ? 'success' : 'secondary'}>
          {feature.isEnabled ? t('platform.organization.features.enabled') : t('platform.organization.features.disabled')}
        </Badge>
      </CardHeader>
      <CardContent>
        <p>{feature.description}</p>

        {/* Главное на экране — чем решено, а не просто «вкл/выкл»: без этого нельзя ответить на
            вопрос «почему у клуба нет магазина» — не куплено (тариф) или не выкачено (умолчание). */}
        <p className="pc-kv"><span>{decisionLabel}</span></p>
        {feature.decisionLevel === 'override' ? (
          <p className="pc-analytics-footnote">
            {feature.overrideReason}
            {feature.overrideSetAtUtc !== null ? ` · ${formatDate(feature.overrideSetAtUtc)}` : ''}
          </p>
        ) : null}

        {canManage ? (
          <>
            <label className="ui-field">
              <span>{draftEnabled ? t('platform.organization.features.enabled') : t('platform.organization.features.disabled')}</span>
              <Switch checked={draftEnabled} onCheckedChange={setDraftEnabled} aria-label={feature.name} />
            </label>

            <label className="ui-field">
              <span>{t('platform.organization.features.reason')}</span>
              <Textarea
                aria-label={t('platform.organization.features.reason')}
                rows={2}
                value={reason}
                onChange={event => setReason(event.target.value)}
              />
              {trimmedReason === '' ? <span className="mgmt-drawer-hint">{t('platform.organization.features.reasonRequired')}</span> : null}
            </label>

            <div>
              <Button disabled={trimmedReason === '' || pending} onClick={() => void submit()}>
                {t('platform.organization.features.set')}
              </Button>
              {feature.decisionLevel === 'override' ? (
                <Button variant="outline" disabled={pending} onClick={() => void clear()}>{t('platform.organization.features.clear')}</Button>
              ) : null}
            </div>
          </>
        ) : null}
      </CardContent>
    </Card>
  );
}
