import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { KioskRemoval } from './Kiosk';
import { HostBridgeRequestError } from './hostBridge';
import type { WizardKioskStatus } from './wizardApi';

function renderRemoval(options: {
  installed?: boolean;
  remove?: () => Promise<WizardKioskStatus>;
  reboot?: () => Promise<void>;
} = {}) {
  const remove = mock(options.remove ?? (async () => ({ installed: false })));
  const reboot = mock(options.reboot ?? (async () => {}));
  render(
    <I18nProvider initialLocale="ru">
      <KioskRemoval loadStatus={async () => ({ installed: options.installed ?? true })} remove={remove} reboot={reboot} />
    </I18nProvider>,
  );
  return { remove, reboot };
}

describe('«Снять киоск»', () => {
  afterEach(cleanup);

  it('на ПК без киоска ссылки нет', async () => {
    renderRemoval({ installed: false });

    await new Promise((resolve) => setTimeout(resolve, 10));
    expect(screen.queryByRole('button', { name: 'Снять киоск с этого ПК' })).toBeNull();
  });

  it('сначала объясняет, что уйдёт, и только потом снимает', async () => {
    const { remove, reboot } = renderRemoval();

    fireEvent.click(await screen.findByRole('button', { name: 'Снять киоск с этого ПК' }));
    expect(screen.getByText(/Учётка AFK4 Player будет удалена/)).toBeInTheDocument();
    expect(remove).not.toHaveBeenCalled();

    fireEvent.click(screen.getByRole('button', { name: 'Снять киоск' }));

    expect(await screen.findByText(/Киоск снят/)).toBeInTheDocument();
    expect(remove).toHaveBeenCalledTimes(1);
    fireEvent.click(screen.getByRole('button', { name: 'Перезагрузить сейчас' }));
    await waitFor(() => expect(reboot).toHaveBeenCalledTimes(1));
  });

  it('передумал — возвращается к ссылке, ничего не трогая', async () => {
    const { remove } = renderRemoval();

    fireEvent.click(await screen.findByRole('button', { name: 'Снять киоск с этого ПК' }));
    fireEvent.click(screen.getByRole('button', { name: 'Назад' }));

    expect(screen.getByRole('button', { name: 'Снять киоск с этого ПК' })).toBeInTheDocument();
    expect(remove).not.toHaveBeenCalled();
  });

  it('не вышло — говорит, где искать причину, и даёт повторить', async () => {
    renderRemoval({
      remove: async () => { throw new HostBridgeRequestError('Access is denied.', 'wizard_kiosk_remove_failed', null); }
    });

    fireEvent.click(await screen.findByRole('button', { name: 'Снять киоск с этого ПК' }));
    fireEvent.click(screen.getByRole('button', { name: 'Снять киоск' }));

    expect(await screen.findByText(/setup-wizard\.log/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Снять киоск' })).not.toBeDisabled();
  });
});
