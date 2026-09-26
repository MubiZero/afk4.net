import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import type { OperatorBackendContext } from '../../../operatorTypes';
import { TipsSection } from './TipsSection';

afterEach(cleanup);

function renderSection(update: (request: { enabled: boolean }) => Promise<{ enabled: boolean }>) {
  const client = {
    getSettings: mock(() => Promise.resolve({ enabled: false })),
    updateSettings: mock(update)
  };
  render(
    <I18nProvider initialLocale="ru">
      <TipsSection backend={{} as OperatorBackendContext} client={client} />
    </I18nProvider>
  );
  return client;
}

describe('TipsSection', () => {
  it('включает чаевые одним щелчком — и стоит на ответе сервера', async () => {
    const client = renderSection((request) => Promise.resolve({ enabled: request.enabled }));

    const toggle = await screen.findByRole('checkbox', { name: 'Чаевые на экране ПК' });
    expect((toggle as HTMLInputElement).checked).toBe(false);
    fireEvent.click(toggle);

    await waitFor(() => expect(client.updateSettings).toHaveBeenCalledWith({ enabled: true }));
    await waitFor(() => expect((screen.getByRole('checkbox', { name: 'Чаевые на экране ПК' }) as HTMLInputElement).checked).toBe(true));
  });

  it('отказ сервера не двигает переключатель и называет причину', async () => {
    renderSection(() => Promise.reject(new Error('Нет связи')));

    fireEvent.click(await screen.findByRole('checkbox', { name: 'Чаевые на экране ПК' }));

    expect(await screen.findByRole('alert')).toBeTruthy();
    expect((screen.getByRole('checkbox', { name: 'Чаевые на экране ПК' }) as HTMLInputElement).checked).toBe(false);
  });
});
