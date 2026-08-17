import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { Layers, Pencil, Trash2 } from 'lucide-react';
import { MgmtTable } from '../../kit/MgmtTable';
import { MgmtDrawer } from '../../kit/MgmtDrawer';
import type { RowAction } from '../../kit/types';
import { PanelModal } from '../../../PanelModal';
import { CriticalActionConfirmation, EmptyState } from '../../../operatorPrimitives';
import { projectOperatorError } from '../../../apiErrors';
import { hasPermission, permissionNames } from '../../../operatorPermissions';
import {
  createAuthenticatedOperatorClients,
  readArray,
  readNumber,
  readString,
  requireBackend
} from '../../../operatorHelpers';
import type { ZoneDto } from '../../../operatorApiClients';
import type { Feedback, OperatorBackendContext } from '../../../operatorTypes';

type Zone = Record<string, unknown>;
type Seat = Record<string, unknown>;

type ZoneModalState =
  | { mode: 'create-zone' }
  | { mode: 'edit-zone'; zoneId: string };

type SeatModalState =
  | { mode: 'create-seat'; zoneId: string }
  | { mode: 'edit-seat'; zoneId: string; seatId: string };

type CriticalAction =
  | { kind: 'delete-zone'; zoneId: string; zoneName: string; seatCount: number }
  | { kind: 'delete-seat'; zoneId: string; seatId: string; seatName: string };

