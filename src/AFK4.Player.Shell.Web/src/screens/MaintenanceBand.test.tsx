import { afterEach, describe, expect, it } from 'bun:test';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { ShellBridgeRequestTypeNames, ShellPipeErrorCodeNames } from '@afk4/contracts';
import { ShellI18nProvider } from '../i18n/ShellI18nProvider';
import { devScenarioState } from '../host/devHost';
import { installFakeHost } from '../test/fakeHost';
import { MaintenanceBand } from './MaintenanceBand';

const maintenance = devScenarioState('maintenance')!;

function renderBand(state = maintenance) {
  return render(
    <ShellI18nProvider initialLocale="ru">
      <MaintenanceBand state={state} />
    </ShellI18nProvider>
  );
}

afterEach(() => {
  window.chrome = undefined;
});

describe('полоса обслуживания', () => {
  it('«Вернуть в зал» просит хоста, а экран «Свободен» приходит следующим состоянием', async () => {
    const host = installFakeHost({ state: maintenance });
    renderBand();

    fireEvent.click(screen.getByRole('button', { name: 'Вернуть в зал' }));

    await waitFor(() => expect(host.requests.map((request) => request.type)).toContain(ShellBridgeRequestTypeNames.MaintenanceReturn));
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('без связи говорит, что вернуть можно из Панели', async () => {
    installFakeHost({
      state: maintenance,
      reply: () => ({ ok: false, error: { code: ShellPipeErrorCodeNames.PlatformUnreachable, message: 'offline' } })
    });
    renderBand();

    fireEvent.click(screen.getByRole('button', { name: 'Вернуть в зал' }));

    expect(await screen.findByText('Нет связи с сервером — верните ПК из Панели AFK4.net.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Вернуть в зал' })).not.toBeDisabled();
  });

  it('без имени пишет только когда — поддержка платформы имени клуба не носит', () => {
    installFakeHost({ state: maintenance });
    renderBand({ ...maintenance, maintenanceByName: null });

    expect(screen.getByText(/^Включено из Панели AFK4\.net в \d\d:\d\d$/)).toBeInTheDocument();
  });

  it('пока сервер не назвал время, пишет только, что ПК на обслуживании', () => {
    installFakeHost({ state: maintenance });
    renderBand({ ...maintenance, maintenanceSinceUtc: null, maintenanceByName: null });

    expect(screen.getByText('ПК 07 на обслуживании')).toBeInTheDocument();
    expect(screen.queryByText(/Включено/)).not.toBeInTheDocument();
  });
});
