import { describe, it, expect, mock } from 'bun:test';
import { render, screen, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { FloorZoneInspector } from './FloorZoneInspector';

function renderInspector(props: Partial<Parameters<typeof FloorZoneInspector>[0]> = {}) {
  const handlers = {
    onRename: mock(() => {}),
    onGeometry: mock(() => {}),
    onColor: mock(() => {}),
    onDelete: mock(() => {})
  };
  render(
    <I18nProvider>
      <FloorZoneInspector
        zone={{ id: 'zone-1', name: 'Зал', geoX: 0, geoY: 0, geoWidth: 4, geoHeight: 3, color: null }}
        canDelete
        deleteBlockedReason={null}
        {...handlers}
        {...props}
      />
    </I18nProvider>
  );
  return handlers;
}

describe('FloorZoneInspector', () => {
  it('renames the zone', () => {
    const h = renderInspector();
    fireEvent.change(screen.getByLabelText('Название'), { target: { value: 'VIP' } });
    expect(h.onRename).toHaveBeenCalledWith('VIP');
  });

  it('patches geometry as a partial with a numeric value', () => {
    const h = renderInspector();
    fireEvent.change(screen.getByLabelText('Ширина'), { target: { value: '7' } });
    expect(h.onGeometry).toHaveBeenCalledWith({ geoWidth: 7 });
  });

  it('sets a preset colour and clears it', () => {
    const h = renderInspector();
    fireEvent.click(screen.getByRole('button', { name: 'Без цвета' }));
    expect(h.onColor).toHaveBeenCalledWith(null);
  });

  it('deletes when allowed', () => {
    const h = renderInspector();
    fireEvent.click(screen.getByRole('button', { name: 'Удалить зону' }));
    expect(h.onDelete).toHaveBeenCalledWith('zone-1');
  });

  it('disables delete and shows the reason when blocked', () => {
    const h = renderInspector({ canDelete: false, deleteBlockedReason: 'Сначала перенесите места из этой зоны.' });
    const button = screen.getByRole('button', { name: 'Удалить зону' });
    expect(button).toBeDisabled();
    fireEvent.click(button);
    expect(h.onDelete).not.toHaveBeenCalled();
    expect(screen.getByText('Сначала перенесите места из этой зоны.')).toBeInTheDocument();
  });
});
