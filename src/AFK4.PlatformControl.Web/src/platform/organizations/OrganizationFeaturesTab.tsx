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

export function OrganizationFeaturesTab({ client, organizationId, planCode }: {
  client: Client;
  organizationId: string;
  planCode: string;
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
    }
  }

  async function clearOverride(featureKey: string) {
    try {
      const next = await client.clearOverride(organizationId, featureKey);
      setFeatures(next);
      toast({ title: t('platform.organization.features.updated'), variant: 'success' });
    } catch (cause) {
      toast({ title: describeApiError(cause, t), variant: 'error' });
    }
  }

  return (
    <div className="pc-analytics">
      {features.map(feature => (
        <FeatureRow
          key={feature.featureKey}
          feature={feature}
          planCode={planCode}
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
function FeatureRow({ feature, planCode, onSetOverride, onClearOverride }: {
  feature: OrganizationFeatureState;
  planCode: string;
  onSetOverride: (request: { isEnabled: boolean; reason: string }) => void;
  onClearOverride: () => void;
}) {
  const { t, formatDate } = useI18n();
  const [draftEnabled, setDraftEnabled] = useState(feature.isEnabled);
  const [reason, setReason] = useState('');
  const trimmedReason = reason.trim();

  const decisionLabel = feature.decisionLevel === 'override'
    ? t('platform.organization.features.byOverride')
    : feature.decisionLevel === 'plan'
      ? t('platform.organization.features.byPlan', { planCode })
      : t('platform.organization.features.byDefault');

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
          <Button
            disabled={trimmedReason === ''}
            onClick={() => onSetOverride({ isEnabled: draftEnabled, reason: trimmedReason })}
          >
            {t('platform.organization.features.set')}
          </Button>
          {feature.decisionLevel === 'override' ? (
            <Button variant="outline" onClick={onClearOverride}>{t('platform.organization.features.clear')}</Button>
          ) : null}
        </div>
      </CardContent>
    </Card>
  );
}