// Домен A раздела «Залы и ПК»: узкий список залов слева + ПОСТОЯННАЯ панель деталей выбранного
// зала справа (см. task-B2-halls-rework-brief.md — визуальная приёмка потребовала эффективный
// two-pane лейаут вместо список+закрываемый drawer: правая панель не закрывается и всегда
// показывает актуальный зал, первый зал автовыбирается на маунте и при инвалидации выбора, чтобы
// панель не пустовала). Все 6 операций (A1-A6) портированы 1:1 из
// прежней формы настроек (удалена вместе с разделом Settings) — те же клиентские вызовы,
// тот же двойной
// permission-гейт (canManageLayout проп + серверный hasPermission(nextBackend.session, ...) на
// каждый вызов), та же валидация и onFeedback pending→confirmed/failed. create/edit — в
// PanelModal, удаление — через CriticalActionConfirmation, без предварительного «pending»-тоста
// на клик (современный паттерн, см. MapSidePanel: тост только вокруг реального вызова).
export function ZonesTab({
  zones,
  backend,
  canManageLayout,
  onReload,
  onFeedback
}: {
  zones: ZoneDto[];
  backend: OperatorBackendContext | null;
  canManageLayout: boolean;
  onReload: (nextBackend: OperatorBackendContext) => Promise<void>;
  onFeedback: (feedback: Feedback) => void;
}) {
  const { t } = useI18n();
  const [selectedZoneId, setSelectedZoneId] = useState<string | null>(null);
  const [zoneModal, setZoneModal] = useState<ZoneModalState | null>(null);
  const [seatModal, setSeatModal] = useState<SeatModalState | null>(null);
  const [criticalAction, setCriticalAction] = useState<CriticalAction | null>(null);
  const [zoneName, setZoneName] = useState('');
  const [zoneSortOrder, setZoneSortOrder] = useState('10');
  const [zoneHardware, setZoneHardware] = useState('');
  const [seatName, setSeatName] = useState('');
  const [seatSortOrder, setSeatSortOrder] = useState('10');
  const [busy, setBusy] = useState(false);

  // Правая панель ПОСТОЯННАЯ (не закрывается), поэтому ей всегда нужен валидный зал: автовыбор
  // первого зала на маунте и всякий раз, когда текущий выбор пропал из выборки (удаление/reload) —
  // иначе панель пустовала бы до ручного клика.
  useEffect(() => {
    setSelectedZoneId((current) => {
      if (current && zones.some((zone) => readString(zone, 'zoneId') === current)) {
        return current;
      }
      return zones[0] ? readString(zones[0], 'zoneId') : null;
    });
  }, [zones]);

  const selectedZone = zones.find((zone) => readString(zone, 'zoneId') === selectedZoneId) ?? null;
  const selectedZoneSeats = selectedZone ? readArray<Seat>(selectedZone, 'seats') : [];

  const openCreateZone = () => {
    setZoneName(t('op.settings.prefill.zoneName'));
    setZoneSortOrder(String((zones.length + 1) * 10));
    setZoneHardware('');
    setZoneModal({ mode: 'create-zone' });
  };
  const openEditZone = (zone: Zone) => {
    setZoneName(readString(zone, 'name'));
    setZoneSortOrder(String(readNumber(zone, 'sortOrder', 0)));
    setZoneHardware(readString(zone, 'hardwareSummary'));
    setZoneModal({ mode: 'edit-zone', zoneId: readString(zone, 'zoneId') });
  };
  const openCreateSeat = (zoneId: string) => {
    const seatCountAcrossZones = zones.reduce((sum, zone) => sum + readArray(zone, 'seats').length, 0);
    setSeatName(`PC-${String(seatCountAcrossZones + 1).padStart(2, '0')}`);
    setSeatSortOrder('10');
    setSeatModal({ mode: 'create-seat', zoneId });
  };
  const openEditSeat = (zoneId: string, seat: Seat) => {
    setSeatName(readString(seat, 'name'));
    setSeatSortOrder(String(readNumber(seat, 'sortOrder', 0)));
    setSeatModal({ mode: 'edit-seat', zoneId, seatId: readString(seat, 'seatId') });
  };

  const submitZone = async () => {
    if (!zoneModal) return;
    const label = zoneModal.mode === 'edit-zone' ? t('op.settings.action.updateZone') : t('op.settings.action.createZone');
    setBusy(true);
    onFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.manageLayout)) {
        throw new Error(t('op.settings.layout.error.noPermLayout'));
      }

      const name = zoneName.trim();
      const sortOrder = Number(zoneSortOrder);
      // Пустая строка — это «железо не указано», а не пустая строка в витрине клуба.
      const hardware = zoneHardware.trim() === '' ? null : zoneHardware.trim();
      if (!name || !Number.isInteger(sortOrder)) {
        throw new Error(t('op.settings.layout.error.fillZone'));
      }

      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const zone = zoneModal.mode === 'edit-zone'
        ? await apiClients.settings.updateZone(nextBackend.branchId, zoneModal.zoneId, {
          organizationId: nextBackend.session.organizationId,
          name,
          sortOrder,
          hardwareSummary: hardware
        })
        : await apiClients.settings.createZone(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          name,
          sortOrder,
          hardwareSummary: hardware
        });
      setSelectedZoneId(readString(zone, 'zoneId', zoneModal.mode === 'edit-zone' ? zoneModal.zoneId : ''));
      setZoneModal(null);
      await onReload(nextBackend);
      onFeedback({ label, state: 'confirmed' });
    } catch (error) {
      onFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setBusy(false);
    }
  };

  const submitSeat = async () => {
    if (!seatModal) return;
    const label = seatModal.mode === 'edit-seat' ? t('op.settings.action.updateSeat') : t('op.settings.action.addSeat');
    setBusy(true);
    onFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.manageLayout)) {
        throw new Error(t('op.settings.layout.error.noPermLayout'));
      }

      const zoneId = seatModal.zoneId.trim();
      if (!zoneId) {
        throw new Error(t('op.settings.layout.error.noZoneForSeat'));
      }

      const name = seatName.trim();
      const sortOrder = Number(seatSortOrder);
      if (!name || !Number.isInteger(sortOrder)) {
        throw new Error(t('op.settings.layout.error.fillSeat'));
      }

      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      if (seatModal.mode === 'edit-seat') {
        await apiClients.settings.updateSeat(nextBackend.branchId, seatModal.seatId, {
          organizationId: nextBackend.session.organizationId,
          zoneId,
          name,
          sortOrder
        });
      } else {
        await apiClients.settings.createSeat(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          zoneId,
          name,
          sortOrder
        });
      }
      setSeatModal(null);
      await onReload(nextBackend);
      onFeedback({ label, state: 'confirmed' });
    } catch (error) {
      onFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setBusy(false);
    }
  };

  const confirmDelete = async () => {
    if (!criticalAction) return;
    const label = criticalAction.kind === 'delete-zone' ? t('op.settings.action.deleteZone') : t('op.settings.action.deleteSeat');
    setCriticalAction(null);
    onFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.manageLayout)) {
        throw new Error(t('op.settings.layout.error.noPermLayout'));
      }

      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      if (criticalAction.kind === 'delete-zone') {
        await apiClients.settings.deleteZone(nextBackend.branchId, criticalAction.zoneId, nextBackend.session.organizationId);
      } else {
        await apiClients.settings.deleteSeat(nextBackend.branchId, criticalAction.seatId, nextBackend.session.organizationId);
      }
      await onReload(nextBackend);
      onFeedback({ label, state: 'confirmed' });
    } catch (error) {
      onFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    }
  };

  const zoneRowActions = canManageLayout
    ? (zone: Zone): RowAction[] => [
      {
        id: 'rename',
        label: t('op.settings.action.updateZone'),
        icon: <Pencil size={14} aria-hidden="true" />,
        onSelect: () => openEditZone(zone)
      },
      {
        id: 'delete',
        label: t('op.settings.action.deleteZone'),
        icon: <Trash2 size={14} aria-hidden="true" />,
        danger: true,
        onSelect: () => setCriticalAction({
          kind: 'delete-zone',
          zoneId: readString(zone, 'zoneId'),
          zoneName: readString(zone, 'name', t('op.settings.layout.zoneFallback')),
          seatCount: readArray(zone, 'seats').length
        })
      }
    ]
    : undefined;

  const seatRowActions = (zoneId: string) => canManageLayout
    ? (seat: Seat): RowAction[] => [
      {
        id: 'rename',
        label: t('op.settings.action.updateSeat'),
        icon: <Pencil size={14} aria-hidden="true" />,
        onSelect: () => openEditSeat(zoneId, seat)
      },
      {
        id: 'delete',
        label: t('op.settings.action.deleteSeat'),
        icon: <Trash2 size={14} aria-hidden="true" />,
        danger: true,
        onSelect: () => setCriticalAction({
          kind: 'delete-seat',
          zoneId,
          seatId: readString(seat, 'seatId'),
          seatName: readString(seat, 'name', t('op.settings.layout.seatFallback'))
        })
      }
    ]
    : undefined;

  const drawerActions: RowAction[] = selectedZone && canManageLayout
    ? [
      {
        id: 'rename',
        label: t('op.settings.action.updateZone'),
        icon: <Pencil size={14} aria-hidden="true" />,
        onSelect: () => openEditZone(selectedZone)
      },
      {
        id: 'delete',
        label: t('op.settings.action.deleteZone'),
        icon: <Trash2 size={14} aria-hidden="true" />,
        danger: true,
        onSelect: () => setCriticalAction({
          kind: 'delete-zone',
          zoneId: readString(selectedZone, 'zoneId'),
          zoneName: readString(selectedZone, 'name', t('op.settings.layout.zoneFallback')),
          seatCount: readArray(selectedZone, 'seats').length
        })
      }
    ]
    : [];

  return (
    <>
      <div className="mgmt-master-detail mgmt-master-detail--nav">
        <MgmtTable<Zone>
          columns={[
            {
              key: 'zone',
              header: t('op.management.halls.col.zone'),
              render: (zone) => (
                <span className="mgmt-zone-row">
                  <span>{readString(zone, 'name', t('op.settings.layout.zoneFallback'))}</span>
                  <span className="mgmt-count-badge">{t('op.settings.layout.seatCount', { count: readArray(zone, 'seats').length })}</span>
                </span>
              )
            }
          ]}
          rows={zones}
          rowKey={(zone) => readString(zone, 'zoneId')}
          gridTemplate="1fr"
          selectedKey={selectedZoneId}
          onSelectRow={(zone) => setSelectedZoneId(readString(zone, 'zoneId'))}
          rowActions={zoneRowActions}
          toolbar={{
            title: t('op.management.halls.zonesTable.title'),
            primary: canManageLayout ? { label: t('op.management.halls.addZoneCta'), onClick: openCreateZone } : undefined
          }}
          empty={{
            icon: <Layers size={22} aria-hidden="true" />,
            title: t('op.management.halls.zonesEmpty.title'),
            description: t('op.management.halls.zonesEmpty.description'),
            action: canManageLayout ? { label: t('op.management.halls.addZoneCta'), onClick: openCreateZone } : undefined
          }}
        />

        {selectedZone ? (
          <MgmtDrawer
            title={readString(selectedZone, 'name', t('op.settings.layout.zoneFallback'))}
            subtitle={t('op.settings.layout.seatCount', { count: selectedZoneSeats.length })}
            actions={drawerActions}
          >
            <div className="mgmt-drawer-section">
              <div className="mgmt-section-title"><span>{t('op.management.halls.seatsSection.title')}</span></div>
              <MgmtTable<Seat>
                columns={[
                  { key: 'name', header: t('op.management.halls.col.seatName'), render: (seat) => readString(seat, 'name', t('op.settings.layout.seatFallback')) },
                  { key: 'order', header: t('op.management.halls.col.sortOrder'), align: 'end', render: (seat) => String(readNumber(seat, 'sortOrder', 0)) }
                ]}
                rows={selectedZoneSeats}
                rowKey={(seat) => readString(seat, 'seatId')}
                gridTemplate="1fr 90px"
                rowActions={seatRowActions(readString(selectedZone, 'zoneId'))}
                toolbar={{
                  primary: canManageLayout
                    ? { label: t('op.management.halls.addSeatCta'), onClick: () => openCreateSeat(readString(selectedZone, 'zoneId')) }
                    : undefined
                }}
                empty={{
                  title: t('op.management.halls.seatsEmpty.title'),
                  description: t('op.management.halls.seatsEmpty.description'),
                  action: canManageLayout
                    ? { label: t('op.management.halls.addSeatCta'), onClick: () => openCreateSeat(readString(selectedZone, 'zoneId')) }
                    : undefined
                }}
              />
            </div>
          </MgmtDrawer>
        ) : (
          <div className="mgmt-detail-empty">
            <EmptyState
              icon={<Layers size={22} aria-hidden="true" />}
              title={t('op.management.halls.noZones.title')}
              description={t('op.management.halls.noZones.description')}
              action={canManageLayout ? { label: t('op.management.halls.addZoneCta'), onClick: openCreateZone } : undefined}
            />
          </div>
        )}
      </div>

      {zoneModal && (
        <PanelModal
          title={zoneModal.mode === 'edit-zone' ? t('op.management.halls.zoneModal.editTitle') : t('op.management.halls.zoneModal.createTitle')}
          onClose={() => setZoneModal(null)}
          closeDisabled={busy}
        >
          <form className="mgmt-form" onSubmit={(event) => { event.preventDefault(); void submitZone(); }}>
            <div className="mgmt-form-grid">
              <label>{t('op.settings.layout.zoneName')}
                <input value={zoneName} onChange={(event) => setZoneName(event.currentTarget.value)} autoFocus />
              </label>
              <label>{t('op.settings.layout.zoneOrder')}
                <input inputMode="numeric" value={zoneSortOrder} onChange={(event) => setZoneSortOrder(event.currentTarget.value)} />
              </label>
              <label className="mgmt-form-wide">{t('op.settings.layout.zoneHardware')}
                <input
                  value={zoneHardware}
                  maxLength={200}
                  placeholder={t('op.settings.layout.zoneHardwarePlaceholder')}
                  onChange={(event) => setZoneHardware(event.currentTarget.value)}
                />
                <span className="club-field-hint">{t('op.settings.layout.zoneHardwareHint')}</span>
              </label>
            </div>
            <div className="mgmt-form-actions">
              <button type="button" className="ui-btn" onClick={() => setZoneModal(null)} disabled={busy}>{t('common.cancel')}</button>
              <button type="submit" className="ui-btn ui-btn--primary" disabled={busy}>
                {zoneModal.mode === 'edit-zone' ? t('op.settings.action.updateZone') : t('op.settings.layout.createZoneBtn')}
              </button>
            </div>
          </form>
        </PanelModal>
      )}

      {seatModal && (
        <PanelModal
          title={seatModal.mode === 'edit-seat' ? t('op.management.halls.seatModal.editTitle') : t('op.management.halls.seatModal.createTitle')}
          onClose={() => setSeatModal(null)}
          closeDisabled={busy}
        >
          <form className="mgmt-form" onSubmit={(event) => { event.preventDefault(); void submitSeat(); }}>
            <div className="mgmt-form-grid">
              <label>{t('op.settings.layout.seatName')}
                <input value={seatName} onChange={(event) => setSeatName(event.currentTarget.value)} autoFocus />
              </label>
              <label>{t('op.settings.layout.seatOrder')}
                <input inputMode="numeric" value={seatSortOrder} onChange={(event) => setSeatSortOrder(event.currentTarget.value)} />
              </label>
            </div>
            <div className="mgmt-form-actions">
              <button type="button" className="ui-btn" onClick={() => setSeatModal(null)} disabled={busy}>{t('common.cancel')}</button>
              <button type="submit" className="ui-btn ui-btn--primary" disabled={busy}>
                {seatModal.mode === 'edit-seat' ? t('op.settings.action.updateSeat') : t('op.settings.layout.addSeatBtn')}
              </button>
            </div>
          </form>
        </PanelModal>
      )}

      {criticalAction?.kind === 'delete-zone' && (
        <CriticalActionConfirmation
          title={t('op.settings.layout.confirmDeleteZone.title')}
          detail={`${criticalAction.zoneName} · ${t('op.settings.layout.seatCount', { count: criticalAction.seatCount })}`}
          impact={t('op.settings.layout.confirmDeleteZone.impact')}
          confirmLabel={t('op.settings.layout.confirmDeleteZone.confirm')}
          onCancel={() => setCriticalAction(null)}
          onConfirm={() => void confirmDelete()}
        />
      )}
      {criticalAction?.kind === 'delete-seat' && (
        <CriticalActionConfirmation
          title={t('op.settings.layout.confirmDeleteSeat.title')}
          detail={criticalAction.seatName}
          impact={t('op.settings.layout.confirmDeleteSeat.impact')}
          confirmLabel={t('op.settings.layout.confirmDeleteSeat.confirm')}
          onCancel={() => setCriticalAction(null)}
          onConfirm={() => void confirmDelete()}
        />
      )}
    </>
  );
}
