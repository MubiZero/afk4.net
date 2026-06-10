// src/club/venue/DeviceDrawer.tsx
import { useState } from 'react';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select';
import { ConfirmDialog } from '@/components/shared/ConfirmDialog';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { VenueApi } from '@/api/clients/venue';
import type { DeviceRow, SeatOption } from './devicesModel';

type DeviceActions = Pick<VenueApi, 'renameDevice' | 'moveDeviceSeat' | 'removeDevice' | 'approveDevice' | 'rejectDevice'>;
const DEFAULT_APPROVE_REASON = 'Подтверждено из веб-консоли';

export function DeviceDrawer({ device, seatOptions, client, onDone }: {
  device: DeviceRow;
  seatOptions: SeatOption[];
  client: DeviceActions;
  onDone: () => void;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [name, setName] = useState(device.displayName);
  const [seatId, setSeatId] = useState<string | undefined>(device.seatId ?? undefined);
  const [pending, setPending] = useState(false);
  const [confirm, setConfirm] = useState<null | 'remove' | 'reject'>(null);

  async function run(action: () => Promise<unknown>) {
    setPending(true);
    try {
      await action();
      toast({ title: t('toast.saved'), variant: 'success' });
      onDone();
    } catch {
      toast({ title: t('toast.failed'), variant: 'error' });
    } finally {
      setPending(false);
      setConfirm(null);
    }
  }

  if (device.status === 'pending') {
    return (
      <div className="flex flex-col gap-3">
        <Button disabled={pending}
          onClick={() => void run(() => client.approveDevice(device.deviceId, device.organizationId, DEFAULT_APPROVE_REASON))}>
          {t('devices.action.approve')}
        </Button>
        <Button variant="destructive" disabled={pending} onClick={() => setConfirm('reject')}>
          {t('devices.action.reject')}
        </Button>
        <ConfirmDialog
          open={confirm === 'reject'} title={t('devices.reject.confirm')}
          confirmLabel={t('devices.action.reject')} cancelLabel={t('common.cancel')}
          reasonLabel={t('devices.reason')} destructive pending={pending}
          onConfirm={reason => void run(() => client.rejectDevice(device.deviceId, device.organizationId, reason))}
          onOpenChange={open => { if (!open) setConfirm(null); }}
        />
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-5">
      <label className="block text-sm">
        <span className="mb-1 block text-muted-foreground">{t('common.name')}</span>
        <Input aria-label={t('common.name')} value={name} onChange={e => setName(e.target.value)} />
      </label>
      <Button disabled={pending} onClick={() => void run(() => client.renameDevice(device.deviceId, device.organizationId, name))}>
        {t('common.save')}
      </Button>

      <label className="block text-sm">
        <span className="mb-1 block text-muted-foreground">{t('devices.action.moveSeat')}</span>
        <Select value={seatId} onValueChange={setSeatId}>
          <SelectTrigger aria-label={t('devices.action.moveSeat')}><SelectValue placeholder={t('devices.col.seat')} /></SelectTrigger>
          <SelectContent>
            {seatOptions.map(s => <SelectItem key={s.seatId} value={s.seatId}>{s.label}</SelectItem>)}
          </SelectContent>
        </Select>
      </label>
      <Button variant="outline" disabled={pending || seatId === undefined}
        onClick={() => { if (seatId) void run(() => client.moveDeviceSeat(device.deviceId, device.organizationId, seatId)); }}>
        {t('devices.action.moveSeat')}
      </Button>

      <Button variant="destructive" disabled={pending} onClick={() => setConfirm('remove')}>
        {t('devices.action.remove')}
      </Button>
      <ConfirmDialog
        open={confirm === 'remove'} title={t('devices.remove.confirm')}
        confirmLabel={t('common.delete')} cancelLabel={t('common.cancel')}
        reasonLabel={t('devices.reason')} destructive pending={pending}
        onConfirm={reason => void run(() => client.removeDevice(device.deviceId, device.organizationId, reason))}
        onOpenChange={open => { if (!open) setConfirm(null); }}
      />
    </div>
  );
}
