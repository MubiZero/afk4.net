import { describe, expect, it, mock } from 'bun:test';
import { render, fireEvent, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { FloorInspector } from './FloorInspector';

const seat = { id: 's1', name: 'PC-01', seatType: 'pc', rotation: 0, posX: 2, posY: 1, zoneId: 'zone-1' };
const zones = [{ id: 'zone-1', name: 'Зал' }];

describe('FloorInspector', () => {
  it('rotates by +90 and removes the selected seat', () => {
    const rotations: number[] = [];
    let removed = '';
    const { getByRole } = render(
      <I18nProvider>
        <FloorInspector seat={seat} zones={zones} onSetZone={() => {}} onRotate={(r) => rotations.push(r)} onSetType={() => {}} onRemove={(id) => { removed = id; }} />
      </I18nProvider>);
    fireEvent.click(getByRole('button', { name: 'Повернуть на 90°' }));
    expect(rotations[0]).toBe(90);
    fireEvent.click(getByRole('button', { name: 'Убрать с плана' }));
    expect(removed).toBe('s1');
  });

  it('reassigns the seat to another zone', () => {
    const onSetZone = mock(() => {});
    render(
      <I18nProvider>
        <FloorInspector
          seat={{ id: 'seat-1', name: 'PC-1', seatType: 'pc', rotation: 0, posX: 0, posY: 0, zoneId: 'zone-1' }}
          zones={[{ id: 'zone-1', name: 'Зал' }, { id: 'zone-2', name: 'VIP' }]}
          onSetZone={onSetZone}
          onRotate={() => {}}
          onSetType={() => {}}
          onRemove={() => {}}
        />
      </I18nProvider>
    );
    fireEvent.click(screen.getByRole('combobox', { name: 'Зона' }));
    fireEvent.click(screen.getByRole('option', { name: 'VIP' }));
    expect(onSetZone).toHaveBeenCalledWith('zone-2');
  });
});
