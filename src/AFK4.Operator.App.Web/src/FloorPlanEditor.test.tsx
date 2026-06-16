import { describe, expect, it } from 'bun:test';
import { render, fireEvent, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { FloorPlanEditor } from './FloorPlanEditor';
import type { OperatorFloorMapState } from './floorMapState';
import type { FloorMapBulkUpdateRequest } from './api/clients/floorMap';

function state(): OperatorFloorMapState {
  return {
    branchId: 'b1', branchName: 'AFK4', source: 'backend', loadStatus: 'ready',
    error: null, isOffline: false, etag: 'W/"v1"',
    zones: [{ zoneId: 'z1', name: 'Зал A', sortOrder: 0, geoX: null, geoY: null, geoWidth: null, geoHeight: null, color: null, zoneType: null }],
    walls: [],
    seats: [
      { id: 's1', zoneId: 'z1', name: 'PC-01', tone: 'ready', stateLabel: 'Свободно', sortOrder: 0, posX: 2, posY: 1, rotation: 0, seatType: 'pc' },
      { id: 's2', zoneId: 'z1', name: 'PC-02', tone: 'ready', stateLabel: 'Свободно', sortOrder: 1, posX: null, posY: null, rotation: 0, seatType: 'pc' }
    ]
  } as unknown as OperatorFloorMapState;
}

function renderEditor(onSave: (req: FloorMapBulkUpdateRequest) => Promise<void>, onExit: () => void = () => {}) {
  return render(
    <I18nProvider>
      <FloorPlanEditor floorMap={state()} organizationId="org-1" onSave={onSave} onExit={onExit} />
    </I18nProvider>
  );
}

describe('FloorPlanEditor', () => {
  it('places an unplaced seat from the palette, then Save submits the full layout', async () => {
    let submitted: FloorMapBulkUpdateRequest | null = null;
    let exited = false;
    const { getByText, getByRole } = renderEditor(async (req) => { submitted = req; }, () => { exited = true; });

    fireEvent.click(getByText('PC-02'));                       // palette → place on first free cell
    fireEvent.click(getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(submitted).not.toBeNull());
    expect(submitted!.organizationId).toBe('org-1');
    expect(submitted!.seats).toHaveLength(2);                  // both seats serialized (full-replace)
    const s2 = submitted!.seats.find((s) => s.seatId === 's2')!;
    expect(s2.posX).not.toBeNull();                            // the placed seat got coordinates
    await waitFor(() => expect(exited).toBe(true));            // success returns to the live view
  });

  it('Cancel with unsaved changes asks for confirmation instead of exiting', () => {
    let exited = false;
    const { getByText, getByRole } = renderEditor(async () => {}, () => { exited = true; });

    fireEvent.click(getByText('PC-02'));                       // make the draft dirty
    fireEvent.click(getByRole('button', { name: 'Отмена' }));

    expect(getByText('Отменить изменения?')).not.toBeNull();   // confirm modal shown
    expect(exited).toBe(false);                                // not exited yet
  });

  it('adds a zone and shows it selected in the inspector', () => {
    const { getByRole, getByText } = renderEditor(async () => {});

    fireEvent.click(getByRole('button', { name: 'Добавить зону' }));

    expect(getByText('Свойства зоны')).not.toBeNull();         // zone inspector for the new, auto-selected zone
  });

  it('blocks deleting a zone that still has seats and explains why', () => {
    const { getByRole, getByText } = renderEditor(async () => {});

    fireEvent.click(getByText('Зал A'));                       // select the seeded zone (owns both seats)
    const del = getByRole('button', { name: 'Удалить зону' });

    expect(del).toBeDisabled();
    expect(getByText('Сначала перенесите места из этой зоны.')).not.toBeNull();
  });

  it('serializes new zones into the save request', async () => {
    let submitted: FloorMapBulkUpdateRequest | null = null;
    const { getByRole } = renderEditor(async (req) => { submitted = req; });

    fireEvent.click(getByRole('button', { name: 'Добавить зону' }));
    fireEvent.click(getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(submitted).not.toBeNull());
    expect(submitted!.zones.some((z) => z.zoneId === null)).toBe(true);   // a brand-new zone is in the payload
  });
});
