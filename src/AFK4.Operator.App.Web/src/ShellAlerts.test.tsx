import { cleanup, fireEvent, render } from '@testing-library/react';
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import type { AlertSource, MapFilterId } from './operatorTypes';
import { ShellAlerts } from './ShellAlerts';

describe('ShellAlerts (operator)', () => {
  afterEach(() => cleanup());

  it('takes the danger tone and breaks critical states down by type', () => {
    const sources: AlertSource[] = [
      { id: 'offline', tone: 'danger', label: 'Нет связи', count: 2, filterId: 'offline' }
    ];
    const { container } = render(
      <I18nProvider>
        <ShellAlerts sources={sources} />
      </I18nProvider>
    );
    const root = container.querySelector('.shell-alerts');
    expect(root?.classList.contains('danger')).toBe(true);
    expect(root?.textContent).toContain('Нет связи');
    expect(root?.textContent).toContain('2');
  });

  it('drops the danger tone and shows the all-clear text when there are no critical states', () => {
    const { container } = render(
      <I18nProvider>
        <ShellAlerts sources={[]} />
      </I18nProvider>
    );
    const root = container.querySelector('.shell-alerts');
    expect(root?.classList.contains('danger')).toBe(false);
    expect(root?.textContent).toContain('Всё в норме');
  });

  it('jumps the map to the matching filter when a critical counter is clicked', () => {
    const sources: AlertSource[] = [
      { id: 'offline', tone: 'danger', label: 'Нет связи', count: 3, filterId: 'offline' },
      { id: 'service', tone: 'warning', label: 'Обслуживание', count: 1, filterId: 'service' }
    ];
    const onSelectSource = mock((_filterId: MapFilterId) => {});
    const { getByText } = render(
      <I18nProvider>
        <ShellAlerts sources={sources} onSelectSource={onSelectSource} />
      </I18nProvider>
    );
    fireEvent.click(getByText('Обслуживание'));
    expect(onSelectSource).toHaveBeenCalledTimes(1);
    expect(onSelectSource.mock.calls[0][0]).toBe('service');
  });
});
