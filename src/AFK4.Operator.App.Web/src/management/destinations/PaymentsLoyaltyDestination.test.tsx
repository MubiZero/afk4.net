import { describe, it, expect, mock, afterEach, afterAll } from 'bun:test';
import { render, screen, fireEvent, cleanup, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../../operatorToast';
import { permissionNames } from '../../operatorPermissions';
import type { LoyaltySettingsDto, EskhataConfigDto } from '../../operatorApiClients';

const loyaltyDefaults: LoyaltySettingsDto = {
  topUpEnabled: false, topUpPercentBasisPoints: 0,
  shopEnabled: false, shopPercentBasisPoints: 0,
  sessionEnabled: false, sessionPercentBasisPoints: 0,
  cashbackCapMinorUnits: 0, minimumSourceMinorUnits: 0
};
const loyaltyGet = mock(async (): Promise<LoyaltySettingsDto> => loyaltyDefaults);
const loyaltyUpdate = mock(async (req: LoyaltySettingsDto): Promise<LoyaltySettingsDto> => req);
const eskhataGet = mock(async (): Promise<EskhataConfigDto> => ({ baseUrl: '', companyId: '', posId: 0, hashKeySet: false, status: 'inactive' }));

const actual = (globalThis as Record<string, unknown>).__afk4RealOperatorHelpers as Record<string, unknown>;
mock.module('../../operatorHelpers', () => ({
  ...actual,
  createAuthenticatedOperatorClients: () => ({
    loyaltySettings: { get: loyaltyGet, update: loyaltyUpdate },
    eskhataConfig: { get: eskhataGet, update: mock(async () => ({})) }
  })
}));

// Изолируем контейнер от dcgate-сети: PaymentGatewaysWorkspace подменяем маркером.
mock.module('../../PaymentGatewaysWorkspace', () => ({
  PaymentGatewaysWorkspace: () => <div data-testid="dcgate-stub" />
}));

const { PaymentsLoyaltyDestination } = await import('./PaymentsLoyaltyDestination');

const backend = { config: { platformBaseUrl: 'http://x' }, session: { accessToken: 't', organizationId: 'o1' }, branchId: 'b1' } as never;
const session = (perms: string[]) => ({ permissions: perms, organizationId: 'o1' }) as never;

const view = (perms: string[]) =>
  render(
    <I18nProvider initialLocale="ru">
      <ToastProvider>
        <PaymentsLoyaltyDestination backend={backend} session={session(perms)} currencyCode="TJS" />
      </ToastProvider>
    </I18nProvider>
  );

afterEach(() => {
  loyaltyGet.mockClear();
  loyaltyUpdate.mockClear();
  eskhataGet.mockClear();
  cleanup();
});
afterAll(() => mock.restore());

describe('PaymentsLoyaltyDestination', () => {
  // The gateways tab has the Eskhata form's own «Сохранить»; the shared save bar is the
  // ManagementScreen-level `.management-save-bar`, so assert that element specifically.
  it('shows both tabs and toggles the shared save bar per active tab', async () => {
    const { container } = view([permissionNames.managePaymentGateways, permissionNames.manageLoyaltySettings]);

    expect(screen.getByRole('tab', { name: 'Платёжные шлюзы' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Лояльность' })).toBeInTheDocument();
    // Default tab is gateways: dcgate stub visible, no shared save bar.
    expect(screen.getByTestId('dcgate-stub')).toBeInTheDocument();
    expect(container.querySelector('.management-save-bar')).toBeNull();

    fireEvent.click(screen.getByRole('tab', { name: 'Лояльность' }));
    expect(await screen.findByLabelText(/кэшбэк с пополнений/i)).toBeInTheDocument();
    expect(container.querySelector('.management-save-bar')).not.toBeNull();
  });

  it('shows only the loyalty form (no tab strip, with save bar) for loyalty-only permission', async () => {
    const { container } = view([permissionNames.manageLoyaltySettings]);
    expect(await screen.findByLabelText(/кэшбэк с пополнений/i)).toBeInTheDocument();
    expect(screen.queryByRole('tab')).toBeNull();
    expect(container.querySelector('.management-save-bar')).not.toBeNull();
  });

  it('shows only the gateways tab (no shared save bar) for payments-only permission', () => {
    const { container } = view([permissionNames.managePaymentGateways]);
    expect(screen.getByTestId('dcgate-stub')).toBeInTheDocument();
    expect(screen.queryByRole('tab')).toBeNull();
    expect(container.querySelector('.management-save-bar')).toBeNull();
  });

  it('saves loyalty percents in basis points from the save bar', async () => {
    view([permissionNames.manageLoyaltySettings]);
    const toggle = await screen.findByLabelText(/кэшбэк с пополнений/i);
    fireEvent.click(toggle);
    fireEvent.change(screen.getByLabelText(/процент с пополнений/i), { target: { value: '5' } });
    fireEvent.click(screen.getByRole('button', { name: /сохранить/i }));
    await waitFor(() => expect(loyaltyUpdate).toHaveBeenCalledWith(expect.objectContaining({
      topUpEnabled: true,
      topUpPercentBasisPoints: 500
    })));
  });

  it('shows a live accrual example when a rule is enabled', async () => {
    view([permissionNames.manageLoyaltySettings]);
    const toggle = await screen.findByLabelText(/кэшбэк с пополнений/i);
    fireEvent.click(toggle);
    fireEvent.change(screen.getByLabelText(/процент с пополнений/i), { target: { value: '10' } });
    // 10% со 100 → +10.00 (Money signed рендерит с «+»)
    expect(await screen.findByText(/\+10/)).toBeInTheDocument();
  });

  it('hides the accrual example when the rule is disabled', async () => {
    view([permissionNames.manageLoyaltySettings]);
    await screen.findByLabelText(/кэшбэк с пополнений/i);
    // Percent has a value but the rule stays off → example must not render.
    fireEvent.change(screen.getByLabelText(/процент с пополнений/i), { target: { value: '10' } });
    expect(screen.queryByText(/\+10/)).toBeNull();
  });
});
