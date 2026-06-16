import { describe, expect, it } from 'bun:test';
import { render, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { FloorInspector } from './FloorInspector';

const seat = { id: 's1', name: 'PC-01', seatType: 'pc', rotation: 0, posX: 2, posY: 1 };

describe('FloorInspector', () => {
  it('rotates by +90 and removes the selected seat', () => {
    const rotations: number[] = [];
    let removed = '';
    const { getByRole } = render(
      <I18nProvider>
        <FloorInspector seat={seat} onRotate={(r) => rotations.push(r)} onSetType={() => {}} onRemove={(id) => { removed = id; }} />
      </I18nProvider>);
    fireEvent.click(getByRole('button', { name: 'Повернуть на 90°' }));
    expect(rotations[0]).toBe(90);
    fireEvent.click(getByRole('button', { name: 'Убрать с плана' }));
    expect(removed).toBe('s1');
  });
});
