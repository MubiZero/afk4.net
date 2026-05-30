// src/club/settings/OperatorsTable.tsx
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { EmptyState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import { roleLabelKey } from './roles';
import type { OperatorRow } from './settingsModel';

export function OperatorsTable({ rows, emptyMessage, onSelect }: {
  rows: OperatorRow[];
  emptyMessage: string;
  onSelect: (row: OperatorRow) => void;
}) {
  const { t } = useI18n();
  if (rows.length === 0) return <EmptyState message={emptyMessage} />;
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t('operators.col.name')}</TableHead>
          <TableHead>{t('operators.col.roles')}</TableHead>
          <TableHead>{t('operators.col.status')}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {rows.map(row => (
          <TableRow key={row.staffUserId} data-clickable="true" onClick={() => onSelect(row)}>
            <TableCell>
              <div className="font-medium">{row.displayName}</div>
              <div className="text-xs text-muted-foreground">{row.userName}</div>
            </TableCell>
            <TableCell className="text-sm">{row.roleNames.map(r => t(roleLabelKey(r))).join(', ')}</TableCell>
            <TableCell>
              <Badge variant={row.isActive ? 'default' : 'secondary'}>
                {row.isActive ? t('operators.status.active') : t('operators.status.inactive')}
              </Badge>
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}
