import { describe, it, expect, mock, afterEach, afterAll } from 'bun:test';
import { render, screen, fireEvent, cleanup, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../../operatorToast';
import type { LoyaltySettingsDto } from '../../operatorApiClients';

const defaults: LoyaltySettingsDto = {
  topUpEnabled: false, topUpPercentBasisPoints: 0,
  shopEnabled: false, shopPercentBasisPoints: 0,
  sessionEnabled: false, sessionPercentBasisPoints: 0,
  cashbackCapMinorUnits: 0, minimumSourceMinorUnits: 0
};
const get = mock(async (): Promise<LoyaltySettingsDto> => defaults);
const update = mock(async (req: LoyaltySettingsDto): Promise<LoyaltySettingsDto> => req);

const actual = (globalThis as Record<string, unknown>).__afk4RealOperatorHelpers as Record<string, unknown>;
mock.module('../../operatorHelpers', () => ({
  ...actual,
  createAuthenticatedOperatorClients: () => ({ loyaltySettings: { get, update } })
}));

const { LoyaltyDestination } = await import('./LoyaltyDestination');

const backend = { config: { platformBaseUrl: 'http://x' }, session: { accessToken: 't', organizationId: 'o1' }, branchId: 'b1' } as never;

const view = () =>
  render(
    <I18nProvider initialLocale="ru">
      <ToastProvider>
        <LoyaltyDestination backend={backend} session={null} currencyCode="TJS" />
      </ToastProvider>
    </I18nProvider>
  );

afterEach(() => {
  get.mockClear();
  update.mockClear();
  cleanup();
});
afterAll(() => mock.restore());

describe('LoyaltyDestination', () => {
  it('загружает настройки и сохраняет проценты в basis points', async () => {
    view();
    const toggle = await screen.findByLabelText(/кэшбэк с пополнений/i);
    fireEvent.click(toggle);
    fireEvent.change(screen.getByLabelText(/процент с пополнений/i), { target: { value: '5' } });
    fireEvent.click(screen.getByRole('button', { name: /сохранить/i }));
    await waitFor(() => expect(update).toHaveBeenCalledWith({
      topUpEnabled: true,
      topUpPercentBasisPoints: 500,
      shopEnabled: false,
      shopPercentBasisPoints: 0,
      sessionEnabled: false,
      sessionPercentBasisPoints: 0,
      cashbackCapMinorUnits: 0,
      minimumSourceMinorUnits: 0
    }));
  });

  it('отклоняет процент больше 100 и не сохраняет', async () => {
    view();
    const toggle = await screen.findByLabelText(/кэшбэк с пополнений/i);
    fireEvent.click(toggle);
    fireEvent.change(screen.getByLabelText(/процент с пополнений/i), { target: { value: '150' } });
    fireEvent.click(screen.getByRole('button', { name: /сохранить/i }));
    expect(await screen.findByRole('alert')).toHaveTextContent(/процент должен быть от 0 до 100/i);
    expect(update).not.toHaveBeenCalled();
  });

  it('показывает состояние ошибки вместо вечной загрузки, если настройки не загрузились', async () => {
    get.mockImplementationOnce(async () => { throw new Error('network'); });
    view();
    expect(await screen.findByText(/не удалось/i)).toBeInTheDocument();
    // Форма не отрендерилась — редактировать нечего.
    expect(screen.queryByLabelText(/кэшбэк с пополнений/i)).toBeNull();
  });
});
