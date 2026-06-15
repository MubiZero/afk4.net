import { cleanup, render } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
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
});
