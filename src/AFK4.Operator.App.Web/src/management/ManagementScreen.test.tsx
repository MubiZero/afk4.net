import { describe, it, expect } from 'bun:test';
import { render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ManagementScreen } from './ManagementScreen';

const renderScreen = (ui: React.ReactNode) =>
  render(<I18nProvider initialLocale="ru">{ui}</I18nProvider>);

describe('ManagementScreen', () => {
  it('renders title, subtitle and body', () => {
    renderScreen(
      <ManagementScreen title="Клуб" subtitle="Профиль филиала"><p>тело</p></ManagementScreen>
    );
    expect(screen.getByRole('heading', { name: 'Клуб' })).toBeTruthy();
    expect(screen.getByText('Профиль филиала')).toBeTruthy();
    expect(screen.getByText('тело')).toBeTruthy();
  });

  it('disables save while clean and enables it while dirty', () => {
    const { rerender } = renderScreen(
      <ManagementScreen title="t" subtitle="s" save={{ state: 'clean', onSave: () => {} }}><i/></ManagementScreen>
    );
    expect(screen.getByRole('button', { name: 'Сохранить' }).hasAttribute('disabled')).toBe(true);
    rerender(
      <I18nProvider initialLocale="ru">
        <ManagementScreen title="t" subtitle="s" save={{ state: 'dirty', onSave: () => {} }}><i/></ManagementScreen>
      </I18nProvider>
    );
    expect(screen.getByRole('button', { name: 'Сохранить' }).hasAttribute('disabled')).toBe(false);
  });

  it('omits the save bar when no save prop is given', () => {
    renderScreen(<ManagementScreen title="t" subtitle="s"><i/></ManagementScreen>);
    expect(screen.queryByRole('button', { name: 'Сохранить' })).toBeNull();
  });
});
