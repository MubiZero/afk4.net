import { afterAll, afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { PlatformApiError } from '../platformApi';

const option = {
  packageDefinitionId: 'pkg-1', name: 'Ночь', currencyCode: 'TJS', priceMinorUnits: 5000,
  includedSeconds: 36000, bonusSeconds: 0, expiresAfterDays: 30
};
const getPackageOptions = mock(async () => [option]);
const getCurrentShift = mock(async (): Promise<unknown> => ({ shiftId: 'shift-1' }));

const actualHelpers = await import('../operatorHelpers');
mock.module('../operatorHelpers', () => ({
  ...actualHelpers,
  createAuthenticatedOperatorClients: () => ({
    settings: { getPackageOptions },
    shifts: { getCurrentShift }
  })
}));
const { ClientPackageModal } = await import('./ClientPackageModal');

afterAll(() => {
  mock.module('../operatorHelpers', () => (globalThis as typeof globalThis & {
    __afk4RealOperatorHelpers: typeof import('../operatorHelpers');
  }).__afk4RealOperatorHelpers);
});

afterEach(() => {
  cleanup();
  getPackageOptions.mockClear();
  getCurrentShift.mockClear();
});

const backend = {
  config: { platformBaseUrl: 'http://test' },
  session: { accessToken: 't', organizationId: 'o', permissions: ['organization.packages.purchase'] },
  branchId: 'b1'
} as never;
const player = { playerAccountId: 'p1', name: 'Фаррух', isActive: true } as never;

describe('ClientPackageModal', () => {
  // Смена здесь — только разрешение продавать. Её отказ раньше молча читался как «смены нет», и
  // кассир при открытой смене видел «откройте смену». Теперь пакеты видны, причина названа, а
  // повтор спрашивает только смену.
  it('отказ смены не выдаётся за закрытую смену и повторяет только смену', async () => {
    getCurrentShift.mockImplementationOnce(async () => { throw new PlatformApiError('boom', 500, 'Internal Server Error', ''); });
    render(
      <I18nProvider initialLocale="ru">
        <ClientPackageModal backend={backend} player={player} onClose={() => {}} onPurchased={() => {}} />
      </I18nProvider>
    );

    expect(await screen.findByRole('option', { name: /Ночь/ })).toBeInTheDocument();
    expect(screen.getByText(/Не удалось узнать, открыта ли смена/)).toHaveTextContent('Сервер вернул ошибку. Повторите позже.');
    expect(screen.queryByText('Для продажи пакета откройте смену.')).toBeNull();
    expect(screen.getByRole('button', { name: 'Купить пакет' })).toBeDisabled();

    fireEvent.click(screen.getByRole('button', { name: 'Повторить' }));

    expect(await screen.findByRole('button', { name: 'Купить пакет' })).toBeEnabled();
    expect(getCurrentShift).toHaveBeenCalledTimes(2);
    expect(getPackageOptions).toHaveBeenCalledTimes(1);
    expect(screen.queryByText(/Не удалось узнать, открыта ли смена/)).toBeNull();
  });
});
