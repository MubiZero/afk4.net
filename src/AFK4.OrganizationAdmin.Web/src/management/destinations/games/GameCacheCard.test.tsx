import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../../../operatorToast';
import type { BranchGameCacheDto, createGamesClient } from '../../../api/clients/games';
import { GameCacheCard } from './GameCacheCard';

afterEach(cleanup);

const builtIn: BranchGameCacheDto = {
  address: null, devicesSeeing: 7, devicesReporting: 9, enabled: true, hostName: 'ADMIN-PC', effectiveAddress: '192.168.1.5', storedBytes: 5 * 1024 ** 3
};

function renderCard(client: Partial<ReturnType<typeof createGamesClient>>, canManage = true) {
  return render(
    <I18nProvider><ToastProvider>
      <GameCacheCard client={client as ReturnType<typeof createGamesClient>} branchId="branch-a" canManage={canManage} />
    </ToastProvider></I18nProvider>
  );
}

describe('кеш игр клуба', () => {
  // Без настройки: кеш сам на ПК администратора — карточка говорит где, сколько хранит и кто его видит.
  it('по умолчанию показывает встроенный кеш на ПК администратора', async () => {
    renderCard({ cache: mock(async () => builtIn) });

    const status = await screen.findByRole('status');
    expect(status.textContent).toContain('Кеш на ПК администратора ADMIN-PC, хранит 5.0 ГБ');
    expect(status.textContent).toContain('Кеш видят 7 из 9 ПК');
  });

  it('выключается одной галочкой', async () => {
    const setCache = mock(async () => ({ ...builtIn, enabled: false, hostName: null, effectiveAddress: null }));
    renderCard({ cache: mock(async () => builtIn), setCache });

    fireEvent.click(await screen.findByLabelText('Кеш игр включён'));

    await waitFor(() => expect(setCache).toHaveBeenCalledWith('branch-a', { address: null, enabled: false }));
    expect((await screen.findByRole('status')).textContent).toContain('Кеш выключен');
  });

  it('свой сервер LANCache — только адрес из сети клуба', async () => {
    const setCache = mock(async () => ({ ...builtIn, address: '192.168.1.20', effectiveAddress: '192.168.1.20', hostName: null }));
    renderCard({ cache: mock(async () => builtIn), setCache });
    const input = await screen.findByLabelText('Адрес сервера в сети клуба');

    fireEvent.change(input, { target: { value: '8.8.8.8' } });
    expect((screen.getByRole('button', { name: 'Сохранить адрес' }) as HTMLButtonElement).disabled).toBe(true);

    fireEvent.change(input, { target: { value: '192.168.1.20' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить адрес' }));

    await waitFor(() => expect(setCache).toHaveBeenCalledWith('branch-a', { address: '192.168.1.20', enabled: true }));
  });

  it('без права управлять играми — только статус', async () => {
    renderCard({ cache: mock(async () => builtIn) }, false);

    await screen.findByRole('status');
    expect(screen.queryByLabelText('Кеш игр включён')).toBeNull();
  });
});
