import { cleanup, fireEvent, render } from '@testing-library/react';
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import type { AlertSource, MapFilterId } from './operatorTypes';
import { ShellAlerts } from './ShellAlerts';

describe('ShellAlerts (operator)', () => {
  afterEach(() => cleanup());

  it('takes the danger tone and shows the problem count when there are problems', () => {
    const { container } = render(
      <I18nProvider>
        <ShellAlerts problems={2} offline={1} />
      </I18nProvider>
    );
    const root = container.querySelector('.shell-alerts');
    expect(root?.classList.contains('danger')).toBe(true);
    expect(root?.textContent).toContain('2');
  });

  it('drops the danger tone and shows the zero-state text when there are no problems', () => {
    const { container } = render(
      <I18nProvider>
        <ShellAlerts problems={0} offline={0} />
      </I18nProvider>
    );
    const root = container.querySelector('.shell-alerts');
    expect(root?.classList.contains('danger')).toBe(false);
    expect(root?.textContent).toContain('Тревог нет');
  });

  it('jumps the map to the matching filter when a critical counter is clicked', () => {
    const sources: AlertSource[] = [
      { id: 'offline', tone: 'danger', label: 'Нет связи', count: 3, filterId: 'offline' },
      { id: 'service', tone: 'warning', label: 'Обслуживание', count: 1, filterId: 'service' }
    ];
    const onSelectSource = mock((_filterId: MapFilterId) => {});
    const { getByText } = render(
      <I18nProvider>
        <ShellAlerts problems={4} offline={3} sources={sources} onSelectSource={onSelectSource} />
      </I18nProvider>
    );
    fireEvent.click(getByText('Обслуживание'));
    expect(onSelectSource).toHaveBeenCalledTimes(1);
    expect(onSelectSource.mock.calls[0][0]).toBe('service');
  });
});
