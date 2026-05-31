import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { EmptyState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { TenantRow } from './tenantsModel';
import { STATUS_VARIANT, STATUS_LABEL, SUBSCRIPTION_VARIANT, SUBSCRIPTION_LABEL, PLAN_LABEL } from './tenantsModel';
import type { MessageKey } from '@/i18n/messages';

interface TenantsTableProps {
  rows: TenantRow[];
  selectedId: string | null;
  emptyMessage: string;
  onSelect: (organizationId: string) => void;
}

export function TenantsTable({ rows, selectedId, emptyMessage, onSelect }: TenantsTableProps) {
  const { t, formatNumber, formatDate } = useI18n();
  if (rows.length === 0) return <EmptyState message={emptyMessage} />;
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t('platform.tenants.col.name')}</TableHead>
          <TableHead>{t('platform.tenants.col.slug')}</TableHead>
          <TableHead>{t('platform.tenants.col.status')}</TableHead>
          <TableHead>{t('platform.tenants.col.plan')}</TableHead>
          <TableHead>{t('platform.tenants.col.subscription')}</TableHead>
          <TableHead>{t('platform.tenants.col.branches')}</TableHead>
          <TableHead>{t('platform.tenants.col.updated')}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {rows.map(row => (
          <TableRow
            key={row.organizationId}
            data-clickable="true"
            data-selected={row.organizationId === selectedId ? 'true' : undefined}
            onClick={() => onSelect(row.organizationId)}
          >
            <TableCell>{row.name}</TableCell>
            <TableCell><code>{row.slug}</code></TableCell>
            <TableCell>
              <Badge variant={STATUS_VARIANT[row.status] ?? 'secondary'}>
                {t((STATUS_LABEL[row.status] ?? 'platform.tenant.status.active') as MessageKey)}
              </Badge>
            </TableCell>
            <TableCell>{t((PLAN_LABEL[row.planCode] ?? 'platform.plan.starter') as MessageKey)}</TableCell>
            <TableCell>
              <Badge variant={SUBSCRIPTION_VARIANT[row.subscriptionStatus] ?? 'secondary'}>
                {t((SUBSCRIPTION_LABEL[row.subscriptionStatus] ?? 'platform.tenant.subscription.active') as MessageKey)}
              </Badge>
            </TableCell>
            <TableCell>{formatNumber(row.branchCount)}</TableCell>
            <TableCell>{formatDate(row.updatedAtUtc)}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}
