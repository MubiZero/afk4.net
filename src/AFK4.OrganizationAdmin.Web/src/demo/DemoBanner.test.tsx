import { describe, expect, it, mock } from 'bun:test';
import { fireEvent, render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { DemoBanner } from './DemoBanner';

describe('DemoBanner', () => {
  it('says the data is made up and starts over from a clean page', () => {
    const reload = mock(() => {});
    localStorage.setItem('afk4-demo-leftover', '1');
    sessionStorage.setItem('afk4-demo-leftover', '1');

    render(
      <I18nProvider>
        <DemoBanner reload={reload} />
      </I18nProvider>
    );

    expect(screen.getByRole('status').textContent).toContain('ничего не сохраняется');
    fireEvent.click(screen.getByRole('button', { name: 'Начать заново' }));

    expect(localStorage.getItem('afk4-demo-leftover')).toBeNull();
    expect(sessionStorage.getItem('afk4-demo-leftover')).toBeNull();
    expect(reload).toHaveBeenCalledTimes(1);
  });
});
