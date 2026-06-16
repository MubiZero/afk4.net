import { describe, it, expect, mock } from 'bun:test';
import { render, screen, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { FloorZonePanel } from './FloorZonePanel';

function renderPanel(props: Partial<Parameters<typeof FloorZonePanel>[0]> = {}) {
  const onSelectZone = mock(() => {});
  const onAddZone = mock(() => {});
  const { container } = render(
    <I18nProvider>
      <FloorZonePanel
        zones={[{ id: 'zone-1', name: 'Зал' }, { id: 'zone-2', name: 'VIP' }]}
        selectedZoneId="zone-2"
        onSelectZone={onSelectZone}
        onAddZone={onAddZone}
        {...props}
      />
    </I18nProvider>
  );
  return { onSelectZone, onAddZone, container };
}

describe('FloorZonePanel', () => {
  it('lists zones and selects on click', () => {
    const { onSelectZone } = renderPanel();
    fireEvent.click(screen.getByText('Зал'));
    expect(onSelectZone).toHaveBeenCalledWith('zone-1');
  });

  it('marks the selected zone', () => {
    renderPanel();
    expect(screen.getByText('VIP').closest('button')?.className).toContain('is-selected');
  });

  it('fires onAddZone', () => {
    const { onAddZone, container } = renderPanel();
    fireEvent.click(container.querySelector('.floor-zone-add')!);
    expect(onAddZone).toHaveBeenCalled();
  });
});
