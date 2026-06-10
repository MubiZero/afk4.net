import { ArrowDown, ArrowUp, Trash2 } from 'lucide-react';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { VenueApi } from '@/api/clients/venue';
import { useFloorMap } from './useFloorMap';
import { makeClientId, moveByIndex, type EditorSeat, type EditorZone } from './floorMapModel';

type Client = Pick<VenueApi, 'getFloorMap' | 'updateFloorMap'>;

export function FloorMapEditor({ client, branchId, organizationId, canEdit }: {
  client: Client;
  branchId: string;
  organizationId: string;
  canEdit: boolean;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  const state = useFloorMap(client, branchId, organizationId);

  if (state.status === 'loading') return <LoadingCards count={2} />;
  if (state.status === 'error') return <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />;

  const { zones, setZones, saving, conflict, save, reload, branchName } = state;

  const renameZone = (clientId: string, name: string) =>
    setZones(prev => prev.map(z => (z.clientId === clientId ? { ...z, name } : z)));
  const removeZone = (clientId: string) =>
    setZones(prev => prev.filter(z => z.clientId !== clientId));
  const moveZone = (index: number, direction: -1 | 1) =>
    setZones(prev => moveByIndex(prev, index, direction));
  const addZone = () =>
    setZones(prev => [...prev, { clientId: makeClientId('zone'), zoneId: null, name: `${t('floor.zoneDefault')} ${prev.length + 1}`, seats: [] }]);

  const addSeat = (zoneClientId: string) =>
    setZones(prev => prev.map(z => (z.clientId === zoneClientId
      ? { ...z, seats: [...z.seats, { clientId: makeClientId('seat'), seatId: null, name: `${t('floor.seatDefault')}-${z.seats.length + 1}` }] }
      : z)));
  const renameSeat = (zoneClientId: string, seatClientId: string, name: string) =>
    setZones(prev => prev.map(z => (z.clientId === zoneClientId
      ? { ...z, seats: z.seats.map(s => (s.clientId === seatClientId ? { ...s, name } : s)) }
      : z)));
  const removeSeat = (zoneClientId: string, seatClientId: string) =>
    setZones(prev => prev.map(z => (z.clientId === zoneClientId
      ? { ...z, seats: z.seats.filter(s => s.clientId !== seatClientId) }
      : z)));
  const moveSeat = (zoneClientId: string, index: number, direction: -1 | 1) =>
    setZones(prev => prev.map(z => (z.clientId === zoneClientId ? { ...z, seats: moveByIndex(z.seats, index, direction) } : z)));

  async function onSave() {
    const outcome = await save();
    if (outcome === 'ok') toast({ title: t('toast.saved'), variant: 'success' });
    else if (outcome === 'error') toast({ title: t('toast.failed'), variant: 'error' });
    // 'conflict' surfaces as the inline banner below
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-semibold">{branchName}</h2>
        <div className="flex gap-2">
          <Button variant="outline" disabled={saving} onClick={reload}>{t('floor.reload')}</Button>
          {canEdit && <Button disabled={saving} onClick={() => void onSave()}>{t('floor.save')}</Button>}
        </div>
      </div>

      {conflict && (
        <Card><CardContent className="py-3 text-sm text-destructive">{t('floor.conflict')}</CardContent></Card>
      )}
      {!canEdit && (
        <p className="text-sm text-muted-foreground">{t('floor.readonly')}</p>
      )}

      {zones.length === 0 ? (
        <EmptyState message={t('floor.empty')} />
      ) : (
        <div className="flex flex-col gap-4">
          {zones.map((zone, zoneIndex) => (
            <ZoneCard
              key={zone.clientId}
              zone={zone}
              zoneIndex={zoneIndex}
              zoneCount={zones.length}
              canEdit={canEdit}
              onRenameZone={renameZone}
              onRemoveZone={removeZone}
              onMoveZone={moveZone}
              onAddSeat={addSeat}
              onRenameSeat={renameSeat}
              onRemoveSeat={removeSeat}
              onMoveSeat={moveSeat}
            />
          ))}
        </div>
      )}

      {canEdit && (
        <div>
          <Button variant="outline" onClick={addZone}>{t('floor.addZone')}</Button>
        </div>
      )}
    </div>
  );
}

function ZoneCard({ zone, zoneIndex, zoneCount, canEdit, onRenameZone, onRemoveZone, onMoveZone, onAddSeat, onRenameSeat, onRemoveSeat, onMoveSeat }: {
  zone: EditorZone;
  zoneIndex: number;
  zoneCount: number;
  canEdit: boolean;
  onRenameZone: (clientId: string, name: string) => void;
  onRemoveZone: (clientId: string) => void;
  onMoveZone: (index: number, direction: -1 | 1) => void;
  onAddSeat: (zoneClientId: string) => void;
  onRenameSeat: (zoneClientId: string, seatClientId: string, name: string) => void;
  onRemoveSeat: (zoneClientId: string, seatClientId: string) => void;
  onMoveSeat: (zoneClientId: string, index: number, direction: -1 | 1) => void;
}) {
  const { t } = useI18n();
  return (
    <Card>
      <CardHeader className="flex flex-row items-center gap-2">
        <Input aria-label={t('floor.zoneName')} value={zone.name} disabled={!canEdit}
          onChange={e => onRenameZone(zone.clientId, e.target.value)} />
        {canEdit && (
          <>
            <Button variant="ghost" size="icon" aria-label={t('floor.moveUp')} disabled={zoneIndex === 0} onClick={() => onMoveZone(zoneIndex, -1)}><ArrowUp className="size-4" /></Button>
            <Button variant="ghost" size="icon" aria-label={t('floor.moveDown')} disabled={zoneIndex === zoneCount - 1} onClick={() => onMoveZone(zoneIndex, 1)}><ArrowDown className="size-4" /></Button>
            <Button variant="ghost" size="icon" aria-label={t('floor.removeZone')} onClick={() => onRemoveZone(zone.clientId)}><Trash2 className="size-4" /></Button>
          </>
        )}
      </CardHeader>
      <CardContent className="flex flex-col gap-2">
        {zone.seats.map((seat: EditorSeat, seatIndex) => (
          <div key={seat.clientId} className="flex items-center gap-2">
            <Input aria-label={t('floor.seatName')} value={seat.name} disabled={!canEdit}
              onChange={e => onRenameSeat(zone.clientId, seat.clientId, e.target.value)} />
            {canEdit && (
              <>
                <Button variant="ghost" size="icon" aria-label={t('floor.moveUp')} disabled={seatIndex === 0} onClick={() => onMoveSeat(zone.clientId, seatIndex, -1)}><ArrowUp className="size-4" /></Button>
                <Button variant="ghost" size="icon" aria-label={t('floor.moveDown')} disabled={seatIndex === zone.seats.length - 1} onClick={() => onMoveSeat(zone.clientId, seatIndex, 1)}><ArrowDown className="size-4" /></Button>
                <Button variant="ghost" size="icon" aria-label={t('floor.removeSeat')} onClick={() => onRemoveSeat(zone.clientId, seat.clientId)}><Trash2 className="size-4" /></Button>
              </>
            )}
          </div>
        ))}
        {canEdit && (
          <div><Button variant="outline" size="sm" onClick={() => onAddSeat(zone.clientId)}>{t('floor.addSeat')}</Button></div>
        )}
      </CardContent>
    </Card>
  );
}
