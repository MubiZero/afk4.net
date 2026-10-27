import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../../../operatorToast';
import type { createGamesClient } from '../../../api/clients/games';
import { GameCacheCard } from './GameCacheCard';

afterEach(cleanup);

function renderCard(client: Partial<ReturnType<typeof createGamesClient>>, canManage = true) {
  return render(
    <I18nProvider><ToastProvider>
      <GameCacheCard client={client as ReturnType<typeof createGamesClient>} branchId="branch-a" canManage={canManage} />
    </ToastProvider></I18nProvider>
  );
}

describe('кеш игр клуба', () => {
  it('показывает, со скольких ПК виден кеш', async () => {
    renderCard({ cache: mock(async () => ({ address: '192.168.1.10', devicesSeeing: 7, devicesReporting: 9 })) });

    expect(await screen.findByText('Кеш видят 7 из 9 ПК')).toBeDefined();
  });

  it('сохраняет адрес из сети клуба, а адрес из интернета не принимает', async () => {
    const setCache = mock(async () => ({ address: '192.168.1.20', devicesSeeing: 0, devicesReporting: 0 }));
    renderCard({ cache: mock(async () => ({ address: null, devicesSeeing: 0, devicesReporting: 0 })), setCache });
    const input = await screen.findByLabelText('Адрес сервера в сети клуба');

    fireEvent.change(input, { target: { value: '8.8.8.8' } });
    expect(screen.getByRole('alert').textContent).toContain('192.168.1.10');
    expect((screen.getByRole('button', { name: 'Сохранить адрес' }) as HTMLButtonElement).disabled).toBe(true);

    fireEvent.change(input, { target: { value: '192.168.1.20' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить адрес' }));

    await waitFor(() => expect(setCache).toHaveBeenCalledWith('branch-a', { address: '192.168.1.20' }));
  });

  it('без права управлять играми — только статус, без поля', async () => {
    renderCard({ cache: mock(async () => ({ address: '192.168.1.10', devicesSeeing: 1, devicesReporting: 1 })) }, false);

    await screen.findByText('Кеш видят 1 из 1 ПК');
    expect(screen.queryByLabelText('Адрес сервера в сети клуба')).toBeNull();
  });
});
