import { Badge } from '@/components/ui/badge';
import { useI18n, type MessageKey } from '@/i18n/I18nProvider';
import type { AttentionRow } from './metricsModel';

const LABEL: Record<AttentionRow['reason'], MessageKey> = {
  suspended: 'platform.overview.attention.suspended',
  past_due: 'platform.overview.attention.pastDue',
  health_errors: 'platform.overview.attention.healthErrors',
  expiring_invite: 'platform.overview.attention.expiringInvite',
  rollout_attention: 'platform.overview.attention.rollout'
};

function attentionHref(row: AttentionRow): string {
  if (row.reason === 'rollout_attention') return '/admin/updates?tab=rollouts';
  const tab = row.reason === 'past_due' ? 'invoices' : row.reason === 'expiring_invite' ? 'access' : 'clubs';
  return `/admin/organizations/${encodeURIComponent(row.organizationId)}?tab=${tab}`;
}

export function AttentionQueue({ rows }: { rows: AttentionRow[] }) {
  const { t } = useI18n();
  if (rows.length === 0) return <p className="text-sm text-muted-foreground">{t('platform.overview.attention.empty')}</p>;
  return <div className="overflow-hidden rounded-lg border border-border">
    {rows.map(row => <a key={`${row.organizationId}-${row.reason}`} href={attentionHref(row)} className="flex min-h-11 items-center justify-between gap-4 border-b border-border px-4 py-2.5 text-sm last:border-0 hover:bg-accent focus-visible:bg-accent">
      <span className="font-semibold">{row.name}</span>
      <Badge variant={row.reason === 'suspended' ? 'destructive' : 'secondary'}>{t(LABEL[row.reason])}</Badge>
    </a>)}
  </div>;
}
