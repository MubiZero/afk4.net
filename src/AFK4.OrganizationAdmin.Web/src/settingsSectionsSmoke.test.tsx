import { cleanup, fireEvent, render, waitFor } from '@testing-library/react';
import { afterAll, afterEach, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from './operatorToast';

// Дымовой тест декомпозиции settings: каждый из 6 разделов должен отрендериться без падения
// даже на пустых данных (после разреза монолита на settings/Settings*Section). Прощёлкиваем
// все вкладки нав-панели; если секция бросает на рендере/seeding — тест упадёт здесь.
const empty = mock(async () => []);
const obj = mock(async () => ({}));

const actualHelpers = await import('./operatorHelpers');
mock.module('./operatorHelpers', () => ({
  ...actualHelpers,
  createAuthenticatedOperatorClients: () => ({
    settings: {
      getBranchProfile: obj,
      getStaffUsers: empty,
      getLayoutZones: empty,
      getTariffOptions: empty,
      getPackageOptions: empty
    },
    pos: { getCatalog: empty },
    inventory: { getStockMovements: empty },
    diagnostics: { getDiagnostics: obj },
    updates: { getRolloutStatuses: empty },
    devices: { listDevices: empty, listBranchDeviceCommands: empty }
  })
}));

const { BackendSettingsWorkspace } = await import('./BackendSettingsWorkspace');

afterAll(() => {
  mock.module('./operatorHelpers', () => (globalThis as typeof globalThis & {
    __afk4RealOperatorHelpers: typeof import('./operatorHelpers');
  }).__afk4RealOperatorHelpers);
});

afterEach(() => cleanup());

const backend = {
  config: { platformBaseUrl: 'http://test' },
  session: { accessToken: 't', organizationId: 'org', permissions: [] },
  branchId: 'b1'
};

it('renders every settings section tab without crashing', async () => {
  const { container } = render(
    <I18nProvider><ToastProvider><BackendSettingsWorkspace currencyCode="TJS" backend={backend as never} /></ToastProvider></I18nProvider>
  );
  await waitFor(() => expect(obj).toHaveBeenCalled());

  const navButtons = Array.from(container.querySelectorAll('.settings-nav-panel button')) as HTMLButtonElement[];
  expect(navButtons.length).toBe(6); // профиль/макет/тарифы/персонал/товары/интеграции

  for (const button of navButtons) {
    fireEvent.click(button);
    // Главная панель раздела должна остаться в DOM (а не схлопнуться из-за throw на рендере).
    await waitFor(() => expect(container.querySelector('.settings-main-panel')).not.toBeNull());
  }
});
