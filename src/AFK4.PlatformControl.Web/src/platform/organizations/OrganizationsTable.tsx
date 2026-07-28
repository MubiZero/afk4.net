import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { EmptyState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { OrganizationRow } from './organizationsModel';
import { STATUS_VARIANT, STATUS_LABEL, SUBSCRIPTION_VARIANT, SUBSCRIPTION_LABEL, PLAN_LABEL } from './organizationsModel';
import type { MessageKey } from '@/i18n/messages';

interface OrganizationsTableProps {
  rows: OrganizationRow[];
  selectedId: string | null;
  emptyMessage: string;
  onSelect: (organizationId: string) => void;
}

export function OrganizationsTable({ rows, selectedId, emptyMessage, onSelect }: OrganizationsTableProps) {
  const { t, formatNumber, formatDate } = useI18n();
  if (rows.length === 0) return <EmptyState message={emptyMessage} />;
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t('platform.organizations.col.name')}</TableHead>
          <TableHead>{t('platform.organizations.col.slug')}</TableHead>
          <TableHead>{t('platform.organizations.col.status')}</TableHead>
          <TableHead>{t('platform.organizations.col.plan')}</TableHead>
          <TableHead>{t('platform.organizations.col.subscription')}</TableHead>
          <TableHead>{t('platform.organizations.col.branches')}</TableHead>
          <TableHead>{t('platform.organizations.col.updated')}</TableHead>
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
                {t((STATUS_LABEL[row.status] ?? 'platform.organization.status.active') as MessageKey)}
              </Badge>
            </TableCell>
            <TableCell>{t((PLAN_LABEL[row.planCode] ?? 'platform.plan.starter') as MessageKey)}</TableCell>
            <TableCell>
              <Badge variant={SUBSCRIPTION_VARIANT[row.subscriptionStatus] ?? 'secondary'}>
                {t((SUBSCRIPTION_LABEL[row.subscriptionStatus] ?? 'platform.organization.subscription.active') as MessageKey)}
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
