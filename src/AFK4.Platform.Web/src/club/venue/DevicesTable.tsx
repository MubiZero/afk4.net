import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { EmptyState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { MessageKey } from '@/i18n/messages';
import type { DeviceRow, DeviceRowStatus } from './devicesModel';

const STATUS_LABEL: Record<DeviceRowStatus, MessageKey> = {
  online: 'devices.status.online',
  offline: 'devices.status.offline',
  pending: 'devices.status.pending'
};
const STATUS_VARIANT: Record<DeviceRowStatus, 'default' | 'secondary' | 'destructive'> = {
  online: 'default',
  offline: 'destructive',
  pending: 'secondary'
};

export function DevicesTable({ rows, emptyMessage, onSelect }: {
  rows: DeviceRow[];
  emptyMessage: string;
  onSelect: (row: DeviceRow) => void;
}) {
  const { t, formatDate } = useI18n();
  if (rows.length === 0) return <EmptyState message={emptyMessage} />;
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t('devices.col.name')}</TableHead>
          <TableHead>{t('devices.col.seat')}</TableHead>
          <TableHead>{t('devices.col.status')}</TableHead>
          <TableHead>{t('devices.col.heartbeat')}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {rows.map(row => (
          <TableRow key={row.deviceId} data-clickable="true" onClick={() => onSelect(row)}>
            <TableCell>
              <div className="font-medium">{row.displayName}</div>
              <div className="text-xs text-muted-foreground">{row.machineName}</div>
            </TableCell>
            <TableCell>{row.seatLabel}</TableCell>
            <TableCell><Badge variant={STATUS_VARIANT[row.status]}>{t(STATUS_LABEL[row.status])}</Badge></TableCell>
            <TableCell className="text-sm text-muted-foreground">
              {row.lastHeartbeatAtUtc ? formatDate(row.lastHeartbeatAtUtc) : t('devices.heartbeat.never')}
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}
